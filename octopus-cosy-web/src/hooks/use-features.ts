import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'

export function useFeatures() {
  const query = useQuery({
    queryKey: queryKeys.features.availability(),
    queryFn: () => api.features.getAvailability(),
    staleTime: 5 * 60_000, // check every 5 minutes
    retry: 1,
    // If fetch fails entirely (offline/away from home), assume lite mode
    placeholderData: { database: false, history: false, efficiency: false, liveData: false },
  })

  return {
    features: query.data ?? { database: false, history: false, efficiency: false, liveData: false },
    isLoading: query.isLoading,
    hasDatabase: query.data?.database ?? false,
    hasHistory: query.data?.history ?? false,
  }
}
