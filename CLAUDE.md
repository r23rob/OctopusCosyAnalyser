# Cosydays — Project Context

## Purpose

A personal heat pump monitoring dashboard for Octopus Energy Cosy heat pump customers (branded **Cosydays**). The goal is a simple, clear view of how your heat pump is running — efficiency, power, temperatures, and energy use — with the long-term aim of passing this data through AI to suggest improvements (e.g. whether weather compensation curve adjustments would help).

**Core question the app answers:** Is my heat pump running efficiently, and did any changes I made improve it?

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend API | .NET 10, ASP.NET Core Minimal APIs |
| Hosting (API) | AWS Lambda (container image, ARM64 Graviton, Function URL) |
| Hosting (PWA) | S3 + CloudFront |
| Workers | AWS Lambda + EventBridge scheduled rules |
| Frontend (web) | React 19 + Vite 8, TanStack Router/Query, TypeScript, Tailwind CSS + shadcn/ui |
| Frontend (mobile) | Expo React Native, Expo Router, TanStack Query |
| Database | Neon Free PostgreSQL (EF Core 10, Npgsql) |
| Infrastructure | AWS CDK (TypeScript) |
| Orchestration (dev) | .NET Aspire |
| Container | Docker (Lambda container images) |
| CI/CD | GitHub Actions → CDK deploy + S3 sync |

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
│   ├── Workers/HeatPumpSnapshotWorker.cs # 30-min background data collector
│   ├── Migrations/                     # EF Core migrations
│   └── Program.cs                      # Service registration + route mapping
├── octopus-cosy-web/                   # React 19 SPA frontend (Cosydays UI)
│   ├── src/
│   │   ├── components/                 # Reusable UI components
│   │   │   ├── charts/                 # Recharts-based chart components
│   │   │   ├── dashboard/              # Dashboard widget cards
│   │   │   ├── layout/                 # AppLayout, NavBar, BottomTabs
│   │   │   ├── onboarding/            # Onboarding wizard steps
│   │   │   ├── settings/              # Settings page components
│   │   │   └── shared/                 # LoadingSpinner, ErrorAlert, FeatureGate, etc.
│   │   ├── hooks/                      # Custom React hooks (use-features, use-dashboard, etc.)
│   │   ├── lib/                        # api-client, query-keys, utils
│   │   ├── routes/                     # TanStack Router file-based routes
│   │   │   ├── __root.tsx              # Root layout
│   │   │   ├── index.tsx               # Home (dashboard)
│   │   │   ├── history.tsx             # History page
│   │   │   ├── compare.tsx             # Compare page
│   │   │   ├── more.tsx                # More/Settings page
│   │   │   └── onboarding.tsx          # First-run setup wizard
│   │   └── types/                      # TypeScript type definitions (api.ts)
│   ├── vite.config.ts                  # Vite + React plugin + Tailwind
│   └── package.json
├── octopus-cosy-mobile/                # Expo React Native app (iOS + Android)
│   ├── app/                            # Expo Router file-based routes
│   │   ├── _layout.tsx                 # Root layout (QueryClientProvider)
│   │   └── (tabs)/                     # Tab navigator (Home/History/Compare/More)
│   ├── src/
│   │   ├── hooks/                      # React hooks (mirrors web hooks)
│   │   ├── lib/                        # API client (configurable base URL), SecureStore
│   │   └── types/                      # Shared TypeScript types (copied from web)
│   ├── app.json                        # Expo config (bundle IDs, icons)
│   └── eas.json                        # EAS Build config (dev/preview/production)
├── infra/aws/                          # AWS CDK infrastructure (TypeScript)
│   ├── bin/cosydays.ts                 # CDK app entry point
│   ├── lib/cosydays-stack.ts           # Stack: Lambda, S3, CloudFront, EventBridge
│   └── cdk.json
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
| `HeatPumpSnapshots` | 30-min telemetry snapshots (COP, temps, power, heating/hot water zone state, controller state, weather compensation, all sensor readings as JSONB, flow temp allowable range) |
| `ConsumptionReadings` | Smart meter readings (kWh, demand) |
| `OctopusAccountSettings` | Octopus API credentials (AccountNumber, ApiKey) |
| `HeatPumpEfficiencyRecords` | Manual daily records for efficiency tracking |

Unique constraints prevent duplicate snapshots `(DeviceId, SnapshotTakenAt)` and consumption readings `(DeviceId, ReadAt)`.

### Optional Database (Lite Mode)

