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
    })
    .Build();

host.Run();
