using System.Text.Json.Serialization;

namespace GithubAsanaSync.Functions.Models;

// Minimal shape of GitHub's "pull_request" webhook payload — only the
// fields this project actually reads. See:
// https://docs.github.com/en/webhooks/webhook-events-and-payloads#pull_request
public sealed class GitHubPullRequestEvent
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("pull_request")]
    public GitHubPullRequest PullRequest { get; set; } = new();

    [JsonPropertyName("repository")]
    public GitHubRepository Repository { get; set; } = new();
}

public sealed class GitHubPullRequest
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    // Only meaningful when action == "closed": true if the PR was merged,
    // false if it was closed without merging.
    [JsonPropertyName("merged")]
    public bool Merged { get; set; }
}
