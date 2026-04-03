import { createFileRoute } from '@tanstack/react-router'
import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Settings } from 'lucide-react'
import { Link } from '@tanstack/react-router'

import { useDevice } from '@/hooks/use-device'
import { useDashboard, useAiSummary } from '@/hooks/use-dashboard'
import { usePeriodData } from '@/hooks/use-period-data'

import { GaugeSection } from '@/components/dashboard/GaugeSection'
import { StatusPills } from '@/components/dashboard/StatusPills'
import { PeriodSelector, type PeriodDays } from '@/components/dashboard/PeriodSelector'
import { MetricsStrip } from '@/components/dashboard/MetricsStrip'
import { SeriesToggle } from '@/components/dashboard/SeriesToggle'
import { AiSummaryPanel } from '@/components/dashboard/AiSummaryPanel'

import { PerformanceTab } from '@/components/tabs/PerformanceTab'
import { ComfortTab } from '@/components/tabs/ComfortTab'
import { AnalysisTab } from '@/components/tabs/AnalysisTab'
import { CostsTab } from '@/components/tabs/CostsTab'

import { TrendChart } from '@/components/charts/TrendChart'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { ErrorAlert } from '@/components/shared/ErrorAlert'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'
import { downsample } from '@/lib/utils'

export const Route = createFileRoute('/heatpump/')({
  component: DashboardPage,
})

type TabId = 'performance' | 'comfort' | 'analysis' | 'costs'

const TABS: { id: TabId; label: string }[] = [
  { id: 'performance', label: 'Performance & History' },
  { id: 'comfort', label: 'Comfort & System' },
  { id: 'analysis', label: 'Analysis' },
  { id: 'costs', label: 'Costs' },
]

const TREND_SERIES = [
  { key: 'coefficientOfPerformance' as const, label: 'COP', color: '#22c55e' },
  { key: 'powerInputKilowatt' as const, label: 'Power In', color: '#3b82f6' },
  { key: 'heatOutputKilowatt' as const, label: 'Heat Out', color: '#ef4444' },
  { key: 'outdoorTemperatureCelsius' as const, label: 'Outdoor', color: '#f59e0b' },
]

