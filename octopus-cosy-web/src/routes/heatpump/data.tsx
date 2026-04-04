import { createFileRoute } from '@tanstack/react-router'
import { useState, useCallback, useMemo } from 'react'
import { useDevice } from '@/hooks/use-device'
import { useDailyAggregates, usePeriodData } from '@/hooks/use-period-data'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { periodStart, fmtDec } from '@/lib/utils'
import { copColor } from '@/lib/utils'

export const Route = createFileRoute('/heatpump/data')({
  component: DataPage,
})

type DataView = 'daily' | 'hourly'

function DataPage() {
  const [view, setView] = useState<DataView>('daily')
  const { device } = useDevice()
  const deviceId = device?.deviceId

  const from = periodStart(90)
  const to = new Date()

  const { aggregates, isLoading: aggLoading } = useDailyAggregates(deviceId, from, to)
  const { snapshots, isLoading: snapLoading } = usePeriodData(deviceId, from, to)

  const isLoading = view === 'daily' ? aggLoading : snapLoading

  const exportCSV = useCallback(() => {
    let csv: string
    if (view === 'daily') {
      csv = 'Date,COP,kWh In,kWh Out,Outdoor C,Flow C,Setpoint C,Heating Duty %,Cost p\n'
      aggregates.forEach(r => {
        csv += `${r.date},${fmtDec(r.avgCopHeating, 2)},${r.totalElectricityKwh.toFixed(1)},${r.totalHeatOutputKwh.toFixed(1)},${fmtDec(r.avgOutdoorTemp, 1)},${fmtDec(r.avgFlowTemp, 1)},${fmtDec(r.avgSetpoint, 1)},${r.heatingDutyCyclePercent.toFixed(0)},${fmtDec(r.dailyCostPence, 1)}\n`
      })
    } else {
      csv = 'Timestamp,COP,kW In,kW Out,Outdoor C,Flow C,Room C\n'
      snapshots.forEach(r => {
        csv += `${r.snapshotTakenAt},${fmtDec(r.coefficientOfPerformance, 2)},${fmtDec(r.powerInputKilowatt, 2)},${fmtDec(r.heatOutputKilowatt, 2)},${fmtDec(r.outdoorTemperatureCelsius, 1)},${fmtDec(r.heatingFlowTemperatureCelsius, 1)},${fmtDec(r.roomTemperatureCelsius, 1)}\n`
      })
    }
    const a = document.createElement('a')
    a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `heat-pump-${view}.csv`
    a.click()
  }, [view, aggregates, snapshots])

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-lg font-semibold tracking-tight">Data & export</h1>
        <div className="flex gap-[7px] items-center">
          <div className="flex bg-white border border-border-card rounded-[9px] p-[3px] gap-0.5">
            <button
              onClick={() => setView('daily')}
              className={`font-mono text-[9px] tracking-[.07em] uppercase px-[11px] py-[5px] rounded-[6px] border-none cursor-pointer transition-all duration-150 ${
                view === 'daily' ? 'bg-ink text-white' : 'bg-transparent text-ink3 hover:text-ink'
              }`}
            >
              Daily
            </button>
            <button
              onClick={() => setView('hourly')}
              className={`font-mono text-[9px] tracking-[.07em] uppercase px-[11px] py-[5px] rounded-[6px] border-none cursor-pointer transition-all duration-150 ${
                view === 'hourly' ? 'bg-ink text-white' : 'bg-transparent text-ink3 hover:text-ink'
              }`}
            >
              Snapshots
            </button>
          </div>
          <button
            onClick={exportCSV}
            className="font-mono text-[8.5px] tracking-[.07em] uppercase px-3 py-[6px] border border-border-card rounded-[6px] bg-white cursor-pointer text-ink2 hover:bg-bg-surface hover:text-ink transition-all duration-150"
          >
            Export CSV ↓
          </button>
        </div>
      </div>

      <div className="bg-white border border-border-subtle rounded-[10px] overflow-hidden">
        {isLoading ? (
          <div className="h-64 flex items-center justify-center">
            <LoadingSpinner label="Loading data…" />
          </div>
        ) : view === 'daily' ? (
          <DailyTable aggregates={aggregates} />
        ) : (
          <SnapshotTable snapshots={snapshots} />
        )}
      </div>
    </div>
  )
}

