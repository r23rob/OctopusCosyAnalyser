import { cn } from '@/lib/utils'
import type { HeatPumpSummaryDto, LatestSnapshotDto } from '@/types/api'

interface PillProps {
  label: string
  value: string
  active?: boolean
  variant?: 'default' | 'success' | 'warning' | 'danger' | 'info'
}

function Pill({ label, value, active, variant = 'default' }: PillProps) {
  const dotColors = {
    default: 'bg-ink3',
    success: 'bg-success',
    warning: 'bg-warning',
    danger: 'bg-danger',
    info: 'bg-primary',
  }

  return (
    <div className="flex items-center gap-1.5 px-2 py-1 rounded-full border border-border-subtle bg-white text-xs">
      <span
        className={cn(
          'w-1.5 h-1.5 rounded-full flex-shrink-0',
          dotColors[variant],
          active && 'pulse',
        )}
      />
      <span className="text-ink3">{label}:</span>
      <span className="text-ink font-medium">{value}</span>
    </div>
  )
}

interface Props {
  summary: HeatPumpSummaryDto | null | undefined
  latest: LatestSnapshotDto | null | undefined
}

export function StatusPills({ summary, latest }: Props) {
  const config = summary?.controllerConfiguration
  const status = summary?.controllerStatus

  const heatingZone = config?.zones.find((z) => z.configuration?.zoneType === 'HEAT')?.configuration
  const hwZone = config?.zones.find((z) => z.configuration?.zoneType === 'HOT_WATER')?.configuration
  const controllerState = config?.controller?.state?.[0] ?? 'UNKNOWN'
  const isHeating = controllerState === 'HEATING'

  const wc = config?.heatPump?.weatherCompensation
  const wcEnabled = wc?.enabled === true

  const hwDemand = hwZone?.heatDemand === true
  const heatingDemand = heatingZone?.heatDemand === true

  const minutesAgo = latest?.minutesAgo
  const workerHealthy = minutesAgo != null && minutesAgo < 20
  const workerLabel = minutesAgo != null ? `${Math.round(minutesAgo)}m ago` : 'No data'

  const roomSensor = status?.sensors.find((s) => s.telemetry?.temperatureInCelsius != null)
  const roomTemp = roomSensor?.telemetry?.temperatureInCelsius

  return (
    <div className="flex flex-wrap gap-1.5">
      <Pill label="Controller" value={controllerState} active={isHeating} variant={isHeating ? 'success' : 'default'} />
      <Pill label="Flow mode" value={wcEnabled ? 'Weather Comp' : 'Fixed Flow'} variant={wcEnabled ? 'info' : 'warning'} />
      {heatingDemand && <Pill label="Heating" value="Demand" active variant="warning" />}
      {hwDemand && <Pill label="Hot water" value="Demand" active variant="info" />}
      {roomTemp != null && <Pill label="Room" value={`${roomTemp.toFixed(1)}°C`} variant="default" />}
      <Pill label="Worker" value={workerLabel} variant={workerHealthy ? 'success' : 'danger'} active={workerHealthy} />
    </div>
  )
}
