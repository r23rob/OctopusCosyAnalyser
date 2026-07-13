using Amazon.Lambda.AspNetCoreServer.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using OctopusCosyAnalyser.ApiService;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Endpoints;
using OctopusCosyAnalyser.ApiService.Options;
using OctopusCosyAnalyser.ApiService.GraphQL;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.ApiService.Services.CurrentUser;
using OctopusCosyAnalyser.ApiService.Services.GraphQL;
using OctopusCosyAnalyser.ApiService.Services.SecretProtection;
using OctopusCosyAnalyser.ApiService.Workers;

// ── Lambda worker mode ──────────────────────────────────────────────────────
// When LAMBDA_WORKER_MODE is set, this container image serves as the
// EventBridge-triggered worker instead of the API.
if (Environment.GetEnvironmentVariable("LAMBDA_WORKER_MODE") == "true")
{
    await WorkerLambdaHandler.RunAsync();
    return 0;
}

var builder = WebApplication.CreateBuilder(args);

// Run-once mode: when invoked with --run-worker-once <name>, the host runs the named worker
// once and exits. Used by container jobs (or any cron runner) so the same image
// serves both the long-lived API and the scheduled background work.
var workerJobName = ResolveWorkerJobName(args);
var isRunOnceMode = workerJobName is not null;
var isLambda = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") is not null;

// ── Database ────────────────────────────────────────────────────────────────
// Detect whether a real database connection is available.
// When no connection string is configured the API runs in "lite mode":
// live data from the Octopus API still works, but history, snapshots,
// settings persistence, and background workers are disabled.
var connectionString = builder.Configuration.GetConnectionString("cosydb");
var databaseAvailable = !string.IsNullOrWhiteSpace(connectionString);

var features = new FeatureAvailability
{
    DatabaseAvailable = databaseAvailable,
    FallbackAccountNumber = builder.Configuration["OCTOPUS_ACCOUNT_NUMBER"],
    FallbackApiKey = builder.Configuration["OCTOPUS_API_KEY"],
    FallbackEuid = builder.Configuration["OCTOPUS_EUID"],
};
builder.Services.AddSingleton(features);

// PostgreSQL via standard Npgsql EF Core provider.
// Maximum Pool Size kept low for Lambda (each instance gets its own pool).
if (databaseAvailable)
{
    var pooledConnectionString = connectionString!.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase)
        ? connectionString
        : connectionString + ";Maximum Pool Size=5";

    builder.Services.AddDbContext<CosyDbContext>(options =>
        options.UseNpgsql(pooledConnectionString));
}
else
{
    builder.Services.AddDbContext<CosyDbContext>(options =>
        options.UseNpgsql("Host=localhost;Port=0;Database=lite_mode_unused"));
}

// Current-user accessor — returns a fixed user for all requests (auth disabled).
if (isRunOnceMode)
{
    builder.Services.AddSingleton<ICurrentUserAccessor, SystemCurrentUserAccessor>();
}
else
{
    builder.Services.AddSingleton<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
}

// Trust X-Forwarded-* from CloudFront / reverse proxies.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Data Protection — encrypts auth cookies and any IDataProtector-protected payloads.
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("OctopusCosyAnalyser");
if (databaseAvailable)
{
    dataProtection.PersistKeysToDbContext<CosyDbContext>();
}

builder.Services.AddSingleton<ISecretProtector, SecretProtector>();

// Bind configuration sections
var octopusOptions = new OctopusApiOptions();
builder.Configuration.GetSection(OctopusApiOptions.SectionName).Bind(octopusOptions);

var anthropicOptions = new AnthropicOptions();
builder.Configuration.GetSection(AnthropicOptions.SectionName).Bind(anthropicOptions);

builder.Services.Configure<OctopusApiOptions>(builder.Configuration.GetSection(OctopusApiOptions.SectionName));
builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection(AnthropicOptions.SectionName));

// CORS — when the SPA is hosted from a separate origin, allow credentialed requests.
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

// ── HTTP clients ────────────────────────────────────────────────────────────

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

// AI services
var anthropicKey = anthropicOptions.ApiKey;
builder.Services.AddHttpClient<IAiAnalysisService, AiAnalysisService>(client =>
{
    client.Timeout = anthropicOptions.Timeout;
    if (!string.IsNullOrEmpty(anthropicKey))
    {
        client.BaseAddress = new Uri(anthropicOptions.BaseUrl);
        client.DefaultRequestHeaders.Add("x-api-key", anthropicKey);
        client.DefaultRequestHeaders.Add("anthropic-version", anthropicOptions.ApiVersion);
    }
});

builder.Services.AddScoped<IHeatPumpAiService, HeatPumpAiService>();
builder.Services.AddSingleton<IHeatPumpDataService, HeatPumpDataService>();
builder.Services.AddScoped<ITariffSyncService, TariffSyncService>();

// ── Workers ─────────────────────────────────────────────────────────────────
// In Lambda API mode, workers are handled by a separate Lambda function —
// do NOT register them as HostedServices (they would start polling loops
// inside every API cold start).
if (databaseAvailable && !isLambda)
{
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
}

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// In Lambda, replace Kestrel with the Lambda Runtime Interface Client.
// Outside Lambda (AWS_LAMBDA_RUNTIME_API not set), this is a complete no-op.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

// ── Database migration ──────────────────────────────────────────────────────
// In production (Lambda), migrations run via CI/CD — skip here.
var skipMigration = string.Equals(
    Environment.GetEnvironmentVariable("SKIP_AUTO_MIGRATE"), "true",
    StringComparison.OrdinalIgnoreCase);

if (databaseAvailable && !skipMigration)
{
    using var scope = app.Services.CreateScope();
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
else if (!databaseAvailable)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Running in lite mode — no database configured. "
        + "Live data from Octopus API is available; history/snapshots/settings require PostgreSQL.");
}

// ── Run-once worker mode ────────────────────────────────────────────────────
if (isRunOnceMode && !databaseAvailable)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical("--run-worker-once requires a database connection. Set ConnectionStrings__cosydb.");
    return 1;
}
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

// ── HTTP pipeline ───────────────────────────────────────────────────────────
app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Spa");

var originSecret = app.Configuration["CLOUDFRONT_ORIGIN_SECRET"];
if (!string.IsNullOrEmpty(originSecret))
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/alive"))
        {
            await next();
            return;
        }

        if (context.Request.Headers["x-origin-verify"] != originSecret)
        {
            context.Response.StatusCode = 403;
            return;
        }

        await next();
    });
}

app.MapGet("/", () => "OctopusCosyAnalyser API is running.");

app.MapGet("/api/auth/me", () => Results.Ok(new
{
    id = HttpContextCurrentUserAccessor.FixedUserId,
    email = "Rob@hutchin.co.uk",
}));

app.MapGet("/api/features", (FeatureAvailability f) => Results.Ok(new
{
    database = f.DatabaseAvailable,
    history = f.History,
    efficiency = f.Efficiency,
    liveData = f.LiveData,
})).WithName("GetFeatures");

app.MapHeatPumpEndpoints();
app.MapAccountSettingsEndpoints();
app.MapStatusEndpoints();

app.MapHealthChecks("/health");
app.MapHealthChecks("/alive", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});

await app.RunAsync();
return 0;

// ── Helpers ─────────────────────────────────────────────────────────────────

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
