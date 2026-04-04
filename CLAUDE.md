# OctopusCosyAnalyser — Project Context

## Purpose

A personal heat pump monitoring dashboard for Octopus Energy Cosy heat pump customers. The goal is a simple, clear view of how your heat pump is running — efficiency, power, temperatures, and energy use — with the long-term aim of passing this data through AI to suggest improvements (e.g. whether weather compensation curve adjustments would help).

**Core question the app answers:** Is my heat pump running efficiently, and did any changes I made improve it?

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend API | .NET 10, ASP.NET Core Minimal APIs |
| Frontend | React 19 + Vite 8, TanStack Router/Query, TypeScript, Tailwind CSS + shadcn/ui |
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
├── OctopusCosyAnalyser.Web/            # (Legacy Blazor — no longer deployed)
├── octopus-cosy-web/                   # React 19 SPA frontend
│   ├── src/
│   │   ├── components/                 # Reusable UI components
│   │   │   ├── charts/                 # Recharts-based chart components
│   │   │   ├── dashboard/              # Dashboard widget cards
│   │   │   ├── layout/                 # AppLayout, NavBar
│   │   │   └── shared/                 # LoadingSpinner, ErrorAlert, etc.
│   │   ├── hooks/                      # Custom React hooks
│   │   ├── lib/                        # api-client, query-keys, utils
│   │   ├── routes/                     # TanStack Router file-based routes
│   │   │   ├── __root.tsx              # Root layout
│   │   │   ├── settings.tsx            # Settings page
│   │   │   └── heatpump/              # Heat pump pages
│   │   └── types/                      # TypeScript type definitions (api.ts)
│   ├── vite.config.ts                  # Vite + React plugin + Tailwind
│   └── package.json
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

4.  **React route page** — Add or update a `.tsx` route in `octopus-cosy-web/src/routes/heatpump/`
    -   Charts use Recharts (`ComposedChart`, `Line`, `ResponsiveContainer`)
    -   Data fetching via TanStack Query hooks in `octopus-cosy-web/src/hooks/`
    -   Add nav entry in `octopus-cosy-web/src/components/layout/NavBar.tsx`

## Database Tables (EF Core)

- PostgreSQL via EF Core with Npgsql
- Auto-migrates on startup (`db.Database.Migrate()`)
- Single migration: `20260220232518_InitialCreate`
- Add new migrations with: `dotnet ef migrations add <Name> --project OctopusCosyAnalyser.ApiService`

| Table | Purpose |
|-------|---------|
| `HeatPumpDevices` | Registered heat pump devices (DeviceId, AccountNumber, MPAN, Euid) |
| `HeatPumpSnapshots` | 15-min telemetry snapshots (COP, temps, power, heating/hot water zone state, controller state, weather compensation, all sensor readings as JSONB, flow temp allowable range) |
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
2. Extracts: COP, heat output, power input, outdoor temp, lifetime performance, room temp/humidity, heating zone setpoints, hot water zone setpoints, controller state (HEATING/IDLE), weather compensation settings (enabled + min/max range), flow temperature (current + allowable min/max range), all sensor readings (serialised to JSONB)
3. Upserts a `HeatPumpSnapshot` row (skips duplicates via unique constraint)

## API Endpoints

