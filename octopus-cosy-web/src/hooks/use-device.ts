import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'

/** Returns the first active registered device + its account settings. */
export function useDevice() {
  const devicesQuery = useQuery({
    queryKey: queryKeys.devices.all(),
    queryFn: () => api.devices.getAll(),
    staleTime: 5 * 60_000,
  })

  const settingsQuery = useQuery({
    queryKey: queryKeys.settings.all(),
    queryFn: () => api.settings.getAll(),
    staleTime: 5 * 60_000,
  })

  const device = devicesQuery.data?.find((d) => d.isActive) ?? devicesQuery.data?.[0]
  const settings = settingsQuery.data?.[0]

  return {
    device,
    settings,
    isLoading: devicesQuery.isLoading || settingsQuery.isLoading,
    isError: devicesQuery.isError || settingsQuery.isError,
    hasDevice: !!device,
    hasSettings: !!settings,
  }
}
