import {
  Bar,
  CartesianGrid,
  ComposedChart,
  Legend,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { TimeSeriesChartPoint } from '@/types/api'
import { shortDate, fmtDec } from '@/lib/utils'

interface Props {
  points: TimeSeriesChartPoint[]
  showCop?: boolean
  showOutput?: boolean
  showInput?: boolean
  showOutdoor?: boolean
}

export function TimeSeriesChart({ points, showCop = true, showOutput = true, showInput = true, showOutdoor = false }: Props) {
  const data = points.map((p) => ({
    t: shortDate(p.endAt),
    cop: p.cop,
    output: p.energyOutputVal,
    input: p.energyInputVal,
    outdoor: p.outdoorTempVal,
  }))

  return (
    <ResponsiveContainer width="100%" height={260}>
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
          yAxisId="kwh"
          tick={{ fontSize: 10, fill: 'rgba(255,255,255,0.38)' }}
          tickLine={false}
          axisLine={false}
        />
        <YAxis
          yAxisId="cop"
          orientation="right"
          domain={[0, 6]}
          tick={{ fontSize: 10, fill: 'rgba(255,255,255,0.38)' }}
          tickLine={false}
          axisLine={false}
        />
        <Tooltip
          contentStyle={{ background: '#1e2130', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 8, fontSize: 11 }}
          labelStyle={{ color: 'rgba(255,255,255,0.6)' }}
          itemStyle={{ color: 'rgba(255,255,255,0.85)' }}
          formatter={(value: unknown) => fmtDec(typeof value === 'number' ? value : 0)}
        />
        <Legend wrapperStyle={{ fontSize: 11, color: 'rgba(255,255,255,0.6)' }} iconType="circle" iconSize={8} />
        {showOutput && (
          <Bar yAxisId="kwh" dataKey="output" name="Heat Out (kWh)" fill="#ef4444" opacity={0.8} />
        )}
        {showInput && (
          <Bar yAxisId="kwh" dataKey="input" name="Power In (kWh)" fill="#3b82f6" opacity={0.8} />
        )}
        {showCop && (
          <Line yAxisId="cop" type="monotone" dataKey="cop" name="COP" stroke="#22c55e" strokeWidth={2} dot={false} connectNulls />
        )}
        {showOutdoor && (
          <Line yAxisId="cop" type="monotone" dataKey="outdoor" name="Outdoor °C" stroke="#f59e0b" strokeWidth={1.5} dot={false} strokeDasharray="4 2" connectNulls />
        )}
      </ComposedChart>
    </ResponsiveContainer>
  )
}