PostgreSQL is optional — the API runs in "lite mode" without a database connection:
- **With DB**: full functionality — snapshot history, efficiency records, device registration, background worker
- **Without DB**: live Octopus API queries work (summary, time-series, time-ranged), but no snapshot history, no efficiency records, no device persistence
- **Environment variable fallback** for credentials (no DB needed): `OCTOPUS_ACCOUNT_NUMBER`, `OCTOPUS_API_KEY`, `OCTOPUS_EUID`
- `GET /api/features` endpoint reports which capabilities are available (database, snapshots, efficiency, etc.)
- Frontend uses the `useFeatures()` hook (`hooks/use-features.ts`) and `FeatureGate` component (`components/shared/FeatureGate.tsx`) for conditional rendering based on available features

## Octopus Energy API

Full schema reference (auto-generated from introspection): [`docs/octopus-graphql-schema.md`](docs/octopus-graphql-schema.md)

Heat pump query docs, example requests/responses, and time-series samples: [`docs/octopus-api/heat-pump-api-reference.md`](docs/octopus-api/heat-pump-api-reference.md)

**Auth endpoint (token acquisition):** `https://api.octopus.energy/v1/graphql/`
**Data endpoint (heat pump queries):** `https://api.backend.octopus.energy/v1/graphql/`

Both endpoints accept GraphQL over HTTPS. Authentication uses the auth endpoint; all heat pump queries (status, config, performance) use the backend endpoint. See `OctopusEnergyClient.cs` for how the two base URLs are configured.

Authentication: POST to the auth endpoint with `obtainKrakenToken(input: {APIKey: "..."})` mutation → returns JWT. Token cached for 55 minutes in a `ConcurrentDictionary`.

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
| **Batched query (4-in-1):** `heatPumpControllerStatus` + `heatPumpControllerConfiguration` + `heatPumpLifetimePerformance` + `heatPumpLivePerformance` | `GetHeatPumpStatusAndConfigAsync` | **Primary workhorse** — used by `/summary` endpoint and the 30-min snapshot worker |
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

`HeatPumpSnapshotWorker` runs every **30 minutes**. For each active `HeatPumpDevice`, it:
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
| Home | `/` | Current status, COP gauge, metrics, trends |
| History | `/history` | Daily aggregates, temperature charts, COP scatter |
| Compare | `/compare` | Two-period efficiency comparison |
| More/Settings | `/more` | Account credentials, device info, system status |
| Onboarding | `/onboarding` | First-run setup wizard |
| AI Analysis | `/ai` | Claude-powered heat pump insights |

Navigation: bottom tabs (Home / History / Compare / More) on mobile, left rail on desktop.
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
cd octopus-cosy-mobile && npm install
cd octopus-cosy-mobile && npx expo start
cd infra/aws && npm install
cd infra/aws && npx cdk deploy
cd infra/aws && npx cdk synth
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

### AWS Deployment (Production)

The production stack runs on AWS Lambda behind CloudFront:

- **API Lambda** (`cosydays-api`): 512 MB, 300s timeout, ARM64 Graviton, Function URL
- **Worker Lambda** (`cosydays-worker`): same image with `LAMBDA_WORKER_MODE=true`, 900s timeout
- **EventBridge**: 4 scheduled rules trigger the worker Lambda
- **S3 + CloudFront**: PWA static files, `/*` → S3, `/api/*` → Lambda Function URL
- **Database**: Neon Free PostgreSQL (0.5 GB free tier)

```bash
# First-time infrastructure deploy
cd infra/aws && npm install
NEON_CONNECTION_STRING="postgres://..." npx cdk bootstrap
NEON_CONNECTION_STRING="postgres://..." npx cdk deploy
```

CI/CD (`.github/workflows/deploy-aws.yml`) runs on push to main:
1. Builds React PWA
2. `cdk deploy` (builds Docker image via `fromImageAsset`, updates Lambda functions)
3. Runs EF Core migrations against Neon
4. Syncs PWA to S3 with correct cache headers
5. Invalidates CloudFront

Required GitHub secrets: `AWS_ROLE_ARN`, `NEON_CONNECTION_STRING`, `ANTHROPIC_API_KEY`

### Lambda Architecture

**Program.cs branching:**
- `LAMBDA_WORKER_MODE=true` → `WorkerLambdaHandler.RunAsync()` (EventBridge handler)
- `AWS_LAMBDA_FUNCTION_NAME` set → API mode, workers NOT registered as HostedServices
- Neither set → local dev mode with Kestrel + BackgroundService workers

**WorkerLambdaHandler** receives EventBridge events `{"worker": "snapshot"}` and calls
the existing `RunOnceAsync()` method on the resolved worker. DI container built once
per cold start, reused across invocations.

| Schedule | Worker | Interval |
|----------|--------|----------|
| `cosydays-snapshot` | HeatPumpSnapshotWorker | 30 min |
| `cosydays-timeseries` | HeatPumpTimeSeriesSyncWorker | 30 min |
| `cosydays-cost` | CostDataSyncWorker | 6 hours |
| `cosydays-energy-intervals` | EnergyIntervalWorker | 35 min |

