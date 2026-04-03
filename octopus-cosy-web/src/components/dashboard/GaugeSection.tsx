import { RadialGauge } from '@/components/charts/RadialGauge'
import type { HeatPumpSummaryDto } from '@/types/api'

interface Props {
  summary: HeatPumpSummaryDto | null | undefined
}

export function GaugeSection({ summary }: Props) {
  const live = summary?.livePerformance
  const cop = live?.coefficientOfPerformance != null ? parseFloat(live.coefficientOfPerformance) : null
  const powerIn = live?.powerInput?.value != null ? parseFloat(live.powerInput.value) : null
  const heatOut = live?.heatOutput?.value != null ? parseFloat(live.heatOutput.value) : null

  return (
    <div className="flex justify-around items-center py-2">
      <RadialGauge
        value={cop}
        label="COP"
        unit="×"
        min={0}
        max={6}
        copMode
      />
      <RadialGauge
        value={powerIn}
        label="Power In"
        unit="kW"
        min={0}
        max={6}
        color="#3b82f6"
      />
      <RadialGauge
        value={heatOut}
        label="Heat Out"
        unit="kW"
        min={0}
        max={14}
        color="#ef4444"
      />
    </div>
  )
}
