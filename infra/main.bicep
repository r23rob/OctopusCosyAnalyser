// Azure infrastructure for OctopusCosyAnalyser SaaS deployment.
//
// Provisions:
//   - Log Analytics workspace (logs from ACA + Postgres)
//   - Container Apps managed environment
//   - Postgres Flexible Server (Burstable B1ms, zonal — cheap tier)
//   - Container App for the API (minReplicas=1, maxReplicas=5)
//   - Container Apps Jobs for each scheduled worker (every 30 min)
//   - Storage account + Static Website for the SPA bundle
//
// Image tags are passed as parameters so the same template deploys any commit.
// Run with the deploy.sh helper or:
//   az deployment sub create --location uksouth \
//     --template-file main.bicep \
//     --parameters environmentName=cosy-prod imageTag=sha-<commit>

targetScope = 'subscription'

@description('Short name for this environment (e.g. cosy-prod, cosy-staging). Used as a prefix for all resource names.')
@minLength(3)
@maxLength(20)
param environmentName string

@description('Azure region for all resources.')
param location string = 'uksouth'

@description('Container image tag to deploy (e.g. sha-abc1234, or latest). Both API and worker use the same image.')
param imageTag string = 'latest'

@description('GitHub repo owner — used to construct the ghcr.io image path.')
param ghcrOwner string = 'r23rob'

@description('GitHub repo name — used to construct the ghcr.io image path.')
param ghcrRepo string = 'octopuscosyanalyser'

@description('Postgres administrator username.')
param postgresAdminUser string = 'cosyadmin'

@description('Postgres administrator password. Pass via secure parameter file or KeyVault reference.')
@secure()
param postgresAdminPassword string

@description('Comma-separated CORS origins for the SPA (e.g. https://cosy.example.com).')
param corsAllowedOrigins string = ''

@description('Anthropic API key fallback — leave empty if every user supplies their own via Settings.')
@secure()
param anthropicApiKey string = ''

var resourceGroupName = '${environmentName}-rg'

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module main './modules/main-group.bicep' = {
  name: 'main-group'
  scope: rg
  params: {
    environmentName: environmentName
    location: location
    imageTag: imageTag
    ghcrOwner: ghcrOwner
    ghcrRepo: ghcrRepo
    postgresAdminUser: postgresAdminUser
    postgresAdminPassword: postgresAdminPassword
    corsAllowedOrigins: corsAllowedOrigins
    anthropicApiKey: anthropicApiKey
  }
}

output apiContainerAppName string = main.outputs.apiContainerAppName
output apiFqdn string = main.outputs.apiFqdn
output postgresHost string = main.outputs.postgresHost
output staticWebsiteUrl string = main.outputs.staticWebsiteUrl
output snapshotJobName string = main.outputs.snapshotJobName
output timeseriesJobName string = main.outputs.timeseriesJobName
output costJobName string = main.outputs.costJobName
output intervalsJobName string = main.outputs.intervalsJobName
