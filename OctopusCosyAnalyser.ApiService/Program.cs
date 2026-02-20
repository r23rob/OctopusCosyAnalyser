using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Features.AccountSettings;
using OctopusCosyAnalyser.ApiService.Features.Efficiency;
using OctopusCosyAnalyser.ApiService.Features.HeatPump;
using OctopusCosyAnalyser.ApiService.Features.HeatPumpSnapshots;
using OctopusCosyAnalyser.ApiService.Features.Tado;
using OctopusCosyAnalyser.ApiService.Infrastructure;
using OctopusCosyAnalyser.ApiService.Infrastructure.Repositories;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.ApiService.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add PostgreSQL DbContext
builder.AddNpgsqlDbContext<CosyDbContext>("cosydb");

// Add external API clients
builder.Services.AddHttpClient<OctopusEnergyClient>();
builder.Services.AddHttpClient<TadoClient>();

// ── Infrastructure: repository implementations ────────────────────────────────
builder.Services.AddScoped<IEfficiencyRepository, EfficiencyRepository>();
builder.Services.AddScoped<IAccountSettingsRepository, AccountSettingsRepository>();
builder.Services.AddScoped<ITadoRepository, TadoRepository>();
builder.Services.AddScoped<IHeatPumpSnapshotRepository, HeatPumpSnapshotRepository>();
builder.Services.AddScoped<IHeatPumpProvider, OctopusHeatPumpProvider>();

// ── Feature services (only those that need DI injection) ─────────────────────
builder.Services.AddScoped<TakeHeatPumpSnapshots>();

// ── Background worker ─────────────────────────────────────────────────────────
builder.Services.AddHostedService<HeatPumpSnapshotWorker>();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CosyDbContext>();
    db.Database.EnsureCreated();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/", () => "API service is running.");

// ── Feature endpoints ─────────────────────────────────────────────────────────
app.MapHeatPumpEndpoints();
app.MapAccountSettingsEndpoints();
app.MapTadoEndpoints();
app.MapEfficiencyEndpoints();

app.MapDefaultEndpoints();

app.Run();
