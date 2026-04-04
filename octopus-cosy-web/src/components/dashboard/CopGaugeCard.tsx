import { fmtDec, copColor, copLabel, copCls } from '@/lib/utils'

interface Props {
  cop: number | null | undefined
  flowTemp: number | null | undefined
  setpointTemp: number | null | undefined
}

export function CopGaugeCard({ cop, flowTemp, setpointTemp }: Props) {
  const clr = copColor(cop)
  const label = copLabel(cop)
  const cls = copCls(cop)

  const spDelta = flowTemp != null && setpointTemp != null ? +(flowTemp - setpointTemp).toFixed(1) : null
  const spColor = spDelta != null
    ? Math.abs(spDelta) > 4 ? '#F87171' : Math.abs(spDelta) > 2 ? '#FCD34D' : '#4ADE80'
    : 'rgba(255,255,255,0.3)'

  return (
    <div className="bg-ink rounded-[10px] p-4 flex flex-col items-center">
      <div className="font-mono text-[7.5px] tracking-[.1em] uppercase text-white/20 self-start mb-0.5">
        Avg COP
      </div>
      <GaugeSvg cop={cop ?? 0} color={clr} />
      <div className="font-mono text-[34px] font-normal leading-none tracking-tight" style={{ color: clr }}>
        {fmtDec(cop, 2)}
      </div>
      <div
        className={`font-mono text-[8px] tracking-[.08em] uppercase mt-1.5 px-[9px] py-[3px] rounded ${
          cls === 'g' ? 'bg-[rgba(22,163,74,0.18)] text-[#4ADE80]'
          : cls === 'w' ? 'bg-[rgba(217,119,6,0.18)] text-[#FCD34D]'
          : 'bg-[rgba(220,38,38,0.18)] text-[#F87171]'
        }`}
      >
        {label}
      </div>
      <div className="flex justify-between items-center self-stretch mt-2.5 px-2.5 py-[7px] bg-white/[0.06] rounded-[7px]">
        <span className="font-mono text-[8px] tracking-[.07em] uppercase text-white/30">Flow vs setpoint</span>
        <span className="font-mono text-[11px]" style={{ color: spColor }}>
          {spDelta != null ? `${spDelta > 0 ? '+' : ''}${spDelta}°` : '—'}
        </span>
      </div>
    </div>
  )
}

function GaugeSvg({ cop, color }: { cop: number; color: string }) {
  const pct = Math.min(Math.max((cop - 1) / 4, 0), 1)
  const r = 66, cx = 85, cy = 86
  const pa = (a: number): [number, number] => [cx + r * Math.cos(a), cy + r * Math.sin(a)]
  const sa = Math.PI, ea = 2 * Math.PI

  const [x1, y1] = pa(sa)
  const fa = sa + (ea - sa) * pct
  const [fx, fy] = pa(fa)

  // Three background arc segments
  const f1 = sa + (ea - sa) * 1.5 / 4
  const f2 = sa + (ea - sa) * 2.2 / 4
  const [b2x, b2y] = pa(f1)
  const [b3x, b3y] = pa(f2)
  const [b4x, b4y] = pa(ea)

  return (
    <svg width="170" height="94" viewBox="0 0 170 94" className="my-[-2px]">
      {/* Background arcs */}
      <path
        d={`M${x1},${y1} A${r},${r} 0 0,1 ${b2x.toFixed(1)},${b2y.toFixed(1)}`}
        stroke="rgba(248,113,113,0.13)" strokeWidth="9" fill="none"
      />
      <path
        d={`M${b2x.toFixed(1)},${b2y.toFixed(1)} A${r},${r} 0 0,1 ${b3x.toFixed(1)},${b3y.toFixed(1)}`}
        stroke="rgba(252,211,77,0.13)" strokeWidth="9" fill="none"
      />
      <path
        d={`M${b3x.toFixed(1)},${b3y.toFixed(1)} A${r},${r} 0 0,1 ${b4x.toFixed(1)},${b4y.toFixed(1)}`}
        stroke="rgba(6,182,212,0.13)" strokeWidth="9" fill="none"
      />
      {/* Active arc */}
      <path
        className="gauge-arc"
        d={`M${x1},${y1} A${r},${r} 0 0,1 ${fx.toFixed(1)},${fy.toFixed(1)}`}
        stroke={color} strokeWidth="9" fill="none" strokeLinecap="round"
      />
      {/* Scale labels */}
      <text x="11" y="85" fill="rgba(255,255,255,0.18)" fontSize="8.5" fontFamily="JetBrains Mono, monospace">1</text>
      <text x="154" y="85" fill="rgba(255,255,255,0.18)" fontSize="8.5" fontFamily="JetBrains Mono, monospace">5</text>
    </svg>
  )
}
