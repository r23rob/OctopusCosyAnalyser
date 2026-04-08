import {
  CartesianGrid,
  ComposedChart,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { HeatPumpSnapshotDto } from '@/types/api'
import { shortDate, fmtDec } from '@/lib/utils'

interface SeriesConfig {
  key: keyof HeatPumpSnapshotDto
  label: string
  color: string
  yAxisId?: string
  strokeDasharray?: string
}

interface Props {
  snapshots: HeatPumpSnapshotDto[]
  series: SeriesConfig[]
  dualAxis?: boolean
  height?: number
}

const CustomTooltip = ({ active, payload, label }: { active?: boolean; payload?: { name: string; value: number; color: string }[]; label?: string }) => {
  if (!active || !payload?.length) return null
  return (
    <div className="rounded-[10px] bg-ink text-white p-[12px_15px] text-sm shadow-[0_8px_28px_rgba(0,0,0,0.2)] min-w-[210px]">
      <p className="mb-1.5 font-mono text-[10px] tracking-[.09em] uppercase text-white/28">{label}</p>
      {payload.map((p) => (
        <div key={p.name} className="flex justify-between gap-3.5 py-[2px] items-baseline">
          <span className="text-white/45 text-[11px]" style={{ color: p.color }}>{p.name}</span>
          <span className="font-mono text-[13px] text-white">{fmtDec(p.value)}</span>
        </div>
      ))}
    </div>
  )
}

export function TrendChart({ snapshots, series, dualAxis = false, height = 228 }: Props) {
  const data = snapshots.map((s) => {
    const pt: Record<string, number | string> = {
      t: shortDate(s.snapshotTakenAt),
    }
    for (const sr of series) {
      const v = s[sr.key]
      pt[String(sr.key)] = typeof v === 'number' ? v : 0
    }
    return pt
  })

  // Group series by yAxisId
  const hasLeftAxis = series.some(s => (s.yAxisId ?? 'left') === 'left')
  const hasRightAxis = dualAxis || series.some(s => s.yAxisId === 'right')

  return (
    <ResponsiveContainer width="100%" height={height}>
      <ComposedChart data={data} margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.04)" />
        <XAxis
          dataKey="t"
          tick={{ fontSize: 10, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
          tickLine={false}
          axisLine={false}
          interval="preserveStartEnd"
        />
        {hasLeftAxis && (
          <YAxis
            yAxisId="left"
            tick={{ fontSize: 10, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
            tickLine={false}
            axisLine={false}
          />
        )}
        {hasRightAxis && (
          <YAxis
            yAxisId="right"
            orientation="right"
            tick={{ fontSize: 10, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
            tickLine={false}
            axisLine={false}
          />
        )}
        <Tooltip content={<CustomTooltip />} />
        {series.map((sr) => (
          <Line
            key={String(sr.key)}
            yAxisId={sr.yAxisId ?? 'left'}
            type="monotone"
            dataKey={String(sr.key)}
            name={sr.label}
            stroke={sr.color}
            strokeWidth={sr.strokeDasharray ? 1 : 1.5}
            dot={false}
            strokeDasharray={sr.strokeDasharray}
            connectNulls
          />
        ))}
      </ComposedChart>
    </ResponsiveContainer>
  )
}
