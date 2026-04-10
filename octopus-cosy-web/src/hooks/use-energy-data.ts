import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'

/** Fetches 30-min energy intervals for the selected period. */
export function useEnergyIntervals(deviceId: string | undefined, from: Date, to: Date) {
  const fromStr = from.toISOString()
  const toStr = to.toISOString()

  return useQuery({
    queryKey: queryKeys.heatpump.energyIntervals(deviceId ?? '', fromStr, toStr),
    queryFn: () => api.heatpump.getEnergyIntervals(deviceId!, from, to),
    enabled: !!deviceId,
    staleTime: 5 * 60_000,
  })
}

/** Fetches aggregated energy summaries grouped by day/week/month. */
export function useEnergySummary(
  deviceId: string | undefined,
  from: Date,
  to: Date,
  grouping: 'day' | 'week' | 'month' = 'day',
) {
  const fromStr = from.toISOString()
  const toStr = to.toISOString()

  return useQuery({
    queryKey: queryKeys.heatpump.energySummary(deviceId ?? '', fromStr, toStr, grouping),
    queryFn: () => api.heatpump.getEnergySummary(deviceId!, from, to, grouping),
    enabled: !!deviceId,
    staleTime: 5 * 60_000,
  })
}
