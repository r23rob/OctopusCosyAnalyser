using OctopusCosyAnalyser.ApiService.Application.AccountSettings;
using OctopusCosyAnalyser.ApiService.Application.Efficiency;
using OctopusCosyAnalyser.ApiService.Application.HeatPumpSnapshots;
using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Application.Tado;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Endpoints;
using OctopusCosyAnalyser.ApiService.Infrastructure;
using OctopusCosyAnalyser.ApiService.Infrastructure.Repositories;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.ApiService.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add PostgreSQL DbContext
builder.AddNpgsqlDbContext<CosyDbContext>("cosydb");

// Add Octopus Energy API client
builder.Services.AddHttpClient<OctopusEnergyClient>();

// Add Tado API client
builder.Services.AddHttpClient<TadoClient>();

// ── Infrastructure: repository implementations ────────────────────────────────
builder.Services.AddScoped<IEfficiencyRepository, EfficiencyRepository>();
builder.Services.AddScoped<IAccountSettingsRepository, AccountSettingsRepository>();
builder.Services.AddScoped<ITadoRepository, TadoRepository>();
builder.Services.AddScoped<IHeatPumpSnapshotRepository, HeatPumpSnapshotRepository>();
builder.Services.AddScoped<IHeatPumpProvider, OctopusHeatPumpProvider>();

// ── Application: use-case handlers ───────────────────────────────────────────
// Efficiency
builder.Services.AddScoped<GetEfficiencyRecordsHandler>();
builder.Services.AddScoped<GetEfficiencyRecordHandler>();
builder.Services.AddScoped<CreateEfficiencyRecordHandler>();
builder.Services.AddScoped<UpdateEfficiencyRecordHandler>();
builder.Services.AddScoped<DeleteEfficiencyRecordHandler>();
builder.Services.AddScoped<ComparePeriodHandler>();
builder.Services.AddScoped<GetEfficiencyGroupsHandler>();
builder.Services.AddScoped<FilterEfficiencyByTempHandler>();
// Account settings
builder.Services.AddScoped<GetAccountSettingsHandler>();
builder.Services.AddScoped<GetAccountSettingsByAccountHandler>();
builder.Services.AddScoped<UpsertAccountSettingsHandler>();
// Tado
builder.Services.AddScoped<GetTadoSettingsHandler>();
builder.Services.AddScoped<UpsertTadoSettingsHandler>();
// Heat pump snapshots
builder.Services.AddScoped<TakeHeatPumpSnapshotsHandler>();

// Add Heat Pump Snapshot Worker
builder.Services.AddHostedService<HeatPumpSnapshotWorker>();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CosyDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// Map Heat Pump endpoints
app.MapHeatPumpEndpoints();
app.MapAccountSettingsEndpoints();
app.MapTadoEndpoints();
app.MapEfficiencyEndpoints();

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
