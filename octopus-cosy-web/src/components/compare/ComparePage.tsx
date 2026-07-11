import { useState, useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  CartesianGrid,
  ComposedChart,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { useDevice } from '@/hooks/use-device'
import { useDailyAggregates } from '@/hooks/use-period-data'
import { FeatureGate } from '@/components/shared/FeatureGate'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'
import { fmtDec } from '@/lib/utils'
import type { DailyAggregateDto, PeriodSummaryDto } from '@/types/api'

/* ── Constants ────────────────────────────────────────────────────── */

const HDD_BASE_TEMP = 15.5

type ChartMetric = 'cop' | 'power' | 'temperature'

interface Preset {
  label: string
  getBaseline: () => { from: Date; to: Date }
  getChange: () => { from: Date; to: Date }
}

/* ── Date helpers ─────────────────────────────────────────────────── */

function daysAgo(n: number): Date {
  const d = new Date()
  d.setDate(d.getDate() - n)
  d.setHours(0, 0, 0, 0)
  return d
}

function endOfDay(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59, 999)
}

function startOfDay(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0, 0)
}

function toDateInput(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

function fromDateInput(s: string): Date {
  const [y, m, d] = s.split('-').map(Number)
  return new Date(y, m - 1, d)
}

/* ── Presets ───────────────────────────────────────────────────────── */

const PRESETS: Preset[] = [
  {
    label: 'Last 7d vs prev 7d',
    getBaseline: () => ({ from: daysAgo(14), to: endOfDay(daysAgo(8)) }),
    getChange: () => ({ from: daysAgo(7), to: endOfDay(new Date()) }),
  },
  {
    label: 'Last 30d vs prev 30d',
    getBaseline: () => ({ from: daysAgo(60), to: endOfDay(daysAgo(31)) }),
    getChange: () => ({ from: daysAgo(30), to: endOfDay(new Date()) }),
  },
  {
    label: 'This month vs last',
    getBaseline: () => {
      const now = new Date()
      return {
        from: new Date(now.getFullYear(), now.getMonth() - 1, 1),
        to: new Date(now.getFullYear(), now.getMonth(), 0, 23, 59, 59, 999),
      }
    },
    getChange: () => {
      const now = new Date()
      return {
        from: new Date(now.getFullYear(), now.getMonth(), 1),
        to: endOfDay(now),
      }
    },
  },
]

/* ── Chart metric definitions ─────────────────────────────────────── */

const CHART_METRICS: { key: ChartMetric; label: string }[] = [
  { key: 'cop', label: 'COP' },
  { key: 'power', label: 'Power' },
  { key: 'temperature', label: 'Temp' },
]

/* ── HDD computation ──────────────────────────────────────────────── */

function computeTotalHDD(aggregates: DailyAggregateDto[]): number {
  return aggregates.reduce((sum, d) => {
    if (d.avgOutdoorTemp == null) return sum
    return sum + Math.max(0, HDD_BASE_TEMP - d.avgOutdoorTemp)
  }, 0)
}

/* ── Period summary hook ──────────────────────────────────────────── */

function usePeriodSummary(deviceId: string | undefined, from: Date, to: Date) {
  return useQuery({
    queryKey: queryKeys.heatpump.periodSummary(
      deviceId ?? '',
      from.toISOString(),
      to.toISOString(),
    ),
    queryFn: () => api.heatpump.getPeriodSummary(deviceId!, from, to),
    enabled: !!deviceId,
    staleTime: 5 * 60_000,
  })
}

/* ── Metric card definitions ──────────────────────────────────────── */

interface MetricDef {
  label: string
  unit: string
  getValue: (
    summary: PeriodSummaryDto | undefined,
    aggs: DailyAggregateDto[],
  ) => number | null
  /** true = higher is better, false = lower is better, null = neutral */
  higherIsBetter: boolean | null
  decimals: number
}

const METRICS: MetricDef[] = [
  {
    label: 'Avg COP',
    unit: '',
    getValue: (s) => s?.avgCop ?? null,
    higherIsBetter: true,
    decimals: 2,
  },
  {
    label: 'Heat output',
    unit: 'kWh',
    getValue: (s) => (s ? s.totalOutputKwh : null),
    higherIsBetter: true,
    decimals: 1,
  },
  {
    label: 'Energy used',
    unit: 'kWh',
    getValue: (s) => (s ? s.totalInputKwh : null),
    higherIsBetter: false,
    decimals: 1,
  },
  {
    label: 'Avg outdoor',
    unit: '°C',
    getValue: (s) => s?.avgOutdoorTemp ?? null,
    higherIsBetter: null,
    decimals: 1,
  },
  {
    label: 'Degree days',
    unit: 'HDD',
    getValue: (_, aggs) => (aggs.length > 0 ? computeTotalHDD(aggs) : null),
    higherIsBetter: null,
    decimals: 1,
  },
  {
    label: 'Energy / HDD',
    unit: 'kWh/HDD',
    getValue: (s, aggs) => {
      if (!s || aggs.length === 0) return null
      const hdd = computeTotalHDD(aggs)
      if (hdd === 0) return null
      return s.totalInputKwh / hdd
    },
    higherIsBetter: false,
    decimals: 2,
  },
]

/* ── Overlay chart data ───────────────────────────────────────────── */

interface OverlayPoint {
  label: string
  baseline: number | null
  change: number | null
}

function getAggValue(
  agg: DailyAggregateDto,
  metric: ChartMetric,
): number | null {
  switch (metric) {
    case 'cop':
      return agg.avgCopHeating ?? null
    case 'power':
      return agg.totalElectricityKwh
    case 'temperature':
      return agg.avgOutdoorTemp ?? null
  }
}

function buildOverlayData(
  baselineAggs: DailyAggregateDto[],
  changeAggs: DailyAggregateDto[],
  metric: ChartMetric,
): OverlayPoint[] {
  const len = Math.max(baselineAggs.length, changeAggs.length)
  if (len === 0) return []

  const points: OverlayPoint[] = []
  for (let i = 0; i < len; i++) {
    const bAgg = i < baselineAggs.length ? baselineAggs[i] : null
    const cAgg = i < changeAggs.length ? changeAggs[i] : null
    points.push({
      label: `Day ${i + 1}`,
      baseline: bAgg ? getAggValue(bAgg, metric) : null,
      change: cAgg ? getAggValue(cAgg, metric) : null,
    })
  }
  return points
}

/* ── Formatting helpers ───────────────────────────────────────────── */

function formatMetricValue(v: number | null, decimals: number): string {
  if (v == null) return '—'
  return v.toFixed(decimals)
}

function formatDelta(n: number, decimals: number): string {
  const sign = n >= 0 ? '+' : '−'
  return `${sign}${Math.abs(n).toFixed(decimals)}`
}

/* ══════════════════════════════════════════════════════════════════ */
/*  Main component                                                    */
/* ══════════════════════════════════════════════════════════════════ */

export function ComparePage() {
  const { device, isLoading: deviceLoading, hasDevice } = useDevice()
  const deviceId = device?.deviceId

  /* period state */
  const [baselineFrom, setBaselineFrom] = useState(
    () => PRESETS[0].getBaseline().from,
  )
  const [baselineTo, setBaselineTo] = useState(
    () => PRESETS[0].getBaseline().to,
  )
  const [changeFrom, setChangeFrom] = useState(
    () => PRESETS[0].getChange().from,
  )
  const [changeTo, setChangeTo] = useState(
    () => PRESETS[0].getChange().to,
  )
  const [chartMetric, setChartMetric] = useState<ChartMetric>('cop')

  /* data fetching */
  const baselineSummary = usePeriodSummary(deviceId, baselineFrom, baselineTo)
  const changeSummary = usePeriodSummary(deviceId, changeFrom, changeTo)
  const baselineAggs = useDailyAggregates(deviceId, baselineFrom, baselineTo)
  const changeAggs = useDailyAggregates(deviceId, changeFrom, changeTo)

  const isLoading =
    baselineSummary.isLoading ||
    changeSummary.isLoading ||
    baselineAggs.isLoading ||
    changeAggs.isLoading

  /* preset handler */
  const applyPreset = (preset: Preset) => {
    const b = preset.getBaseline()
    const c = preset.getChange()
    setBaselineFrom(b.from)
    setBaselineTo(b.to)
    setChangeFrom(c.from)
    setChangeTo(c.to)
  }

  /* chart data */
  const chartData = useMemo(
    () =>
      buildOverlayData(
        baselineAggs.aggregates,
        changeAggs.aggregates,
        chartMetric,
      ),
    [baselineAggs.aggregates, changeAggs.aggregates, chartMetric],
  )

  /* ── device gate ────────────────────────────────────────────────── */

  if (deviceLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <LoadingSpinner size="lg" label="Loading…" />
      </div>
    )
  }

  if (!hasDevice) {
    return (
      <div className="max-w-md mx-auto mt-16 text-center">
        <h2 className="text-lg font-semibold mb-2">No device registered</h2>
        <p className="text-sm text-ink2">
          Set up your heat pump to compare periods.
        </p>
      </div>
    )
  }

  /* ── render ──────────────────────────────────────────────────────── */

  return (
    <div className="animate-up">
      {/* Header */}
      <div className="mb-5">
        <h1 className="text-xl font-semibold tracking-tight">Compare</h1>
        <p className="text-sm text-ink3 mt-0.5">
          See how changes affect your efficiency
        </p>
      </div>

      <FeatureGate
        requires="history"
        fallback={
          <div className="text-center py-20 text-sm text-ink3">
            Snapshot history is required for comparisons. Data will appear after
            the worker has collected snapshots.
          </div>
        }
      >
        {/* Presets */}
        <div className="flex gap-2 mb-4 overflow-x-auto pb-1 -mx-1 px-1">
          {PRESETS.map((p) => (
            <button
              key={p.label}
              onClick={() => applyPreset(p)}
              className="flex-shrink-0 px-3 py-2 rounded-[var(--radius-md)] border border-border-subtle bg-white text-sm text-ink2 hover:border-border-card hover:text-ink transition-colors duration-150 cursor-pointer whitespace-nowrap"
            >
              {p.label}
            </button>
          ))}
        </div>

        {/* Period pickers */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-5">
          <PeriodPicker
            label="Baseline"
            from={baselineFrom}
            to={baselineTo}
            onFromChange={(d) => setBaselineFrom(startOfDay(d))}
            onToChange={(d) => setBaselineTo(endOfDay(d))}
            accentClass="bg-ink3"
          />
          <PeriodPicker
            label="Change"
            from={changeFrom}
            to={changeTo}
            onFromChange={(d) => setChangeFrom(startOfDay(d))}
            onToChange={(d) => setChangeTo(endOfDay(d))}
            accentClass="bg-cyan-accent"
          />
        </div>

        {/* Metric cards */}
        {isLoading ? (
          <div className="grid grid-cols-2 md:grid-cols-3 gap-3 mb-5">
            {METRICS.map((m) => (
              <div
                key={m.label}
                className="h-[140px] bg-white border border-border-subtle rounded-[var(--radius-lg)] animate-pulse"
              />
            ))}
          </div>
        ) : (
          <div className="grid grid-cols-2 md:grid-cols-3 gap-3 mb-5">
            {METRICS.map((m) => (
              <CompareCard
                key={m.label}
                metric={m}
                baselineSummary={baselineSummary.data}
                changeSummary={changeSummary.data}
                baselineAggs={baselineAggs.aggregates}
                changeAggs={changeAggs.aggregates}
              />
            ))}
          </div>
        )}

        {/* Overlay chart */}
        <OverlayChart
          data={chartData}
          metric={chartMetric}
          onMetricChange={setChartMetric}
          isLoading={isLoading}
        />
      </FeatureGate>
    </div>
  )
}

/* ═══════════════════════════════════════════════════════════════════ */
/*  Subcomponents                                                      */
/* ═══════════════════════════════════════════════════════════════════ */

/* ── PeriodPicker ─────────────────────────────────────────────────── */

interface PeriodPickerProps {
  label: string
  from: Date
  to: Date
  onFromChange: (d: Date) => void
  onToChange: (d: Date) => void
  accentClass: string
}

function PeriodPicker({
  label,
  from,
  to,
  onFromChange,
  onToChange,
  accentClass,
}: PeriodPickerProps) {
  return (
    <div className="bg-white border border-border-subtle rounded-[var(--radius-lg)] p-4">
      <div className="flex items-center gap-2 mb-3">
        <span className={`w-2.5 h-2.5 rounded-full ${accentClass}`} />
        <span className="font-mono text-[11px] tracking-[.07em] uppercase text-ink3">
          {label}
        </span>
      </div>
      <div className="flex items-center gap-2">
        <input
          type="date"
          value={toDateInput(from)}
          onChange={(e) => {
            const d = fromDateInput(e.target.value)
            if (!isNaN(d.getTime())) onFromChange(d)
          }}
          className="flex-1 min-w-0 px-2.5 py-2 rounded-[var(--radius-sm)] border border-border-subtle bg-bg-base text-sm text-ink focus:outline-none focus:border-primary/50"
        />
        <span className="text-ink3 text-xs flex-shrink-0">to</span>
        <input
          type="date"
          value={toDateInput(to)}
          onChange={(e) => {
            const d = fromDateInput(e.target.value)
            if (!isNaN(d.getTime())) onToChange(d)
          }}
          className="flex-1 min-w-0 px-2.5 py-2 rounded-[var(--radius-sm)] border border-border-subtle bg-bg-base text-sm text-ink focus:outline-none focus:border-primary/50"
        />
      </div>
    </div>
  )
}

/* ── CompareCard ──────────────────────────────────────────────────── */

interface CompareCardProps {
  metric: MetricDef
  baselineSummary: PeriodSummaryDto | undefined
  changeSummary: PeriodSummaryDto | undefined
  baselineAggs: DailyAggregateDto[]
  changeAggs: DailyAggregateDto[]
}

function CompareCard({
  metric,
  baselineSummary,
  changeSummary,
  baselineAggs,
  changeAggs,
}: CompareCardProps) {
  const baselineVal = metric.getValue(baselineSummary, baselineAggs)
  const changeVal = metric.getValue(changeSummary, changeAggs)

  const hasBoth = baselineVal != null && changeVal != null
  const absDelta = hasBoth ? changeVal - baselineVal : null
  const pctDelta =
    hasBoth && baselineVal !== 0
      ? (absDelta! / Math.abs(baselineVal)) * 100
      : null
  const isUp = absDelta != null && absDelta >= 0

  let deltaColorClass = 'bg-bg-surface text-ink2'
  if (metric.higherIsBetter != null && absDelta != null) {
    const isGood = metric.higherIsBetter ? isUp : !isUp
    deltaColorClass = isGood
      ? 'bg-purple-bg text-purple'
      : 'bg-danger-bg text-danger'
  }

  return (
    <div className="bg-white border border-border-subtle rounded-[var(--radius-lg)] p-4 flex flex-col gap-1.5 min-h-[130px]">
      <div className="font-mono text-[11px] tracking-[.07em] uppercase text-ink3">
        {metric.label}
      </div>
      <div className="font-mono text-[28px] md:text-[32px] font-normal tracking-tight leading-none text-ink">
        {formatMetricValue(changeVal, metric.decimals)}
        {metric.unit && (
          <span className="text-[14px] md:text-[16px] font-light text-ink3 ml-1">
            {metric.unit}
          </span>
        )}
      </div>
      {hasBoth ? (
        <div className="flex flex-col gap-[2px] mt-auto pt-1">
          <span
            className={`self-start font-mono text-[12px] px-2 py-[3px] rounded whitespace-nowrap ${deltaColorClass}`}
          >
            {`${isUp ? '↑' : '↓'} ${formatDelta(absDelta!, metric.decimals)}${
              pctDelta != null
                ? ` (${Math.abs(pctDelta).toFixed(0)}%)`
                : ''
            }`}
          </span>
          <span className="font-mono text-ink3 text-[11px] tracking-[.02em]">
            was {formatMetricValue(baselineVal, metric.decimals)}
            {metric.unit ? ` ${metric.unit}` : ''}
          </span>
        </div>
      ) : (
        <span className="font-mono text-ink3 text-[11px] mt-auto pt-1">
          No data
        </span>
      )}
    </div>
  )
}

/* ── OverlayChart ─────────────────────────────────────────────────── */

interface ChartTooltipProps {
  active?: boolean
  payload?: { name: string; value: number; color: string }[]
  label?: string
}

function ChartTooltip({ active, payload, label }: ChartTooltipProps) {
  if (!active || !payload?.length) return null
  return (
    <div className="rounded-[10px] bg-ink text-white p-[12px_15px] text-sm shadow-[0_8px_28px_rgba(0,0,0,0.2)] min-w-[180px]">
      <p className="mb-1.5 font-mono text-[10px] tracking-[.09em] uppercase text-white/28">
        {label}
      </p>
      {payload.map((p) => (
        <div
          key={p.name}
          className="flex justify-between gap-3.5 py-[2px] items-baseline"
        >
          <span className="text-[11px]" style={{ color: p.color }}>
            {p.name}
          </span>
          <span className="font-mono text-[13px] text-white">
            {fmtDec(p.value, 2)}
          </span>
        </div>
      ))}
    </div>
  )
}

interface OverlayChartProps {
  data: OverlayPoint[]
  metric: ChartMetric
  onMetricChange: (m: ChartMetric) => void
  isLoading: boolean
}

function OverlayChart({
  data,
  metric,
  onMetricChange,
  isLoading,
}: OverlayChartProps) {
  return (
    <div className="bg-white border border-border-subtle rounded-[var(--radius-lg)] p-5 hover:border-border-card transition-colors duration-150">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 mb-4">
        <div>
          <div className="text-[15px] font-medium">Period overlay</div>
          <div className="text-ink3 text-[13px] mt-[2px]">
            Both periods on the same axis
          </div>
        </div>
        <div className="flex gap-[3px]">
          {CHART_METRICS.map((m) => (
            <button
              key={m.key}
              onClick={() => onMetricChange(m.key)}
              className={`font-mono text-[10px] tracking-[.06em] uppercase px-3 py-1.5 rounded-full border cursor-pointer transition-colors duration-150 ${
                metric === m.key
                  ? 'bg-white text-ink border-border-card'
                  : 'bg-transparent text-ink3 border-transparent hover:text-ink2'
              }`}
            >
              {m.label}
            </button>
          ))}
        </div>
      </div>

      {/* Legend */}
      <div className="flex gap-4 mb-3 text-[12px]">
        <span className="font-mono text-ink3 inline-flex items-center gap-1.5">
          <span className="w-3.5 h-[2px] bg-ink3 inline-block rounded-full" />
          Baseline
        </span>
        <span className="font-mono text-ink3 inline-flex items-center gap-1.5">
          <span className="w-3.5 h-[2px] bg-cyan-accent inline-block rounded-full" />
          Change
        </span>
      </div>

      <div className="h-[280px]">
        {isLoading ? (
          <div className="h-full flex items-center justify-center">
            <LoadingSpinner label="Loading…" />
          </div>
        ) : data.length === 0 ? (
          <div className="h-full flex items-center justify-center text-xs text-ink3">
            No data for the selected periods
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={280}>
            <ComposedChart
              data={data}
              margin={{ top: 4, right: 8, left: -16, bottom: 0 }}
            >
              <CartesianGrid
                strokeDasharray="3 3"
                stroke="rgba(0,0,0,0.04)"
              />
              <XAxis
                dataKey="label"
                tick={{
                  fontSize: 10,
                  fill: '#A1A1AA',
                  fontFamily: 'JetBrains Mono, monospace',
                }}
                tickLine={false}
                axisLine={false}
                interval="preserveStartEnd"
              />
              <YAxis
                tick={{
                  fontSize: 10,
                  fill: '#A1A1AA',
                  fontFamily: 'JetBrains Mono, monospace',
                }}
                tickLine={false}
                axisLine={false}
              />
              <Tooltip content={<ChartTooltip />} />
              <Line
                type="monotone"
                dataKey="baseline"
                name="Baseline"
                stroke="#A1A1AA"
                strokeWidth={1.5}
                dot={false}
                connectNulls
              />
              <Line
                type="monotone"
                dataKey="change"
                name="Change"
                stroke="#22D3EE"
                strokeWidth={1.5}
                dot={false}
                connectNulls
              />
            </ComposedChart>
          </ResponsiveContainer>
        )}
      </div>
    </div>
  )
}
