using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Endpoints;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.ApiService.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add PostgreSQL DbContext
builder.AddNpgsqlDbContext<CosyDbContext>("cosydb");

// Add Octopus Energy API client
builder.Services.AddHttpClient<OctopusEnergyClient>();

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
app.MapEfficiencyEndpoints();

app.MapDefaultEndpoints();

app.Run();
