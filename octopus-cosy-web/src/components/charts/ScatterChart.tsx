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
  height?: number
}

export function CopScatterChart({ snapshots, xKey, xLabel, xUnit = '', height = 220 }: Props) {
  const data = snapshots
    .filter((s) => s.coefficientOfPerformance != null && s[xKey] != null)
    .map((s) => ({
      x: s[xKey] as number,
      cop: s.coefficientOfPerformance as number,
      fill: copColor(s.coefficientOfPerformance),
    }))

  return (
    <ResponsiveContainer width="100%" height={height}>
      <ReScatterChart margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.04)" />
        <XAxis
          dataKey="x"
          name={xLabel}
          unit={xUnit}
          type="number"
          tick={{ fontSize: 8.5, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
          tickLine={false}
          axisLine={false}
          label={{ value: `${xLabel} (${xUnit || '°C'})`, position: 'bottom', offset: -4, style: { fontSize: 8.5, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' } }}
        />
        <YAxis
          dataKey="cop"
          name="COP"
          domain={[0, 6]}
          tick={{ fontSize: 8.5, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
          tickLine={false}
          axisLine={false}
          label={{ value: 'COP', angle: -90, position: 'insideLeft', offset: 20, style: { fontSize: 8.5, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' } }}
        />
        <Tooltip
          contentStyle={{ background: '#09090B', border: 'none', borderRadius: 10, fontSize: 10, fontFamily: 'JetBrains Mono, monospace', color: '#fff', padding: '10px 13px' }}
          cursor={{ strokeDasharray: '3 3', stroke: 'rgba(0,0,0,0.15)' }}
          formatter={(value: unknown, name: unknown) => [fmtDec(typeof value === 'number' ? value : 0), String(name)] as [string, string]}
        />
        <Scatter data={data} fillOpacity={0.6} />
      </ReScatterChart>
    </ResponsiveContainer>
  )
}
