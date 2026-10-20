using System.Text.Json;
using GithubAsanaSync.Functions.Models;
using GithubAsanaSync.Functions.Services;
using Microsoft.Azure.Functions.Worker;

namespace GithubAsanaSync.Functions.Functions;

// Queue entry point for the GitHub -> Asana sync. Runs off the queue instead
// of inline in the webhook receiver, so a slow or failing Asana call retries
// here (Service Bus redelivery + dead-letter) without ever affecting GitHub's
// view of the webhook delivery. Only reads the message; the sync rules live
// in IssueSyncService.
public sealed class ProcessSyncMessage(IIssueSyncService syncService)
{
    [Function(nameof(ProcessSyncMessage))]
    public async Task Run(
        [ServiceBusTrigger("github-issue-events", Connection = "ServiceBusConnection")] string messageBody,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<SyncMessage>(messageBody)
            ?? throw new InvalidOperationException("Could not parse the queued sync message.");

        await syncService.HandleAsync(message, cancellationToken);
    }
}
