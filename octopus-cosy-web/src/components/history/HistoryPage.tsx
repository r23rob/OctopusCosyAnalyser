import { useState, useMemo, useCallback } from 'react'
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
import { useDailyAggregates, usePeriodData } from '@/hooks/use-period-data'
import { CopScatterChart } from '@/components/charts/ScatterChart'
import { FeatureGate } from '@/components/shared/FeatureGate'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { periodStart, fmtDec, copColor, shortDate } from '@/lib/utils'
import type { DailyAggregateDto, HeatPumpSnapshotDto } from '@/types/api'

// ── Types ────────────────────────────────────────────────────────────

type TabId = 'overview' | 'temperature' | 'scatter'

interface TabDef {
  id: TabId
  label: string
}

const TABS: TabDef[] = [
  { id: 'overview', label: 'Overview' },
  { id: 'temperature', label: 'Temperature' },
  { id: 'scatter', label: 'COP Scatter' },
]

type SortColumn = 'date' | 'cop' | 'heatOut' | 'powerIn' | 'outdoor' | 'hoursRun'
type SortDir = 'asc' | 'desc'

type TempPeriod = 7 | 30 | 90

// ── Main component ───────────────────────────────────────────────────

export function HistoryPage() {
  const [activeTab, setActiveTab] = useState<TabId>('overview')

  return (
    <FeatureGate
      requires="history"
      fallback={
        <div className="flex flex-col items-center justify-center h-[60vh] animate-up">
          <h1 className="text-xl font-semibold tracking-tight text-ink">History</h1>
          <p className="text-sm text-ink3 mt-2">
            Historical data requires a database connection. Check your settings.
          </p>
        </div>
      }
    >
      <div className="max-w-screen-2xl mx-auto animate-up">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-5">
          <h1 className="text-xl font-semibold tracking-tight">History</h1>
          <TabBar activeTab={activeTab} onTabChange={setActiveTab} />
        </div>

        {activeTab === 'overview' && <OverviewTab />}
        {activeTab === 'temperature' && <TemperatureTab />}
        {activeTab === 'scatter' && <ScatterTab />}
      </div>
    </FeatureGate>
  )
}

// ── Tab bar ──────────────────────────────────────────────────────────

function TabBar({ activeTab, onTabChange }: { activeTab: TabId; onTabChange: (id: TabId) => void }) {
  return (
    <div className="flex bg-bg-surface border border-border-subtle rounded-[var(--radius-pill)] p-[3px] gap-0.5">
      {TABS.map(({ id, label }) => (
        <button
          key={id}
          onClick={() => onTabChange(id)}
          className={`font-mono text-[10px] sm:text-[12px] tracking-[.07em] uppercase px-[10px] sm:px-[16px] py-[7px] rounded-[var(--radius-pill)] border-none cursor-pointer transition-all duration-150 whitespace-nowrap ${
            activeTab === id
              ? 'bg-purple text-white'
              : 'bg-transparent text-ink2 hover:text-ink'
          }`}
        >
          {label}
        </button>
      ))}
    </div>
  )
}

// ── Overview Tab ─────────────────────────────────────────────────────

