# OctopusCosyAnalyser — Project Context

## Purpose

A personal heat pump monitoring dashboard for Octopus Energy Cosy heat pump customers. The goal is a simple, clear view of how your heat pump is running — efficiency, power, temperatures, and energy use — with the long-term aim of passing this data through AI to suggest improvements (e.g. whether weather compensation curve adjustments would help).

**Core question the app answers:** Is my heat pump running efficiently, and did any changes I made improve it?

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend API | .NET 10, ASP.NET Core Minimal APIs |
| Frontend | Blazor Interactive Server, Radzen UI v9 |
| Database | PostgreSQL 17 (EF Core 10, Npgsql) |
| Orchestration (dev) | .NET Aspire |
| Container | Docker / Docker Compose |
| CI/CD | GitHub Actions → ghcr.io (multi-platform: amd64 + arm64) |

## Project Structure

```
OctopusCosyAnalyser/
├── OctopusCosyAnalyser.AppHost/        # .NET Aspire orchestrator (postgres + pgadmin + services)
├── OctopusCosyAnalyser.ServiceDefaults/ # Shared: health checks, OpenTelemetry, service discovery
├── OctopusCosyAnalyser.Shared/         # Shared DTOs (Models/)
│   └── Models/                         # *Dto.cs files — the API contract
├── OctopusCosyAnalyser.ApiService/     # Backend API (Minimal APIs)
│   ├── Data/CosyDbContext.cs           # EF Core context (PostgreSQL)
│   ├── Models/                         # EF entity models
│   ├── Endpoints/                      # Minimal API endpoint groups
│   │   ├── HeatPumpEndpoints.cs        # /api/heatpump/* (main)
│   │   ├── AccountSettingsEndpoints.cs # /api/settings/*
│   │   ├── EfficiencyEndpoints.cs      # /api/efficiency/*
│   │   └── TadoEndpoints.cs            # /api/tado/*
│   ├── Services/
│   │   ├── OctopusEnergyClient.cs      # GraphQL + REST client for Octopus API
│   │   ├── TadoClient.cs               # Tado API client
│   │   ├── GraphQLIntrospection.cs     # Schema introspection helpers
│   │   └── Efficiency*.cs              # Efficiency analysis services
│   ├── Workers/HeatPumpSnapshotWorker.cs # 15-min background data collector
│   ├── Migrations/                     # EF Core migrations
│   └── Program.cs                      # Service registration + route mapping
├── OctopusCosyAnalyser.Web/            # Blazor Server frontend
│   ├── Components/Pages/               # Blazor pages
│   │   └── HeatPump/                   # All heat pump pages
│   ├── Components/Layout/NavMenu.razor # Navigation sidebar
│   └── Services/HeatPumpApiClient.cs   # Typed HTTP client → ApiService
└── OctopusCosyAnalyser.Tests/          # NUnit tests (unit + integration)
```

## Key Patterns

### Adding a new Octopus GraphQL query

Follow this 4-layer pattern:

1.  **OctopusEnergyClient.cs** — Add query method returning `Task<JsonDocument>`
    -   Simple queries: use raw string literal with `$"""` interpolation
    -   Parameterised queries: use `ExecuteRawQueryAsync()` with a variables object
    -   Auth is handled automatically via `GetAuthTokenAsync()` (JWT cached 55 min)

2.  **HeatPumpEndpoints.cs** — Add endpoint in `MapHeatPumpEndpoints()`
    -   Use `GetSettingsForAccountAsync(db, accountNumber)` or `GetDeviceAndSettingsAsync(db, deviceId)` for auth
    -   Return `Results.Ok(data)` with the response

3.  **HeatPumpApiClient.cs** — Add frontend client method
    -   Typed methods return DTOs; raw methods return `Task<string>` for JSON

4.  **Blazor page** — Add or update a `.razor` page in `Components/Pages/HeatPump/`
    -   Charts use Radzen (`RadzenChart`, `RadzenLineSeries`, `RadzenColumnSeries`)
    -   Add nav entry in `Components/Layout/NavMenu.razor`

## Database Tables (EF Core)

- PostgreSQL via EF Core with Npgsql
- Auto-migrates on startup (`db.Database.Migrate()`)
- Single migration: `20260220232518_InitialCreate`
- Add new migrations with: `dotnet ef migrations add <Name> --project OctopusCosyAnalyser.ApiService`

| Table | Purpose |
|-------|---------|
| `HeatPumpDevices` | Registered heat pump devices (DeviceId, AccountNumber, MPAN, Euid) |
| `HeatPumpSnapshots` | 15-min telemetry snapshots (COP, temps, power, zone state, weather compensation, flow temp range) |
| `ConsumptionReadings` | Smart meter readings (kWh, demand) |
| `OctopusAccountSettings` | Octopus API credentials (AccountNumber, ApiKey) |
| `HeatPumpEfficiencyRecords` | Manual daily records for efficiency tracking |

