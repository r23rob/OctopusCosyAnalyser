# Azure deployment — first-run guide

Step-by-step to take this branch from "merged into main" to "running in Azure
with multi-tenant sign-in." Estimated time: 30–45 minutes the first time.

If you just want the architecture and cost breakdown, see [`infra/README.md`](../infra/README.md).

---

## Prerequisites

- An Azure subscription (a Pay-As-You-Go is fine — total monthly cost at low
  usage is ~£20–28 — see [`infra/README.md`](../infra/README.md) for the breakdown).
- The `az` CLI installed and logged in: `az login`
- Bicep CLI: `az bicep install`
- The `gh` CLI for setting GitHub repo secrets (or do it via the GitHub web UI).

---

## Step 1 — First Azure deploy (one-time)

This provisions the resource group, Postgres, Container App, Container App Jobs,
Storage account, and Log Analytics workspace.

```bash
cd infra
cp main.parameters.example.json main.parameters.json
```

Edit `main.parameters.json` and set:

- **`environmentName`** — short prefix used for every resource (e.g. `cosy-prod`).
  All resources will be created in `<environmentName>-rg`.
- **`location`** — Azure region, default `uksouth`.
- **`postgresAdminPassword`** — a strong password (16+ chars, mixed case + digits + symbol).
  Keep this somewhere safe; you'll need it again for `psql` debugging.
- **`corsAllowedOrigins`** — leave empty for the first deploy. Set later if you
  put the SPA on a separate domain (e.g. `https://cosy.example.com`).
- **`anthropicApiKey`** — leave empty. Each user supplies their own key in Settings;
  this is only a global fallback.

Run the deploy:

```bash
./deploy.sh
```

After ~5–8 minutes you'll see outputs including `apiContainerAppName`, `apiFqdn`,
`postgresHost`, `staticWebsiteUrl`, and the four worker job names. **Note these
down** — the next step needs them.

If the deploy fails partway through, fix the issue and re-run `./deploy.sh` —
Bicep deployments are idempotent.

---

## Step 2 — Wire up GitHub Actions for continuous deployment

This lets `git push origin main` automatically deploy to Azure.

### 2a. Create an Azure App Registration with a federated credential

The CI job authenticates to Azure with OIDC — no client secret stored in GitHub.

```bash
# Create the App Registration
APP_NAME="cosy-github-actions"
APP_ID=$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)
SP_ID=$(az ad sp create --id "$APP_ID" --query id -o tsv)

# Grant it Contributor on the resource group (so it can update Container Apps)
RG_ID=$(az group show --name cosy-prod-rg --query id -o tsv)  # match environmentName
az role assignment create --role "Contributor" --assignee-object-id "$SP_ID" \
  --assignee-principal-type ServicePrincipal --scope "$RG_ID"

# Grant Storage Blob Data Contributor (for SPA bundle upload)
STORAGE_ID=$(az storage account show --name cosyprodweb --resource-group cosy-prod-rg --query id -o tsv)
az role assignment create --role "Storage Blob Data Contributor" --assignee-object-id "$SP_ID" \
  --assignee-principal-type ServicePrincipal --scope "$STORAGE_ID"

# Add a federated credential for this repo's main branch
GITHUB_OWNER="r23rob"
GITHUB_REPO="OctopusCosyAnalyser"
az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:'"$GITHUB_OWNER/$GITHUB_REPO"':ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'

# Print what GitHub needs
echo "AZURE_CLIENT_ID=$APP_ID"
echo "AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)"
echo "AZURE_SUBSCRIPTION_ID=$(az account show --query id -o tsv)"
```

### 2b. Set GitHub repo secrets and vars

Use the values printed above plus the deployment outputs from Step 1.

```bash
# Secrets (encrypted, only the workflow can read them)
gh secret set AZURE_CLIENT_ID --body "<paste from step 2a>"
gh secret set AZURE_TENANT_ID --body "<paste from step 2a>"
gh secret set AZURE_SUBSCRIPTION_ID --body "<paste from step 2a>"

# Variables (visible in the workflow log — these aren't sensitive)
gh variable set AZURE_RESOURCE_GROUP --body "cosy-prod-rg"
gh variable set AZURE_API_APP_NAME --body "cosy-prod-api"
gh variable set AZURE_STORAGE_ACCOUNT --body "cosyprodweb"
gh variable set AZURE_JOB_SNAPSHOT_NAME --body "cosy-prod-job-snapshot"
gh variable set AZURE_JOB_TIMESERIES_NAME --body "cosy-prod-job-timeseries"
gh variable set AZURE_JOB_COST_NAME --body "cosy-prod-job-cost"
gh variable set AZURE_JOB_INTERVALS_NAME --body "cosy-prod-job-intervals"
```

