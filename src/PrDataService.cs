namespace AzDoPrMonitor;

public sealed class PrSnapshot
{
    public required List<PrEntry> CreatedByMe { get; init; }
    public required List<PrEntry> ReviewingForMe { get; init; }
    public required DateTimeOffset FetchedAt { get; init; }
}

public sealed class PrDataService
{
    private readonly AzureDevOpsClient _client;
    private readonly string _org;
    private const int MaxConcurrentProjects = 8;

    public PrDataService(AzureDevOpsClient client, string org)
    {
        _client = client;
        _org = org;
    }

    public async Task<PrSnapshot> FetchAsync(string myId, CancellationToken ct)
    {
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

        return new PrSnapshot
        {
            CreatedByMe = created,
            ReviewingForMe = reviewing,
            FetchedAt = DateTimeOffset.Now
        };
    }
}
