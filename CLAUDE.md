# CLAUDE.md

## Project Overview

OctopusCosyAnalyser is a .NET Aspire application that monitors and analyses Octopus Energy Cosy heat pump performance. It collects data from the Octopus Energy GraphQL API and a Tado thermostat integration, stores snapshots in PostgreSQL, and presents dashboards via a Blazor Server frontend.

## Architecture

```
OctopusCosyAnalyser.sln
├── OctopusCosyAnalyser.AppHost/        # .NET Aspire orchestrator
├── OctopusCosyAnalyser.ApiService/     # Backend API (Minimal APIs)
│   ├── Data/CosyDbContext.cs           # EF Core context (PostgreSQL)
│   ├── Endpoints/                      # Minimal API endpoint groups
│   │   ├── HeatPumpEndpoints.cs        # /api/heatpump/* (main)
│   │   ├── AccountSettingsEndpoints.cs # /api/settings/*
│   │   ├── EfficiencyEndpoints.cs      # /api/efficiency/*
│   │   └── TadoEndpoints.cs           # /api/tado/*
│   ├── Services/
│   │   ├── OctopusEnergyClient.cs     # GraphQL + REST client for Octopus API
│   │   ├── TadoClient.cs             # Tado API client
│   │   ├── GraphQLIntrospection.cs    # Schema introspection helpers
│   │   └── Efficiency*.cs            # Efficiency analysis services
│   └── Workers/
│       └── HeatPumpSnapshotWorker.cs  # Background service, snapshots every 15 min
├── OctopusCosyAnalyser.Web/           # Blazor Server frontend
│   ├── Services/HeatPumpApiClient.cs  # Typed HTTP client to API service
│   └── Components/Pages/HeatPump/     # Dashboard, Performance, History, Costs, etc.
├── OctopusCosyAnalyser.Shared/        # Shared DTOs (Models/)
├── OctopusCosyAnalyser.ServiceDefaults/# Aspire service defaults
└── OctopusCosyAnalyser.Tests/         # Integration tests (requires Docker)
```

## Key Patterns

### Adding a new Octopus GraphQL query

Follow this 4-layer pattern:

1. **OctopusEnergyClient.cs** — Add query method returning `Task<JsonDocument>`
   - Simple queries: use raw string literal with `$$"""` interpolation
   - Parameterised queries: use `ExecuteRawQueryAsync()` with a variables object
   - Auth is handled automatically via `GetAuthTokenAsync()` (JWT cached 55 min)

2. **HeatPumpEndpoints.cs** — Add endpoint in `MapHeatPumpEndpoints()`
   - Use `GetSettingsForAccountAsync(db, accountNumber)` or `GetDeviceAndSettingsAsync(db, deviceId)` for auth
   - Return `Results.Ok(data)` with the response

3. **HeatPumpApiClient.cs** — Add frontend client method
   - Typed methods return DTOs; raw methods return `Task<string>` for JSON

4. **Blazor page** — Add or update a `.razor` page in `Components/Pages/HeatPump/`
   - Charts use Radzen (`RadzenChart`, `RadzenLineSeries`, `RadzenColumnSeries`)
   - Add nav entry in `Components/Layout/NavMenu.razor`

### Database

- PostgreSQL via EF Core with Npgsql
- Auto-migrates on startup (`db.Database.Migrate()`)
- Single migration: `20260220232518_InitialCreate`
- Key tables: `HeatPumpDevices`, `HeatPumpSnapshots`, `ConsumptionReadings`, `OctopusAccountSettings`, `HeatPumpEfficiencyRecords`
- Add new migrations with: `dotnet ef migrations add <Name> --project OctopusCosyAnalyser.ApiService`

### Octopus Energy API

- GraphQL endpoint: `https://api.octopus.energy/v1/graphql/`
- Auth: API key → `obtainKrakenToken` mutation → JWT token (Bearer header)
- REST consumption endpoint uses Basic auth (`apiKey:` base64)
- Use `/api/heatpump/introspect/{typeName}?accountNumber=X` to explore the schema
- Use `/api/heatpump/graphql` POST for ad-hoc queries

## Build & Run

```bash
# Build
dotnet build

# Run locally (requires Docker for PostgreSQL)
dotnet run --project OctopusCosyAnalyser.AppHost

# Run tests (requires Docker)
dotnet test

# Production: use docker-compose.yml with .env file
docker compose up -d
```

The web UI runs at `http://localhost:8080` (configurable via `WEB_PORT`).

## Common Tasks

- **Add an endpoint**: Follow the 4-layer pattern above
- **Add a DB table**: Add entity in `Data/`, DbSet in `CosyDbContext.cs`, create migration
- **Add a page**: Create `.razor` in `Components/Pages/HeatPump/`, add to `NavMenu.razor`
- **Explore Octopus API**: Use the introspect endpoint or graphql passthrough
