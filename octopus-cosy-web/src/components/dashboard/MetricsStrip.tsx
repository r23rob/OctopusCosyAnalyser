import { cn, copColorClass, fmtDec, fmtKwh, fmtTemp, fmtPercent } from '@/lib/utils'
import type { PeriodSummaryDto } from '@/types/api'

interface MetricCardProps {
  label: string
  value: string
  sub?: string
  valueClass?: string
}

function MetricCard({ label, value, sub, valueClass }: MetricCardProps) {
  return (
    <div className="flex flex-col gap-0.5 px-3 py-2 rounded-lg bg-white/[0.03] border border-white/[0.06] min-w-0">
      <span className="text-[10px] text-white/40 uppercase tracking-wide leading-none">{label}</span>
      <span className={cn('text-base font-bold leading-tight', valueClass ?? 'text-white')}>{value}</span>
      {sub && <span className="text-[10px] text-white/30 leading-none">{sub}</span>}
    </div>
  )
}

interface Props {
  periodSummary: PeriodSummaryDto | null | undefined
}

export function MetricsStrip({ periodSummary: p }: Props) {
  if (!p) {
    return (
      <div className="grid grid-cols-3 sm:grid-cols-6 gap-2">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="h-12 rounded-lg bg-white/[0.03] border border-white/[0.06] animate-pulse" />
        ))}
      </div>
    )
  }

  return (
    <div className="grid grid-cols-3 sm:grid-cols-6 gap-2">
      <MetricCard
        label="Avg COP"
        value={fmtDec(p.avgCop)}
        sub={`${fmtDec(p.minCop)}–${fmtDec(p.maxCop)}`}
        valueClass={copColorClass(p.avgCop)}
      />
      <MetricCard
        label="Heat Out"
        value={fmtKwh(p.totalOutputKwh)}
        sub="total"
      />
      <MetricCard
        label="Power In"
        value={fmtKwh(p.totalInputKwh)}
        sub="total"
      />
      <MetricCard
        label="Outdoor"
        value={fmtTemp(p.avgOutdoorTemp)}
        sub={`${fmtTemp(p.minOutdoorTemp)}–${fmtTemp(p.maxOutdoorTemp)}`}
        valueClass="text-amber-400"
      />
      <MetricCard
        label="Room"
        value={fmtTemp(p.avgRoomTemp)}
        sub={`${fmtTemp(p.minRoomTemp)}–${fmtTemp(p.maxRoomTemp)}`}
      />
      <MetricCard
        label="Heating Duty"
        value={fmtPercent(p.heatingDutyCyclePercent)}
      />
    </div>
  )
}
