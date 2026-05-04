// Container Apps Job: schedule-triggered worker run.
// Uses the same image as the API container app, just with a different startup command.

param name string
param location string
param acaEnvId string
param image string

@description('Cron expression — Linux/Unix-style 5-field. ACA Jobs interpret in UTC.')
param cronExpression string

@description('Argument passed as --run-worker-once <name> to switch the API host into run-once mode.')
param workerArg string

param pgConnection string
@secure()
param anthropicApiKey string

resource job 'Microsoft.App/jobs@2024-03-01' = {
  name: name
  location: location
  properties: {
    environmentId: acaEnvId
    configuration: {
      replicaTimeout: 1800 // 30 min — generous; most runs finish in < 60s.
      replicaRetryLimit: 1
      triggerType: 'Schedule'
      scheduleTriggerConfig: {
        cronExpression: cronExpression
        parallelism: 1
        replicaCompletionCount: 1
      }
      secrets: [
        { name: 'pg-connection', value: pgConnection }
        { name: 'anthropic-api-key', value: anthropicApiKey }
      ]
    }
    template: {
      containers: [
        {
          name: 'worker'
          image: image
          // Run-once mode — the host runs the worker, then exits with code 0/1.
          args: [
            '--run-worker-once'
            workerArg
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ConnectionStrings__cosydb', secretRef: 'pg-connection' }
            { name: 'Anthropic__ApiKey', secretRef: 'anthropic-api-key' }
          ]
        }
      ]
    }
  }
}

output name string = job.name
