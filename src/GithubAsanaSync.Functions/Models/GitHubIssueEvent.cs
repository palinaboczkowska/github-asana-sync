using System.Text.Json.Serialization;

namespace GithubAsanaSync.Functions.Models;

// Minimal shape of GitHub's "issues" webhook payload — only the fields this
// project actually reads. See:
// https://docs.github.com/en/webhooks/webhook-events-and-payloads#issues
public sealed class GitHubIssueEvent
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("issue")]
    public GitHubIssue Issue { get; set; } = new();

    [JsonPropertyName("repository")]
    public GitHubRepository Repository { get; set; } = new();
}

public sealed class GitHubIssue
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}

public sealed class GitHubRepository
{
    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;
}
