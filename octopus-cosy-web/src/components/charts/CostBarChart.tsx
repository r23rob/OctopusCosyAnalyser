import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { DailyAggregateDto } from '@/types/api'
import { shortDate, fmtDec } from '@/lib/utils'

interface Props {
  aggregates: DailyAggregateDto[]
  showCost?: boolean
  showUsage?: boolean
}

export function CostBarChart({ aggregates, showCost = true, showUsage = true }: Props) {
  const data = aggregates.map((a) => ({
    t: shortDate(new Date(a.date)),
    cost: a.dailyCostPence != null ? a.dailyCostPence / 100 : null,
    usage: a.dailyUsageKwh,
  }))

  return (
    <ResponsiveContainer width="100%" height={240}>
      <BarChart data={data} margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.04)" />
        <XAxis
          dataKey="t"
          tick={{ fontSize: 7.5, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
          tickLine={false}
          axisLine={false}
          interval="preserveStartEnd"
        />
        <YAxis
          yAxisId="cost"
          tick={{ fontSize: 7.5, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
          tickLine={false}
          axisLine={false}
          unit="£"
        />
        <YAxis
          yAxisId="kwh"
          orientation="right"
          tick={{ fontSize: 7.5, fill: '#A1A1AA', fontFamily: 'JetBrains Mono, monospace' }}
          tickLine={false}
          axisLine={false}
          unit=" kWh"
        />
        <Tooltip
          contentStyle={{ background: '#09090B', border: 'none', borderRadius: 10, fontSize: 10, fontFamily: 'JetBrains Mono, monospace', color: '#fff', padding: '10px 13px' }}
          formatter={(value: unknown, name: unknown) => {
            const v = typeof value === 'number' ? value : 0
            const n = String(name)
            if (n === 'Cost (£)') return [`£${fmtDec(v, 2)}`, n] as [string, string]
            return [`${fmtDec(v)} kWh`, n] as [string, string]
          }}
        />
        <Legend wrapperStyle={{ fontSize: 9, fontFamily: 'JetBrains Mono, monospace', color: '#52525B' }} iconType="rect" iconSize={8} />
        {showCost && (
          <Bar yAxisId="cost" dataKey="cost" name="Cost (£)" fill="#F97316" opacity={0.85} radius={[2, 2, 0, 0]} />
        )}
        {showUsage && (
          <Bar yAxisId="kwh" dataKey="usage" name="Usage (kWh)" fill="#06B6D4" opacity={0.7} radius={[2, 2, 0, 0]} />
        )}
      </BarChart>
    </ResponsiveContainer>
  )
}
