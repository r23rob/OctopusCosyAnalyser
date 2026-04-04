import { createFileRoute } from '@tanstack/react-router'
import { useState, useMemo } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Settings } from 'lucide-react'
import { Link } from '@tanstack/react-router'

import { useDevice } from '@/hooks/use-device'
import { useDashboard } from '@/hooks/use-dashboard'
import { usePeriodData } from '@/hooks/use-period-data'
import { usePeriodNavigation } from '@/hooks/use-period-navigation'

import { PeriodSelector } from '@/components/dashboard/PeriodSelector'
import { MetricsStrip } from '@/components/dashboard/MetricsStrip'
import { SeriesToggle } from '@/components/dashboard/SeriesToggle'
import { CopGaugeCard } from '@/components/dashboard/CopGaugeCard'
import { RoomTempsCard } from '@/components/dashboard/RoomTempsCard'
import { AvgPowerCard } from '@/components/dashboard/AvgPowerCard'
import { EfficiencySplitCard } from '@/components/dashboard/EfficiencySplitCard'
import { CopByTempCard } from '@/components/dashboard/CopByTempCard'

import { TrendChart } from '@/components/charts/TrendChart'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { ErrorAlert } from '@/components/shared/ErrorAlert'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'
import { downsample, periodSubtitle, vsLabel } from '@/lib/utils'

export const Route = createFileRoute('/heatpump/')({
  component: DashboardPage,
})

interface TrendSeriesItem {
  key: keyof import('@/types/api').HeatPumpSnapshotDto
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

function DashboardPage() {
  const period = usePeriodNavigation()
  const [activeSeries, setActiveSeries] = useState<Set<string>>(
    new Set(TREND_SERIES.map(s => s.key)),
  )

  const { device, settings, isLoading: deviceLoading, hasDevice } = useDevice()
  const deviceId = device?.deviceId

  const { summary, isError: summaryError } = useDashboard(deviceId)
  const { snapshots, periodSummary, isLoading: periodLoading } = usePeriodData(deviceId, period.from, period.to)

  const displayed = downsample(snapshots, 500)
  const visibleSeries = TREND_SERIES.filter(s => activeSeries.has(s.key))

  const toggleSeries = (key: string) => {
    setActiveSeries(prev => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  // Room temps derived from snapshots
  const roomTemps = useMemo(() => {
    if (snapshots.length === 0) return []
    const temps = snapshots.map(s => s.roomTemperatureCelsius).filter((t): t is number => t != null)
    if (temps.length === 0) return []
    const sum = temps.reduce((a, b) => a + b, 0)
    const avg = +(sum / temps.length).toFixed(1)
    const min = +temps.reduce((a, b) => Math.min(a, b)).toFixed(1)
    const max = +temps.reduce((a, b) => Math.max(a, b)).toFixed(1)
    const variance = +((max - min) / 4).toFixed(1)
    return [{ name: 'Room', avg, min, max, variance }]
  }, [snapshots])

  // Avg power values
  const avgPowerIn = periodSummary?.totalInputKwh != null && periodSummary.snapshotCount > 0
    ? periodSummary.totalInputKwh / periodSummary.snapshotCount * 4
    : (summary?.livePerformance?.powerInput?.value != null ? parseFloat(summary.livePerformance.powerInput.value) : null)
  const avgPowerOut = periodSummary?.totalOutputKwh != null && periodSummary.snapshotCount > 0
    ? periodSummary.totalOutputKwh / periodSummary.snapshotCount * 4
    : (summary?.livePerformance?.heatOutput?.value != null ? parseFloat(summary.livePerformance.heatOutput.value) : null)

  // Setup mutation
  const queryClient = useQueryClient()
  const setupMutation = useMutation({
    mutationFn: () => api.devices.setup(settings!.accountNumber),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.devices.all() }),
  })

  if (deviceLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <LoadingSpinner size="lg" label="Loading…" />
      </div>
    )
  }

  if (!settings) {
    return (
      <div className="max-w-md mx-auto mt-16 text-center flex flex-col items-center gap-4">
        <div className="w-12 h-12 rounded-full bg-primary/10 border border-primary/20 flex items-center justify-center">
          <Settings size={20} className="text-primary" />
        </div>
        <h2 className="text-lg font-semibold">Welcome to Ecodan Monitor</h2>
        <p className="text-sm text-ink2">Enter your Octopus Energy credentials to get started.</p>
        <Link
          to="/settings"
          className="px-4 py-2 rounded-lg bg-ink hover:bg-ink2 text-white text-sm font-medium transition-colors"
        >
          Go to Settings
        </Link>
      </div>
    )
  }

  if (!hasDevice) {
    return (
      <div className="max-w-md mx-auto mt-16 text-center flex flex-col items-center gap-4">
        <h2 className="text-lg font-semibold">No Device Registered</h2>
        <p className="text-sm text-ink2">Your credentials are saved. Now discover your heat pump device.</p>
        <button
          onClick={() => setupMutation.mutate()}
          disabled={setupMutation.isPending}
          className="px-4 py-2 rounded-lg bg-ink hover:bg-ink2 disabled:opacity-40 text-white text-sm font-medium transition-colors"
        >
          {setupMutation.isPending ? 'Setting up…' : 'Setup Device'}
        </button>
        {setupMutation.isError && (
          <ErrorAlert message="Setup failed. Check your account number in Settings." />
        )}
      </div>
    )
  }

  return (
    <div>
      {/* Period bar */}
      <PeriodSelector
        periodType={period.periodType}
        onPeriodChange={period.setPeriodType}
        label={period.label}
        subtitle={periodSubtitle(period.periodType)}
        onPrev={period.prev}
        onNext={period.next}
        canGoNext={period.canGoNext}
      />

      {summaryError && (
        <ErrorAlert message="Could not load live data. The API may be unavailable." />
      )}

      {/* KPI strip */}
      <MetricsStrip
        periodSummary={periodSummary}
        vsLabel={vsLabel(period.periodType)}
      />

      {/* Main grid: chart + dial column */}
      <div className="grid grid-cols-1 lg:grid-cols-[1fr_204px] gap-2.5 mb-2.5 animate-up" style={{ animationDelay: '0.2s' }}>
        {/* Chart card */}
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
            {periodLoading ? (
              <div className="h-full flex items-center justify-center">
                <LoadingSpinner label="Loading data…" />
              </div>
            ) : displayed.length === 0 ? (
              <div className="h-full flex items-center justify-center text-xs text-ink3">
                No data for this period
              </div>
            ) : (
              <TrendChart
                snapshots={displayed}
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

        {/* Dial column */}
        <div className="flex flex-col gap-2.5">
          <CopGaugeCard
            cop={periodSummary?.avgCop ?? (summary?.livePerformance?.coefficientOfPerformance != null ? parseFloat(summary.livePerformance.coefficientOfPerformance) : null)}
            flowTemp={periodSummary?.avgFlowTemp ?? null}
            setpointTemp={null}
          />
          <RoomTempsCard rooms={roomTemps} />
        </div>
      </div>

      {/* Bottom row */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-2.5 animate-up" style={{ animationDelay: '0.28s' }}>
        <AvgPowerCard
          avgPowerIn={avgPowerIn}
          avgPowerOut={avgPowerOut}
          snapshots={snapshots}
        />
        <EfficiencySplitCard snapshots={snapshots} />
        <CopByTempCard snapshots={snapshots} />
      </div>
    </div>
  )
}
