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
    default: 'bg-white/40',
    success: 'bg-green-400',
    warning: 'bg-amber-400',
    danger: 'bg-red-400',
    info: 'bg-cyan-400',
  }

  return (
    <div className="flex items-center gap-1.5 px-2 py-1 rounded-full border border-white/10 bg-white/[0.04] text-xs">
      <span
        className={cn(
          'w-1.5 h-1.5 rounded-full flex-shrink-0',
          dotColors[variant],
          active && 'pulse',
        )}
      />
      <span className="text-white/50">{label}:</span>
      <span className="text-white/85 font-medium">{value}</span>
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

  // Controller state from zones
  const heatingZone = config?.zones.find((z) => z.configuration?.zoneType === 'HEAT')?.configuration
  const hwZone = config?.zones.find((z) => z.configuration?.zoneType === 'HOT_WATER')?.configuration
  const controllerState = config?.controller?.state?.[0] ?? 'UNKNOWN'
  const isHeating = controllerState === 'HEATING'

  // Weather compensation
  const wc = config?.heatPump?.weatherCompensation
  const wcEnabled = wc?.enabled === true

  // Hot water demand
  const hwDemand = hwZone?.heatDemand === true
  const heatingDemand = heatingZone?.heatDemand === true

  // Worker health
  const minutesAgo = latest?.minutesAgo
  const workerHealthy = minutesAgo != null && minutesAgo < 20
  const workerLabel = minutesAgo != null ? `${Math.round(minutesAgo)}m ago` : 'No data'

  // Room sensor
  const roomSensor = status?.sensors.find((s) => s.telemetry?.temperatureInCelsius != null)
  const roomTemp = roomSensor?.telemetry?.temperatureInCelsius

  return (
    <div className="flex flex-wrap gap-1.5">
      <Pill
        label="Controller"
        value={controllerState}
        active={isHeating}
        variant={isHeating ? 'success' : 'default'}
      />
      <Pill
        label="Flow mode"
        value={wcEnabled ? 'Weather Comp' : 'Fixed Flow'}
        variant={wcEnabled ? 'info' : 'warning'}
      />
      {heatingDemand && (
        <Pill label="Heating" value="Demand" active variant="warning" />
      )}
      {hwDemand && (
        <Pill label="Hot water" value="Demand" active variant="info" />
      )}
      {roomTemp != null && (
        <Pill label="Room" value={`${roomTemp.toFixed(1)}°C`} variant="default" />
      )}
      <Pill
        label="Worker"
        value={workerLabel}
        variant={workerHealthy ? 'success' : 'danger'}
        active={workerHealthy}
      />
    </div>
  )
}
