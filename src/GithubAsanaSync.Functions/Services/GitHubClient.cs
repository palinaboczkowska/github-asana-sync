using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace GithubAsanaSync.Functions.Services;

public interface IGitHubClient
{
    Task<bool> IsIssueClosedAsync(string repository, int number, CancellationToken cancellationToken);

    Task SetIssueClosedAsync(string repository, int number, bool closed, CancellationToken cancellationToken);

    Task AddIssueCommentAsync(string repository, int number, string text, CancellationToken cancellationToken);
}

// Thin wrapper over the GitHub REST API, used only by the Asana -> GitHub
// direction. The token is read on each call rather than in the constructor:
// this client is built for every queue message, and a missing token must not
// break the GitHub -> Asana direction, which never needs it.
public sealed class GitHubClient(HttpClient httpClient, IConfiguration configuration) : IGitHubClient
{
    public async Task<bool> IsIssueClosedAsync(string repository, int number, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"repos/{repository}/issues/{number}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var issue = await response.Content.ReadFromJsonAsync<IssueState>(cancellationToken)
            ?? throw new InvalidOperationException("GitHub returned an empty issue.");

        return string.Equals(issue.State, "closed", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetIssueClosedAsync(string repository, int number, bool closed, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Patch, $"repos/{repository}/issues/{number}");
        request.Content = JsonContent.Create(closed
            ? new UpdateIssue("closed", "completed")
            : new UpdateIssue("open", null));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddIssueCommentAsync(string repository, int number, string text, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, $"repos/{repository}/issues/{number}/comments");
        request.Content = JsonContent.Create(new IssueComment(text));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var token = configuration["GitHubToken"];
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("GitHubToken is not configured, so the Asana -> GitHub direction cannot run.");
        }

        var baseUrl = (configuration["GitHubApiBaseUrl"] ?? "https://api.github.com").TrimEnd('/');
        var request = new HttpRequestMessage(method, $"{baseUrl}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("github-asana-sync", "1.0"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private sealed record IssueState([property: JsonPropertyName("state")] string State);

    private sealed record UpdateIssue(
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("state_reason"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StateReason);

    private sealed record IssueComment([property: JsonPropertyName("body")] string Body);
}
