using System.Text.Json;
using GithubAsanaSync.Functions.Models;
using GithubAsanaSync.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace GithubAsanaSync.Functions.Functions;

// Does the actual GitHub -> Asana sync. Runs off the queue instead of
// inline in the webhook receiver, so a slow or failing Asana API call
// retries here (Service Bus redelivery + dead-letter) without ever
// affecting GitHub's view of the webhook delivery.
public sealed class ProcessSyncMessage(
    IAsanaClient asanaClient,
    IIdMappingStore idMappingStore,
    ILogger<ProcessSyncMessage> logger)
{
    [Function(nameof(ProcessSyncMessage))]
    public async Task Run(
        [ServiceBusTrigger("github-issue-events", Connection = "ServiceBusConnection")] string messageBody,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<SyncMessage>(messageBody)
            ?? throw new InvalidOperationException("Could not parse the queued sync message.");

        var taskGid = await idMappingStore.FindAsanaTaskGidAsync(message.Repository, message.IssueNumber, cancellationToken);

        if (taskGid is null)
        {
            var notes = $"{message.Body}\n\n{message.HtmlUrl}";
            taskGid = await asanaClient.CreateTaskAsync(message.Title, notes, cancellationToken);
            await idMappingStore.SaveMappingAsync(message.Repository, message.IssueNumber, taskGid, cancellationToken);

            logger.LogInformation(
                "Created Asana task {TaskGid} for {Repository}#{IssueNumber}.",
                taskGid, message.Repository, message.IssueNumber);
        }

        switch (message.Action)
        {
            case GitHubIssueAction.Closed:
                await asanaClient.UpdateTaskAsync(taskGid, completed: true, cancellationToken);
                break;

            case GitHubIssueAction.Reopened:
                await asanaClient.UpdateTaskAsync(taskGid, completed: false, cancellationToken);
                break;

            case GitHubIssueAction.Edited:
                await asanaClient.AddCommentAsync(taskGid, $"Issue edited on GitHub: {message.HtmlUrl}", cancellationToken);
                break;

            case GitHubIssueAction.Opened:
                // The CreateTaskAsync call above already covers this case.
                break;
        }
    }
}
