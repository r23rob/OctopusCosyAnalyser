import {
  CartesianGrid,
  ComposedChart,
  Legend,
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
}

const CustomTooltip = ({ active, payload, label }: { active?: boolean; payload?: { name: string; value: number; color: string }[]; label?: string }) => {
  if (!active || !payload?.length) return null
  return (
    <div className="rounded-lg border border-white/10 bg-[#1e2130] p-3 text-xs shadow-xl">
      <p className="mb-2 text-white/60 font-medium">{label}</p>
      {payload.map((p) => (
        <div key={p.name} className="flex items-center gap-2 mb-1">
          <span className="w-2 h-2 rounded-full" style={{ background: p.color }} />
          <span className="text-white/70">{p.name}:</span>
          <span className="text-white font-medium">{fmtDec(p.value)}</span>
        </div>
      ))}
    </div>
  )
}

export function TrendChart({ snapshots, series, dualAxis = false }: Props) {
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

  return (
    <ResponsiveContainer width="100%" height={240}>
      <ComposedChart data={data} margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
        <XAxis
          dataKey="t"
          tick={{ fontSize: 10, fill: 'rgba(255,255,255,0.38)' }}
          tickLine={false}
          axisLine={false}
          interval="preserveStartEnd"
        />
        <YAxis
          yAxisId="left"
          tick={{ fontSize: 10, fill: 'rgba(255,255,255,0.38)' }}
          tickLine={false}
          axisLine={false}
        />
        {dualAxis && (
          <YAxis
            yAxisId="right"
            orientation="right"
            tick={{ fontSize: 10, fill: 'rgba(255,255,255,0.38)' }}
            tickLine={false}
            axisLine={false}
          />
        )}
        <Tooltip content={<CustomTooltip />} />
        <Legend
          wrapperStyle={{ fontSize: 11, color: 'rgba(255,255,255,0.6)' }}
          iconType="circle"
          iconSize={8}
        />
        {series.map((sr) => (
          <Line
            key={String(sr.key)}
            yAxisId={sr.yAxisId ?? 'left'}
            type="monotone"
            dataKey={String(sr.key)}
            name={sr.label}
            stroke={sr.color}
            strokeWidth={2}
            dot={false}
            strokeDasharray={sr.strokeDasharray}
            connectNulls
          />
        ))}
      </ComposedChart>
    </ResponsiveContainer>
  )
}
