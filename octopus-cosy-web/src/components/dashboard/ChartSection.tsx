import { useState } from 'react'
import type { HeatPumpSnapshotDto } from '@/types/api'
import { SeriesToggle } from '@/components/dashboard/SeriesToggle'
import { TrendChart } from '@/components/charts/TrendChart'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'

export type SnapshotKey = keyof HeatPumpSnapshotDto

interface TrendSeriesItem {
  key: SnapshotKey
  label: string
  color: string
  yAxisId: 'left' | 'right'
  dash?: string
}

const TREND_SERIES: TrendSeriesItem[] = [
  { key: 'powerInputKilowatt', label: 'kW in', color: 'var(--chart-1)', yAxisId: 'left' },
  { key: 'heatOutputKilowatt', label: 'kW out', color: 'var(--cyan-accent)', yAxisId: 'left' },
  { key: 'coefficientOfPerformance', label: 'COP', color: 'var(--chart-3)', yAxisId: 'left' },
  { key: 'outdoorTemperatureCelsius', label: 'Outside °C', color: 'var(--chart-4)', yAxisId: 'right' },
  { key: 'heatingFlowTemperatureCelsius', label: 'Flow °C', color: 'var(--chart-5)', yAxisId: 'right' },
  { key: 'heatingZoneSetpointCelsius', label: 'Setpoint', color: 'var(--chart-5)', yAxisId: 'right', dash: '4 3' },
]

interface Props {
  snapshots: HeatPumpSnapshotDto[]
  isLoading: boolean
}

export function ChartSection({ snapshots, isLoading }: Props) {
  const [activeSeries, setActiveSeries] = useState<Set<string>>(
    () => new Set(TREND_SERIES.map(s => s.key)),
  )

  const visibleSeries = TREND_SERIES.filter(s => activeSeries.has(s.key))

  const toggleSeries = (key: string) => {
    setActiveSeries(prev => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  return (
    <div className="bg-bg-card border border-border-subtle rounded-[var(--radius-lg)] p-5 hover:border-border-card transition-colors duration-150">
      <div className="flex justify-between items-start mb-2.5 gap-2">
        <div className="font-mono text-[11px] tracking-[.1em] uppercase text-ink3">
          Energy · COP · Temperature · Setpoint
        </div>
        <div className="flex flex-wrap gap-[3px]">
          {TREND_SERIES.map(s => (
            <SeriesToggle
              key={s.key}
              color={s.color}
              label={s.label}
              active={activeSeries.has(s.key)}
              onToggle={() => toggleSeries(s.key)}
            />
          ))}
        </div>
      </div>
      <div className="relative h-[360px]">
        {isLoading ? (
          <div className="h-full flex items-center justify-center">
            <LoadingSpinner label="Loading data…" />
          </div>
        ) : snapshots.length === 0 ? (
          <div className="h-full flex items-center justify-center text-xs text-ink3">
            No data for this period
          </div>
        ) : (
          <TrendChart
            snapshots={snapshots}
            series={visibleSeries.map(s => ({
              key: s.key,
              label: s.label,
              color: s.color,
              yAxisId: s.yAxisId,
              strokeDasharray: s.dash,
            }))}
            dualAxis
            height={360}
          />
        )}
      </div>
    </div>
  )
}
