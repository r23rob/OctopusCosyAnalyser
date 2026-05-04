# Azure deployment — OctopusCosyAnalyser

Bicep templates that provision the full Azure stack: Container Apps for the API,
Container Apps Jobs for the four scheduled workers, Postgres Flexible Server B1ms
(with PgBouncer), Storage Account for the SPA bundle, and Log Analytics for logs.

## Cost (list price, late 2025)

| Component | Monthly |
|---|---:|
| Container Apps (API, `minReplicas: 1`) | £4–8 |
| Container Apps Jobs (4 × scheduled) | £0–1 |
| Postgres Flexible B1ms zonal + 32 GB + backup | ~£14 |
| Storage + egress | ~£2–4 |
| **Total at 100 → 500 users** | **~£20 → £28/mo** |

## One-time setup

```bash
# 1. Log in
az login

# 2. Install Bicep CLI
az bicep install

# 3. Copy and edit parameters
cd infra
cp main.parameters.example.json main.parameters.json
# Set:
#   - postgresAdminPassword: a strong password (or wire up KeyVault reference)
#   - corsAllowedOrigins: your production SPA origin (e.g. https://cosy.example.com)
#   - anthropicApiKey: optional global fallback (users typically supply their own)

# 4. Deploy
./deploy.sh
# Or for a specific commit:
./deploy.sh sha-abc1234
```

After the first deploy, push the SPA bundle to the static website:

```bash
cd ../octopus-cosy-web
npm run build
STORAGE_ACCOUNT=$(az deployment sub show --name main --query 'properties.outputs.staticWebsiteUrl.value' -o tsv | sed 's|https://||;s|\.z.*||')
az storage blob upload-batch \
  --account-name "$STORAGE_ACCOUNT" \
  --source dist \
  --destination '$web' \
  --overwrite
```

## How updates roll out

GitHub Actions (`.github/workflows/docker-build.yml`) builds and pushes images to
`ghcr.io` on every push to `main`, then runs `az containerapp update` to point
the API + each worker job at the new image tag. ACA creates a new immutable
revision named after the commit SHA; rollback is one command.

```bash
# Roll back to a previous revision:
az containerapp revision activate \
  --name cosy-prod-api --resource-group cosy-prod-rg \
  --revision <previous-sha>
```

## What's deployed

- **`cosy-prod-rg`** — resource group
- **`cosy-prod-logs`** — Log Analytics workspace
- **`cosy-prod-aca`** — Container Apps managed environment
- **`cosy-prod-pg`** — Postgres Flexible Server (zonal, B1ms, PgBouncer enabled)
- **`cosy-prod-api`** — Container App (the API + Identity)
- **`cosy-prod-job-snapshot`** — every 30 min
- **`cosy-prod-job-timeseries`** — every 6 hours
- **`cosy-prod-job-cost`** — every 6 hours, offset 15 min
- **`cosy-prod-job-intervals`** — every 35 min
- **`cosyprodweb`** — Storage Account hosting the React SPA at `$web`

## Key gotchas

- **Don't enable Postgres HA.** It doubles cost. The template forces
  `highAvailability: { mode: 'Disabled' }` — leave it.
- **Connection ceiling.** B1ms has ~50 connections; PgBouncer is enabled by
  default in this template. The API's connection string adds
  `Maximum Pool Size=10` to stay well under that.
- **Data Protection keys** persist to Postgres via `PersistKeysToDbContext`
  (wired up in `Program.cs`) — required so auth cookies survive scale-to-zero
  or container restarts.
- **CORS.** If you serve the SPA from the same origin as the API (e.g. a custom
  domain in front of both via Front Door), leave `corsAllowedOrigins` empty —
  CORS isn't needed. Only set it when the SPA lives on a different origin.
