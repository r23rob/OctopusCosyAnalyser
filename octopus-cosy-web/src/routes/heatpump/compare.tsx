import { useMemo, useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useDevice } from '@/hooks/use-device'
import { usePeriodData } from '@/hooks/use-period-data'
import { CopGaugeCard } from '@/components/dashboard/CopGaugeCard'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { downsample, periodLabel, type PeriodType } from '@/lib/utils'
import type { HeatPumpSnapshotDto, PeriodSummaryDto } from '@/types/api'

export const Route = createFileRoute('/heatpump/compare')({
  component: ComparePage,
})

const PERIOD_OPTIONS: PeriodType[] = ['day', 'week', 'month', 'year']

interface PeriodChoice {
  id: string
  label: string
  from: Date
  to: Date
}

function ComparePage() {
  const { device, hasDevice, isLoading: deviceLoading } = useDevice()
  const deviceId = device?.deviceId

  const [granA, setGranA] = useState<PeriodType>('week')
  const [granB, setGranB] = useState<PeriodType>('week')
  const [offsetA, setOffsetA] = useState(0)
  const [offsetB, setOffsetB] = useState(-1)

  const choicesA = useMemo(() => buildChoices(granA), [granA])
  const choicesB = useMemo(() => buildChoices(granB), [granB])

  const choiceA = choicesA.find((c) => c.id === offsetKey(granA, offsetA)) ?? choicesA[0]
  const choiceB = choicesB.find((c) => c.id === offsetKey(granB, offsetB)) ?? choicesB[0]

  const A = usePeriodData(deviceId, choiceA.from, choiceA.to)
  const B = usePeriodData(deviceId, choiceB.from, choiceB.to)

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
        <p className="text-sm text-ink2">Set up your heat pump to compare periods.</p>
      </div>
    )
  }

  const rows = buildRows(A.periodSummary, B.periodSummary, choiceA.from, choiceA.to, choiceB.from, choiceB.to)

  return (
    <div>
      <div className="flex items-center justify-between mb-6 animate-up gap-3">
        <div>
          <h1 className="text-[28px] font-semibold tracking-tight leading-tight">Compare</h1>
          <div className="font-mono text-[12px] text-ink3 tracking-[.05em] uppercase mt-[3px]">
            Two periods, side by side
          </div>
        </div>
      </div>

      {/* Period pickers */}
      <div className="grid gap-3 mb-3" style={{ gridTemplateColumns: '1fr auto 1fr' }}>
        <PeriodPicker
          letter="A"
          accent="var(--color-ink)"
          gran={granA}
          choices={choicesA}
          selectedId={choiceA.id}
          onGran={(g) => { setGranA(g); setOffsetA(0) }}
          onChoice={(id) => setOffsetA(parseOffsetKey(id).offset)}
        />
        <div className="font-mono text-ink3 uppercase self-center text-[11px] tracking-[.1em] px-1.5">vs</div>
        <PeriodPicker
          letter="B"
          accent="var(--color-primary)"
          gran={granB}
          choices={choicesB}
          selectedId={choiceB.id}
          onGran={(g) => { setGranB(g); setOffsetB(-1) }}
          onChoice={(id) => setOffsetB(parseOffsetKey(id).offset)}
        />
      </div>

      {/* Comparison header */}
      <div className="grid gap-3 mb-3" style={{ gridTemplateColumns: '1fr auto 1fr' }}>
        <ComparisonHeader letter="A" accent="var(--color-ink)" label={choiceA.label} />
        <div />
        <ComparisonHeader letter="B" accent="var(--color-primary)" label={choiceB.label} />
      </div>

      {/* Twin metric rows */}
      <div className="bg-white border border-border-subtle rounded-[10px] overflow-hidden mb-3">
        {rows.map((r, i) => (
          <CompareRow key={r.label} {...r} last={i === rows.length - 1} />
        ))}
      </div>

      {/* Twin gauges + overlaid chart */}
      <div className="grid gap-3 mb-3" style={{ gridTemplateColumns: 'minmax(0, 1fr) minmax(0, 2fr)' }}>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <CopGaugeCard cop={A.periodSummary?.avgCop ?? null} flowTemp={A.periodSummary?.avgFlowTemp ?? null} setpointTemp={null} />
          <CopGaugeCard cop={B.periodSummary?.avgCop ?? null} flowTemp={B.periodSummary?.avgFlowTemp ?? null} setpointTemp={null} />
        </div>
        <CompareTrend
          aLabel={choiceA.label}
          bLabel={choiceB.label}
          aSnapshots={A.snapshots}
          bSnapshots={B.snapshots}
          loading={A.isLoading || B.isLoading}
        />
      </div>
    </div>
  )
}

