using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Endpoints;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.ApiService.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add PostgreSQL DbContext
builder.AddNpgsqlDbContext<CosyDbContext>("cosydb");

// Add Octopus Energy API client with extended timeouts for large queries
// Paginated queries (e.g. applicableRates, sync-timeseries) may make many sequential API calls
builder.Services.AddHttpClient<OctopusEnergyClient>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(5))
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
    });

// Add AI Analysis Service (Anthropic API)
// API key can come from DB (Account Settings) or from env var / config as fallback
var anthropicKey = builder.Configuration["Anthropic:ApiKey"]
    ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

builder.Services.AddHttpClient<AiAnalysisService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
    if (!string.IsNullOrEmpty(anthropicKey))
    {
        // Pre-configure for env var key (fallback when no DB key is set)
        client.BaseAddress = new Uri("https://api.anthropic.com/");
        client.DefaultRequestHeaders.Add("x-api-key", anthropicKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }
});

// Add Heat Pump Snapshot Worker
builder.Services.AddHostedService<HeatPumpSnapshotWorker>();

// Add Heat Pump Time Series Sync Worker (every 6 hours)
builder.Services.AddHostedService<HeatPumpTimeSeriesSyncWorker>();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CosyDbContext>();
    db.Database.Migrate();
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
