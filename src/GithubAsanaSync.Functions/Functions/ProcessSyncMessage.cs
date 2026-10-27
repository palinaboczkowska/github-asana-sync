using System.Text.Json;
using GithubAsanaSync.Functions.Models;
using GithubAsanaSync.Functions.Services;
using Microsoft.Azure.Functions.Worker;

namespace GithubAsanaSync.Functions.Functions;

// Queue entry point for both directions. Runs off the queue instead of
// inline in the webhook receivers, so a slow or failing API call retries
// here (Service Bus redelivery + dead-letter) without ever affecting how
// GitHub or Asana see the webhook delivery. Only reads the message; the sync
// rules live in IssueSyncService (GitHub -> Asana) and TaskSyncService
// (Asana -> GitHub).
//
// Both kinds share the existing queue rather than a new one, so adding the
// Asana direction needed no Service Bus change. An Asana message is told
// apart by its AsanaTaskGid property.
public sealed class ProcessSyncMessage(IIssueSyncService issueSyncService, ITaskSyncService taskSyncService)
{
    [Function(nameof(ProcessSyncMessage))]
    public async Task Run(
        [ServiceBusTrigger("github-issue-events", Connection = "ServiceBusConnection")] string messageBody,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(messageBody);

        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(nameof(AsanaTaskMessage.AsanaTaskGid), out _))
        {
            var asanaMessage = document.Deserialize<AsanaTaskMessage>()
                ?? throw new InvalidOperationException("Could not parse the queued Asana message.");

            await taskSyncService.HandleAsync(asanaMessage, cancellationToken);
            return;
        }

        var message = document.Deserialize<SyncMessage>()
            ?? throw new InvalidOperationException("Could not parse the queued sync message.");

        await issueSyncService.HandleAsync(message, cancellationToken);
    }
}
