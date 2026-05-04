import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'
import type { PeriodType } from '@/lib/utils'

/** Computes the prior window for a given period and fetches its summary.
 *  Used to drive the "vs last week" comparison on KPI cards. */
export function usePreviousPeriodSummary(
  deviceId: string | undefined,
  periodType: PeriodType,
  from: Date,
  to: Date,
) {
  const { prevFrom, prevTo } = useMemo(() => previousWindow(periodType, from, to), [periodType, from, to])

  const fromStr = prevFrom.toISOString()
  const toStr = prevTo.toISOString()

  const query = useQuery({
    queryKey: queryKeys.heatpump.periodSummary(deviceId ?? '', fromStr, toStr),
    queryFn: () => api.heatpump.getPeriodSummary(deviceId!, prevFrom, prevTo),
    enabled: !!deviceId,
    staleTime: 5 * 60_000,
  })

  return {
    previousPeriodSummary: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    prevFrom,
    prevTo,
  }
}

function previousWindow(periodType: PeriodType, from: Date, to: Date): { prevFrom: Date; prevTo: Date } {
  switch (periodType) {
    case 'day': {
      const prevFrom = new Date(from); prevFrom.setDate(prevFrom.getDate() - 1)
      const prevTo = new Date(to); prevTo.setDate(prevTo.getDate() - 1)
      return { prevFrom, prevTo }
    }
    case 'week': {
      const prevFrom = new Date(from); prevFrom.setDate(prevFrom.getDate() - 7)
      const prevTo = new Date(to); prevTo.setDate(prevTo.getDate() - 7)
      return { prevFrom, prevTo }
    }
    case 'month': {
      const prevFrom = new Date(from.getFullYear(), from.getMonth() - 1, 1)
      const prevTo = new Date(from.getFullYear(), from.getMonth(), 0, 23, 59, 59, 999)
      return { prevFrom, prevTo }
    }
    case 'year': {
      const year = from.getFullYear() - 1
      return {
        prevFrom: new Date(year, 0, 1),
        prevTo: new Date(year, 11, 31, 23, 59, 59, 999),
      }
    }
  }
}
