import { useMemo } from 'react'
import type { HeatPumpSnapshotDto } from '@/types/api'

interface Props {
  snapshots: HeatPumpSnapshotDto[]
}

const BUCKETS = [
  { label: 'Below 0°', min: -99, max: 0, exp: [2.0, 2.6] },
  { label: '0 – 5°', min: 0, max: 5, exp: [2.4, 3.0] },
  { label: '5 – 10°', min: 5, max: 10, exp: [2.9, 3.5] },
  { label: '10 – 15°', min: 10, max: 15, exp: [3.3, 4.0] },
  { label: 'Above 15°', min: 15, max: 99, exp: [3.8, 4.6] },
]

export function CopByTempCard({ snapshots }: Props) {
  const rows = useMemo(() => BUCKETS.map(b => {
    const pts = snapshots.filter(s => {
      const t = s.outdoorTemperatureCelsius ?? -999
      return t >= b.min && t < b.max
    })
    const actual = pts.length > 0
      ? +(pts.reduce((a, s) => a + (s.coefficientOfPerformance ?? 0), 0) / pts.length).toFixed(2)
      : null
    return { ...b, actual, count: pts.length }
  }).filter(r => r.count > 0), [snapshots])

  return (
    <div className="bg-white border border-border-subtle rounded-[10px] p-4 hover:border-border-card transition-colors duration-150">
      <div className="font-mono text-[8px] tracking-[.1em] uppercase text-ink3 mb-[5px]">COP by outdoor temp</div>

      <table className="w-full border-collapse mt-0.5">
        <thead>
          <tr>
            <th className="font-mono text-[7.5px] tracking-[.08em] uppercase text-ink3 py-[3px] border-b border-border-subtle font-normal text-left">Outdoor</th>
            <th className="font-mono text-[7.5px] tracking-[.08em] uppercase text-ink3 py-[3px] border-b border-border-subtle font-normal text-right">Actual</th>
            <th className="font-mono text-[7.5px] tracking-[.08em] uppercase text-ink3 py-[3px] border-b border-border-subtle font-normal text-right">Expected</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => {
            const pct = r.actual ? (r.actual / 5) * 100 : 0
            const inRange = r.actual != null && r.actual >= r.exp[0] && r.actual <= r.exp[1]
            const clr = r.actual == null
              ? '#A1A1AA'
              : inRange ? '#16A34A' : r.actual < r.exp[0] ? '#DC2626' : '#06B6D4'

            return (
              <tr key={r.label}>
                <td className="font-mono text-[8.5px] text-ink2 py-[5.5px] border-b border-border-subtle last:border-b-0">{r.label}</td>
                <td className="font-mono text-[9.5px] py-[5.5px] border-b border-border-subtle text-right last:border-b-0">
                  <div className="flex items-center gap-1 justify-end">
                    <div className="w-9 h-[3px] bg-bg-elevated rounded-sm relative flex-shrink-0">
                      <div className="h-full rounded-sm absolute left-0" style={{ width: `${pct}%`, background: clr }} />
                    </div>
                    <span style={{ color: clr, fontSize: '10px' }}>{r.actual ?? '—'}</span>
                  </div>
                </td>
                <td className="font-mono text-[8px] text-ink3 py-[5.5px] border-b border-border-subtle text-right last:border-b-0">
                  {r.exp[0]}–{r.exp[1]}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