/* ─── helpers ───────────────────────────────────────────────────────── */

function offsetKey(gran: PeriodType, offset: number): string {
  return `${gran}:${offset}`
}

function parseOffsetKey(id: string): { gran: PeriodType; offset: number } {
  const [gran, offsetStr] = id.split(':')
  return { gran: gran as PeriodType, offset: parseInt(offsetStr, 10) }
}

function buildChoices(gran: PeriodType): PeriodChoice[] {
  const offsets = gran === 'year' ? [0, -1, -2] : [0, -1, -2, -3]
  return offsets.map((o) => {
    const { from, to } = windowFor(gran, o)
    return { id: offsetKey(gran, o), label: choiceLabel(gran, o, from, to), from, to }
  })
}

function windowFor(gran: PeriodType, offset: number): { from: Date; to: Date } {
  const now = new Date()
  switch (gran) {
    case 'day': {
      const d = new Date(now); d.setDate(d.getDate() + offset); d.setHours(0, 0, 0, 0)
      const end = new Date(d); end.setHours(23, 59, 59, 999)
      return { from: d, to: end }
    }
    case 'week': {
      const d = new Date(now)
      const day = d.getDay()
      const diff = day === 0 ? -6 : 1 - day
      d.setDate(d.getDate() + diff + offset * 7); d.setHours(0, 0, 0, 0)
      const end = new Date(d); end.setDate(end.getDate() + 6); end.setHours(23, 59, 59, 999)
      return { from: d, to: end }
    }
    case 'month': {
      const d = new Date(now.getFullYear(), now.getMonth() + offset, 1)
      const end = new Date(d.getFullYear(), d.getMonth() + 1, 0, 23, 59, 59, 999)
      return { from: d, to: end }
    }
    case 'year': {
      const year = now.getFullYear() + offset
      return { from: new Date(year, 0, 1), to: new Date(year, 11, 31, 23, 59, 59, 999) }
    }
  }
}

function choiceLabel(gran: PeriodType, offset: number, from: Date, to: Date): string {
  if (gran === 'day') {
    if (offset === 0) return 'Today'
    if (offset === -1) return 'Yesterday'
    return from.toLocaleDateString('en-GB', { weekday: 'short', day: '2-digit', month: 'short' })
  }
  if (gran === 'week') {
    if (offset === 0) return 'This week'
    if (offset === -1) return 'Last week'
    return periodLabel('week', from, to)
  }
  if (gran === 'month') {
    return from.toLocaleDateString('en-GB', { month: 'long', year: 'numeric' })
  }
  return from.getFullYear().toString()
}

interface Row {
  label: string
  unit: string
  a: number | null
  b: number | null
  higherIsBetter: boolean
}

function buildRows(
  A: PeriodSummaryDto | null | undefined,
  B: PeriodSummaryDto | null | undefined,
  aFrom: Date,
  aTo: Date,
  bFrom: Date,
  bTo: Date,
): Row[] {
  return [
    { label: 'Avg COP', unit: '', a: A?.avgCop ?? null, b: B?.avgCop ?? null, higherIsBetter: true },
    { label: 'Heat output', unit: 'kWh', a: A?.totalOutputKwh ?? null, b: B?.totalOutputKwh ?? null, higherIsBetter: true },
    { label: 'Energy used', unit: 'kWh', a: A?.totalInputKwh ?? null, b: B?.totalInputKwh ?? null, higherIsBetter: false },
    { label: 'Avg outdoor', unit: '°C', a: A?.avgOutdoorTemp ?? null, b: B?.avgOutdoorTemp ?? null, higherIsBetter: true },
    {
      label: 'Heating runtime',
      unit: 'h',
      a: estimateRuntime(A, aFrom, aTo),
      b: estimateRuntime(B, bFrom, bTo),
      higherIsBetter: true,
    },
  ]
}

function estimateRuntime(p: PeriodSummaryDto | null | undefined, from: Date, to: Date): number | null {
  if (!p) return null
  const totalHours = Math.max(0.0001, (to.getTime() - from.getTime()) / 3_600_000)
  return +(totalHours * (p.heatingDutyCyclePercent / 100)).toFixed(1)
}

/* ─── subcomponents ─────────────────────────────────────────────────── */

