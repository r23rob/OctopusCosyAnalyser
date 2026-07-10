using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Endpoints;
using OctopusCosyAnalyser.ApiService.Options;
using OctopusCosyAnalyser.ApiService.GraphQL;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.ApiService.Services.CurrentUser;
using OctopusCosyAnalyser.ApiService.Services.GraphQL;
using OctopusCosyAnalyser.ApiService.Services.SecretProtection;
using OctopusCosyAnalyser.ApiService.Workers;

var builder = WebApplication.CreateBuilder(args);

// Run-once mode: when invoked with --run-worker-once <name>, the host runs the named worker
// once and exits. Used by Azure Container Apps Jobs (or any cron runner) so the same image
// serves both the long-lived API and the scheduled background work.
var workerJobName = ResolveWorkerJobName(args);
var isRunOnceMode = workerJobName is not null;

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add PostgreSQL DbContext.
// Maximum Pool Size=10 keeps each replica well under the connection ceiling of small
// managed Postgres tiers (Azure Flex B1ms ≈ 50 conns; Cloud SQL smallest ≈ 25).
builder.AddNpgsqlDbContext<CosyDbContext>("cosydb", configureSettings: settings =>
{
    if (!string.IsNullOrEmpty(settings.ConnectionString) &&
        !settings.ConnectionString.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase))
    {
        settings.ConnectionString += ";Maximum Pool Size=10";
    }
});

// Current-user accessor — returns a fixed user for all requests (auth disabled).
// Workers iterate all tenants' devices via IgnoreQueryFilters().
if (isRunOnceMode)
{
    builder.Services.AddSingleton<ICurrentUserAccessor, SystemCurrentUserAccessor>();
}
else
{
    builder.Services.AddSingleton<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
}

// Trust X-Forwarded-* from ACA's ingress (TLS is terminated at the edge — the container
// sees HTTP). Without this, IsHttps is false, redirect URLs are http://, and cookie
// SecurePolicy decisions are wrong.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Data Protection — encrypts auth cookies and any IDataProtector-protected payloads.
// Persisted to the DB so keys survive container restarts and scale-to-zero on ACA.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<CosyDbContext>()
    .SetApplicationName("OctopusCosyAnalyser");

// Secret protection for Octopus / Anthropic credentials at rest.
builder.Services.AddSingleton<ISecretProtector, SecretProtector>();

// Bind configuration sections
var octopusOptions = new OctopusApiOptions();
builder.Configuration.GetSection(OctopusApiOptions.SectionName).Bind(octopusOptions);

var anthropicOptions = new AnthropicOptions();
builder.Configuration.GetSection(AnthropicOptions.SectionName).Bind(anthropicOptions);

builder.Services.Configure<OctopusApiOptions>(builder.Configuration.GetSection(OctopusApiOptions.SectionName));
builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection(AnthropicOptions.SectionName));