function DailyTable({ aggregates }: { aggregates: ReturnType<typeof useDailyAggregates>['aggregates'] }) {
  const reversed = useMemo(() => [...aggregates].reverse(), [aggregates])

  if (reversed.length === 0) return <div className="p-8 text-center text-xs text-ink3">No data available</div>

  return (
    <table className="w-full border-collapse">
      <thead>
        <tr>
          {['Date', 'COP', 'kWh in', 'kWh out', 'Outdoor °C', 'Flow °C', 'Setpoint °C', 'Duty %', 'Cost'].map(h => (
            <th key={h} className="text-left px-3 py-[9px] font-mono text-[8px] tracking-[.1em] uppercase text-ink3 border-b border-border-subtle font-normal">
              {h}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {reversed.map((r) => (
          <tr key={r.date} className="hover:bg-bg-base">
            <td className="px-3 py-[9px] border-b border-border-subtle text-[10.5px] text-ink2 font-light" style={{ fontFamily: 'Instrument Sans, sans-serif' }}>
              {new Date(r.date).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}
            </td>
            <td className="px-3 py-[9px] border-b border-border-subtle">
              <CopBadge value={r.avgCopHeating} />
            </td>
            <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[10.5px] text-ink font-light">{r.totalElectricityKwh.toFixed(1)}</td>
            <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[10.5px] text-ink font-light">{r.totalHeatOutputKwh.toFixed(1)}</td>
            <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[10.5px] text-ink font-light">{fmtDec(r.avgOutdoorTemp, 1)}</td>
            <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[10.5px] text-ink font-light">{fmtDec(r.avgFlowTemp, 1)}</td>
            <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[10.5px] text-ink font-light">{fmtDec(r.avgSetpoint, 1)}</td>
            <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[10.5px] text-ink font-light">{r.heatingDutyCyclePercent.toFixed(0)}</td>
            <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[10.5px] text-ink font-light">
              {r.dailyCostPence != null ? `${(r.dailyCostPence / 100).toFixed(2)}` : '—'}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function SnapshotTable({ snapshots }: { snapshots: ReturnType<typeof usePeriodData>['snapshots'] }) {
  const display = useMemo(() => snapshots.slice(-200).reverse(), [snapshots])
  if (display.length === 0) return <div className="p-8 text-center text-xs text-ink3">No data available</div>

  return (
    <table className="w-full border-collapse">
      <thead>
        <tr>
          {['Time', 'COP', 'kW in', 'kW out', 'Outdoor °C', 'Flow °C', 'Room °C'].map(h => (
            <th key={h} className="text-left px-3 py-[9px] font-mono text-[8px] tracking-[.1em] uppercase text-ink3 border-b border-border-subtle font-normal">
              {h}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {display.map((r) => (
          <tr key={r.snapshotTakenAt} className="hover:bg-bg-base">
            <td className="px-3 py-[9px] border-b border-border-subtle text-[10.5px] text-ink2 font-light" style={{ fontFamily: 'Instrument Sans, sans-serif' }}>
              {new Date(r.snapshotTakenAt).toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })}
            </td>
            <td className="px-3 py-[9px] border-b border-border-subtle">
              <CopBadge value={r.coefficientOfPerformance} />
            </td>
            <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[10.5px] text-ink font-light">{fmtDec(r.powerInputKilowatt, 2)}</td>
            <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[10.5px] text-ink font-light">{fmtDec(r.heatOutputKilowatt, 2)}</td>
            <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[10.5px] text-ink font-light">{fmtDec(r.outdoorTemperatureCelsius, 1)}</td>
            <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[10.5px] text-ink font-light">{fmtDec(r.heatingFlowTemperatureCelsius, 1)}</td>
            <td className="px-3 py-[9px] border-b border-border-subtle font-mono text-[10.5px] text-ink font-light">{fmtDec(r.roomTemperatureCelsius, 1)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function CopBadge({ value }: { value: number | null | undefined }) {
  if (value == null) return <span className="font-mono text-[10.5px] text-ink3">—</span>
  const color = copColor(value)
  const bgColor = value >= 3.2 ? 'rgba(22,163,74,0.08)' : value >= 2.5 ? 'rgba(217,119,6,0.08)' : 'rgba(220,38,38,0.08)'
  return (
    <span className="inline-block px-1.5 py-0.5 rounded font-mono text-[9px]" style={{ color, background: bgColor }}>
      {value.toFixed(2)}
    </span>
  )
}