(Adjust the names if you used a different `environmentName`.)

### 2c. Verify

Push any small change to `main`. The `Build and Push Docker Images` workflow
should run, build the images, and the new `Deploy to Azure Container Apps` job
should run after it — check the Actions tab on GitHub.

After this, every push to `main` deploys automatically. Rollback is a single
command (no rebuild needed):

```bash
az containerapp revision activate \
  --name cosy-prod-api --resource-group cosy-prod-rg \
  --revision <previous-sha-from-az-containerapp-revision-list>
```

---

## Step 3 — Upload the initial SPA bundle

The first deploy creates the Storage account but doesn't populate it. Subsequent
pushes to `main` upload via CI — but the very first time you need to do it
manually so users have something to load.

```bash
cd ../octopus-cosy-web
npm ci
npm run build
az storage blob upload-batch \
  --account-name cosyprodweb \
  --auth-mode login \
  --source dist \
  --destination '$web' \
  --overwrite
```

The static site URL is in the deployment outputs (`staticWebsiteUrl`).

---

## Step 4 — Custom domain (optional but recommended)

By default the API is at `<random>.uksouth.azurecontainerapps.io` and the SPA at
`<random>.z33.web.core.windows.net`. Add a custom domain for both via Azure
Front Door so users hit `https://cosy.example.com` for the SPA and the SPA's
fetch calls go to `/api/*` on the same origin (no CORS config required).

If you set up Front Door:

1. Add two routes — one for `/api/*` to the Container App, one for `/*` to the
   Storage `$web`.
2. Update the parameters file: `corsAllowedOrigins` stays empty (same-origin).
3. Re-run `./deploy.sh` to apply.

If you skip Front Door and run the SPA on a different origin, set
`corsAllowedOrigins` to your SPA URL and re-deploy.

---

## Step 5 — First sign-up

Open the SPA URL in a browser. You'll be redirected to `/login` because there
are no users yet. Click "Create an account" and register with your email +
password (8+ chars, mixed case + digit). After signup you're auto-logged-in and
land on the dashboard. Open Settings, paste your Octopus account number + API
key, hit Save, and run setup. The snapshot worker picks up your device on its
next 30-min cron tick.

To verify multi-tenancy: register a second account from a private window. From
account B, all `/api/heatpump/*` responses must return either empty arrays or
404 — never account A's data. The global EF query filter enforces this at the
DbContext level, so even an endpoint that forgets to filter manually is safe.

---

## Common operations

### Tail logs

```bash
# API logs (live tail)
az containerapp logs show --name cosy-prod-api --resource-group cosy-prod-rg --follow

# Worker job logs (last 100 lines from latest execution)
az containerapp job execution list --name cosy-prod-job-snapshot \
  --resource-group cosy-prod-rg --query "[0].name" -o tsv | \
  xargs -I {} az containerapp job execution show --name cosy-prod-job-snapshot \
  --resource-group cosy-prod-rg --execution-name {}
```

Or use Log Analytics in the portal for full-text search across everything.

### Inspect Postgres

The Flexible Server has public network access enabled with PgBouncer. Connect with:

```bash
PGPASSWORD='<your-admin-password>' psql \
  -h cosy-prod-pg.postgres.database.azure.com -p 6432 \
  -U cosyadmin -d cosydb
```

### Force a one-off worker run

```bash
az containerapp job start --name cosy-prod-job-snapshot --resource-group cosy-prod-rg
```

### Tear it all down

```bash
az group delete --name cosy-prod-rg --yes --no-wait
```

This removes everything in this environment. The federated credential on the
App Registration stays — re-use it if you re-deploy with the same
`environmentName`.

---

## Troubleshooting

**`az containerapp update` fails with "image not found"** — the GitHub Actions
build job may have failed before the deploy job ran. Check the build logs;
common causes are TypeScript errors in the SPA build or .NET build errors.

**Auth cookie doesn't persist across page reloads** — Data Protection keys
aren't being persisted. Confirm the `DataProtectionKeys` table exists in
Postgres (`\d "DataProtectionKeys"` in `psql`). The migration creates it
automatically on first boot, but if the migration silently failed you'll see
this symptom.

**`/api/auth/login` returns 401 even with correct credentials** — Identity's
default password policy needs ≥ 8 chars with at least one digit and one
lowercase letter. Try resetting the password via the API or directly in
Postgres (`UPDATE "AspNetUsers" SET "PasswordHash" = ...`).

**Octopus API calls silently fail after deploy** — your API keys are encrypted
in Postgres after this change. Existing rows from a pre-encryption deploy fall
back to plaintext on read (see `SecretProtector.Unprotect`), but the cleanest
path is to re-enter credentials via Settings after deploy.
