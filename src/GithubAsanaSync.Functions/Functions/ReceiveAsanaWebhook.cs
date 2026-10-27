using System.Net;
using System.Text.Json;
using GithubAsanaSync.Functions.Models;
using GithubAsanaSync.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GithubAsanaSync.Functions.Functions;

// Accepts Asana's webhook for the synced project and queues one message per
// changed task. Like the GitHub receiver it does no API work itself.
//
// Two kinds of request arrive here:
// - The handshake, once, when the webhook is created: Asana sends a secret
//   in "X-Hook-Secret" and expects it echoed back. The secret is stored and
//   used to check every later delivery.
// - Event deliveries, signed in "X-Hook-Signature".
//
// The endpoint needs the Function key (AuthorizationLevel.Function), so only
// someone holding the key can perform a handshake and replace the stored
// secret. The key goes into the webhook's target URL when it is registered.
public sealed class ReceiveAsanaWebhook(
    IAsanaWebhookValidator validator,
    IWebhookSecretStore secretStore,
    ILogger<ReceiveAsanaWebhook> logger)
{
    [Function(nameof(ReceiveAsanaWebhook))]
    public async Task<AsanaWebhookOutput> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "webhooks/asana")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var handshakeSecret = GetHeader(request, "X-Hook-Secret");
        if (!string.IsNullOrEmpty(handshakeSecret))
        {
            await secretStore.SaveAsanaSecretAsync(handshakeSecret, cancellationToken);
            logger.LogInformation("Completed the Asana webhook handshake and stored the new secret.");

            var handshake = request.CreateResponse(HttpStatusCode.OK);
            handshake.Headers.Add("X-Hook-Secret", handshakeSecret);
            return new AsanaWebhookOutput { Response = handshake };
        }

        var body = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);
        var secret = await secretStore.GetAsanaSecretAsync(cancellationToken);

        if (secret is null || !validator.IsValidSignature(body, GetHeader(request, "X-Hook-Signature"), secret))
        {
            logger.LogWarning("Rejected Asana webhook with an invalid or missing signature.");
            return new AsanaWebhookOutput { Response = request.CreateResponse(HttpStatusCode.Unauthorized) };
        }

        var messages = ParseTaskMessages(body);

        if (messages.Length > 0)
        {
            logger.LogInformation("Queuing Asana sync for {Count} task(s).", messages.Length);
        }

        return new AsanaWebhookOutput
        {
            Messages = messages.Length > 0 ? messages : null,
            Response = request.CreateResponse(HttpStatusCode.OK),
        };
    }

    // Asana may batch several events, and several can concern the same task;
    // one message per task is enough, since the worker reads the current state.
    // Heartbeats carry an empty list and produce no messages.
    public static string[] ParseTaskMessages(string body)
    {
        var payload = JsonSerializer.Deserialize<AsanaWebhookPayload>(body);

        return (payload?.Events ?? [])
            .Where(e => e.Resource?.ResourceType == "task" && !string.IsNullOrEmpty(e.Resource.Gid))
            .Select(e => e.Resource!.Gid!)
            .Distinct()
            .Select(gid => JsonSerializer.Serialize(new AsanaTaskMessage(gid)))
            .ToArray();
    }

    private static string? GetHeader(HttpRequestData request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
}

public sealed class AsanaWebhookOutput
{
    [ServiceBusOutput("github-issue-events", Connection = "ServiceBusConnection")]
    public string[]? Messages { get; set; }

    public HttpResponseData Response { get; set; } = null!;
}