**Migrations**: run via CI/CD step (`dotnet ef database update`), not on Lambda cold
start. Both Lambda functions set `SKIP_AUTO_MIGRATE=true`. Local dev still
auto-migrates on startup.

### Expo React Native App

```bash
cd octopus-cosy-mobile
npm install
npx expo start          # Expo Go on phone
eas build --platform ios --profile production
eas submit --platform ios
```

The mobile app uses Expo Router (file-based tabs), TanStack Query, and a configurable
API base URL stored in SecureStore. Set the server URL in the More tab to point at
your CloudFront domain.

### First-time Setup
1. Create a Neon free PostgreSQL database
2. Deploy infrastructure: `cd infra/aws && NEON_CONNECTION_STRING=... npx cdk deploy`
3. Open the CloudFront URL → go to Settings → enter Octopus account number and API key
4. Use the Setup endpoint to discover and register the heat pump device
5. The EventBridge schedules start collecting data automatically

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
- **Blazor → React 19 + Vite**: migrated from Blazor Server to a React SPA for richer UI capabilities; the old Blazor project has been removed from the tree
- **Cosydays rebrand**: the dashboard was rebranded from "OctopusCosyAnalyser" to "Cosydays" as the user-facing product name; solution/project names remain OctopusCosyAnalyser
- **Optional PostgreSQL**: the API runs in "lite mode" without a database — live Octopus API queries work, but snapshot history and efficiency records are unavailable. This allows zero-config first use with environment variable credentials
- **30-minute snapshot interval**: aligns with Octopus's underlying half-hourly telemetry cadence and keeps API call volume reasonable
- **Manual efficiency records**: added as a pragmatic workaround while automatic daily cost/COP rollups are not yet implemented — lets the user track their own observations and make before/after comparisons manually
- **Aspire for dev orchestration**: used for local development only (AppHost project); production runs via plain Docker Compose
- **Tado integration removed**: was briefly added then removed — the Octopus API provides all necessary indoor/outdoor temperature data via the Cosy Pod sensors
- **JSONB for sensor readings**: all sensor data stored as a single JSONB column (`SensorReadingsJson`) on snapshot rows rather than a child table — keeps the schema flat with no joins
- **Raw GraphQL endpoint gated to Development**: `/api/heatpump/graphql` only registers in `IsDevelopment()` to prevent arbitrary query proxying in production
- **Per-request HTTP auth headers**: `OctopusEnergyClient` uses `HttpRequestMessage` headers (not `DefaultRequestHeaders`) to avoid thread-safety issues between concurrent JWT (GraphQL) and Basic (REST) auth requests
- **Azure → AWS Lambda**: migrated from Azure Container Apps to AWS Lambda for lower cost (<$1/mo for single user). API runs as a Lambda container image behind CloudFront Function URL. Workers triggered by EventBridge instead of Container App Jobs. PWA served from S3 + CloudFront instead of Azure Storage Static Website
- **Neon Free PostgreSQL**: replaced Azure-managed PostgreSQL with Neon free tier (0.5 GB). Connection pooling via Neon's serverless driver keeps Lambda connection counts low (`Maximum Pool Size=5`)
- **CDK over SAM**: chose CDK (TypeScript) for infrastructure because the stack includes non-Lambda resources (CloudFront distribution, S3 OAC, CloudFront Functions for SPA routing) that would need raw CloudFormation in SAM
- **Single container image, two Lambda functions**: API and Worker Lambda share one Docker image. `LAMBDA_WORKER_MODE` env var branches at the top of Program.cs — avoids maintaining two Dockerfiles
- **Expo React Native**: added a native mobile app alongside the PWA. Uses Expo Router (file-based tabs matching web routes), TanStack Query (same data fetching pattern), and a configurable base URL via SecureStore. Types shared by copying `types/api.ts` from the web project

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
- Bottom tab bar on mobile (Home / History / Compare / More), left rail navigation on desktop
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
- Do not change fonts (JetBrains Mono + Instrument Sans),
  colours (cyan #06B6D4 accent, near-black ink), or layout structure
- Data source: PostgreSQL via existing OctopusCosyAnalyser connection (optional — see "Optional Database" below)
- AI analysis card calls Anthropic API — keep that wiring intact

## UI Standards (apply to all React routes)

### Typography
- Fonts are **Instrument Sans** (sans) and **JetBrains Mono** (mono). Loaded once in
  `index.html`. Never inline a `<link>` for additional font families per page.
- Body line-height is **1.55**, headings **1.2**. Set globally in `src/index.css` —
  do not override per-component without justification.
- All `.font-mono` text is automatically tabular (`font-variant-numeric: tabular-nums`)
  so number columns don't jitter. Sans-serif numeric displays should add the
  `tabular-nums` utility class when columns of digits need to align (KPI cards, tables).
- **Minimum readable sizes**: dashboard widgets may use `text-[10px]–text-[12px]`
  for tertiary labels, but everything else (data tables, settings,
  scatter, page headings) must be **at least 12px**, scaling to 13px on `md:`.
- Page titles use `text-xl font-semibold tracking-tight`.
- Body copy uses `text-sm` (14px) or `text-[13px]`.
- Avoid `font-light` on body text — it reads thin on Windows ClearType. Use
  `font-normal` as the default weight for content.

### Layout & responsiveness
- Top-level container: `max-w-[1440px] mx-auto` (set in `AppLayout.tsx`).
- Content-heavy pages (data tables, scatter, charts) wrap in
  `max-w-screen-2xl mx-auto` so they don't stretch on ultra-wide monitors.
- Forms wrap in `max-w-xl mx-auto` (or narrower) and centre horizontally.
- Tables that may overflow on mobile go inside `overflow-x-auto` and use
  `whitespace-nowrap` on cells.
- Responsive breakpoints (mobile-first): `sm:` 640px, `md:` 768px, `lg:` 1024px,
  `xl:` 1280px, `2xl:` 1536px. Always specify the mobile style first.
- Test all routes at **375px (mobile), 768px (tablet), 1280px (laptop), and
  1920px (desktop monitor)**.
- Touch targets: minimum **44×44px** on mobile (use `h-11` / `min-h-[44px]`).

### Accessibility
- Every interactive element must have a visible `:focus-visible` ring (set globally
  in `src/index.css` — cyan 2px outline). Don't override with `outline-none`.
- Animations respect `prefers-reduced-motion` (handled globally in `src/index.css`).
- Colour contrast: text on white backgrounds must meet **WCAG AA** (4.5:1 for body,
  3:1 for large text). `text-ink3` (#A1A1AA) on white fails AA — use it only for
  decorative labels, never for important information.

### Progressive Web App (PWA)
- The web frontend is a PWA via **`vite-plugin-pwa`** (configured in
  `octopus-cosy-web/vite.config.ts`).
- Service worker uses `registerType: 'autoUpdate'` so users always get the latest
  build on next navigation; registration happens in `src/main.tsx` via
  `registerSW({ immediate: true })`.
- Caching strategy:
  - Static assets (JS, CSS, HTML, fonts, icons): precached at build time
  - Google Fonts CSS: `StaleWhileRevalidate`
  - Google Fonts WOFF2: `CacheFirst` (1 year)
  - `/api/*` requests: `NetworkFirst` with 6s timeout, 24h fallback cache
- Manifest fields are defined inline in `vite.config.ts` — keep `theme_color`
  matching the navbar background (`#09090B`) and `background_color` matching
  the page (`#F8F8F9`).
- Icons live in `octopus-cosy-web/public/`:
  `pwa-icon.svg`, `pwa-icon-maskable.svg` (sources) and `pwa-192.png`,
  `pwa-512.png`, `pwa-maskable-512.png`, `apple-touch-icon.png`, `favicon-32.png`
  (build outputs). Regenerate with `sharp` if the SVG sources change:
  ```bash
  npm i -D sharp
  node -e "const s=require('sharp'),fs=require('fs');\
   Promise.all([\
     s(fs.readFileSync('public/pwa-icon.svg')).resize(192).png().toFile('public/pwa-192.png'),\
     s(fs.readFileSync('public/pwa-icon.svg')).resize(512).png().toFile('public/pwa-512.png'),\
     s(fs.readFileSync('public/pwa-icon-maskable.svg')).resize(512).png().toFile('public/pwa-maskable-512.png'),\
     s(fs.readFileSync('public/pwa-icon.svg')).resize(180).png().toFile('public/apple-touch-icon.png'),\
     s(fs.readFileSync('public/favicon.svg')).resize(32).png().toFile('public/favicon-32.png'),\
   ]).then(()=>console.log('done'))"
  npm uninstall sharp
  ```
- iOS install support: `apple-mobile-web-app-*` meta tags in `index.html` must
  remain. Status bar style is `black-translucent`.
- Safe-area: always honour `env(safe-area-inset-bottom)` on bottom-fixed elements
  (mobile tab bar uses this).

### Anti-Patterns — UI specific
- Don't add font sizes below `text-[10px]` (8–9px text is unreadable on retina at
  100% zoom). The dashboard density tokens are the only exception.
- Don't use `font-light` for body text — too thin on Windows.
- Don't override the global `:focus-visible` ring with `outline-none` unless you
  immediately replace it with an equivalent visible focus state.
- Don't inline `<style>` blocks in components — use Tailwind utilities or extend
  `src/index.css` with a class.
- Don't ship a route page without testing it at 375px **and** 1920px.

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
