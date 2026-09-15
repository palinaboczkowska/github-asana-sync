namespace GithubAsanaSync.Functions.Models;

public enum GitHubIssueAction
{
    Opened,
    Edited,
    Closed,
    Reopened,
}

public enum GitHubSourceKind
{
    Issue,
    PullRequest,
}

public sealed record SyncMessage(
    string Repository,
    GitHubSourceKind SourceKind,
    int Number,
    string Title,
    string Body,
    GitHubIssueAction Action,
    string HtmlUrl,
    bool Merged = false);