interface PickerProps {
  letter: 'A' | 'B'
  accent: string
  gran: PeriodType
  choices: PeriodChoice[]
  selectedId: string
  onGran: (g: PeriodType) => void
  onChoice: (id: string) => void
}

function PeriodPicker({ letter, accent, gran, choices, selectedId, onGran, onChoice }: PickerProps) {
  const isB = letter === 'B'
  return (
    <div
      className="bg-white border rounded-[10px] p-3.5"
      style={{ borderColor: isB ? 'rgba(6,182,212,.30)' : 'var(--color-border-card)' }}
    >
      <div className="flex items-center gap-2 mb-2.5">
        <span
          className="w-[22px] h-[22px] rounded-[5px] text-white inline-flex items-center justify-center font-mono uppercase text-[11px] font-semibold"
          style={{ background: accent }}
        >
          {letter}
        </span>
        <span className="font-mono text-ink3 uppercase text-[11px] tracking-[.07em]">Period {letter}</span>
      </div>
      <div className="flex gap-2 flex-wrap">
        <div className="flex bg-bg-surface border border-border-subtle rounded-[8px] p-[3px] gap-[2px]">
          {PERIOD_OPTIONS.map((g) => (
            <button
              key={g}
              onClick={() => onGran(g)}
              className={`font-mono uppercase text-[11px] tracking-[.07em] px-2.5 py-1.5 rounded-[5px] cursor-pointer font-medium transition-colors ${
                gran === g ? 'bg-white text-ink' : 'bg-transparent text-ink3 hover:text-ink'
              }`}
            >
              {g}
            </button>
          ))}
        </div>
        <select
          value={selectedId}
          onChange={(e) => onChoice(e.target.value)}
          className="flex-1 min-w-[140px] px-3 py-1.5 rounded-[8px] border border-border-subtle bg-white text-[14px] text-ink cursor-pointer"
        >
          {choices.map((c) => (
            <option key={c.id} value={c.id}>{c.label}</option>
          ))}
        </select>
      </div>
    </div>
  )
}

function ComparisonHeader({ letter, accent, label }: { letter: 'A' | 'B'; accent: string; label: string }) {
  return (
    <div className="flex items-center gap-2.5 px-4 py-3 bg-white border border-border-subtle rounded-[10px]">
      <span
        className="w-5 h-5 rounded-[5px] text-white inline-flex items-center justify-center font-mono text-[10px] font-semibold"
        style={{ background: accent }}
      >
        {letter}
      </span>
      <span className="text-[15px] font-medium">{label}</span>
    </div>
  )
}

interface CompareRowProps extends Row { last: boolean }

function CompareRow({ label, a, b, unit, higherIsBetter, last }: CompareRowProps) {
  const both = a != null && b != null
  const absDelta = both ? (b as number) - (a as number) : null
  const pctDelta = both && a !== 0 ? ((absDelta as number) / Math.abs(a as number)) * 100 : null
  const isUp = absDelta != null && absDelta >= 0
  const isGood = absDelta != null && (higherIsBetter ? isUp : !isUp)

  return (
    <div
      className="grid items-center"
      style={{ gridTemplateColumns: '1fr auto 1fr', borderBottom: last ? 'none' : '1px solid var(--border-subtle)' }}
    >
      {/* A side */}
      <div className="flex items-baseline gap-3 px-5 py-4 justify-end">
        <span className="text-ink3 text-[13px]">{label}</span>
        <span className="font-mono text-[24px] text-ink min-w-[110px] text-right whitespace-nowrap">{fmt(a, unit)}</span>
      </div>
      {/* Center delta */}
      <div className="flex flex-col items-center gap-1 px-4 py-3 min-w-[150px]">
        <span className="font-mono uppercase text-ink3 text-[10px] tracking-[.07em]">B − A</span>
        {absDelta != null ? (
          <>
            <span
              className={`font-mono text-[13px] font-medium px-2.5 py-1 rounded-full whitespace-nowrap ${
                isGood ? 'bg-success-bg text-success' : 'bg-danger-bg text-danger'
              }`}
            >
              {`${isUp ? '↑' : '↓'} ${fmtAbs(absDelta, unit)}`}
            </span>
            {pctDelta != null && <span className="font-mono text-ink3 text-[11px]">{`${Math.abs(pctDelta).toFixed(0)}%`}</span>}
          </>
        ) : (
          <span className="font-mono text-ink3 text-[13px]">—</span>
        )}
      </div>
      {/* B side */}
      <div className="flex items-baseline gap-3 px-5 py-4">
        <span className="font-mono text-[24px] text-primary min-w-[110px] whitespace-nowrap">{fmt(b, unit)}</span>
        <span className="text-ink3 text-[13px]">{label}</span>
      </div>
    </div>
  )
}

