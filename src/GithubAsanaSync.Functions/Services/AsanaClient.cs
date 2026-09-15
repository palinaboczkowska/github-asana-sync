using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace GithubAsanaSync.Functions.Services;

public sealed class AsanaClient : IAsanaClient
{
    private readonly HttpClient _httpClient;
    private readonly string _projectGid;

    public AsanaClient(HttpClient httpClient, IConfiguration configuration)
    {
        var baseUrl = configuration["AsanaBaseUrl"] ?? "https://app.asana.com/api/1.0";
        var accessToken = configuration["AsanaAccessToken"]
            ?? throw new InvalidOperationException("AsanaAccessToken is not configured.");
        _projectGid = configuration["AsanaProjectGid"]
            ?? throw new InvalidOperationException("AsanaProjectGid is not configured.");

        httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        _httpClient = httpClient;
    }

    public async Task<string> CreateTaskAsync(string name, string notes, CancellationToken cancellationToken)
    {
        var request = new AsanaEnvelope<CreateTaskData>(new CreateTaskData(name, notes, [_projectGid]));

        using var response = await _httpClient.PostAsJsonAsync("tasks", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AsanaEnvelope<TaskGidData>>(cancellationToken)
            ?? throw new InvalidOperationException("Asana returned an empty response for task creation.");

        return body.Data.Gid;
    }

    public async Task UpdateTaskAsync(string taskGid, bool completed, CancellationToken cancellationToken)
    {
        var request = new AsanaEnvelope<UpdateTaskData>(new UpdateTaskData(completed));

        using var response = await _httpClient.PutAsJsonAsync($"tasks/{taskGid}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddCommentAsync(string taskGid, string text, CancellationToken cancellationToken)
    {
        var request = new AsanaEnvelope<AddCommentData>(new AddCommentData(text));

        using var response = await _httpClient.PostAsJsonAsync($"tasks/{taskGid}/stories", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed record AsanaEnvelope<T>([property: JsonPropertyName("data")] T Data);

    private sealed record CreateTaskData(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("notes")] string Notes,
        [property: JsonPropertyName("projects")] string[] Projects);

    private sealed record UpdateTaskData([property: JsonPropertyName("completed")] bool Completed);

    private sealed record AddCommentData([property: JsonPropertyName("text")] string Text);

    private sealed record TaskGidData([property: JsonPropertyName("gid")] string Gid);
}
