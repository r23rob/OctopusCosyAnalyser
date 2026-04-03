import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'
import type { HeatPumpSnapshotDto, HeatPumpSummaryDto } from '@/types/api'
import type { PeriodDays } from '@/components/dashboard/PeriodSelector'
import { CopScatterChart } from '@/components/charts/ScatterChart'
import { TimeSeriesChart } from '@/components/charts/TimeSeriesChart'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { ErrorAlert } from '@/components/shared/ErrorAlert'
import { useAnalysisMetrics } from '@/hooks/use-analysis-metrics'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'
import { copColorClass, fmtDec, fmtPercent, periodStart, downsample } from '@/lib/utils'
import { TrendChart } from '@/components/charts/TrendChart'

interface Props {
  snapshots: HeatPumpSnapshotDto[]
  summary: HeatPumpSummaryDto | null | undefined
  deviceId: string
  euid: string | null | undefined
  accountNumber: string
  periodDays: PeriodDays
  isLoading?: boolean
}

type SubTab = 'insights' | 'timeseries'

export function PerformanceTab({ snapshots, summary, deviceId, euid, accountNumber, periodDays, isLoading }: Props) {
  const [subTab, setSubTab] = useState<SubTab>('insights')
  const [useStored, setUseStored] = useState(true)
  const queryClient = useQueryClient()

  const metrics = useAnalysisMetrics(snapshots)
  const displayed = downsample(snapshots, 500)

  const from = periodStart(periodDays)
  const to = new Date()

  const tsQuery = useQuery({
    queryKey: useStored
      ? queryKeys.heatpump.storedTimeSeries(deviceId, from.toISOString(), to.toISOString())
      : queryKeys.heatpump.timeSeries(accountNumber, euid ?? '', from.toISOString(), to.toISOString()),
    queryFn: () =>
      useStored
        ? api.heatpump.getStoredTimeSeries(deviceId, from, to)
        : api.heatpump.getTimeSeries(accountNumber, euid!, from, to),
    enabled: subTab === 'timeseries' && (useStored ? true : !!euid),
    staleTime: 5 * 60_000,
  })

  const syncMutation = useMutation({
    mutationFn: () => api.heatpump.syncTimeSeries(deviceId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['heatpump', 'stored-time-series', deviceId] })
    },
  })

  const copFromSummary = summary?.livePerformance?.coefficientOfPerformance
  const cop = copFromSummary != null ? parseFloat(copFromSummary) : null

  return (
    <div className="flex flex-col gap-5">
      {/* Sub-tab selector */}
      <div className="flex gap-1 border-b border-white/[0.06] pb-2">
        {(['insights', 'timeseries'] as SubTab[]).map((t) => (
          <button
            key={t}
            onClick={() => setSubTab(t)}
            className={`px-3 py-1.5 rounded-t text-xs font-medium transition-colors ${
              subTab === t ? 'bg-white/[0.08] text-white' : 'text-white/40 hover:text-white/70'
            }`}
          >
            {t === 'insights' ? 'Efficiency Insights' : 'Historic Time Series'}
          </button>
        ))}
      </div>

      {subTab === 'insights' && (
        <>
          {/* Live COP highlight */}
          {cop != null && (
            <div className="flex items-center gap-3 px-4 py-3 rounded-lg bg-white/[0.03] border border-white/[0.06]">
              <span className="text-white/50 text-sm">Current COP</span>
              <span className={`text-3xl font-bold ${copColorClass(cop)}`}>{fmtDec(cop)}</span>
              <span className="text-white/30 text-xs">×</span>
            </div>
          )}

          {/* Duty cycle */}
          <div className="grid grid-cols-2 gap-3">
            <div className="rounded-lg bg-white/[0.03] border border-white/[0.06] px-3 py-2">
              <div className="text-[10px] text-white/40 uppercase tracking-wide mb-1">Heating Duty Cycle</div>
              <div className="text-lg font-bold text-amber-400">{fmtPercent(metrics.heatingDutyCycle)}</div>
            </div>
            <div className="rounded-lg bg-white/[0.03] border border-white/[0.06] px-3 py-2">
              <div className="text-[10px] text-white/40 uppercase tracking-wide mb-1">Hot Water Duty Cycle</div>
              <div className="text-lg font-bold text-cyan-400">{fmtPercent(metrics.hotWaterDutyCycle)}</div>
            </div>
          </div>

          {isLoading ? (
            <LoadingSpinner label="Loading snapshots…" />
          ) : snapshots.length === 0 ? (
            <p className="text-xs text-white/40">No snapshot data for this period.</p>
          ) : (
            <>
              {/* COP trend */}
              <Section title="COP Over Time">
                <TrendChart
                  snapshots={displayed}
                  series={[
                    { key: 'coefficientOfPerformance', label: 'COP', color: '#22c55e', yAxisId: 'left' },
                    { key: 'outdoorTemperatureCelsius', label: 'Outdoor °C', color: '#f59e0b', yAxisId: 'right', strokeDasharray: '4 2' },
                  ]}
                  dualAxis
                />
              </Section>

              {/* COP vs Outdoor scatter */}
              <Section title="COP vs Outdoor Temperature">
                <CopScatterChart snapshots={snapshots} xKey="outdoorTemperatureCelsius" xLabel="Outdoor Temp" xUnit="°C" />
              </Section>

              {/* COP vs Flow scatter */}
              {metrics.copVsFlow.length > 0 && (
                <Section title="COP vs Flow Temperature">
                  <CopScatterChart snapshots={snapshots} xKey="heatingFlowTemperatureCelsius" xLabel="Flow Temp" xUnit="°C" />
                </Section>
              )}

              {/* Room temp trend */}
              {snapshots.some((s) => s.roomTemperatureCelsius != null) && (
                <Section title="Room Temperature & Humidity">
                  <TrendChart
                    snapshots={displayed}
                    series={[
                      { key: 'roomTemperatureCelsius', label: 'Room °C', color: '#8b5cf6', yAxisId: 'left' },
                      { key: 'roomHumidityPercentage', label: 'Humidity %', color: '#06b6d4', yAxisId: 'right', strokeDasharray: '4 2' },
                    ]}
                    dualAxis
                  />
                </Section>
              )}
            </>
          )}
        </>
      )}

      {subTab === 'timeseries' && (
        <>
          <div className="flex items-center justify-between">
            <div className="flex gap-1">
              {(['stored', 'live'] as const).map((mode) => (
                <button
                  key={mode}
                  onClick={() => setUseStored(mode === 'stored')}
                  className={`px-2.5 py-1 rounded text-xs transition-colors ${
                    (mode === 'stored') === useStored
                      ? 'bg-blue-500/20 text-blue-300 border border-blue-500/30'
                      : 'text-white/40 hover:text-white/70'
                  }`}
                >
                  {mode === 'stored' ? 'Stored (DB)' : 'Live (Octopus API)'}
                </button>
              ))}
            </div>
            {useStored && (
              <button
                onClick={() => syncMutation.mutate()}
                disabled={syncMutation.isPending}
                className="flex items-center gap-1.5 px-2.5 py-1 rounded text-xs text-white/50 hover:text-white/80 border border-white/10 hover:border-white/20 transition-colors disabled:opacity-40"
              >
                <RefreshCw size={11} className={syncMutation.isPending ? 'animate-spin' : ''} />
                {syncMutation.isPending ? 'Syncing…' : 'Sync History'}
              </button>
            )}
          </div>

          {syncMutation.isSuccess && (
            <div className="text-xs text-green-300 bg-green-500/10 border border-green-500/20 rounded px-2 py-1">
              Synced {syncMutation.data.synced} records, skipped {syncMutation.data.skipped}.
            </div>
          )}

          {tsQuery.isLoading && <LoadingSpinner label="Loading time series…" />}
          {tsQuery.isError && <ErrorAlert message="Failed to load time series data." />}
          {tsQuery.data?.status === 'NoData' && (
            <p className="text-xs text-white/40">No time series data available. Try syncing history first.</p>
          )}
          {tsQuery.data?.status === 'Ok' && tsQuery.data.points.length > 0 && (
            <Section title="Energy Performance">
              <TimeSeriesChart points={tsQuery.data.points} />
            </Section>
          )}
        </>
      )}
    </div>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-lg border border-white/[0.06] bg-white/[0.02]">
      <div className="px-3 py-2 border-b border-white/[0.06]">
        <span className="text-xs font-medium text-white/60">{title}</span>
      </div>
      <div className="p-3">{children}</div>
    </div>
  )
}