Unique constraints prevent duplicate snapshots `(DeviceId, SnapshotTakenAt)` and consumption readings `(DeviceId, ReadAt)`.

## Octopus Energy API

Base URL: `https://api.octopus.energy/v1/graphql/`

Authentication: POST to GraphQL with `obtainKrakenToken(input: {APIKey: "..."})` mutation → returns JWT. Token cached for 55 minutes in a `ConcurrentDictionary`.

All user-supplied values (`apiKey`, `accountNumber`, `euid`, `deviceId`, `make`) are validated against `^[A-Za-z0-9\-_]{1,200}` before string interpolation into GraphQL payloads to prevent injection.

### GraphQL Queries in Use

| Query | Method | Used By |
|-------|--------|---------|
| `account(accountNumber)` | `GetAccountAsync` | Device setup — discovers MPAN, meter serial, smart device IDs |
| `viewer { accounts { properties } }` | `GetViewerPropertiesAsync` | Device setup — finds EUIDs via occupierEuids |
| `viewer { ... heatPumpDevice }` | `GetViewerPropertiesWithDevicesAsync` | Device setup fallback — device details + EUIDs |
| `octoHeatPumpControllerEuids(accountNumber)` | `GetHeatPumpControllerEuidsAsync` | Device setup fallback — direct EUID lookup |
| `heatPumpDevice(accountNumber, propertyId)` | `GetHeatPumpDeviceAsync` | Debug endpoint — device serial/make/model |
| `heatPumpStatus` | `GetHeatPumpStatusAsync` | Debug/status endpoints |
| `heatPumpVariants` | `GetHeatPumpVariantsAsync` | Debug endpoint — lists supported makes/models |
| **Batched query (4-in-1):** `octoHeatPumpControllerStatus` + `octoHeatPumpControllerConfiguration` + `octoHeatPumpLivePerformance` + `octoHeatPumpLifetimePerformance` | `GetHeatPumpStatusAndConfigAsync` | **Primary workhorse** — used by `/summary` endpoint and the 15-min snapshot worker |
| `octoHeatPumpTimeRangedPerformance(euid, startAt, endAt)` | `GetHeatPumpTimeRangedPerformanceAsync` | `/time-ranged` endpoint — aggregated totals for a date range |
| `octoHeatPumpTimeSeriesPerformance(euid, startAt, endAt, performanceGrouping)` | `GetHeatPumpTimeSeriesPerformanceAsync` | `/time-series` endpoint — bucketed data for charts |

### REST API (not GraphQL)

Consumption history uses basic auth REST:
`GET https://api.octopus.energy/v1/electricity-meter-points/{mpan}/meters/{serialNumber}/consumption/`

### GraphQL Queries NOT implemented (available in Octopus API but unused)
- `heatPumpControllerConfiguration`
- `heatPumpControllerStatus`
- `octoHeatPumpControllersAtLocation`

## Background Worker

`HeatPumpSnapshotWorker` runs every **15 minutes**. For each active `HeatPumpDevice`, it:
1. Calls `GetHeatPumpStatusAndConfigAsync` (the batched 4-in-1 query)
2. Extracts: COP, heat output, power input, outdoor temp, lifetime performance, room temp/humidity, zone setpoints, weather compensation settings (enabled + min/max range), flow temperature (current + allowable min/max range)
3. Upserts a `HeatPumpSnapshot` row (skips duplicates via unique constraint)

## API Endpoints

### `/api/heatpump`
| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/setup` | Discover and register heat pump device for an account |
| GET | `/devices` | List registered devices |
| GET | `/summary/{deviceId}` | Live parsed summary (COP, temps, zones) |
| GET | `/snapshots/{deviceId}` | Historical snapshots with date range filter |
| GET | `/snapshots/{deviceId}/latest` | Latest snapshot timestamp + health status |
| GET | `/time-series/{accountNumber}/{euid}` | Bucketed chart data from Octopus API |
| GET | `/time-ranged/{accountNumber}/{euid}` | Aggregated totals for a date range |
| GET | `/consumption/{deviceId}` | Smart meter consumption readings |
| POST | `/sync/{deviceId}` | Backfill consumption readings |

### `/api/settings`
CRUD for Octopus account credentials (AccountNumber + ApiKey).

### `/api/efficiency`
| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/records` | List efficiency records (date filter) |
| POST | `/records` | Create daily record |
| PUT | `/records/{id}` | Update record |
| DELETE | `/records/{id}` | Delete record |
| GET | `/comparison` | Before/after efficiency comparison (HDD-normalised) |
| GET | `/groups` | Records grouped by ChangeDescription |
| GET | `/filter` | Filter by outdoor temperature range |

## Blazor Pages

All under `/heatpump`:

