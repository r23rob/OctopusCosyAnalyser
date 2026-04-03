import {
  CartesianGrid,
  ResponsiveContainer,
  Scatter,
  ScatterChart as ReScatterChart,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { HeatPumpSnapshotDto } from '@/types/api'
import { copColor, fmtDec } from '@/lib/utils'

interface Props {
  snapshots: HeatPumpSnapshotDto[]
  xKey: keyof HeatPumpSnapshotDto
  xLabel: string
  xUnit?: string
}

export function CopScatterChart({ snapshots, xKey, xLabel, xUnit = '' }: Props) {
  const data = snapshots
    .filter((s) => s.coefficientOfPerformance != null && s[xKey] != null)
    .map((s) => ({
      x: s[xKey] as number,
      cop: s.coefficientOfPerformance as number,
      fill: copColor(s.coefficientOfPerformance),
    }))

  return (
    <ResponsiveContainer width="100%" height={220}>
      <ReScatterChart margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
        <XAxis
          dataKey="x"
          name={xLabel}
          unit={xUnit}
          type="number"
          tick={{ fontSize: 10, fill: 'rgba(255,255,255,0.38)' }}
          tickLine={false}
          axisLine={false}
        />
        <YAxis
          dataKey="cop"
          name="COP"
          domain={[0, 6]}
          tick={{ fontSize: 10, fill: 'rgba(255,255,255,0.38)' }}
          tickLine={false}
          axisLine={false}
        />
        <Tooltip
          contentStyle={{ background: '#1e2130', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 8, fontSize: 11 }}
          labelStyle={{ color: 'rgba(255,255,255,0.6)' }}
          cursor={{ strokeDasharray: '3 3', stroke: 'rgba(255,255,255,0.2)' }}
          formatter={(value: unknown, name: unknown) => [fmtDec(typeof value === 'number' ? value : 0), String(name)] as [string, string]}
        />
        <Scatter data={data} fill="#22c55e" fillOpacity={0.7} />
      </ReScatterChart>
    </ResponsiveContainer>
  )
}
