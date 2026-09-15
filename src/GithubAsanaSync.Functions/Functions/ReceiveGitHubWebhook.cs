using System.Text.Json;
using GithubAsanaSync.Functions.Models;
using GithubAsanaSync.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GithubAsanaSync.Functions.Functions;

// Accepts GitHub's "issues" and "pull_request" webhooks, validates the
// signature, and drops a SyncMessage on the queue. Deliberately does no
// Asana work itself — that happens in ProcessSyncMessage, so a slow/failing
// Asana call never risks GitHub's webhook delivery timeout (10s) or retries.
//
// This function has no HttpResponseData output, so the host always answers
// GitHub with a plain 200 — on purpose. An invalid signature and an
// ignored action/event look identical from the outside, which avoids
// turning the endpoint into an oracle for guessing the webhook secret.
public sealed class ReceiveGitHubWebhook(
    IGitHubWebhookValidator validator,
    IConfiguration configuration,
    ILogger<ReceiveGitHubWebhook> logger)
{
    private static readonly HashSet<string> HandledActions =
        new(StringComparer.OrdinalIgnoreCase) { "opened", "edited", "closed", "reopened" };

    [Function(nameof(ReceiveGitHubWebhook))]
    [ServiceBusOutput("github-issue-events", Connection = "ServiceBusConnection")]
    public async Task<string?> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/github")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var body = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);

        var secret = configuration["GitHubWebhookSecret"]
            ?? throw new InvalidOperationException("GitHubWebhookSecret is not configured.");
        var signature = GetHeader(request, "X-Hub-Signature-256");

        if (!validator.IsValidSignature(body, signature, secret))
        {
            logger.LogWarning("Rejected GitHub webhook with an invalid or missing signature.");
            return null;
        }

        var eventType = GetHeader(request, "X-GitHub-Event");
        var message = eventType?.ToLowerInvariant() switch
        {
            "issues" => BuildFromIssue(body),
            "pull_request" => BuildFromPullRequest(body),
            _ => null,
        };

        if (message is null)
        {
            logger.LogInformation("Ignoring '{EventType}' event — not one this project syncs.", eventType);
            return null;
        }

        logger.LogInformation(
            "Queuing sync for {Repository} {SourceKind} #{Number} ({Action}).",
            message.Repository, message.SourceKind, message.Number, message.Action);

        return JsonSerializer.Serialize(message);
    }

    private SyncMessage? BuildFromIssue(string body)
    {
        var payload = JsonSerializer.Deserialize<GitHubIssueEvent>(body)
            ?? throw new InvalidOperationException("Could not parse the GitHub issue payload.");

        if (!HandledActions.Contains(payload.Action))
        {
            return null;
        }

        return new SyncMessage(
            payload.Repository.FullName,
            GitHubSourceKind.Issue,
            payload.Issue.Number,
            payload.Issue.Title,
            payload.Issue.Body ?? string.Empty,
            ParseAction(payload.Action),
            payload.Issue.HtmlUrl);
    }

    private SyncMessage? BuildFromPullRequest(string body)
    {
        var payload = JsonSerializer.Deserialize<GitHubPullRequestEvent>(body)
            ?? throw new InvalidOperationException("Could not parse the GitHub pull request payload.");

        if (!HandledActions.Contains(payload.Action))
        {
            return null;
        }

        return new SyncMessage(
            payload.Repository.FullName,
            GitHubSourceKind.PullRequest,
            payload.PullRequest.Number,
            payload.PullRequest.Title,
            payload.PullRequest.Body ?? string.Empty,
            ParseAction(payload.Action),
            payload.PullRequest.HtmlUrl,
            payload.PullRequest.Merged);
    }

    private static GitHubIssueAction ParseAction(string action) => action.ToLowerInvariant() switch
    {
        "opened" => GitHubIssueAction.Opened,
        "edited" => GitHubIssueAction.Edited,
        "closed" => GitHubIssueAction.Closed,
        "reopened" => GitHubIssueAction.Reopened,
        _ => throw new InvalidOperationException($"Unhandled action '{action}'."),
    };

    private static string? GetHeader(HttpRequestData request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
}
