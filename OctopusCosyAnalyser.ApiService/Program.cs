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
builder.Services.AddHttpClient<OctopusEnergyClient>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(2))
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
    });

// Add AI Analysis Service (Anthropic API)
var anthropicKey = builder.Configuration["Anthropic:ApiKey"]
    ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

if (!string.IsNullOrEmpty(anthropicKey))
{
    builder.Services.AddHttpClient<AiAnalysisService>(client =>
    {
        client.BaseAddress = new Uri("https://api.anthropic.com/");
        client.DefaultRequestHeaders.Add("x-api-key", anthropicKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        client.Timeout = TimeSpan.FromMinutes(3);
    });
}
else
{
    builder.Services.AddSingleton<AiAnalysisService>(sp =>
        new AiAnalysisService(new HttpClient(), sp.GetRequiredService<ILogger<AiAnalysisService>>()));
}

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