function DashboardPage() {
  const [periodDays, setPeriodDays] = useState<PeriodDays>(7)
  const [activeTab, setActiveTab] = useState<TabId>('performance')
  const [activeSeries, setActiveSeries] = useState<Set<string>>(
    new Set(['coefficientOfPerformance', 'powerInputKilowatt', 'heatOutputKilowatt', 'outdoorTemperatureCelsius']),
  )

  const { device, settings, isLoading: deviceLoading, hasDevice } = useDevice()
  const deviceId = device?.deviceId
  const euid = device?.euid
  const accountNumber = device?.accountNumber ?? settings?.accountNumber ?? ''

  const { summary, latest, isLoading: summaryLoading, isError: summaryError } = useDashboard(deviceId)
  const { snapshots, periodSummary, isLoading: periodLoading } = usePeriodData(deviceId, periodDays)

  const { summary: aiSummary, isLoading: aiLoading, isRefreshing, refresh: refreshAi } = useAiSummary(deviceId)

  const displayed = downsample(snapshots, 500)
  const visibleSeries = TREND_SERIES.filter((s) => activeSeries.has(s.key))

  const toggleSeries = (key: string) => {
    setActiveSeries((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  // Setup mutation
  const queryClient = useQueryClient()
  const setupMutation = useMutation({
    mutationFn: () => api.devices.setup(settings!.accountNumber),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.devices.all() }),
  })

  // Not configured state
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
        <div className="w-12 h-12 rounded-full bg-blue-500/10 border border-blue-500/20 flex items-center justify-center">
          <Settings size={20} className="text-blue-400" />
        </div>
        <h2 className="text-lg font-semibold text-white/90">Welcome to Cosy Analyser</h2>
        <p className="text-sm text-white/50">
          Enter your Octopus Energy credentials to get started.
        </p>
        <Link
          to="/settings"
          className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
        >
          Go to Settings
        </Link>
      </div>
    )
  }

  if (!hasDevice) {
    return (
      <div className="max-w-md mx-auto mt-16 text-center flex flex-col items-center gap-4">
        <h2 className="text-lg font-semibold text-white/90">No Device Registered</h2>
        <p className="text-sm text-white/50">
          Your credentials are saved. Now discover your heat pump device.
        </p>
        <button
          onClick={() => setupMutation.mutate()}
          disabled={setupMutation.isPending}
          className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 disabled:opacity-40 text-white text-sm font-medium transition-colors"
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
    <div className="flex flex-col gap-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold text-white/90">Dashboard</h1>
        <div className="flex items-center gap-3">
          <PeriodSelector value={periodDays} onChange={setPeriodDays} loading={periodLoading} />
        </div>
      </div>

      {summaryError && (
        <ErrorAlert message="Could not load live data. The API may be unavailable." />
      )}

      {/* Gauges */}
      <div className="rounded-xl border border-white/[0.08] bg-[#1e2130] p-4">
        {summaryLoading ? (
          <div className="flex justify-around py-4">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="w-32 h-32 rounded-full bg-white/[0.04] animate-pulse" />
            ))}
          </div>
        ) : (
          <GaugeSection summary={summary} />
        )}
        <div className="mt-3">
          <StatusPills summary={summary} latest={latest} />
        </div>
      </div>

      {/* Metrics strip */}
      <MetricsStrip periodSummary={periodSummary} />

      {/* Main trend chart */}
      <div className="rounded-xl border border-white/[0.08] bg-[#1e2130]">
        <div className="flex items-center justify-between px-4 py-3 border-b border-white/[0.06]">
          <span className="text-xs font-medium text-white/60">Trend</span>
          <div className="flex gap-1">
            {TREND_SERIES.map((s) => (
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
        <div className="p-3">
          {periodLoading ? (
            <div className="h-60 flex items-center justify-center">
              <LoadingSpinner label="Loading data…" />
            </div>
          ) : displayed.length === 0 ? (
            <div className="h-60 flex items-center justify-center text-xs text-white/30">
              No data for this period
            </div>
          ) : (
            <TrendChart snapshots={displayed} series={visibleSeries.map(s => ({ ...s, yAxisId: 'left' }))} />
          )}
        </div>
      </div>

      {/* AI Summary */}
      {deviceId && (
        <AiSummaryPanel
          summary={aiSummary}
          isLoading={aiLoading}
          onRefresh={() => refreshAi()}
          isRefreshing={isRefreshing}
        />
      )}

      {/* Tab navigation */}
      <div className="rounded-xl border border-white/[0.08] bg-[#1e2130]">
        <div className="flex border-b border-white/[0.06] overflow-x-auto">
          {TABS.map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`px-4 py-3 text-xs font-medium whitespace-nowrap transition-colors border-b-2 ${
                activeTab === tab.id
                  ? 'border-blue-500 text-white'
                  : 'border-transparent text-white/40 hover:text-white/70'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>

        <div className="p-4">
          {activeTab === 'performance' && deviceId && (
            <PerformanceTab
              snapshots={snapshots}
              summary={summary}
              deviceId={deviceId}
              euid={euid}
              accountNumber={accountNumber}
              periodDays={periodDays}
              isLoading={periodLoading}
            />
          )}
          {activeTab === 'comfort' && (
            <ComfortTab summary={summary} isLoading={summaryLoading} />
          )}
          {activeTab === 'analysis' && deviceId && (
            <AnalysisTab
              snapshots={snapshots}
              deviceId={deviceId}
              accountNumber={accountNumber}
              euid={euid}
              periodDays={periodDays}
              isLoading={periodLoading}
            />
          )}
          {activeTab === 'costs' && deviceId && (
            <CostsTab
              deviceId={deviceId}
              accountNumber={accountNumber}
              periodDays={periodDays}
            />
          )}
        </div>
      </div>
    </div>
  )
}
