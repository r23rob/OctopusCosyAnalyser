import { fmtDec } from '@/lib/utils'
import type { PeriodSummaryDto } from '@/types/api'

interface Props {
  periodSummary: PeriodSummaryDto | null | undefined
  previousPeriodSummary?: PeriodSummaryDto | null
  vsLabel?: string
  /** When true, render a 2-up grid (used by the gauge-as-hero layout). */
  hero?: boolean
}

export function MetricsStrip({ periodSummary: p, previousPeriodSummary: prev, vsLabel = '', hero = false }: Props) {
  const gridClass = hero
    ? 'bg-gradient-to-br from-aurora-deep to-aurora-mid rounded-[var(--radius-xl)] p-5 grid grid-cols-1 sm:grid-cols-2 gap-3'
    : 'bg-gradient-to-br from-aurora-deep to-aurora-mid rounded-[var(--radius-xl)] p-5 grid grid-cols-2 lg:grid-cols-4 gap-3 mb-3'

  if (!p) {
    return (
      <div className={gridClass}>
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-[132px] rounded-[var(--radius-lg)] bg-white/[0.07] animate-pulse" />
        ))}
      </div>
    )
  }

  const kpis: KpiProps[] = [
    {
      label: 'Heat output',
      unit: 'kWh',
      current: p.totalOutputKwh,
      previous: prev?.totalOutputKwh,
      higherIsBetter: true,
    },
    {
      label: 'Energy used',
      unit: 'kWh',
      current: p.totalInputKwh,
      previous: prev?.totalInputKwh,
      higherIsBetter: false,
    },
    {
      label: 'Avg COP',
      unit: '',
      current: p.avgCop,
      previous: prev?.avgCop,
      higherIsBetter: true,
    },
    {
      label: 'Avg outdoor',
      unit: '°C',
      current: p.avgOutdoorTemp,
      previous: prev?.avgOutdoorTemp,
      higherIsBetter: true,
    },
  ]

  return (
    <div className={gridClass}>
      {kpis.map((k, i) => (
        <KpiCard key={k.label} {...k} vsLabel={vsLabel} delay={i * 0.04} />
      ))}
    </div>
  )
}

interface KpiProps {
  label: string
  unit: string
  current?: number | null
  previous?: number | null
  higherIsBetter: boolean
  vsLabel?: string
  delay?: number
}

function KpiCard({ label, unit, current, previous, higherIsBetter, vsLabel = '', delay = 0 }: KpiProps) {
  const value = formatValue(current, unit)
  const hasPrev = previous != null && Number.isFinite(previous) && current != null && Number.isFinite(current)
  const absDelta = hasPrev ? (current as number) - (previous as number) : null
  const pctDelta = hasPrev && previous !== 0 ? ((absDelta as number) / Math.abs(previous as number)) * 100 : null
  const isUp = absDelta != null && absDelta >= 0
  const isGood = absDelta != null && (higherIsBetter ? isUp : !isUp)

  return (
    <div
      className="animate-up bg-white/[0.07] backdrop-blur-sm rounded-[var(--radius-lg)] px-[18px] pt-[18px] pb-4 transition-colors duration-150 flex flex-col gap-2 min-h-[132px]"
      style={{ animationDelay: `${delay}s` }}
    >
      <div className="font-mono text-[12px] tracking-[.1em] uppercase text-white/40">{label}</div>
      <div className="font-mono text-[32px] font-normal tracking-tight leading-none text-white">
        {value}
        {unit && unit !== '°C' && <span className="text-[16px] font-light text-white/40 ml-[3px]">{unit}</span>}
        {unit === '°C' && <span className="text-[20px] font-light text-white/40">°C</span>}
      </div>

      {hasPrev ? (
        <div className="flex flex-col gap-[2px] mt-auto">
          <span
            className={`self-start font-mono text-[12px] px-2 py-[3px] rounded whitespace-nowrap ${
              isGood ? 'bg-[rgba(22,163,74,0.18)] text-[#4ADE80]' : 'bg-[rgba(220,38,38,0.18)] text-[#F87171]'
            }`}
          >
            {`${isUp ? '↑' : '↓'} ${formatAbsDelta(absDelta as number, unit)}${
              pctDelta != null ? ` (${Math.abs(pctDelta).toFixed(0)}%)` : ''
            }`}
          </span>
          <span className="font-mono text-white/30 text-[11px] tracking-[.02em]">
            was {formatPrevValue(previous as number, unit)} {vsLabel}
          </span>
        </div>
      ) : (
        <span className="font-mono text-white/30 text-[11px] mt-auto">No prior period</span>
      )}
    </div>
  )
}

function formatValue(n: number | null | undefined, unit: string): string {
  if (n == null) return '—'
  if (unit === '£') return `£${n.toFixed(2)}`
  if (unit === '') return n.toFixed(2)
  return fmtDec(n, 1)
}

function formatPrevValue(n: number, unit: string): string {
  if (unit === '£') return `£${n.toFixed(2)}`
  if (unit === '') return n.toFixed(2)
  if (unit === '°C') return `${n.toFixed(1)}°C`
  return `${fmtDec(n, 1)} ${unit}`
}

function formatAbsDelta(n: number, unit: string): string {
  const sign = n >= 0 ? '+' : '−'
  const abs = Math.abs(n)
  if (unit === '£') return `${sign}£${abs.toFixed(2)}`
  if (unit === '') return `${sign}${abs.toFixed(2)}`
  if (unit === '°C') return `${sign}${abs.toFixed(1)}°C`
  return `${sign}${fmtDec(abs, 1)} ${unit}`
}