### `/api/heatpump`
| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/setup` | Discover and register heat pump device for an account |
| GET | `/devices` | List registered devices |
| GET | `/summary/{deviceId}` | Live parsed summary (COP, temps, zones) |
| GET | `/snapshots/{deviceId}` | Historical snapshots with date range filter + pagination (skip/take) |
| GET | `/snapshots/{deviceId}/latest` | Latest snapshot timestamp + health status |
| GET | `/time-series/{accountNumber}/{euid}` | Bucketed chart data from Octopus API |
| GET | `/time-ranged/{accountNumber}/{euid}` | Aggregated totals for a date range |
| GET | `/consumption/{deviceId}` | Smart meter consumption readings + pagination (skip/take) |
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

## React Routes

TanStack Router file-based routing in `octopus-cosy-web/src/routes/`:

| Page | Route | Shows |
|------|-------|-------|
| Dashboard | `/heatpump` | Current status overview, trend chart, COP gauge, room temps, power, efficiency |
| Data Explorer | `/heatpump/data` | Daily aggregates table + CSV export |
| Scatter Plot | `/heatpump/scatter` | COP vs outdoor temperature scatter analysis |
| Settings | `/settings` | Octopus API credentials (account number + API key) |

Navigation: top navbar on desktop (`sm:flex`), fixed bottom tab bar on mobile (`sm:hidden`).
Route tree auto-generated by TanStack Router plugin (`routeTree.gen.ts`).

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

## Allowed Commands

The following commands are safe to run without confirmation:

```bash
dotnet build
dotnet test
dotnet run --project OctopusCosyAnalyser.AppHost
dotnet ef migrations add <Name> --project OctopusCosyAnalyser.ApiService
export PATH="$HOME/.dotnet:$PATH:/usr/local/share/dotnet"
git status
git diff
git log
git add
git commit
git push
git pull
git checkout
git branch
git merge
git stash
git fetch
gh pr create
gh pr view
gh pr list
gh pr merge
ls
cd octopus-cosy-web && npm install
cd octopus-cosy-web && npm run build
cd octopus-cosy-web && npm run dev
cd octopus-cosy-web && npm run lint
cd octopus-cosy-web && npx tsc --noEmit
```

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
- **Blazor → React 19 + Vite**: migrated from Blazor Server to a React SPA for richer UI capabilities; the old Blazor project remains in-tree but is no longer deployed
- **15-minute snapshot interval**: original design suggested 30 minutes; tightened to 15 to match Octopus telemetry resolution
- **Manual efficiency records**: added as a pragmatic workaround while automatic daily cost/COP rollups are not yet implemented — lets the user track their own observations and make before/after comparisons manually
- **Aspire for dev orchestration**: used for local development only (AppHost project); production runs via plain Docker Compose
- **Tado integration removed**: was briefly added then removed — the Octopus API provides all necessary indoor/outdoor temperature data via the Cosy Pod sensors
- **JSONB for sensor readings**: all sensor data stored as a single JSONB column (`SensorReadingsJson`) on snapshot rows rather than a child table — keeps the schema flat with no joins
- **Raw GraphQL endpoint gated to Development**: `/api/heatpump/graphql` only registers in `IsDevelopment()` to prevent arbitrary query proxying in production
- **Per-request HTTP auth headers**: `OctopusEnergyClient` uses `HttpRequestMessage` headers (not `DefaultRequestHeaders`) to avoid thread-safety issues between concurrent JWT (GraphQL) and Basic (REST) auth requests

## React / React Native Patterns & Best Practices

### Components
- **Functional components only** — no class components under any circumstances
- Keep components small and single-responsibility; split at ~150 lines
- **PascalCase** for component names, **camelCase** for variables, hooks, and functions
- Co-locate component files with their styles and types in the same folder where practical

### Hooks
- Extract all reusable stateful logic into **custom hooks** (`use` prefix)
- Never duplicate stateful logic across components — abstract it into a hook
- **No cascading `useEffect` chains** — if one effect sets state that triggers another, redesign using derived state or a single effect
- Use `useState` initialisers (callback form) for expensive initial values: `useState(() => expensiveComputation())`

### State Management
- Keep state as close to the component that uses it as possible
- Lift state only when genuinely needed by multiple components
- Do not add a global state library unless local state + React Context is provably insufficient; prefer **Zustand** or **Jotai** over Redux if needed
- Use **TanStack Query** for all server/async data fetching — never manually manage loading/error/data state triplets with `useEffect` + `useState`

### Performance
- Fix architectural problems first (request waterfalls, large bundles, deep component trees) before reaching for `useMemo`, `useCallback`, or `React.memo`
- Use `React.memo` only on components that are demonstrably expensive to re-render
- Only wrap functions in `useCallback` when passed as a prop to a memoised child or used as a `useEffect` dependency
- Prefer **lazy loading** (`React.lazy` / dynamic imports) for heavy screens not needed on initial render

### TypeScript
- All components, hooks, and utility functions must be **fully typed**
- Define explicit prop interfaces for every component — no implicit `any`
- Type API responses at the boundary (see `types/api.ts`); do not propagate `unknown` or `any` inward

### File & Import Structure
- Group imports: React core → third-party libraries → internal modules → styles/assets
- One component per file; file name matches the component name exactly
- Shared hooks in `/hooks`, shared components in `/components`, route pages in `/routes`

### Navigation & Responsive Design
- Use **TanStack Router** file-based routing; route tree auto-generated
- **Mobile-first**: design for mobile viewport first, enhance for larger screens
- Bottom tab bar on mobile (`sm:hidden fixed bottom-0`), top navbar on desktop (`hidden sm:flex`)
- Use Tailwind responsive breakpoints (`sm:`, `md:`, `lg:`) — always specify mobile styles first
- Use responsive grid patterns: `grid-cols-1 md:grid-cols-2 lg:grid-cols-3`
- Touch-friendly: minimum 44px tap targets on interactive elements
- Test all layouts at mobile (375px), tablet (768px), and desktop (1280px+) widths

### Testing
- Write tests for all custom hooks and non-trivial components
- Use **Vitest** + **React Testing Library**
- Target **80%+ coverage** on business logic and hook behaviour
- A feature is not complete until its happy path and primary error path are tested

### Anti-Patterns — Never Do These
- No class components
- No cascading `useEffect` chains
- No prop drilling more than 2 levels deep — use Context or lift to a shared ancestor
- No `any` types except at third-party boundaries, and even then wrap them immediately
- No `useEffect` for data that can be derived directly from existing state or props
- No manual `loading/error/data` state triplets for async calls — use TanStack Query

## Heat Pump Dashboard
- Design: heat_pump_v7.html — do not change fonts (JetBrains Mono + Instrument Sans),
  colours (cyan #06B6D4 accent, near-black ink), or layout structure
- Data source: PostgreSQL via existing OctopusCosyAnalyser connection
- AI analysis card calls Anthropic API — keep that wiring intact

## .NET API Patterns & Best Practices

### Architecture
- Use **Minimal APIs** for all endpoints — no MVC controllers.
- Business logic lives in service classes, not in endpoint delegates.
  Endpoint handlers should be thin: validate, call service, return result.
- Keep it simple. This is a small personal-use API. Do not introduce
  abstractions that do not earn their place at this scale.
- EF Core `DbContext` is acceptable as the data access layer — no need
  for a separate Repository layer on top for this project size.

### Design Patterns
- Use **service classes** for business logic, injected directly into
  endpoint delegates via DI. No MediatR, no command/query objects.
```csharp
// Good — direct, clean, readable
app.MapPost("/tasks", async (CreateTaskRequest req, ITaskService taskService) =>
    await taskService.CreateAsync(req));

