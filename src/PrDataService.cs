namespace AzDoPrMonitor;

public sealed class PrSnapshot
{
    public required List<PrEntry> CreatedByMe { get; init; }
    public required List<PrEntry> ReviewingForMe { get; init; }
    public required List<WorkItemEntry> WorkItems { get; init; }
    public required DateTimeOffset FetchedAt { get; init; }
}

public sealed class PrDataService
{
    private readonly AzureDevOpsClient _client;
    private readonly string _org;
    private const int MaxConcurrentProjects = 8;
    private const int MaxConcurrentStatusFetches = 8;

    public PrDataService(AzureDevOpsClient client, string org)
    {
        _client = client;
        _org = org;
    }

    public async Task<PrSnapshot> FetchAsync(string myId, CancellationToken ct)
    {
        var workItemsTask = _client.GetMyWorkItemsAsync(ct);
        var projects = await _client.GetProjectsAsync(ct);

        var created = new List<PrEntry>();
        var reviewing = new List<PrEntry>();
        var gate = new SemaphoreSlim(MaxConcurrentProjects);
        var tasks = projects.Select(async project =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var createdTask = _client.GetActivePullRequestsAsync(project.Name, creatorId: myId, reviewerId: null, ct);
                var reviewingTask = _client.GetActivePullRequestsAsync(project.Name, creatorId: null, reviewerId: myId, ct);
                await Task.WhenAll(createdTask, reviewingTask);

                lock (created)
                {
                    created.AddRange(createdTask.Result.Select(pr => new PrEntry { Kind = PrKind.CreatedByMe, Pr = pr, Org = _org }));
                }
                lock (reviewing)
                {
                    reviewing.AddRange(reviewingTask.Result
                        .Where(pr => pr.CreatedBy.Id != myId)
                        .Select(pr => new PrEntry { Kind = PrKind.ReviewingForMe, Pr = pr, Org = _org }));
                }
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        created.Sort((a, b) => b.Pr.CreationDate.CompareTo(a.Pr.CreationDate));
        reviewing.Sort((a, b) => b.Pr.CreationDate.CompareTo(a.Pr.CreationDate));

        // Only the visible ("My Pull Requests") list needs build status; the reviewing
        // list is fetched but never shown (see PrMonitorApp).
        await FetchBuildStatusesAsync(created, ct);

        var workItems = (await workItemsTask)
            .OrderByDescending(w => w.Fields.ChangedDate)
            .Select(w => new WorkItemEntry { Item = w, Org = _org })
            .ToList();

        return new PrSnapshot
        {
            CreatedByMe = created,
            ReviewingForMe = reviewing,
            WorkItems = workItems,
            FetchedAt = DateTimeOffset.Now
        };
    }

    private async Task FetchBuildStatusesAsync(List<PrEntry> entries, CancellationToken ct)
    {
        var gate = new SemaphoreSlim(MaxConcurrentStatusFetches);
        var tasks = entries.Select(async entry =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var evaluations = await _client.GetPolicyEvaluationsAsync(
                    entry.ProjectName, entry.Pr.Repository.Project.Id, entry.Pr.PullRequestId, ct);
                entry.BuildStatus = Formatting.AggregateBuildState(evaluations);
                entry.MinimumApproverCount = Formatting.MinimumApproverCount(evaluations);
                entry.MinReviewerPolicy = Formatting.MinimumReviewerPolicyState(evaluations);
                entry.RequiredReviewerPolicy = Formatting.RequiredReviewerPolicyState(evaluations);
                entry.OtherPolicies = Formatting.OtherPolicyState(evaluations);
                entry.BlockingPolicyNames = Formatting.BlockingPolicyNames(evaluations);
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(tasks);
    }
}
