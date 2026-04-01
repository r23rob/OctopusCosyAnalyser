using Microsoft.Extensions.Http.Resilience;
using OctopusCosyAnalyser.Web;
using OctopusCosyAnalyser.Web.Components;
using OctopusCosyAnalyser.Web.Services;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

// Radzen component services (notifications, dialogs, tooltips, etc.)
builder.Services.AddRadzenComponents();

builder.Services.AddHttpClient<WeatherApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

builder.Services.AddHttpClient<HeatPumpApiClient>(client =>
    {
        client.BaseAddress = new("https+http://apiservice");
        client.Timeout = TimeSpan.FromMinutes(2);
    });

// Override the standard resilience handler timeouts for the HeatPumpApiClient
// (the handler itself is added globally by AddServiceDefaults)
builder.Services.Configure<HttpStandardResilienceOptions>("HeatPumpApiClient-standard", options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
