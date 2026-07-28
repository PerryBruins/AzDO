using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AzDoPrMonitor;

public sealed class AzureDevOpsClient : IDisposable
{
    private const string ApiVersion = "7.1";
    private readonly HttpClient _http;
    private readonly string _org;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AzureDevOpsClient(string org, string pat)
    {
        _org = org;
        _http = new HttpClient { BaseAddress = new Uri($"https://dev.azure.com/{org}/") };
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<string> GetMyIdAsync(CancellationToken ct)
    {
        var data = await _http.GetFromJsonAsync<ConnectionData>(
            $"_apis/connectionData?api-version={ApiVersion}-preview.1", JsonOptions, ct);
        return data?.AuthenticatedUser?.Id
            ?? throw new InvalidOperationException("Could not resolve authenticated user id from AzDO connectionData.");
    }

    public async Task<List<ProjectRef>> GetProjectsAsync(CancellationToken ct)
    {
        var result = new List<ProjectRef>();
        var skip = 0;
        while (true)
        {
            var page = await _http.GetFromJsonAsync<ProjectsResponse>(
                $"_apis/projects?api-version={ApiVersion}&$top=100&$skip={skip}", JsonOptions, ct);
            if (page is null || page.Value.Count == 0) break;
            result.AddRange(page.Value);
            if (page.Value.Count < 100) break;
            skip += 100;
        }
        return result;
    }

    public async Task<List<GitPullRequest>> GetActivePullRequestsAsync(
        string project, string? creatorId, string? reviewerId, CancellationToken ct)
    {
        var query = new StringBuilder($"{Uri.EscapeDataString(project)}/_apis/git/pullrequests?api-version={ApiVersion}")
            .Append("&searchCriteria.status=active");
        if (creatorId is not null) query.Append("&searchCriteria.creatorId=").Append(creatorId);
        if (reviewerId is not null) query.Append("&searchCriteria.reviewerId=").Append(reviewerId);

        try
        {
            var response = await _http.GetFromJsonAsync<PullRequestsResponse>(query.ToString(), JsonOptions, ct);
            return response?.Value ?? new List<GitPullRequest>();
        }
        catch (HttpRequestException)
        {
            return new List<GitPullRequest>();
        }
    }

    public async Task<List<CommentThread>> GetThreadsAsync(
        string project, string repositoryId, int pullRequestId, CancellationToken ct)
    {
        var url = $"{Uri.EscapeDataString(project)}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}/threads?api-version={ApiVersion}";
        var response = await _http.GetFromJsonAsync<ThreadsResponse>(url, JsonOptions, ct);
        return response?.Value ?? new List<CommentThread>();
    }

    public void Dispose() => _http.Dispose();
}
