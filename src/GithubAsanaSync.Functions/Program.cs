using GithubAsanaSync.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddSingleton<IGitHubWebhookValidator, GitHubWebhookValidator>();
        services.AddSingleton<IIdMappingStore, TableIdMappingStore>();
        services.AddSingleton<IAsanaWebhookValidator, AsanaWebhookValidator>();
        services.AddSingleton<IWebhookSecretStore, TableWebhookSecretStore>();

        services.AddHttpClient<IAsanaClient, AsanaClient>();
        services.AddHttpClient<IGitHubClient, GitHubClient>();

        // Transient, like the typed clients they depend on.
        services.AddTransient<IIssueSyncService, IssueSyncService>();
        services.AddTransient<ITaskSyncService, TaskSyncService>();
    })
    .Build();

host.Run();
