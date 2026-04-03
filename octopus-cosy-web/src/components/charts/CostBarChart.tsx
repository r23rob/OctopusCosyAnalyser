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
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
        <XAxis
          dataKey="t"
          tick={{ fontSize: 10, fill: 'rgba(255,255,255,0.38)' }}
          tickLine={false}
          axisLine={false}
          interval="preserveStartEnd"
        />
        <YAxis
          yAxisId="cost"
          tick={{ fontSize: 10, fill: 'rgba(255,255,255,0.38)' }}
          tickLine={false}
          axisLine={false}
          unit="£"
        />
        <YAxis
          yAxisId="kwh"
          orientation="right"
          tick={{ fontSize: 10, fill: 'rgba(255,255,255,0.38)' }}
          tickLine={false}
          axisLine={false}
          unit=" kWh"
        />
        <Tooltip
          contentStyle={{ background: '#1e2130', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 8, fontSize: 11 }}
          labelStyle={{ color: 'rgba(255,255,255,0.6)' }}
          formatter={(value: unknown, name: unknown) => {
            const v = typeof value === 'number' ? value : 0
            const n = String(name)
            if (n === 'Cost (£)') return [`£${fmtDec(v, 2)}`, n] as [string, string]
            return [`${fmtDec(v)} kWh`, n] as [string, string]
          }}
        />
        <Legend wrapperStyle={{ fontSize: 11, color: 'rgba(255,255,255,0.6)' }} iconType="rect" iconSize={8} />
        {showCost && (
          <Bar yAxisId="cost" dataKey="cost" name="Cost (£)" fill="#f97316" opacity={0.85} radius={[2, 2, 0, 0]} />
        )}
        {showUsage && (
          <Bar yAxisId="kwh" dataKey="usage" name="Usage (kWh)" fill="#3b82f6" opacity={0.7} radius={[2, 2, 0, 0]} />
        )}
      </BarChart>
    </ResponsiveContainer>
  )
}
