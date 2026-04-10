using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Endpoints;
using OctopusCosyAnalyser.ApiService.Options;
using OctopusCosyAnalyser.ApiService.GraphQL;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.ApiService.Services.GraphQL;
using OctopusCosyAnalyser.ApiService.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add PostgreSQL DbContext
builder.AddNpgsqlDbContext<CosyDbContext>("cosydb");

// Bind configuration sections
var octopusOptions = new OctopusApiOptions();
builder.Configuration.GetSection(OctopusApiOptions.SectionName).Bind(octopusOptions);

var anthropicOptions = new AnthropicOptions();
builder.Configuration.GetSection(AnthropicOptions.SectionName).Bind(anthropicOptions);

builder.Services.Configure<OctopusApiOptions>(builder.Configuration.GetSection(OctopusApiOptions.SectionName));
builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection(AnthropicOptions.SectionName));

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

// Add Heat Pump Snapshot Worker
builder.Services.AddHostedService<HeatPumpSnapshotWorker>();

// Add Heat Pump Time Series Sync Worker (every 6 hours)
builder.Services.AddHostedService<HeatPumpTimeSeriesSyncWorker>();

// Add Cost Data Sync Worker (every 6 hours)
builder.Services.AddHostedService<CostDataSyncWorker>();

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

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "OctopusCosyAnalyser API is running.");

// Map Heat Pump endpoints
app.MapHeatPumpEndpoints();
app.MapAccountSettingsEndpoints();

app.MapDefaultEndpoints();

app.Run();
