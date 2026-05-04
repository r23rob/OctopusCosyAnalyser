// Resource-group-scoped module: all the resources for one environment.

param environmentName string
param location string
param imageTag string
param ghcrOwner string
param ghcrRepo string
param postgresAdminUser string
@secure()
param postgresAdminPassword string
param corsAllowedOrigins string
@secure()
param anthropicApiKey string

var apiImage = 'ghcr.io/${ghcrOwner}/${ghcrRepo}/apiservice:${imageTag}'
var dbName = 'cosydb'
// 30 + 6h cycle for non-snapshot workers — keeps Octopus API call volume modest.
var snapshotCron = '*/30 * * * *'
var timeseriesCron = '0 */6 * * *'
var costCron = '15 */6 * * *'
var intervalsCron = '*/35 * * * *'

// ── Log Analytics workspace ────────────────────────────────────────────────
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${environmentName}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ── Container Apps managed environment ─────────────────────────────────────
resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${environmentName}-aca'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

// ── Postgres Flexible Server (Burstable B1ms zonal) ────────────────────────
// ~£14/mo at this tier. PgBouncer enabled so the small connection ceiling
// (~50 conns) survives multiple replicas + workers + identity queries.
resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: '${environmentName}-pg'
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '17'
    administratorLogin: postgresAdminUser
    administratorLoginPassword: postgresAdminPassword
    storage: {
      storageSizeGB: 32
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

resource postgresFirewallAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgres
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource postgresPgBouncer 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: postgres
  name: 'pgbouncer.enabled'
  properties: {
    value: 'true'
    source: 'user-override'
  }
}

resource postgresDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: dbName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
  dependsOn: [
    postgresFirewallAzure
  ]
}

// ── Storage account for SPA static hosting ─────────────────────────────────
// We deploy the React build to $web with `az storage blob upload-batch`.
resource storage 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: replace('${environmentName}web', '-', '')
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: true
    supportsHttpsTrafficOnly: true
  }
}

resource staticWebsite 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  parent: storage
  name: 'default'
  properties: {
    cors: {
      corsRules: []
    }
  }
}

// Connection string assembled with PgBouncer (port 6432) and Maximum Pool Size
// matching the per-replica allowance on B1ms.
var pgConn = 'Host=${postgres.properties.fullyQualifiedDomainName};Port=6432;Database=${dbName};Username=${postgresAdminUser};Password=${postgresAdminPassword};SSL Mode=Require;Trust Server Certificate=true;Maximum Pool Size=10'

// ── Container App: API (long-running) ──────────────────────────────────────
resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${environmentName}-api'
  location: location
  properties: {
    managedEnvironmentId: acaEnv.id
    configuration: {
      activeRevisionsMode: 'Multiple' // enables instant rollback via `az containerapp revision activate`
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      secrets: [
        { name: 'pg-connection', value: pgConn }
        { name: 'anthropic-api-key', value: anthropicApiKey }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'ConnectionStrings__cosydb', secretRef: 'pg-connection' }
            { name: 'Anthropic__ApiKey', secretRef: 'anthropic-api-key' }
            { name: 'Cors__AllowedOrigins__0', value: corsAllowedOrigins }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/alive', port: 8080 }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 5
              periodSeconds: 15
            }
          ]
        }
      ]
      scale: {
        // minReplicas=1 avoids cold start on first byte at the cost of ~£4/mo.
        // Set to 0 if you'd rather pay only for traffic and accept ~3-8s cold start.
        minReplicas: 1
        maxReplicas: 5
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '40'
              }
            }
          }
        ]
      }
    }
  }
}

// ── Container Apps Jobs: scheduled workers ────────────────────────────────
// Each job uses the *same* image as the API but invokes it with --run-worker-once.
module snapshotJob './worker-job.bicep' = {
  name: 'snapshot-job'
  params: {
    name: '${environmentName}-job-snapshot'
    location: location
    acaEnvId: acaEnv.id
    image: apiImage
    cronExpression: snapshotCron
    workerArg: 'snapshot'
    pgConnection: pgConn
    anthropicApiKey: anthropicApiKey
  }
}

module timeseriesJob './worker-job.bicep' = {
  name: 'timeseries-job'
  params: {
    name: '${environmentName}-job-timeseries'
    location: location
    acaEnvId: acaEnv.id
    image: apiImage
    cronExpression: timeseriesCron
    workerArg: 'timeseries'
    pgConnection: pgConn
    anthropicApiKey: anthropicApiKey
  }
}

module costJob './worker-job.bicep' = {
  name: 'cost-job'
  params: {
    name: '${environmentName}-job-cost'
    location: location
    acaEnvId: acaEnv.id
    image: apiImage
    cronExpression: costCron
    workerArg: 'cost'
    pgConnection: pgConn
    anthropicApiKey: anthropicApiKey
  }
}

module intervalsJob './worker-job.bicep' = {
  name: 'intervals-job'
  params: {
    name: '${environmentName}-job-intervals'
    location: location
    acaEnvId: acaEnv.id
    image: apiImage
    cronExpression: intervalsCron
    workerArg: 'energy-intervals'
    pgConnection: pgConn
    anthropicApiKey: anthropicApiKey
  }
}

output apiContainerAppName string = api.name
output apiFqdn string = api.properties.configuration.ingress.fqdn
output postgresHost string = postgres.properties.fullyQualifiedDomainName
output staticWebsiteUrl string = storage.properties.primaryEndpoints.web
output snapshotJobName string = snapshotJob.outputs.name
output timeseriesJobName string = timeseriesJob.outputs.name
output costJobName string = costJob.outputs.name
output intervalsJobName string = intervalsJob.outputs.name