function fmt(n: number | null, unit: string): string {
  if (n == null) return '—'
  if (unit === '£') return `£${n.toFixed(2)}`
  if (unit === '') return n.toFixed(2)
  if (unit === '°C') return `${n.toFixed(1)}°C`
  return `${n.toFixed(1)} ${unit}`
}

function fmtAbs(n: number, unit: string): string {
  const sign = n >= 0 ? '+' : '−'
  const abs = Math.abs(n)
  if (unit === '£') return `${sign}£${abs.toFixed(2)}`
  if (unit === '') return `${sign}${abs.toFixed(2)}`
  if (unit === '°C') return `${sign}${abs.toFixed(1)}°C`
  return `${sign}${abs.toFixed(1)} ${unit}`
}

/* ─── overlaid trend chart with crosshair ────────────────────────────── */

interface TrendProps {
  aLabel: string
  bLabel: string
  aSnapshots: HeatPumpSnapshotDto[]
  bSnapshots: HeatPumpSnapshotDto[]
  loading: boolean
}

function CompareTrend({ aLabel, bLabel, aSnapshots, bSnapshots, loading }: TrendProps) {
  const [hover, setHover] = useState<number | null>(null)

  const data = useMemo(() => buildTrendData(aSnapshots, bSnapshots), [aSnapshots, bSnapshots])

  const w = 800, h = 280, padL = 36, padR = 14, padT = 14, padB = 30
  const innerW = w - padL - padR, innerH = h - padT - padB

  const allVals = data.flatMap((d) => [d.a, d.b].filter((x): x is number => x != null))
  const yMin = allVals.length ? Math.floor(Math.min(...allVals) - 1) : 0
  const yMax = allVals.length ? Math.ceil(Math.max(...allVals) + 1) : 1
  const xs = (i: number) => padL + (i / Math.max(1, data.length - 1)) * innerW
  const ys = (v: number) => padT + innerH - ((v - yMin) / Math.max(1, yMax - yMin)) * innerH
  const buildPath = (key: 'a' | 'b'): string =>
    data
      .filter((d) => d[key] != null)
      .map((d, i) => `${i === 0 ? 'M' : 'L'}${xs(data.indexOf(d)).toFixed(1)},${ys(d[key]!).toFixed(1)}`)
      .join(' ')

  const onMove = (e: React.MouseEvent<SVGSVGElement>) => {
    const rect = e.currentTarget.getBoundingClientRect()
    const x = ((e.clientX - rect.left) / rect.width) * w
    if (x < padL || x > w - padR) { setHover(null); return }
    const i = Math.round(((x - padL) / innerW) * (data.length - 1))
    setHover(Math.max(0, Math.min(data.length - 1, i)))
  }

  const yTicks = [yMin, Math.round((yMin + yMax) / 2), yMax]

  return (
    <div className="bg-white border border-border-subtle rounded-[10px] p-5 hover:border-border-card transition-colors duration-150 flex flex-col">
      <div className="flex items-center justify-between mb-3">
        <div>
          <div className="text-[15px] font-medium">Flow temp · overlaid</div>
          <div className="text-ink3 text-[13px] mt-[2px]">A solid · B dashed · same time-of-day axis</div>
        </div>
        <div className="flex gap-3">
          <span className="font-mono text-ink2 text-[12px] inline-flex items-center gap-1.5">
            <span className="w-3.5 h-[2px] bg-ink inline-block" />
            {aLabel}
          </span>
          <span className="font-mono text-ink2 text-[12px] inline-flex items-center gap-1.5">
            <span className="w-3.5 h-[2px] inline-block" style={{ backgroundImage: 'linear-gradient(90deg, var(--color-primary) 50%, transparent 50%)', backgroundSize: '4px 2px' }} />
            {bLabel}
          </span>
        </div>
      </div>

      {loading ? (
        <div className="flex items-center justify-center h-[280px] text-ink3"><LoadingSpinner size="md" label="Loading…" /></div>
      ) : data.length === 0 ? (
        <div className="flex items-center justify-center h-[280px] text-ink3 text-[13px]">No flow temperature data for this range.</div>
      ) : (
        <svg
          width="100%"
          viewBox={`0 0 ${w} ${h}`}
          preserveAspectRatio="none"
          className="block"
          style={{ cursor: hover != null ? 'crosshair' : 'default' }}
          onMouseMove={onMove}
          onMouseLeave={() => setHover(null)}
        >
          {yTicks.map((t) => (
            <g key={t}>
              <line x1={padL} x2={w - padR} y1={ys(t)} y2={ys(t)} stroke="var(--border-subtle)" strokeWidth="1" />
              <text x={padL - 8} y={ys(t) + 3} fontSize="11" fill="var(--text-muted)" fontFamily="JetBrains Mono, monospace" textAnchor="end">{t}°</text>
            </g>
          ))}
          {data.map((d, i) => i % 12 === 0 && (
            <text key={i} x={xs(i)} y={h - 10} fontSize="11" fill="var(--text-muted)" fontFamily="JetBrains Mono, monospace" textAnchor="middle">{d.t}</text>
          ))}
          <path d={buildPath('a')} stroke="var(--text-primary)" strokeWidth="1.6" fill="none" strokeLinejoin="round" strokeLinecap="round" />
          <path d={buildPath('b')} stroke="var(--color-primary)" strokeWidth="1.6" fill="none" strokeLinejoin="round" strokeLinecap="round" strokeDasharray="4 3" />
          {hover != null && (
            <g>
              <line x1={xs(hover)} x2={xs(hover)} y1={padT} y2={h - padB} stroke="var(--text-primary)" strokeWidth=".75" strokeDasharray="2 2" opacity=".4" />
              {data[hover].a != null && (
                <circle cx={xs(hover)} cy={ys(data[hover].a!)} r="3.5" fill="#fff" stroke="var(--text-primary)" strokeWidth="1.6" />
              )}
              {data[hover].b != null && (
                <circle cx={xs(hover)} cy={ys(data[hover].b!)} r="3.5" fill="#fff" stroke="var(--color-primary)" strokeWidth="1.6" />
              )}
            </g>
          )}
        </svg>
      )}

      <div className="flex items-center border-t border-border-subtle mt-2 pt-3 gap-6 text-[13px]">
        <span className="font-mono text-ink3 uppercase text-[11px] tracking-[.07em]">{hover != null ? data[hover]?.t ?? '—' : '—'}</span>
        <span className="flex items-center gap-1.5">
          <span className="w-2 h-[3px] rounded-[1px] bg-ink" />
          <span className="text-ink3">A</span>
          <span className="font-mono font-medium">{hover != null && data[hover]?.a != null ? `${data[hover].a!.toFixed(1)}°` : '—'}</span>
        </span>
        <span className="flex items-center gap-1.5">
          <span className="w-2 h-[3px] rounded-[1px] bg-primary" />
          <span className="text-ink3">B</span>
          <span className="font-mono font-medium">{hover != null && data[hover]?.b != null ? `${data[hover].b!.toFixed(1)}°` : '—'}</span>
        </span>
        <span className="flex items-center gap-1.5 ml-auto">
          <span className="text-ink3">Δ</span>
          <span className="font-mono font-medium">
            {hover != null && data[hover]?.a != null && data[hover]?.b != null
              ? `${data[hover].b! - data[hover].a! >= 0 ? '+' : '−'}${Math.abs(data[hover].b! - data[hover].a!).toFixed(1)}°`
              : '—'}
          </span>
        </span>
      </div>
    </div>
  )
}

interface TrendPoint { t: string; a: number | null; b: number | null }

function buildTrendData(aSnaps: HeatPumpSnapshotDto[], bSnaps: HeatPumpSnapshotDto[]): TrendPoint[] {
  const aDown = downsample(aSnaps, 60)
  const bDown = downsample(bSnaps, 60)
  const len = Math.max(aDown.length, bDown.length)
  if (len === 0) return []

  const points: TrendPoint[] = []
  for (let i = 0; i < len; i++) {
    const a = aDown[Math.min(i, aDown.length - 1)]
    const b = bDown[Math.min(i, bDown.length - 1)]
    const ts = a?.snapshotTakenAt ?? b?.snapshotTakenAt
    const date = ts ? new Date(ts) : null
    points.push({
      t: date ? `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}` : '',
      a: i < aDown.length ? aDown[i]?.heatingFlowTemperatureCelsius ?? null : null,
      b: i < bDown.length ? bDown[i]?.heatingFlowTemperatureCelsius ?? null : null,
    })
  }
  return points
}
