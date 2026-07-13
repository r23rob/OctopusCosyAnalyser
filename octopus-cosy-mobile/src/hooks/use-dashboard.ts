import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../lib/api-client';

export function useDashboard(deviceId: string | null) {
  const summary = useQuery({
    queryKey: ['summary', deviceId],
    queryFn: () => api.heatpump.getSummary(deviceId!),
    enabled: !!deviceId,
    refetchInterval: 60_000,
  });

  const latestSnapshot = useQuery({
    queryKey: ['latestSnapshot', deviceId],
    queryFn: () => api.heatpump.getLatestSnapshot(deviceId!),
    enabled: !!deviceId,
    refetchInterval: 60_000,
  });

  return {
    summary: summary.data,
    latestSnapshot: latestSnapshot.data,
    isLoading: summary.isLoading,
    isError: summary.isError,
    error: summary.error,
  };
}

export function useAiSummary(deviceId: string | null) {
  const qc = useQueryClient();

  const query = useQuery({
    queryKey: ['aiSummary', deviceId],
    queryFn: () => api.heatpump.getAiSummary(deviceId!),
    enabled: !!deviceId,
    staleTime: 10 * 60_000,
  });

  const refresh = useMutation({
    mutationFn: () => api.heatpump.refreshAiSummary(deviceId!),
    onSuccess: (data) => {
      qc.setQueryData(['aiSummary', deviceId], data);
    },
  });

  return { ...query, refresh };
}