// Bad — unnecessary indirection for this scale
app.MapPost("/tasks", async (CreateTaskCommand cmd, IMediator mediator) =>
    await mediator.Send(cmd));
```
- Use the **Options pattern** for all configuration. No magic strings,
  no inline config values, no `Environment.GetEnvironmentVariable()` reads
  outside of Options classes.
- Use a **ServiceResult\<T\>** return type for service methods that can fail
  in expected ways (not found, validation error, API failure).
  Reserve exceptions for truly unexpected infrastructure failures.
- Centralise API response shaping in extension methods —
  keep `Results.Ok()` / `Results.Problem()` patterns consistent.

### Dependency Injection
- Always register and inject by **interface**, never by concrete type.
- Use appropriate lifetimes: Scoped for per-request services,
  Singleton for stateless clients, Transient sparingly.
- Never use the service locator pattern (`IServiceProvider` injected into
  business logic). Workers that need scoped services should use
  `IServiceScopeFactory`.

### Error Handling
- Global exception handling via `UseExceptionHandler()` +
  `AddProblemDetails()` — no try/catch in handlers for generic exceptions.
- Return ProblemDetails-compliant error responses for all 4xx/5xx.
- Use `ServiceResult<T>` for expected failure paths — not exceptions.

### Async
- Every method that performs I/O must be async and return `Task` or `Task<T>`.
- Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.
- `CancellationToken` must be accepted and propagated through all async
  call chains — endpoints, services, and EF Core queries.

### Naming & Conventions
- Services: `I[Name]Service` / `[Name]Service` or `I[Name]Client` / `[Name]Client`
- DTOs: `*Dto` suffix (existing convention in `Shared/Models/`)
- Extension classes: `[Subject]Extensions`
- Request records: `[Entity]Request` (inline in endpoint files is fine)

### Anti-Patterns — Never Do These
- No MediatR or command/query pipeline — direct service injection only.
- No business logic in endpoint delegates (validation is OK, computation is not).
- No `.Result` or `.Wait()` on async methods.
- No magic strings for config keys, URIs, or timeouts.
- No exceptions thrown for expected failure states (not found, bad input).
- No concrete type registration in DI for services that have interfaces.
- No shared mutable static state (except thread-safe caches with
  `ConcurrentDictionary`).
