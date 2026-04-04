import { useQueries } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'

/** Fetches snapshots + period summary in parallel for the selected period. */
export function usePeriodData(deviceId: string | undefined, from: Date, to: Date) {
  const fromStr = from.toISOString()
  const toStr = to.toISOString()

  const results = useQueries({
    queries: [
      {
        queryKey: queryKeys.heatpump.snapshots(deviceId ?? '', fromStr, toStr),
        queryFn: () => api.heatpump.getSnapshots(deviceId!, from, to),
        enabled: !!deviceId,
        staleTime: 5 * 60_000,
      },
      {
        queryKey: queryKeys.heatpump.periodSummary(deviceId ?? '', fromStr, toStr),
        queryFn: () => api.heatpump.getPeriodSummary(deviceId!, from, to),
        enabled: !!deviceId,
        staleTime: 5 * 60_000,
      },
    ],
  })

  const [snapshotsQuery, periodQuery] = results

  return {
    snapshots: snapshotsQuery.data?.snapshots ?? [],
    periodSummary: periodQuery.data,
    isLoading: snapshotsQuery.isLoading || periodQuery.isLoading,
    isError: snapshotsQuery.isError || periodQuery.isError,
    from,
    to,
  }
}

/** Fetches daily aggregates for the selected period. */
export function useDailyAggregates(deviceId: string | undefined, from: Date, to: Date) {
  return useQueries({
    queries: [
      {
        queryKey: queryKeys.heatpump.dailyAggregates(deviceId ?? '', from.toISOString(), to.toISOString()),
        queryFn: () => api.heatpump.getDailyAggregates(deviceId!, from, to),
        enabled: !!deviceId,
        staleTime: 5 * 60_000,
      },
    ],
    combine: (results) => ({
      aggregates: results[0].data ?? [],
      isLoading: results[0].isLoading,
      isError: results[0].isError,
    }),
  })
}
