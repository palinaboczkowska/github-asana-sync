using System.Text.Json.Serialization;

namespace GithubAsanaSync.Functions.Models;

// Queued when Asana reports that a task changed. Carries only the task GID:
// Asana's event payload does not say what the new value is, so the worker
// reads the task's current state itself. That also makes a late or repeated
// delivery harmless — it always acts on how things are now.
public sealed record AsanaTaskMessage(string AsanaTaskGid);

// One webhook delivery from Asana. Heartbeats arrive with an empty list.
public sealed record AsanaWebhookPayload(
    [property: JsonPropertyName("events")] List<AsanaEvent>? Events);

public sealed record AsanaEvent(
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("resource")] AsanaResource? Resource);

public sealed record AsanaResource(
    [property: JsonPropertyName("gid")] string? Gid,
    [property: JsonPropertyName("resource_type")] string? ResourceType);

// The GitHub issue or pull request an Asana task was created for.
public sealed record GitHubItemRef(string Repository, GitHubSourceKind Kind, int Number);
