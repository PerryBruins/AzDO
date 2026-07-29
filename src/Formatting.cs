using System.Text;

namespace AzDoPrMonitor;

public static class Formatting
{
    public static string VoteIcon(int vote) => vote switch
    {
        10 => "✓",
        5 => "≈",
        0 => "·",
        -5 => "…",
        -10 => "✗",
        _ => "?"
    };

    public static string OverallIcon(GitPullRequest pr)
    {
        if (pr.Reviewers.Any(r => r.Vote == -10)) return "✗";
        if (pr.Reviewers.Any(r => r.Vote == -5)) return "…";
        var required = pr.Reviewers.Where(r => r.IsRequired).ToList();
        var voters = required.Count > 0 ? required : pr.Reviewers;
        if (voters.Count > 0 && voters.All(r => r.Vote is 10 or 5)) return "✓";
        return "·";
    }

    public static string MyVoteIcon(GitPullRequest pr, string myId)
    {
        var mine = pr.Reviewers.FirstOrDefault(r => r.Id == myId);
        return mine is null ? "?" : VoteIcon(mine.Vote);
    }

    // Well-known, org-independent policy type id for the "Build" branch policy.
    private const string BuildPolicyTypeId = "0609b952-1397-4640-95ec-e00a01b2c241";

    public static BuildState AggregateBuildState(List<PolicyEvaluationRecord> evaluations)
    {
        var builds = evaluations.Where(e => e.Configuration.IsEnabled && e.Configuration.Type.Id == BuildPolicyTypeId).ToList();
        if (builds.Count == 0) return BuildState.None;
        if (builds.Any(e => e.Status is "rejected" or "broken")) return BuildState.Failed;
        if (builds.Any(e => e.Status is "queued" or "running")) return BuildState.Pending;
        if (builds.Any(e => e.Status == "approved")) return BuildState.Succeeded;
        return BuildState.None;
    }

    public static string BuildIcon(BuildState state) => state switch
    {
        BuildState.Succeeded => "✓",
        BuildState.Pending => "⟳",
        BuildState.Failed => "✗",
        _ => "·"
    };

    public static string BuildLabel(BuildState state) => state switch
    {
        BuildState.Succeeded => "Succeeded",
        BuildState.Pending => "Running",
        BuildState.Failed => "Failed",
        _ => "No build status"
    };

    public static string Age(DateTimeOffset created)
    {
        var span = DateTimeOffset.Now - created;
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h";
        return $"{Math.Max(1, (int)span.TotalMinutes)}m";
    }

    public static string ApprovalSummary(GitPullRequest pr)
    {
        var required = pr.Reviewers.Where(r => r.IsRequired).ToList();
        var voters = required.Count > 0 ? required : pr.Reviewers;
        if (voters.Count == 0) return "";
        var approved = voters.Count(r => r.Vote is 10 or 5);
        return $"{approved}/{voters.Count}";
    }

    private const int MarkWidth = 3;
    private const int ApprovalWidth = 5;
    private const int PrIdWidth = 7;
    private const int AgeWidth = 5;
    private const int BuildWidth = 4;

    private const int MinRepoWidth = 14;
    private const int MaxRepoWidth = 50;
    private const int TitleReserve = 20;
    private const int FixedPrefixWidth = MarkWidth + 1 + 1 + 1 + ApprovalWidth + 1 + PrIdWidth + 1 + AgeWidth + 1 + BuildWidth + 1;

    public static int ComputeRepoWidth(int totalWidth)
    {
        var remaining = Math.Max(0, totalWidth - FixedPrefixWidth - TitleReserve);
        return Math.Clamp(remaining * 4 / 10, MinRepoWidth, MaxRepoWidth);
    }

    private static string FitColumn(string value, int width)
    {
        if (value.Length > width)
        {
            return width <= 3 ? value[..width] : string.Concat(value.AsSpan(0, width - 3), "...");
        }
        return value.PadRight(width);
    }

    public static string CreatedHeader(int repoWidth) =>
        // Leading blanks line up with the "[x] " mark column and the icon+space row prefix
        // (mark, space, 1-char icon, space — 3 blank chars after the mark column).
        $"{"",-MarkWidth}   {"Appr",-ApprovalWidth} {"PR#",-PrIdWidth} {"Age",-AgeWidth} {"Bld",-BuildWidth} " +
        $"{FitColumn("Repo", repoWidth)} Title";

    public static string CreatedRow(PrEntry entry, bool marked, int repoWidth)
    {
        var pr = entry.Pr;
        var draft = pr.IsDraft ? "[DRAFT] " : "";
        var approval = ApprovalSummary(pr);
        var prId = $"!{pr.PullRequestId}";
        var mark = marked ? "[x]" : "[ ]";
        return $"{mark} {OverallIcon(pr)} {approval,-ApprovalWidth} {prId,-PrIdWidth} {Age(pr.CreationDate),-AgeWidth} {BuildIcon(entry.BuildStatus),-BuildWidth} " +
               $"{FitColumn(entry.RepoName, repoWidth)} {draft}{pr.Title}";
    }