| Page | Route | Shows |
|------|-------|-------|
| Dashboard | `/heatpump` | Current status overview + snapshot worker health indicator |
| Performance | `/heatpump/performance` | Live COP, heat output, power input |
| Home Comfort | `/heatpump/comfort` | Indoor temp and humidity |
| System Health | `/heatpump/system` | Device connectivity, controller state |
| History | `/heatpump/history` | Historical snapshot charts |
| Efficiency Tracking | `/heatpump/efficiency` | Manual daily records (CRUD) |
| Insights | `/heatpump/insights` | Before/after comparison, grouped analysis |

Settings page at `/settings` for Octopus API credentials.

## Efficiency Analysis

`EfficiencyCalculationService` computes derived metrics:
- **Heating Degree Days (HDD)** = max(0, 15.5 - outdoorAvgC) — standard UK baseline
- **Normalised efficiency** = kWh / HDD (excludes days where HDD = 0)

`EfficiencyAnalysisService` provides analysis helpers:
- **Summarise** — computes averages across a set of records (only HDD > 0 days contribute to normalised efficiency)
- **Compare** — baseline vs change period comparison with improvement % and warnings
- **GroupByChange** — groups records by ChangeDescription for per-configuration analysis
- **FilterByTemperatureRange** — filters records by outdoor temperature range

## Tests

Unit tests in `OctopusCosyAnalyser.Tests/` (NUnit):
- `EfficiencyCalculationServiceTests` — HDD computation, normalised efficiency edge cases
- `EfficiencyAnalysisServiceTests` — summarise, compare, group, filter, warm day exclusion, warning generation
- `WebTests` — Aspire integration test (requires Docker)

## Build & Run

### Local Development

```bash
# Build
dotnet build

# Run locally (requires Docker for PostgreSQL)
dotnet run --project OctopusCosyAnalyser.AppHost

# Run tests (requires Docker)
dotnet test
```

The web UI runs at `http://localhost:8080` (configurable via `WEB_PORT`).

### Production Deployment

```bash
# Production (Docker Compose)
cp .env.example .env   # set POSTGRES_PASSWORD and optionally WEB_PORT
docker compose pull
docker compose up -d
# UI at http://<host>:8080
```

Images published to `ghcr.io/r23rob/octopuscosyanalyser/` (apiservice + webfrontend).
Supports AMD64 and ARM64 (runs on Raspberry Pi).

### First-time Setup
1. Go to Settings → enter Octopus account number and API key
2. Use the Setup endpoint to discover and register the heat pump device
3. The snapshot worker starts collecting data automatically every 15 minutes

## Planned Direction

- Feed snapshot data into an AI model to analyse running efficiency
- Suggest weather compensation curve adjustments based on COP trends vs outdoor temperature
- Identify if the heat pump is underperforming relative to conditions
- Weather normalisation: compare COP against outdoor temperature (Met Office / Open-Meteo) to spot degradation over time
- Daily cost tracking: sum(kWh_import × unit_rate) + standing charge
- Cost per kWh heat: daily_cost / heat_out_kWh

### Not Yet Implemented (originally planned metrics)

These were in the original design but not yet built:

- **Tariff rate tracking** — pull unit rates and standing charges from Octopus REST tariff endpoints, store per half-hour to compute actual £ cost per day. Relevant endpoints: `/v1/products/{product_code}/electricity-tariffs/{tariff_code}/standard-unit-rates/` and `/standing-charges/`
- **Daily cost summary** — a `DailySummary` table aggregating: electricInKwh, heatOutKwh, avgCOP, costGbp, avgUnitRatePence. The UI should read rollups not raw snapshots for speed.
- **Polling backfill** — nightly re-pull of "yesterday" to catch late-arriving Octopus data (current worker only captures forward in real time)
- **External weather data** — correlate COP against outdoor temperature from Open-Meteo or Met Office to weather-normalise efficiency comparisons automatically (currently outdoor temp comes from the heat pump sensor only)

## Design Decisions & History

- **SQLite → PostgreSQL**: original design used SQLite for simplicity; switched to PostgreSQL for robustness and easier long-term analytics
- **Separate Collector + API → combined ApiService**: original design had a dedicated collector Worker Service project; merged into a single `ApiService` with a background `HeatPumpSnapshotWorker` for simplicity
- **Blazor Server** (not WebAssembly): chosen for MVP simplicity — no WASM compilation, direct server-side data access
- **15-minute snapshot interval**: original design suggested 30 minutes; tightened to 15 to match Octopus telemetry resolution
- **Manual efficiency records**: added as a pragmatic workaround while automatic daily cost/COP rollups are not yet implemented — lets the user track their own observations and make before/after comparisons manually
- **Aspire for dev orchestration**: used for local development only (AppHost project); production runs via plain Docker Compose
- **Tado integration removed**: was briefly added then removed — the Octopus API provides all necessary indoor/outdoor temperature data via the Cosy Pod sensors