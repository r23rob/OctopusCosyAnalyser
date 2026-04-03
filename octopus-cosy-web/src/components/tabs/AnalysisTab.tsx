import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import type { HeatPumpSnapshotDto } from '@/types/api'
import type { PeriodDays } from '@/components/dashboard/PeriodSelector'
import { TrendChart } from '@/components/charts/TrendChart'
import { TimeSeriesChart } from '@/components/charts/TimeSeriesChart'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { ErrorAlert } from '@/components/shared/ErrorAlert'
import { DateRangePicker } from '@/components/shared/DateRangePicker'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'
import { fmtDec, fmtKwh, fmtPercent, fmtTemp, periodStart, downsample } from '@/lib/utils'

interface Props {
  snapshots: HeatPumpSnapshotDto[]
  deviceId: string
  accountNumber: string
  euid: string | null | undefined
  periodDays: PeriodDays
  isLoading?: boolean
}

type Section = 'snapshots' | 'timeseries' | 'compare'

export function AnalysisTab({ snapshots, deviceId, periodDays, isLoading }: Props) {
  const [section, setSection] = useState<Section>('snapshots')
  const [compareFrom, setCompareFrom] = useState(() => periodStart(14))
  const [compareTo, setCompareTo] = useState(() => new Date())
  const [baselineFrom, setBaselineFrom] = useState(() => periodStart(30))
  const [baselineTo, setBaselineTo] = useState(() => periodStart(14))

  const displayed = downsample(snapshots, 500)


  const from = periodStart(periodDays)
  const to = new Date()

  const storedTsQuery = useQuery({
    queryKey: queryKeys.heatpump.storedTimeSeries(deviceId, from.toISOString(), to.toISOString()),
    queryFn: () => api.heatpump.getStoredTimeSeries(deviceId, from, to),
    enabled: section === 'timeseries',
    staleTime: 5 * 60_000,
  })

  const comparePeriodQuery = useQuery({
    queryKey: queryKeys.heatpump.periodSummary(deviceId, compareFrom.toISOString(), compareTo.toISOString()),
    queryFn: () => api.heatpump.getPeriodSummary(deviceId, compareFrom, compareTo),
    enabled: section === 'compare',
    staleTime: 5 * 60_000,
  })

  const baselinePeriodQuery = useQuery({
    queryKey: queryKeys.heatpump.periodSummary(deviceId, baselineFrom.toISOString(), baselineTo.toISOString()),
    queryFn: () => api.heatpump.getPeriodSummary(deviceId, baselineFrom, baselineTo),
    enabled: section === 'compare',
    staleTime: 5 * 60_000,
  })

  return (
    <div className="flex flex-col gap-5">
      {/* Section tabs */}
      <div className="flex gap-1 border-b border-white/[0.06] pb-2">
        {(['snapshots', 'timeseries', 'compare'] as Section[]).map((s) => (
          <button
            key={s}
            onClick={() => setSection(s)}
            className={`px-3 py-1.5 rounded-t text-xs font-medium transition-colors ${
              section === s ? 'bg-white/[0.08] text-white' : 'text-white/40 hover:text-white/70'
            }`}
          >
            {s === 'snapshots' ? 'Snapshot Charts' : s === 'timeseries' ? 'Time Series' : 'Compare Periods'}
          </button>
        ))}
      </div>

      {/* Snapshot charts */}
      {section === 'snapshots' && (
        <>
          {isLoading && <LoadingSpinner label="Loading snapshots…" />}
          {!isLoading && snapshots.length === 0 && (
            <p className="text-xs text-white/40">No snapshot data for this period.</p>
          )}
          {snapshots.length > 0 && (
            <>
              <ChartCard title="Power Input & Heat Output (kW)">
                <TrendChart
                  snapshots={displayed}
                  series={[
                    { key: 'powerInputKilowatt', label: 'Power In (kW)', color: '#3b82f6', yAxisId: 'left' },
                    { key: 'heatOutputKilowatt', label: 'Heat Out (kW)', color: '#ef4444', yAxisId: 'left' },
                  ]}
                />
              </ChartCard>
              <ChartCard title="COP & Outdoor Temperature">
                <TrendChart
                  snapshots={displayed}
                  series={[
                    { key: 'coefficientOfPerformance', label: 'COP', color: '#22c55e', yAxisId: 'left' },
                    { key: 'outdoorTemperatureCelsius', label: 'Outdoor °C', color: '#f59e0b', yAxisId: 'right', strokeDasharray: '4 2' },
                  ]}
                  dualAxis
                />
              </ChartCard>
              <ChartCard title="Flow Temperature">
                <TrendChart
                  snapshots={displayed}
                  series={[
                    { key: 'heatingFlowTemperatureCelsius', label: 'Flow °C', color: '#06b6d4', yAxisId: 'left' },
                    { key: 'heatingZoneSetpointCelsius', label: 'Setpoint °C', color: '#8b5cf6', yAxisId: 'left', strokeDasharray: '3 3' },
                  ]}
                />
              </ChartCard>
            </>
          )}
        </>
      )}

      {/* Time series */}
      {section === 'timeseries' && (
        <>
          {storedTsQuery.isLoading && <LoadingSpinner label="Loading time series…" />}
          {storedTsQuery.isError && <ErrorAlert message="Failed to load time series data." />}
          {storedTsQuery.data?.status === 'NoData' && (
            <p className="text-xs text-white/40">
              No stored time series. Use the Sync button in Performance tab to populate it.
            </p>
          )}
          {storedTsQuery.data?.status === 'Ok' && storedTsQuery.data.points.length > 0 && (
            <ChartCard title="Historic Energy Performance">
              <TimeSeriesChart points={storedTsQuery.data.points} showCop showOutput showInput showOutdoor />
            </ChartCard>
          )}
        </>
      )}

      {/* Compare periods */}
      {section === 'compare' && (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="rounded-lg border border-white/[0.08] p-3 flex flex-col gap-2">
              <h3 className="text-xs font-medium text-white/60">Baseline Period</h3>
              <DateRangePicker
                from={baselineFrom}
                to={baselineTo}
                onChange={(f, t) => { setBaselineFrom(f); setBaselineTo(t) }}
              />
            </div>
            <div className="rounded-lg border border-blue-500/20 bg-blue-500/[0.03] p-3 flex flex-col gap-2">
              <h3 className="text-xs font-medium text-blue-300/70">Compare Period</h3>
              <DateRangePicker
                from={compareFrom}
                to={compareTo}
                onChange={(f, t) => { setCompareFrom(f); setCompareTo(t) }}
              />
            </div>
          </div>

          {(comparePeriodQuery.isLoading || baselinePeriodQuery.isLoading) && (
            <LoadingSpinner label="Comparing periods…" />
          )}

          {comparePeriodQuery.data && baselinePeriodQuery.data && (
            <div className="overflow-x-auto">
              <table className="w-full text-xs">
                <thead>
                  <tr className="border-b border-white/[0.06]">
                    <th className="text-left py-2 text-white/40 font-medium">Metric</th>
                    <th className="text-right py-2 text-white/40 font-medium">Baseline</th>
                    <th className="text-right py-2 text-white/40 font-medium">Compare</th>
                    <th className="text-right py-2 text-white/40 font-medium">Delta</th>
                  </tr>
                </thead>
                <tbody>
                  {[
                    {
                      label: 'Avg COP',
                      baseline: baselinePeriodQuery.data.avgCop,
                      compare: comparePeriodQuery.data.avgCop,
                      fmt: fmtDec,
                      higherIsBetter: true,
                    },
                    {
                      label: 'Heat Out (kWh)',
                      baseline: baselinePeriodQuery.data.totalOutputKwh,
                      compare: comparePeriodQuery.data.totalOutputKwh,
                      fmt: fmtKwh,
                      higherIsBetter: true,
                    },
                    {
                      label: 'Power In (kWh)',
                      baseline: baselinePeriodQuery.data.totalInputKwh,
                      compare: comparePeriodQuery.data.totalInputKwh,
                      fmt: fmtKwh,
                      higherIsBetter: false,
                    },
                    {
                      label: 'Avg Outdoor °C',
                      baseline: baselinePeriodQuery.data.avgOutdoorTemp,
                      compare: comparePeriodQuery.data.avgOutdoorTemp,
                      fmt: fmtTemp,
                      higherIsBetter: null,
                    },
                    {
                      label: 'Heating Duty %',
                      baseline: baselinePeriodQuery.data.heatingDutyCyclePercent,
                      compare: comparePeriodQuery.data.heatingDutyCyclePercent,
                      fmt: fmtPercent,
                      higherIsBetter: null,
                    },
                  ].map((row) => {
                    const b = row.baseline ?? 0
                    const c = row.compare ?? 0
                    const delta = c - b
                    const pct = b !== 0 ? ((delta / Math.abs(b)) * 100).toFixed(1) : null
                    const better = row.higherIsBetter == null
                      ? null
                      : (row.higherIsBetter ? delta > 0 : delta < 0)
                    const deltaColor = better === null
                      ? 'text-white/50'
                      : better ? 'text-green-400' : 'text-red-400'

                    return (
                      <tr key={row.label} className="border-b border-white/[0.04]">
                        <td className="py-2 text-white/70">{row.label}</td>
                        <td className="py-2 text-right text-white/60">{row.fmt(b)}</td>
                        <td className="py-2 text-right text-white/85">{row.fmt(c)}</td>
                        <td className={`py-2 text-right font-medium ${deltaColor}`}>
                          {delta >= 0 ? '+' : ''}{row.fmt(delta)}
                          {pct && <span className="text-white/30 ml-1">({pct}%)</span>}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  )
}

function ChartCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-lg border border-white/[0.06] bg-white/[0.02]">
      <div className="px-3 py-2 border-b border-white/[0.06]">
        <span className="text-xs font-medium text-white/60">{title}</span>
      </div>
      <div className="p-3">{children}</div>
    </div>
  )
}
