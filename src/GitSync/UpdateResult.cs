namespace GitSync;

public record UpdateResult(string Url, string CommitSha, string? BranchName, long? PullRequestId);
