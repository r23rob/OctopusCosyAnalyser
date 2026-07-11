interface RoomData {
  name: string
  avg: number
  min: number
  max: number
  variance: number
}

interface Props {
  rooms: RoomData[]
}

export function RoomTempsCard({ rooms }: Props) {
  if (rooms.length === 0) {
    return (
      <div className="bg-bg-card border border-border-subtle rounded-[var(--radius-lg)] p-5 flex-1 hover:border-border-card transition-colors duration-150">
        <div className="font-mono text-[11px] tracking-[.1em] uppercase text-ink3 mb-2.5">Room temperatures</div>
        <div className="text-[14px] text-ink3">No sensor data</div>
      </div>
    )
  }

  const tMin = 14
  const span = 14

  return (
    <div className="bg-bg-card border border-border-subtle rounded-[var(--radius-lg)] p-5 flex-1 hover:border-border-card transition-colors duration-150">
      <div className="font-mono text-[11px] tracking-[.1em] uppercase text-ink3 mb-2.5">Room temperatures</div>
      {rooms.map((r) => {
        const pv = ((r.avg - tMin) / span * 100).toFixed(1)
        const pmin = ((r.min - tMin) / span * 100).toFixed(1)
        const pmax = ((r.max - tMin) / span * 100).toFixed(1)
        const pvlo = ((r.avg - r.variance - tMin) / span * 100).toFixed(1)
        const pvhi = ((r.avg + r.variance - tMin) / span * 100).toFixed(1)
        const clr = r.avg < 18 ? '#60A5FA' : r.avg > 23 ? '#F87171' : 'var(--purple)'

        return (
          <div key={r.name} className="flex items-start gap-2 mb-[11px] last:mb-0">
            <div className="font-mono text-[11px] tracking-[.07em] uppercase text-ink2 w-[54px] pt-[5px]">
              {r.name}
            </div>
            <div className="flex-1">
              <div className="h-1 bg-bg-elevated rounded-[3px] relative mb-[5px]">
                {/* Full range */}
                <div
                  className="absolute h-full rounded-[3px]"
                  style={{ left: `${pmin}%`, width: `${parseFloat(pmax) - parseFloat(pmin)}%`, background: 'rgba(124,58,237,0.07)' }}
                />
                {/* Variance range */}
                <div
                  className="absolute h-full rounded-[3px]"
                  style={{ left: `${pvlo}%`, width: `${parseFloat(pvhi) - parseFloat(pvlo)}%`, background: 'rgba(124,58,237,0.15)' }}
                />
                {/* Current dot */}
                <div
                  className="absolute -top-1 w-3 h-3 rounded-full -translate-x-1/2 border-[2.5px] border-white shadow-[0_1px_4px_rgba(0,0,0,0.15)]"
                  style={{ left: `${pv}%`, background: clr }}
                />
              </div>
              <div className="flex justify-between font-mono text-[11px] text-ink4">
                <span>{r.min}°</span>
                <span className="text-ink3">avg</span>
                <span>{r.max}°</span>
              </div>
            </div>
            <div className="font-mono text-[13px] w-[58px] text-right leading-[1.5]">
              <span className="block text-[15px] font-normal" style={{ color: clr }}>{r.avg}°</span>
              <span className="block text-ink3 text-[11px]">±{r.variance}°</span>
            </div>
          </div>
        )
      })}
    </div>
  )
}
