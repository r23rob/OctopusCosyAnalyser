import { useMemo } from 'react'
import { ResponsiveContainer, LineChart, Line } from 'recharts'
import { fmtDec } from '@/lib/utils'
import type { HeatPumpSnapshotDto } from '@/types/api'

interface Props {
  avgPowerIn: number | null | undefined
  avgPowerOut: number | null | undefined
  snapshots: HeatPumpSnapshotDto[]
}

export function AvgPowerCard({ avgPowerIn, avgPowerOut, snapshots }: Props) {
  const chartData = useMemo(() => snapshots.slice(-48).map(s => ({
    kwIn: s.powerInputKilowatt ?? 0,
    kwOut: s.heatOutputKilowatt ?? 0,
  })), [snapshots])

  return (
    <div className="bg-white border border-border-subtle rounded-[10px] p-4 hover:border-border-card transition-colors duration-150">
      <div className="font-mono text-[10px] tracking-[.1em] uppercase text-ink3 mb-[5px]">Avg power</div>
      <div className="flex items-baseline gap-2.5 mb-2">
        <div className="flex flex-col">
          <span className="font-mono text-[24px] font-normal tracking-tight leading-none">{fmtDec(avgPowerIn, 2)}</span>
          <span className="font-mono text-[10px] text-ink3 tracking-[.07em] uppercase mt-[3px]">kW in</span>
        </div>
        <span className="text-[16px] text-ink4 self-center mb-[7px]">→</span>
        <div className="flex flex-col">
          <span className="font-mono text-[24px] font-normal tracking-tight leading-none text-primary">{fmtDec(avgPowerOut, 2)}</span>
          <span className="font-mono text-[10px] text-ink3 tracking-[.07em] uppercase mt-[3px]">kW out</span>
        </div>
      </div>
      <div className="h-[100px] relative">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={chartData}>
            <Line type="monotone" dataKey="kwIn" stroke="#F97316" strokeWidth={1.5} dot={false} />
            <Line type="monotone" dataKey="kwOut" stroke="#06B6D4" strokeWidth={1.5} dot={false} />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}
