import { useMemo } from 'react'
import type { HeatPumpSnapshotDto } from '@/types/api'

export interface ScatterPoint {
  x: number
  cop: number
}

export interface AnalysisMetrics {
  copVsOutdoor: ScatterPoint[]
  copVsFlow: ScatterPoint[]
  roomTempSeries: { t: string; temp: number | null; humidity: number | null }[]
  heatingDutyCycle: number
  hotWaterDutyCycle: number
  avgCopHeating: number | null
  avgCopHotWater: number | null
}

/** Derives scatter plots and duty cycle breakdown from raw snapshots (replaces BuildInsightCharts). */
export function useAnalysisMetrics(snapshots: HeatPumpSnapshotDto[]): AnalysisMetrics {
  return useMemo(() => {
    if (snapshots.length === 0) {
      return {
        copVsOutdoor: [],
        copVsFlow: [],
        roomTempSeries: [],
        heatingDutyCycle: 0,
        hotWaterDutyCycle: 0,
        avgCopHeating: null,
        avgCopHotWater: null,
      }
    }

    const copVsOutdoor: ScatterPoint[] = []
    const copVsFlow: ScatterPoint[] = []
    const roomTempSeries: { t: string; temp: number | null; humidity: number | null }[] = []

    let heatingCount = 0
    let hotWaterCount = 0
    let copHeatSum = 0
    let copHeatCount = 0
    let copHwSum = 0
    let copHwCount = 0

    for (const s of snapshots) {
      const cop = s.coefficientOfPerformance
      const outdoor = s.outdoorTemperatureCelsius
      const flow = s.heatingFlowTemperatureCelsius

      if (cop != null && outdoor != null) {
        copVsOutdoor.push({ x: outdoor, cop })
      }
      if (cop != null && flow != null) {
        copVsFlow.push({ x: flow, cop })
      }

      roomTempSeries.push({
        t: s.snapshotTakenAt,
        temp: s.roomTemperatureCelsius ?? null,
        humidity: s.roomHumidityPercentage ?? null,
      })

      if (s.heatingZoneHeatDemand === true) {
        heatingCount++
        if (cop != null) { copHeatSum += cop; copHeatCount++ }
      }
      if (s.hotWaterZoneHeatDemand === true) {
        hotWaterCount++
        if (cop != null) { copHwSum += cop; copHwCount++ }
      }
    }

    const total = snapshots.length

    return {
      copVsOutdoor,
      copVsFlow,
      roomTempSeries,
      heatingDutyCycle: total > 0 ? (heatingCount / total) * 100 : 0,
      hotWaterDutyCycle: total > 0 ? (hotWaterCount / total) * 100 : 0,
      avgCopHeating: copHeatCount > 0 ? copHeatSum / copHeatCount : null,
      avgCopHotWater: copHwCount > 0 ? copHwSum / copHwCount : null,
    }
  }, [snapshots])
}
