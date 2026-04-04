import type { HeatPumpSnapshotDto } from '@/types/api'

interface Props {
  snapshots: HeatPumpSnapshotDto[]
}

export function EfficiencySplitCard({ snapshots }: Props) {
  const lo = snapshots.filter(s => (s.coefficientOfPerformance ?? 0) < 2.5).length
  const mid = snapshots.filter(s => {
    const c = s.coefficientOfPerformance ?? 0
    return c >= 2.5 && c < 3.2
  }).length
  const hi = snapshots.filter(s => (s.coefficientOfPerformance ?? 0) >= 3.2).length
  const tot = snapshots.length || 1

  const pHi = Math.round(hi / tot * 100)
  const pMid = Math.round(mid / tot * 100)
  const pLo = Math.round(lo / tot * 100)

  return (
    <div className="bg-white border border-border-subtle rounded-[10px] p-4 hover:border-border-card transition-colors duration-150">
      <div className="font-mono text-[10px] tracking-[.1em] uppercase text-ink3 mb-[5px]">Efficiency split</div>

      <div className="flex gap-1.5 mb-2">
        <span className="font-mono text-[10px] px-[8px] py-[4px] rounded bg-success-bg text-success">Good {pHi}%</span>
        <span className="font-mono text-[10px] px-[8px] py-[4px] rounded bg-warning-bg text-warning">OK {pMid}%</span>
        <span className="font-mono text-[10px] px-[8px] py-[4px] rounded bg-danger-bg text-danger">Low {pLo}%</span>
      </div>

      <div className="h-2 bg-bg-elevated rounded flex overflow-hidden gap-0.5 mb-2">
        {pHi > 0 && <div className="h-full rounded-l bg-success transition-all duration-400" style={{ width: `${pHi}%` }} />}
        {pMid > 0 && <div className="h-full bg-warning transition-all duration-400" style={{ width: `${pMid}%` }} />}
        {pLo > 0 && <div className="h-full rounded-r bg-danger transition-all duration-400" style={{ width: `${pLo}%` }} />}
      </div>

      <div className="font-mono text-[10px] text-ink3 leading-[1.6]">
        Of all operating hours, what share ran at good, acceptable, or poor COP. Green = above 3.2, amber = 2.5–3.2, red = below 2.5.
      </div>
    </div>
  )
}
