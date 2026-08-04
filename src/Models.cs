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

    // True when this reviewer entry is a required-reviewer group/team, not a real person
    // (e.g. "[Product Development]\Fuji") — its own "vote" is the group's resolved vote.
    [JsonPropertyName("isContainer")]
    public bool IsContainer { get; set; }

    // Present on a real person's entry when their vote also satisfies a required group
    // reviewer; points back at that group's identity (shown as "Approved via <name>" in ADO).
    [JsonPropertyName("votedFor")]
    public List<ReviewerRef> VotedFor { get; set; } = new();
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

public sealed class PolicyEvaluationsResponse
{
    [JsonPropertyName("value")]
    public List<PolicyEvaluationRecord> Value { get; set; } = new();
}

public sealed class PolicyEvaluationRecord
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("configuration")]
    public PolicyConfiguration Configuration { get; set; } = new();
}

public sealed class PolicyConfiguration
{
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    // Non-blocking policies (e.g. an informational Status check) can be enabled but
    // not actually gate completion — only blocking ones should count against "can complete".
    [JsonPropertyName("isBlocking")]
    public bool IsBlocking { get; set; }

    [JsonPropertyName("type")]
    public PolicyType Type { get; set; } = new();

    [JsonPropertyName("settings")]
    public PolicySettings Settings { get; set; } = new();
}

public sealed class PolicySettings
{
    [JsonPropertyName("minimumApproverCount")]
    public int MinimumApproverCount { get; set; }
}

public sealed class PolicyType
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";
}

public enum BuildState
{
    None,
    Pending,
    Succeeded,
    Failed
}

public enum PolicyState
{
    NotConfigured,
    Pending,
    Satisfied,
    Rejected
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

public sealed class WiqlRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = "";
}

public sealed class WiqlResponse
{
    [JsonPropertyName("workItems")]
    public List<WiqlWorkItemRef> WorkItems { get; set; } = new();
}

public sealed class WiqlWorkItemRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

public sealed class WorkItemsBatchRequest
{
    [JsonPropertyName("ids")]
    public List<int> Ids { get; set; } = new();

    [JsonPropertyName("fields")]
    public List<string> Fields { get; set; } = new();
}

public sealed class WorkItemsBatchResponse
{
    [JsonPropertyName("value")]
    public List<WorkItem> Value { get; set; } = new();
}

public sealed class WorkItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("fields")]
    public WorkItemFields Fields { get; set; } = new();
}

public sealed class WorkItemFields
{
    [JsonPropertyName("System.Title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("System.WorkItemType")]
    public string WorkItemType { get; set; } = "";

    [JsonPropertyName("System.State")]
    public string State { get; set; } = "";

    [JsonPropertyName("System.TeamProject")]
    public string TeamProject { get; set; } = "";

    [JsonPropertyName("System.AssignedTo")]
    public IdentityRef? AssignedTo { get; set; }

    [JsonPropertyName("System.CreatedDate")]
    public DateTimeOffset CreatedDate { get; set; }

    [JsonPropertyName("System.ChangedDate")]
    public DateTimeOffset ChangedDate { get; set; }

    [JsonPropertyName("System.Description")]
    public string? Description { get; set; }

    [JsonPropertyName("Microsoft.VSTS.TCM.ReproSteps")]
    public string? ReproSteps { get; set; }
}

// Mirrors PrEntry: pairs a raw WorkItem with the org, since WebUrl needs both.
public sealed class WorkItemEntry
{
    public required WorkItem Item { get; init; }
    public required string Org { get; init; }

    public string ProjectName => Item.Fields.TeamProject;

    public string WebUrl =>
        $"https://dev.azure.com/{Org}/{Uri.EscapeDataString(ProjectName)}/_workitems/edit/{Item.Id}";
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

    // Filled in by a second fetch pass after the entry is created (PrDataService.FetchBuildStatusesAsync).
    public BuildState BuildStatus { get; set; } = BuildState.None;

    // Minimum-number-of-reviewers branch policy target, if enabled (0 = policy not set).
    public int MinimumApproverCount { get; set; }

    // ADO's own evaluation of whether that policy is satisfied — ground truth, since
    // delegated/group votes make deriving this from the reviewers list unreliable.
    public PolicyState MinReviewerPolicy { get; set; } = PolicyState.NotConfigured;

    // Same reasoning for the "Required reviewers" policy (specific people/groups pinned
    // to the PR) — trust ADO's evaluation instead of the reviewers list's isRequired/vote.
    public PolicyState RequiredReviewerPolicy { get; set; } = PolicyState.NotConfigured;

    // Every other enabled+blocking branch policy this app doesn't model individually
    // (comment requirements, merge strategy, work item linking, status checks, ...).
    public PolicyState OtherPolicies { get; set; } = PolicyState.NotConfigured;
    public List<string> BlockingPolicyNames { get; set; } = new();

    public string ProjectName => Pr.Repository.Project.Name;
    public string RepoName => Pr.Repository.Name;

    public string WebUrl =>
        $"https://dev.azure.com/{Org}/{Uri.EscapeDataString(ProjectName)}/_git/{Uri.EscapeDataString(RepoName)}/pullrequest/{Pr.PullRequestId}";
}
