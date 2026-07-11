import { useFeatures } from '@/hooks/use-features'
import type { FeatureAvailability } from '@/types/api'

interface FeatureGateProps {
  requires: keyof FeatureAvailability
  children: React.ReactNode
  fallback?: React.ReactNode
}

export function FeatureGate({ requires, children, fallback = null }: FeatureGateProps) {
  const { features } = useFeatures()
  return features[requires] ? <>{children}</> : <>{fallback}</>
}