    // Non-selectable divider row grouping the rows below it by team project.
    public static string ProjectSectionHeader(string projectName, int count, int width)
    {
        var label = $" {projectName} ({count}) ";
        var fill = Math.Max(0, width - label.Length);
        var left = fill / 2;
        var right = fill - left;
        return $"{new string('─', left)}{label}{new string('─', right)}";
    }

    public static string ReviewingRow(PrEntry entry, string myId)
    {
        var pr = entry.Pr;
        var draft = pr.IsDraft ? "[DRAFT] " : "";
        return $"{MyVoteIcon(pr, myId)}      !{pr.PullRequestId,-5} {Age(pr.CreationDate),3} {entry.RepoName,-20} {draft}{pr.Title}  (by {pr.CreatedBy.DisplayName})";
    }

    public static string VoteLabel(int vote) => vote switch
    {
        10 => "Approved",
        5 => "Approved w/ suggestions",
        0 => "No vote",
        -5 => "Waiting for author",
        -10 => "Rejected",
        _ => $"Unknown ({vote})"
    };

    private static string ShortRef(string refName) =>
        refName.StartsWith("refs/heads/", StringComparison.Ordinal) ? refName["refs/heads/".Length..] : refName;

    public static (bool CanComplete, string Reason) CompletionReadiness(GitPullRequest pr)
    {
        if (pr.IsDraft) return (false, "still a draft");

        var required = pr.Reviewers.Where(r => r.IsRequired).ToList();
        if (required.Any(r => r.Vote == -10)) return (false, "rejected by a required reviewer");

        if (pr.MergeStatus is "conflicts" or "failure" or "rejectedByPolicy")
            return (false, $"merge status is '{pr.MergeStatus}'");

        var pending = required.Where(r => r.Vote is not (10 or 5)).ToList();
        if (pending.Count > 0)
            return (false, $"waiting on {pending.Count} required reviewer(s): {string.Join(", ", pending.Select(r => r.DisplayName))}");

        if (required.Any(r => r.Vote == -5)) return (true, "approved, but a reviewer is waiting for author changes");

        return (true, "all required reviewers approved");
    }

    public static string DetailText(PrEntry entry, string myId)
    {
        var pr = entry.Pr;
        var sb = new StringBuilder();

        var draft = pr.IsDraft ? " [DRAFT]" : "";
        sb.AppendLine($"!{pr.PullRequestId}{draft}  {pr.Title}");
        sb.AppendLine($"Team project: {entry.ProjectName}   Repo: {entry.RepoName}");
        sb.AppendLine($"{ShortRef(pr.SourceRefName)} → {ShortRef(pr.TargetRefName)}   " +
                      $"created {Age(pr.CreationDate)} ago by {pr.CreatedBy.DisplayName}   merge: {pr.MergeStatus}   build: {BuildLabel(entry.BuildStatus)}");
        sb.AppendLine();

        if (pr.Reviewers.Count == 0)
        {
            sb.AppendLine("No reviewers assigned.");
        }
        else
        {
            foreach (var r in pr.Reviewers.OrderByDescending(r => r.IsRequired).ThenBy(r => r.DisplayName))
            {
                var mine = r.Id == myId ? " (you)" : "";
                var req = r.IsRequired ? " [required]" : "";
                sb.AppendLine($"  {VoteIcon(r.Vote)} {VoteLabel(r.Vote),-24} {r.DisplayName}{mine}{req}");
            }
        }

        sb.AppendLine();
        var (canComplete, reason) = CompletionReadiness(pr);
        sb.AppendLine($"Can complete: {(canComplete ? "YES" : "NO")} — {reason}");

        return sb.ToString();
    }

    public static string RenderThreads(List<CommentThread> threads)
    {
        var sb = new StringBuilder();
        var relevant = threads.Where(t => !t.IsDeleted && t.Comments.Any(c => !c.IsDeleted && !string.IsNullOrWhiteSpace(c.Content)))
            .OrderBy(t => t.Comments.FirstOrDefault()?.PublishedDate ?? DateTimeOffset.MinValue)
            .ToList();

        if (relevant.Count == 0)
        {
            sb.AppendLine("No comments on this pull request.");
            return sb.ToString();
        }

        foreach (var thread in relevant)
        {
            var file = thread.ThreadContext?.FilePath;
            if (!string.IsNullOrEmpty(file))
            {
                sb.AppendLine($"── {file} ──");
            }
            else
            {
                sb.AppendLine("── General ──");
            }

            foreach (var comment in thread.Comments.Where(c => !c.IsDeleted && !string.IsNullOrWhiteSpace(c.Content)))
            {
                sb.AppendLine($"[{comment.PublishedDate.LocalDateTime:yyyy-MM-dd HH:mm}] {comment.Author.DisplayName}:");
                sb.AppendLine(comment.Content);
                sb.AppendLine();
            }

            if (thread.Status is not null)
            {
                sb.AppendLine($"(thread status: {thread.Status})");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
