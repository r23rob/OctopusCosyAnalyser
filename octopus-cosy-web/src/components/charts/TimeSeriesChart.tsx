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
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.04)" />
        <XAxis
          dataKey="t"
          tick={{ fontSize: 7.5, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
          tickLine={false}
          axisLine={false}
          interval="preserveStartEnd"
        />
        <YAxis
          yAxisId="kwh"
          tick={{ fontSize: 7.5, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
          tickLine={false}
          axisLine={false}
        />
        <YAxis
          yAxisId="cop"
          orientation="right"
          domain={[0, 6]}
          tick={{ fontSize: 7.5, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
          tickLine={false}
          axisLine={false}
        />
        <Tooltip
          contentStyle={{ background: '#09090B', border: 'none', borderRadius: 10, fontSize: 10, fontFamily: 'JetBrains Mono, monospace', color: '#fff', padding: '10px 13px' }}
          formatter={(value: unknown) => fmtDec(typeof value === 'number' ? value : 0)}
        />
        <Legend wrapperStyle={{ fontSize: 9, fontFamily: 'JetBrains Mono, monospace', color: '#52525B' }} iconType="circle" iconSize={8} />
        {showOutput && (
          <Bar yAxisId="kwh" dataKey="output" name="Heat Out (kWh)" fill="#DC2626" opacity={0.8} />
        )}
        {showInput && (
          <Bar yAxisId="kwh" dataKey="input" name="Power In (kWh)" fill="#06B6D4" opacity={0.8} />
        )}
        {showCop && (
          <Line yAxisId="cop" type="monotone" dataKey="cop" name="COP" stroke="#16A34A" strokeWidth={2} dot={false} connectNulls />
        )}
        {showOutdoor && (
          <Line yAxisId="cop" type="monotone" dataKey="outdoor" name="Outdoor °C" stroke="#8B5CF6" strokeWidth={1.5} dot={false} strokeDasharray="4 2" connectNulls />
        )}
      </ComposedChart>
    </ResponsiveContainer>
  )
}