function OverviewTab() {
  const { device } = useDevice()
  const deviceId = device?.deviceId

  const from = useMemo(() => periodStart(90), [])
  const to = useMemo(() => new Date(), [])

  const { aggregates, isLoading } = useDailyAggregates(deviceId, from, to)

  const [sortCol, setSortCol] = useState<SortColumn>('date')
  const [sortDir, setSortDir] = useState<SortDir>('desc')

  const handleSort = useCallback((col: SortColumn) => {
    setSortCol((prev) => {
      if (prev === col) {
        setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'))
        return col
      }
      setSortDir('desc')
      return col
    })
  }, [])

  const sorted = useMemo(() => {
    const rows = [...aggregates]
    const dir = sortDir === 'asc' ? 1 : -1
    rows.sort((a, b) => {
      switch (sortCol) {
        case 'date':
          return dir * a.date.localeCompare(b.date)
        case 'cop':
          return dir * ((a.avgCopHeating ?? 0) - (b.avgCopHeating ?? 0))
        case 'heatOut':
          return dir * (a.totalHeatOutputKwh - b.totalHeatOutputKwh)
        case 'powerIn':
          return dir * (a.totalElectricityKwh - b.totalElectricityKwh)
        case 'outdoor':
          return dir * ((a.avgOutdoorTemp ?? 0) - (b.avgOutdoorTemp ?? 0))
        case 'hoursRun':
          return dir * (a.heatingDutyCyclePercent - b.heatingDutyCyclePercent)
        default:
          return 0
      }
    })
    return rows
  }, [aggregates, sortCol, sortDir])

  const exportCSV = useCallback(() => {
    let csv = 'Date,Avg COP,Heat Output kWh,Power Input kWh,Avg Outdoor C,Hours Run\n'
    sorted.forEach((r) => {
      const hoursRun = ((r.heatingDutyCyclePercent / 100) * 24).toFixed(1)
      csv += `${r.date},${fmtDec(r.avgCopHeating, 2)},${r.totalHeatOutputKwh.toFixed(1)},${r.totalElectricityKwh.toFixed(1)},${fmtDec(r.avgOutdoorTemp, 1)},${hoursRun}\n`
    })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = 'heat-pump-daily.csv'
    a.click()
  }, [sorted])

  return (
    <div>
      <div className="flex justify-end mb-3">
        <button
          onClick={exportCSV}
          disabled={aggregates.length === 0}
          className="font-mono text-[11px] tracking-[.07em] uppercase px-3 py-[6px] border border-border-card rounded-[var(--radius-sm)] bg-white cursor-pointer text-ink2 hover:bg-bg-surface hover:text-ink transition-all duration-150 disabled:opacity-40 disabled:cursor-default"
        >
          Export CSV
        </button>
      </div>

      <div className="bg-white border border-border-subtle rounded-[var(--radius-lg)] overflow-x-auto">
        {isLoading ? (
          <div className="h-64 flex items-center justify-center">
            <LoadingSpinner label="Loading data..." />
          </div>
        ) : sorted.length === 0 ? (
          <div className="p-8 text-center text-sm text-ink3">No daily data available</div>
        ) : (
          <DailyTable rows={sorted} sortCol={sortCol} sortDir={sortDir} onSort={handleSort} />
        )}
      </div>
    </div>
  )
}

// ── Sortable column header ───────────────────────────────────────────

const COLUMNS: { key: SortColumn; label: string }[] = [
  { key: 'date', label: 'Date' },
  { key: 'cop', label: 'Avg COP' },
  { key: 'heatOut', label: 'Heat Out kWh' },
  { key: 'powerIn', label: 'Power In kWh' },
  { key: 'outdoor', label: 'Outdoor' },
  { key: 'hoursRun', label: 'Hours Run' },
]

function SortIndicator({ col, sortCol, sortDir }: { col: SortColumn; sortCol: SortColumn; sortDir: SortDir }) {
  if (col !== sortCol) return null
  return <span className="ml-1 text-ink4">{sortDir === 'asc' ? '↑' : '↓'}</span>
}

