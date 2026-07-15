using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Http.Resilience;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.GraphQL;
using OctopusCosyAnalyser.ApiService.Options;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.ApiService.Services.CurrentUser;
using OctopusCosyAnalyser.ApiService.Services.GraphQL;
using OctopusCosyAnalyser.ApiService.Services.SecretProtection;
using OctopusCosyAnalyser.ApiService.Workers;

namespace OctopusCosyAnalyser.ApiService;

public static class WorkerLambdaHandler
{
    public static async Task RunAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        ConfigureWorkerServices(builder);
        var host = builder.Build();

        using var bootstrap = LambdaBootstrapBuilder
            .Create<JsonElement>(
                async (evt, context) => await HandleEventAsync(host.Services, evt, context),
                new DefaultLambdaJsonSerializer())
            .Build();

        await bootstrap.RunAsync();
    }

    private static async Task HandleEventAsync(
        IServiceProvider services, JsonElement evt, ILambdaContext context)
    {
        var workerName = evt.TryGetProperty("worker", out var prop)
            ? prop.GetString()
            : throw new ArgumentException("Missing 'worker' property in event payload");

        context.Logger.LogInformation($"Invoking worker: {workerName}");

        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        switch (workerName!.ToLowerInvariant())
        {
            case "snapshot":
                await sp.GetRequiredService<HeatPumpSnapshotWorker>().RunOnceAsync(CancellationToken.None);
                break;
            case "timeseries":
                await sp.GetRequiredService<HeatPumpTimeSeriesSyncWorker>().RunOnceAsync(CancellationToken.None);
                break;
            case "cost":
                await sp.GetRequiredService<CostDataSyncWorker>().RunOnceAsync(CancellationToken.None);
                break;
            case "energy-intervals":
                await sp.GetRequiredService<EnergyIntervalWorker>().RunOnceAsync(CancellationToken.None);
                break;
            default:
                throw new ArgumentException($"Unknown worker '{workerName}'");
        }

        context.Logger.LogInformation($"Worker {workerName} completed");
    }

    private static void ConfigureWorkerServices(IHostApplicationBuilder builder)
    {
        var features = new FeatureAvailability
        {
            DatabaseAvailable = true,
            FallbackAccountNumber = builder.Configuration["OCTOPUS_ACCOUNT_NUMBER"],
            FallbackApiKey = builder.Configuration["OCTOPUS_API_KEY"],
            FallbackEuid = builder.Configuration["OCTOPUS_EUID"],
        };
        builder.Services.AddSingleton(features);

        builder.Services.AddSingleton<IAmazonDynamoDB>(sp => new AmazonDynamoDBClient());
        builder.Services.AddSingleton<ICosyDataStore, DynamoDataStore>();

        builder.Services.AddSingleton<ICurrentUserAccessor, SystemCurrentUserAccessor>();
        builder.Services.AddSingleton<ISecretProtector, SecretProtector>();

        // Data Protection — keys are persisted to DynamoDB so they survive Lambda cold
        // starts and are shared with the API Lambda (SecretProtector relies on the same
        // key ring to decrypt settings encrypted by the API).
        builder.Services.AddDataProtection()
            .SetApplicationName("OctopusCosyAnalyser")
            .PersistKeysToDynamoDb();

        var octopusOptions = new OctopusApiOptions();
        builder.Configuration.GetSection(OctopusApiOptions.SectionName).Bind(octopusOptions);
        builder.Services.Configure<OctopusApiOptions>(builder.Configuration.GetSection(OctopusApiOptions.SectionName));

        var anthropicOptions = new AnthropicOptions();
        builder.Configuration.GetSection(AnthropicOptions.SectionName).Bind(anthropicOptions);
        builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection(AnthropicOptions.SectionName));

        builder.Services.AddHttpClient<IOctopusEnergyClient, OctopusEnergyClient>()
            .ConfigureHttpClient(client => client.Timeout = octopusOptions.HttpTimeout)
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = octopusOptions.HttpTimeout;
                options.AttemptTimeout.Timeout = octopusOptions.AttemptTimeout;
                options.CircuitBreaker.SamplingDuration = octopusOptions.CircuitBreakerSamplingDuration;
            });

        builder.Services.AddSingleton<IOctopusTokenService, OctopusTokenService>();
        builder.Services.AddTransient<OctopusAuthHandler>();
        builder.Services.AddHttpClient<OctopusGraphQLClient>(client =>
            {
                client.BaseAddress = new Uri(octopusOptions.BackendApiUrl);
                client.Timeout = octopusOptions.HttpTimeout;
            })
            .AddHttpMessageHandler<OctopusAuthHandler>()
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = octopusOptions.HttpTimeout;
                options.AttemptTimeout.Timeout = octopusOptions.AttemptTimeout;
                options.CircuitBreaker.SamplingDuration = octopusOptions.CircuitBreakerSamplingDuration;
            });
        builder.Services.AddScoped<IOctopusGraphQLService, OctopusGraphQLService>();

        builder.Services.AddScoped<ITariffSyncService, TariffSyncService>();

        builder.Services.AddTransient<HeatPumpSnapshotWorker>();
        builder.Services.AddTransient<HeatPumpTimeSeriesSyncWorker>();
        builder.Services.AddTransient<CostDataSyncWorker>();
        builder.Services.AddTransient<EnergyIntervalWorker>();
    }
}
