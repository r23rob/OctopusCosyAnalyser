import { useEffect, useMemo } from 'react'
import { useInfiniteQuery, useQueries, useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'

/** Fetches snapshots + period summary in parallel for the selected period.
 *  Snapshots use cursor-based pagination and auto-fetch all pages so charts
 *  receive the complete dataset. */
export function usePeriodData(deviceId: string | undefined, from: Date, to: Date) {
  const fromStr = from.toISOString()
  const toStr = to.toISOString()

  const snapshotsQuery = useInfiniteQuery({
    queryKey: queryKeys.heatpump.snapshots(deviceId ?? '', fromStr, toStr),
    queryFn: ({ pageParam }) =>
      api.heatpump.getSnapshots(deviceId!, from, to, pageParam),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.cursor ?? undefined,
    enabled: !!deviceId,
    staleTime: 5 * 60_000,
  })

  // Auto-fetch remaining pages so the full dataset is available for charts
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = snapshotsQuery
  useEffect(() => {
    if (hasNextPage && !isFetchingNextPage) {
      fetchNextPage()
    }
  }, [hasNextPage, isFetchingNextPage, fetchNextPage])

  const snapshots = useMemo(
    () => snapshotsQuery.data?.pages.flatMap((page) => page.snapshots) ?? [],
    [snapshotsQuery.data],
  )

  const periodQuery = useQuery({
    queryKey: queryKeys.heatpump.periodSummary(deviceId ?? '', fromStr, toStr),
    queryFn: () => api.heatpump.getPeriodSummary(deviceId!, from, to),
    enabled: !!deviceId,
    staleTime: 5 * 60_000,
  })

  return {
    snapshots,
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
