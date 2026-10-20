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

        services.AddHttpClient<IAsanaClient, AsanaClient>();

        // Transient, like the typed AsanaClient it depends on.
        services.AddTransient<IIssueSyncService, IssueSyncService>();
    })
    .Build();

host.Run();
