import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'

/** Live summary + latest snapshot — auto-refreshes every 60 seconds. */
export function useDashboard(deviceId: string | undefined) {
  const summaryQuery = useQuery({
    queryKey: queryKeys.heatpump.summary(deviceId ?? ''),
    queryFn: () => api.heatpump.getSummary(deviceId!),
    enabled: !!deviceId,
    refetchInterval: 60_000,
    refetchIntervalInBackground: false,
    staleTime: 55_000,
  })

  const latestQuery = useQuery({
    queryKey: queryKeys.heatpump.latestSnapshot(deviceId ?? ''),
    queryFn: () => api.heatpump.getLatestSnapshot(deviceId!),
    enabled: !!deviceId,
    refetchInterval: 60_000,
    refetchIntervalInBackground: false,
    staleTime: 55_000,
  })

  return {
    summary: summaryQuery.data,
    latest: latestQuery.data,
    isLoading: summaryQuery.isLoading || latestQuery.isLoading,
    isError: summaryQuery.isError || latestQuery.isError,
    error: summaryQuery.error ?? latestQuery.error,
    lastRefreshed: summaryQuery.dataUpdatedAt,
  }
}

/** AI summary with manual refresh capability. */
export function useAiSummary(deviceId: string | undefined) {
  const queryClient = useQueryClient()

  const summaryQuery = useQuery({
    queryKey: queryKeys.heatpump.aiSummary(deviceId ?? ''),
    queryFn: () => api.heatpump.getAiSummary(deviceId!),
    enabled: !!deviceId,
    staleTime: 60 * 60_000, // 1 hour
    retry: false,
  })

  const refreshMutation = useMutation({
    mutationFn: () => api.heatpump.refreshAiSummary(deviceId!),
    onSuccess: (data) => {
      queryClient.setQueryData(queryKeys.heatpump.aiSummary(deviceId ?? ''), data)
    },
  })

  return {
    summary: summaryQuery.data,
    isLoading: summaryQuery.isLoading,
    isRefreshing: refreshMutation.isPending,
    refresh: refreshMutation.mutate,
  }
}
