using System.Text.Json.Serialization;

namespace AzDoPrMonitor;

public sealed class ConnectionData
{
    [JsonPropertyName("authenticatedUser")]
    public IdentityRef? AuthenticatedUser { get; set; }
}

public sealed class IdentityRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";
}

public sealed class ReviewerRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("vote")]
    public int Vote { get; set; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }
}

public sealed class ProjectsResponse
{
    [JsonPropertyName("value")]
    public List<ProjectRef> Value { get; set; } = new();
}

public sealed class ProjectRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public sealed class PullRequestsResponse
{
    [JsonPropertyName("value")]
    public List<GitPullRequest> Value { get; set; } = new();
}

public sealed class GitPullRequest
{
    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("isDraft")]
    public bool IsDraft { get; set; }

    [JsonPropertyName("creationDate")]
    public DateTimeOffset CreationDate { get; set; }

    [JsonPropertyName("createdBy")]
    public IdentityRef CreatedBy { get; set; } = new();

    [JsonPropertyName("reviewers")]
    public List<ReviewerRef> Reviewers { get; set; } = new();

    [JsonPropertyName("repository")]
    public GitRepositoryRef Repository { get; set; } = new();

    [JsonPropertyName("mergeStatus")]
    public string MergeStatus { get; set; } = "notSet";

    [JsonPropertyName("sourceRefName")]
    public string SourceRefName { get; set; } = "";

    [JsonPropertyName("targetRefName")]
    public string TargetRefName { get; set; } = "";
}

public sealed class GitRepositoryRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("project")]
    public ProjectRef Project { get; set; } = new();
}

public sealed class ThreadsResponse
{
    [JsonPropertyName("value")]
    public List<CommentThread> Value { get; set; } = new();
}

public sealed class CommentThread
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("comments")]
    public List<Comment> Comments { get; set; } = new();

    [JsonPropertyName("threadContext")]
    public ThreadContext? ThreadContext { get; set; }
}

public sealed class ThreadContext
{
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
}

public sealed class Comment
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("author")]
    public IdentityRef Author { get; set; } = new();

    [JsonPropertyName("publishedDate")]
    public DateTimeOffset PublishedDate { get; set; }

    [JsonPropertyName("commentType")]
    public string? CommentType { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }
}

public enum PrKind
{
    CreatedByMe,
    ReviewingForMe
}

public sealed class PrEntry
{
    public required PrKind Kind { get; init; }
    public required GitPullRequest Pr { get; init; }
    public required string Org { get; init; }

    public string ProjectName => Pr.Repository.Project.Name;
    public string RepoName => Pr.Repository.Name;

    public string WebUrl =>
        $"https://dev.azure.com/{Org}/{Uri.EscapeDataString(ProjectName)}/_git/{Uri.EscapeDataString(RepoName)}/pullrequest/{Pr.PullRequestId}";
}
