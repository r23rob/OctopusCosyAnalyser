import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api-client';
import type { FeatureAvailability } from '../types/api';

const LITE_MODE: FeatureAvailability = {
  database: false,
  history: false,
  efficiency: false,
  liveData: false,
};

export function useFeatures() {
  return useQuery({
    queryKey: ['features'],
    queryFn: api.features.getAvailability,
    placeholderData: LITE_MODE,
    staleTime: 5 * 60_000,
  });
}
