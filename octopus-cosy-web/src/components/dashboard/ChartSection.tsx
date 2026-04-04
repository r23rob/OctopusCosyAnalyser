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
  { key: 'powerInputKilowatt', label: 'kW in', color: '#F97316', yAxisId: 'left' },
  { key: 'heatOutputKilowatt', label: 'kW out', color: '#06B6D4', yAxisId: 'left' },
  { key: 'coefficientOfPerformance', label: 'COP', color: '#16A34A', yAxisId: 'left' },
  { key: 'outdoorTemperatureCelsius', label: 'Outside °C', color: '#8B5CF6', yAxisId: 'right' },
  { key: 'heatingFlowTemperatureCelsius', label: 'Flow °C', color: '#D97706', yAxisId: 'right' },
  { key: 'heatingZoneSetpointCelsius', label: 'Setpoint', color: '#D97706', yAxisId: 'right', dash: '4 3' },
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
    <div className="bg-white border border-border-subtle rounded-[10px] p-4 hover:border-border-card transition-colors duration-150">
      <div className="flex justify-between items-start mb-2.5 gap-2">
        <div className="font-mono text-[8px] tracking-[.1em] uppercase text-ink3">
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
      <div className="relative h-[228px]">
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
          />
        )}
      </div>
    </div>
  )
}
