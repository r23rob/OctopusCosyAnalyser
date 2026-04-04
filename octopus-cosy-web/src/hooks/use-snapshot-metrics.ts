import { useMemo } from 'react'
import type { HeatPumpSnapshotDto, HeatPumpSummaryDto, PeriodSummaryDto } from '@/types/api'

export interface RoomTempSummary {
  name: string
  avg: number
  min: number
  max: number
  variance: number
}

function parseNumericValue(value: string | null | undefined): number | null {
  if (value == null) return null
  const n = parseFloat(value)
  return Number.isNaN(n) ? null : n
}

/** Derives room temperature stats and average power from snapshots + live summary. */
export function useSnapshotMetrics(
  snapshots: HeatPumpSnapshotDto[],
  periodSummary: PeriodSummaryDto | undefined,
  summary: HeatPumpSummaryDto | undefined,
) {
  const roomTemps = useMemo<RoomTempSummary[]>(() => {
    if (snapshots.length === 0) return []
    const temps = snapshots.map(s => s.roomTemperatureCelsius).filter((t): t is number => t != null)
    if (temps.length === 0) return []
    const sum = temps.reduce((a, b) => a + b, 0)
    const avg = +(sum / temps.length).toFixed(1)
    const min = +temps.reduce((a, b) => Math.min(a, b)).toFixed(1)
    const max = +temps.reduce((a, b) => Math.max(a, b)).toFixed(1)
    const variance = +((max - min) / 4).toFixed(1)
    return [{ name: 'Room', avg, min, max, variance }]
  }, [snapshots])

  const avgPowerIn = periodSummary?.totalInputKwh != null && periodSummary.snapshotCount > 0
    ? periodSummary.totalInputKwh / periodSummary.snapshotCount * 4
    : parseNumericValue(summary?.livePerformance?.powerInput?.value)

  const avgPowerOut = periodSummary?.totalOutputKwh != null && periodSummary.snapshotCount > 0
    ? periodSummary.totalOutputKwh / periodSummary.snapshotCount * 4
    : parseNumericValue(summary?.livePerformance?.heatOutput?.value)

  return { roomTemps, avgPowerIn, avgPowerOut }
}
