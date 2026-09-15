namespace GithubAsanaSync.Functions.Models;

public enum GitHubIssueAction
{
    Opened,
    Edited,
    Closed,
    Reopened,
}

public sealed record SyncMessage(
    string Repository,
    int IssueNumber,
    string Title,
    string Body,
    GitHubIssueAction Action,
    string HtmlUrl);
