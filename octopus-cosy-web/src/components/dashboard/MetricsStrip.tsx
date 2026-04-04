import { fmtDec } from '@/lib/utils'
import type { PeriodSummaryDto } from '@/types/api'

interface Props {
  periodSummary: PeriodSummaryDto | null | undefined
  previousPeriodSummary?: PeriodSummaryDto | null
  vsLabel?: string
}

export function MetricsStrip({ periodSummary: p, previousPeriodSummary: prev, vsLabel = '' }: Props) {
  if (!p) {
    return (
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-[72px] rounded-[10px] bg-white border border-border-subtle animate-pulse" />
        ))}
      </div>
    )
  }

  const days = p.snapshotCount > 0 ? Math.max(1, Math.round(p.snapshotCount / 96)) : 1
  const avgKwIn = p.totalInputKwh != null ? p.totalInputKwh / Math.max(days, 1) / 24 : null

  const kpis: KpiProps[] = [
    {
      label: 'Total kWh in',
      value: fmtDec(p.totalInputKwh, 1),
      unit: 'kWh',
      current: p.totalInputKwh,
      previous: prev?.totalInputKwh,
      higherIsBetter: false,
    },
    {
      label: 'Avg COP',
      value: fmtDec(p.avgCop, 2),
      unit: '',
      current: p.avgCop,
      previous: prev?.avgCop,
      higherIsBetter: true,
    },
    {
      label: 'Heat output',
      value: fmtDec(p.totalOutputKwh, 1),
      unit: 'kWh',
      current: p.totalOutputKwh,
      previous: prev?.totalOutputKwh,
      higherIsBetter: true,
    },
    {
      label: 'Avg outdoor',
      value: fmtDec(p.avgOutdoorTemp, 1),
      unit: '°C',
      current: p.avgOutdoorTemp,
      previous: prev?.avgOutdoorTemp,
      higherIsBetter: true,
    },
    {
      label: 'Avg kW in',
      value: fmtDec(avgKwIn, 2),
      unit: 'kW',
      current: avgKwIn,
      previous: prev?.totalInputKwh != null
        ? prev.totalInputKwh / Math.max(1, Math.round((prev.snapshotCount ?? 1) / 96)) / 24
        : undefined,
      higherIsBetter: false,
    },
  ]

  return (
    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-2 mb-3">
      {kpis.map((k, i) => (
        <KpiCard key={i} {...k} vsLabel={vsLabel} delay={i * 0.04} />
      ))}
    </div>
  )
}

interface KpiProps {
  label: string
  value: string
  unit: string
  current?: number | null
  previous?: number | null
  higherIsBetter: boolean
  vsLabel?: string
  delay?: number
}

function KpiCard({ label, value, unit, current, previous, higherIsBetter, vsLabel = '', delay = 0 }: KpiProps) {
  const delta = computeDelta(current, previous, higherIsBetter)

  return (
    <div
      className="bg-white border border-border-subtle rounded-[10px] px-[14px] pt-[15px] pb-3 hover:border-border-card transition-colors duration-150"
      style={{ animation: `slide-up 0.4s ease both`, animationDelay: `${delay}s` }}
    >
      <div className="font-mono text-[8px] tracking-[.1em] uppercase text-ink3 mb-[7px]">{label}</div>
      <div className="font-mono text-[20px] font-normal tracking-tight leading-none text-ink">
        {value}
        {unit && <span className="text-[11px] font-light text-ink3 ml-[1px]">{unit}</span>}
      </div>
      {delta && (
        <div
          className={`inline-flex items-center gap-0.5 mt-[5px] font-mono text-[8px] px-1.5 py-0.5 rounded ${
            delta.good
              ? 'bg-success-bg text-success'
              : 'bg-danger-bg text-danger'
          }`}
        >
          {delta.up ? '↑' : '↓'} {Math.abs(delta.pct).toFixed(0)}% {vsLabel}
        </div>
      )}
    </div>
  )
}

function computeDelta(
  current: number | null | undefined,
  previous: number | null | undefined,
  higherIsBetter: boolean,
) {
  if (current == null || previous == null || previous === 0) return null
  const diff = current - previous
  const pct = (diff / Math.abs(previous)) * 100
  const up = diff > 0
  const good = higherIsBetter ? up : !up
  return { pct, up, good }
}