function DailyTable({
  rows,
  sortCol,
  sortDir,
  onSort,
}: {
  rows: DailyAggregateDto[]
  sortCol: SortColumn
  sortDir: SortDir
  onSort: (col: SortColumn) => void
}) {
  return (
    <table className="w-full border-collapse">
      <thead>
        <tr>
          {COLUMNS.map(({ key, label }) => (
            <th
              key={key}
              onClick={() => onSort(key)}
              className="text-left px-3 py-[9px] font-mono text-[10px] md:text-[11px] tracking-[.1em] uppercase text-ink3 border-b border-border-subtle font-normal whitespace-nowrap cursor-pointer select-none hover:text-ink2 transition-colors duration-100"
            >
              {label}
              <SortIndicator col={key} sortCol={sortCol} sortDir={sortDir} />
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {rows.map((r) => {
          const hoursRun = (r.heatingDutyCyclePercent / 100) * 24
          return (
            <tr key={r.date} className="hover:bg-bg-base transition-colors duration-100">
              <td className="px-3 py-[9px] border-b border-border-subtle text-[12px] md:text-[13px] text-ink2 font-normal whitespace-nowrap">
                {new Date(r.date).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}
              </td>
              <td className="px-3 py-[9px] border-b border-border-subtle">
                <CopBadge value={r.avgCopHeating} />
              </td>
              <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[12px] md:text-[13px] text-ink font-normal whitespace-nowrap tabular-nums">
                {r.totalHeatOutputKwh.toFixed(1)}
              </td>
              <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[12px] md:text-[13px] text-ink font-normal whitespace-nowrap tabular-nums">
                {r.totalElectricityKwh.toFixed(1)}
              </td>
              <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[12px] md:text-[13px] text-ink font-normal whitespace-nowrap tabular-nums">
                {fmtDec(r.avgOutdoorTemp, 1)}
              </td>
              <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[12px] md:text-[13px] text-ink font-normal whitespace-nowrap tabular-nums">
                {hoursRun.toFixed(1)}
              </td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}

// ── COP Badge ────────────────────────────────────────────────────────

function CopBadge({ value }: { value: number | null | undefined }) {
  if (value == null) return <span className="font-mono text-[10.5px] text-ink3">&mdash;</span>
  const color = copColor(value)
  const bgColor = value >= 3.2 ? 'rgba(22,163,74,0.08)' : value >= 2.5 ? 'rgba(217,119,6,0.08)' : 'rgba(220,38,38,0.08)'
  return (
    <span
      className="inline-block px-2 py-0.5 rounded font-mono text-[11px] md:text-[12px] font-medium whitespace-nowrap tabular-nums"
      style={{ color, background: bgColor }}
    >
      {value.toFixed(2)}
    </span>
  )
}

// ── Temperature Chart Tab ────────────────────────────────────────────

const TEMP_PERIODS: { value: TempPeriod; label: string }[] = [
  { value: 7, label: '7d' },
  { value: 30, label: '30d' },
  { value: 90, label: '90d' },
]

function TemperatureTab() {
  const { device } = useDevice()
  const [period, setPeriod] = useState<TempPeriod>(30)

  const from = useMemo(() => periodStart(period), [period])
  const to = useMemo(() => new Date(), [])

  const { snapshots, isLoading } = usePeriodData(device?.deviceId, from, to)

  return (
    <div>
      <div className="flex justify-end mb-3">
        <div className="flex bg-bg-surface border border-border-subtle rounded-[var(--radius-pill)] p-[3px] gap-0.5">
          {TEMP_PERIODS.map(({ value, label }) => (
            <button
              key={value}
              onClick={() => setPeriod(value)}
              className={`font-mono text-[12px] tracking-[.07em] uppercase px-[14px] py-[6px] rounded-[var(--radius-pill)] border-none cursor-pointer transition-all duration-150 ${
                period === value
                  ? 'bg-purple text-white'
                  : 'bg-transparent text-ink2 hover:text-ink'
              }`}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      <div className="bg-white border border-border-subtle rounded-[var(--radius-lg)] p-4 md:p-6">
        {isLoading ? (
          <div className="h-[400px] md:h-[520px] flex items-center justify-center">
            <LoadingSpinner label="Loading temperature data..." />
          </div>
        ) : snapshots.length === 0 ? (
          <div className="h-[400px] md:h-[520px] flex items-center justify-center text-sm text-ink3">
            No temperature data available
          </div>
        ) : (
          <TemperatureChart snapshots={snapshots} />
        )}
      </div>
    </div>
  )
}

// ── Temperature chart rendering ──────────────────────────────────────

interface TempChartPoint {
  t: string
  outdoor: number | null
  flow: number | null
  room: number | null
}

const TempTooltip = ({
  active,
  payload,
  label,
}: {
  active?: boolean
  payload?: { name: string; value: number | null; color: string }[]
  label?: string
}) => {
  if (!active || !payload?.length) return null
  return (
    <div className="rounded-[10px] bg-ink text-white p-[12px_15px] text-sm shadow-[0_8px_28px_rgba(0,0,0,0.2)] min-w-[210px]">
      <p className="mb-1.5 font-mono text-[10px] tracking-[.09em] uppercase text-white/28">{label}</p>
      {payload.map((p) => (
        <div key={p.name} className="flex justify-between gap-3.5 py-[2px] items-baseline">
          <span className="text-[11px]" style={{ color: p.color }}>{p.name}</span>
          <span className="font-mono text-[13px] text-white">
            {p.value != null ? `${p.value.toFixed(1)}°C` : '—'}
          </span>
        </div>
      ))}
    </div>
  )
}

function TemperatureChart({ snapshots }: { snapshots: HeatPumpSnapshotDto[] }) {
  const data = useMemo<TempChartPoint[]>(() => {
    return snapshots.map((s) => ({
      t: shortDate(s.snapshotTakenAt),
      outdoor: s.outdoorTemperatureCelsius ?? null,
      flow: s.heatingFlowTemperatureCelsius ?? null,
      room: s.roomTemperatureCelsius ?? null,
    }))
  }, [snapshots])

  return (
    <div>
      <ResponsiveContainer width="100%" height={400}>
        <ComposedChart data={data} margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.04)" />
          <XAxis
            dataKey="t"
            tick={{ fontSize: 10, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
            tickLine={false}
            axisLine={false}
            interval="preserveStartEnd"
          />
          <YAxis
            tick={{ fontSize: 10, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
            tickLine={false}
            axisLine={false}
            label={{
              value: '°C',
              angle: -90,
              position: 'insideLeft',
              offset: 20,
              style: { fontSize: 10, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' },
            }}
          />
          <Tooltip content={<TempTooltip />} />
          <Line
            type="monotone"
            dataKey="outdoor"
            name="Outdoor"
            stroke="var(--chart-2)"
            strokeWidth={1.5}
            dot={false}
            connectNulls
          />
          <Line
            type="monotone"
            dataKey="flow"
            name="Flow"
            stroke="var(--chart-6)"
            strokeWidth={1.5}
            dot={false}
            connectNulls
          />
          <Line
            type="monotone"
            dataKey="room"
            name="Room"
            stroke="var(--chart-4)"
            strokeWidth={1.5}
            dot={false}
            connectNulls
          />
        </ComposedChart>
      </ResponsiveContainer>

      <div className="flex items-center gap-5 mt-4 px-1">
        <LegendDot color="var(--chart-2)" label="Outdoor" />
        <LegendDot color="var(--chart-6)" label="Flow" />
        <LegendDot color="var(--chart-4)" label="Room" />
      </div>
    </div>
  )
}

function LegendDot({ color, label }: { color: string; label: string }) {
  return (
    <div className="flex items-center gap-1.5">
      <span className="w-2.5 h-2.5 rounded-full inline-block" style={{ background: color }} />
      <span className="font-mono text-[11px] text-ink3 tracking-[.05em] uppercase">{label}</span>
    </div>
  )
}

// ── COP Scatter Tab ──────────────────────────────────────────────────

function ScatterTab() {
  const { device } = useDevice()
  const from = useMemo(() => periodStart(90), [])
  const to = useMemo(() => new Date(), [])
  const { snapshots, isLoading } = usePeriodData(device?.deviceId, from, to)

  return (
    <div className="bg-white border border-border-subtle rounded-[var(--radius-lg)] p-4 md:p-6">
      {isLoading ? (
        <div className="h-[400px] md:h-[520px] flex items-center justify-center">
          <LoadingSpinner label="Loading data..." />
        </div>
      ) : snapshots.length === 0 ? (
        <div className="h-[400px] md:h-[520px] flex items-center justify-center text-sm text-ink3">
          No data available
        </div>
      ) : (
        <CopScatterChart
          snapshots={snapshots}
          xKey="outdoorTemperatureCelsius"
          xLabel="Outside temperature"
          xUnit="°C"
          height={400}
        />
      )}
      <p className="text-[12px] md:text-[13px] text-ink2 mt-4 leading-relaxed max-w-3xl">
        Each point is one snapshot.{' '}
        <span className="text-success font-medium">Green</span> = good (&gt;3.2){' '}
        <span className="text-warning font-medium">amber</span> = OK (2.5-3.2){' '}
        <span className="text-danger font-medium">red</span> = low (&lt;2.5).
        A well-tuned ASHP shows a clear upward trend &mdash; warmer outdoor air lifts COP.
      </p>
    </div>
  )
}
