import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api-client';

export function useDevice() {
  const devicesQuery = useQuery({
    queryKey: ['devices'],
    queryFn: api.devices.getAll,
    staleTime: 5 * 60_000,
  });

  const settingsQuery = useQuery({
    queryKey: ['settings'],
    queryFn: api.settings.getAll,
    staleTime: 5 * 60_000,
  });

  const device = devicesQuery.data?.find(d => d.isActive) ?? devicesQuery.data?.[0] ?? null;
  const settings = settingsQuery.data?.[0] ?? null;

  return {
    device,
    settings,
    deviceId: device?.deviceId ?? null,
    accountNumber: device?.accountNumber ?? settings?.accountNumber ?? null,
    euid: device?.euid ?? null,
    isLoading: devicesQuery.isLoading || settingsQuery.isLoading,
    isError: devicesQuery.isError && settingsQuery.isError,
  };
}
