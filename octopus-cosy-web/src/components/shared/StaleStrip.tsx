import { AlertCircle } from 'lucide-react'
import type { LatestSnapshotDto } from '@/types/api'

interface Props {
  latest: LatestSnapshotDto | null | undefined
}

/** Banner shown above the dashboard when the worker hasn't reported in
 *  20+ minutes. Returns null when the worker is healthy. */
export function StaleStrip({ latest }: Props) {
  const minutesAgo = latest?.minutesAgo
  if (minutesAgo == null || minutesAgo <= 20) return null

  const isOffline = minutesAgo > 60

  return (
    <div
      className={`rounded-[10px] px-4 py-2.5 mb-4 flex items-center gap-2.5 border ${
        isOffline
          ? 'bg-danger-bg text-danger border-[rgba(220,38,38,0.18)]'
          : 'bg-warning-bg text-warning border-[rgba(217,119,6,0.18)]'
      }`}
    >
      <AlertCircle size={15} />
      <span className="text-[14px] font-medium">
        {isOffline ? 'Offline' : `Last reading was ${Math.round(minutesAgo)} minutes ago`}
      </span>
      <span className="text-ink3 text-[13px]">
        {isOffline
          ? 'Showing the most recent values. Reconnect when the worker is back online.'
          : 'Worker is slow — values may be slightly out of date.'}
      </span>
    </div>
  )
}
