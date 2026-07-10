using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Endpoints;
using OctopusCosyAnalyser.ApiService.Models;
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

// HttpContext + current-user accessor — required for multi-tenant query filtering.
// Transient lifetime allows DbContext pooling to work correctly (DbContext constructor
// injects ICurrentUserAccessor, and pooled contexts are created from root scope).
builder.Services.AddHttpContextAccessor();
if (isRunOnceMode)
{
    // Workers iterate all tenants' devices; they must not be scoped to one user.
    builder.Services.AddSingleton<ICurrentUserAccessor, SystemCurrentUserAccessor>();
}
else
{
    builder.Services.AddTransient<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
}

// ASP.NET Core Identity (self-hosted auth).
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedEmail = false;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<CosyDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddApiEndpoints();

// Cookie-based auth for the same-origin SPA (simpler + safer than JWT-in-localStorage).
// In production, ACA terminates TLS upstream so the app sees HTTP — set Always to ensure
// the cookie always gets `Secure`, and rely on the forwarded headers middleware below
// to make Url/Scheme reflect the actual public scheme.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var googleAuthEnabled = !string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret);

var authBuilder = builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        // SPA-friendly: API responses, not redirects to /login.
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

if (googleAuthEnabled)
{
    authBuilder
        // ExternalScheme cookie is required for the OAuth round-trip (AddIdentityCore
        // doesn't register it; AddIdentity would). Short-lived — only needed between the
        // Google redirect and our callback handler.
        .AddCookie(IdentityConstants.ExternalScheme, options =>
        {
            options.Cookie.Name = IdentityConstants.ExternalScheme;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        })
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId!;
            options.ClientSecret = googleClientSecret!;
            options.SignInScheme = IdentityConstants.ExternalScheme;
            // Routed under /api so the existing Vite/nginx proxy forwards it without changes.
            options.CallbackPath = "/api/auth/signin-google";
            options.SaveTokens = false;
        });
}
builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "OctopusCosyAnalyser API is running.");

// Identity API endpoints — register / login / forgot-password / reset-password / refresh / etc.
// Mounted under /api/auth so the SPA hits e.g. POST /api/auth/login.
app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>();

// External (Google) sign-in — challenge + callback handlers.
if (googleAuthEnabled)
{
    app.MapExternalAuthEndpoints();
}

// Logout endpoint — Identity API doesn't ship this for cookie auth; handle it ourselves.
app.MapPost("/api/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Ok();
}).RequireAuthorization();

// "Who am I" — useful for the SPA's beforeLoad guard.
app.MapGet("/api/auth/me", (HttpContext ctx) =>
{
    if (ctx.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        id = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
        email = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? ctx.User.Identity.Name,
    });
}).RequireAuthorization();

// Map Heat Pump endpoints — all require authentication.
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
