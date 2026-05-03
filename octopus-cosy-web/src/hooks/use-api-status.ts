import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'

/**
 * Polls /api/status to surface upstream API health (Octopus auth, Anthropic key, settings/device presence).
 * Long stale time + slow refetch — this is for displaying a banner, not driving live data.
 */
export function useApiStatus() {
  return useQuery({
    queryKey: queryKeys.status.all(),
    queryFn: () => api.status.get(),
    staleTime: 60_000,
    refetchInterval: 5 * 60_000,
    refetchOnWindowFocus: true,
    retry: 1,
  })
}