// CORS — when the SPA is hosted from a separate origin (Front Door / Static Web App
// in production), allow credentialed requests from configured allowed origins.
//
// Cors:AllowedOrigins is a comma-separated string (so Bicep can pass a single env var)
// or an array. Empty/whitespace entries are filtered so an unset value cleanly disables CORS.
var allowedSpaOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? new[] { builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty })
    .SelectMany(o => (o ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .ToArray();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Spa", policy =>
    {
        if (allowedSpaOrigins.Length == 0)
        {
            // No origins configured — assume same-origin deployment (nginx proxy / single container).
            policy.SetIsOriginAllowed(_ => false);
        }
        else
        {
            policy.WithOrigins(allowedSpaOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

// Add Octopus Energy API client with extended timeouts for large queries
// Paginated queries (e.g. applicableRates, sync-timeseries) may make many sequential API calls
builder.Services.AddHttpClient<IOctopusEnergyClient, OctopusEnergyClient>()
    .ConfigureHttpClient(client => client.Timeout = octopusOptions.HttpTimeout)
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = octopusOptions.HttpTimeout;
        options.AttemptTimeout.Timeout = octopusOptions.AttemptTimeout;
        options.CircuitBreaker.SamplingDuration = octopusOptions.CircuitBreakerSamplingDuration;
    });

// Add ZeroQL-based typed GraphQL service for backend API (heat pump queries)
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

// Add AI services
// API key can come from DB (Account Settings) or from config/env var as fallback
var anthropicKey = anthropicOptions.ApiKey;

// Detailed AI analysis service (raw HTTP client for full CSV-based analysis)
builder.Services.AddHttpClient<IAiAnalysisService, AiAnalysisService>(client =>
{
    client.Timeout = anthropicOptions.Timeout;
    if (!string.IsNullOrEmpty(anthropicKey))
    {
        // Pre-configure for config/env var key (fallback when no DB key is set)
        client.BaseAddress = new Uri(anthropicOptions.BaseUrl);
        client.DefaultRequestHeaders.Add("x-api-key", anthropicKey);
        client.DefaultRequestHeaders.Add("anthropic-version", anthropicOptions.ApiVersion);
    }
});

// Add HeatPumpAiService
builder.Services.AddScoped<IHeatPumpAiService, HeatPumpAiService>();

// Add Heat Pump Data Service (daily aggregates, time series enrichment)
builder.Services.AddSingleton<IHeatPumpDataService, HeatPumpDataService>();

// Add Tariff Sync Service
builder.Services.AddScoped<ITariffSyncService, TariffSyncService>();

// Worker registration:
//   - Long-running mode (the API container): each worker runs as a HostedService on its
//     internal interval.
//   - Run-once mode (--run-worker-once <name>, used by ACA Jobs): the worker class is
//     resolved as a transient service so we can call its run-once entry point and exit.
if (isRunOnceMode)
{
    builder.Services.AddTransient<HeatPumpSnapshotWorker>();
    builder.Services.AddTransient<HeatPumpTimeSeriesSyncWorker>();
    builder.Services.AddTransient<CostDataSyncWorker>();
    builder.Services.AddTransient<EnergyIntervalWorker>();
}
else
{
    builder.Services.AddHostedService<HeatPumpSnapshotWorker>();
    builder.Services.AddHostedService<HeatPumpTimeSeriesSyncWorker>();
    builder.Services.AddHostedService<CostDataSyncWorker>();
    builder.Services.AddHostedService<EnergyIntervalWorker>();
}

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CosyDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        db.Database.Migrate();
        logger.LogInformation("Database migration completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration failed");
        throw;
    }
}

// ── Run-once worker mode (for ACA Jobs / cron runners) ───────────────────────
if (isRunOnceMode)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Running worker {Worker} once and exiting", workerJobName);

    try
    {
        await RunWorkerOnceAsync(scope.ServiceProvider, workerJobName!, CancellationToken.None);
        logger.LogInformation("Worker {Worker} completed", workerJobName);
        return 0;
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Worker {Worker} failed", workerJobName);
        return 1;
    }
}

// Configure the HTTP request pipeline.
// Forwarded headers MUST run before any middleware that inspects scheme/IP (auth, CORS).
app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Spa");

app.MapGet("/", () => "OctopusCosyAnalyser API is running.");

// "Who am I" — returns the hardcoded user (auth disabled).
app.MapGet("/api/auth/me", () => Results.Ok(new
{
    id = HttpContextCurrentUserAccessor.FixedUserId,
    email = "Rob@hutchin.co.uk",
}));


app.MapHeatPumpEndpoints();
app.MapAccountSettingsEndpoints();
app.MapStatusEndpoints();

app.MapDefaultEndpoints();

await app.RunAsync();
return 0;

// ────────────────────────────────────────────────────────────────────────────
// Helpers (top-level local functions)
// ────────────────────────────────────────────────────────────────────────────

static string? ResolveWorkerJobName(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], "--run-worker-once", StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    var envVar = Environment.GetEnvironmentVariable("RUN_WORKER_ONCE");
    return string.IsNullOrWhiteSpace(envVar) ? null : envVar;
}

static async Task RunWorkerOnceAsync(IServiceProvider services, string workerName, CancellationToken ct)
{
    switch (workerName.ToLowerInvariant())
    {
        case "snapshot":
        case "heatpump-snapshot":
            await services.GetRequiredService<HeatPumpSnapshotWorker>().RunOnceAsync(ct);
            break;
        case "timeseries":
        case "heatpump-timeseries":
            await services.GetRequiredService<HeatPumpTimeSeriesSyncWorker>().RunOnceAsync(ct);
            break;
        case "cost":
        case "cost-sync":
            await services.GetRequiredService<CostDataSyncWorker>().RunOnceAsync(ct);
            break;
        case "energy-intervals":
        case "intervals":
            await services.GetRequiredService<EnergyIntervalWorker>().RunOnceAsync(ct);
            break;
        default:
            throw new ArgumentException(
                $"Unknown worker '{workerName}'. Valid: snapshot, timeseries, cost, energy-intervals.");
    }
}
