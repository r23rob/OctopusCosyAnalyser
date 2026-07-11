import { useEffect } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { AlertCircle, RefreshCw } from 'lucide-react'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'
import type { SetupResponseDto } from '@/types/api'

interface Props {
  accountNumber: string
  onNext: (device: SetupResponseDto) => void
}

export function DiscoverStep({ accountNumber, onNext }: Props) {
  const queryClient = useQueryClient()

  const setupMutation = useMutation({
    mutationFn: () => api.devices.setup(accountNumber),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.devices.all() })
      // Short delay so the user sees the success state before advancing
      setTimeout(() => onNext(data), 1200)
    },
  })

  // Auto-trigger discovery on mount
  useEffect(() => {
    setupMutation.mutate()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-bg-base px-6 text-center">
      <div className="flex flex-col items-center gap-6 max-w-sm w-full">
        {/* Pending state */}
        {setupMutation.isPending && (
          <>
            <Spinner />
            <div>
              <h2 className="text-xl font-semibold tracking-tight text-ink mb-1">
                Discovering your heat pump
              </h2>
              <p className="text-sm text-ink2">
                Connecting to Octopus Energy and finding your device...
              </p>
            </div>
          </>
        )}

        {/* Success state */}
        {setupMutation.isSuccess && (
          <>
            <div className="w-14 h-14 rounded-full bg-success/10 flex items-center justify-center">
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth={2.5}
                strokeLinecap="round"
                strokeLinejoin="round"
                className="w-7 h-7 text-success"
              >
                <polyline points="20 6 9 17 4 12" />
              </svg>
            </div>
            <div>
              <h2 className="text-xl font-semibold tracking-tight text-ink mb-1">
                Device found
              </h2>
              {setupMutation.data?.message && (
                <p className="text-sm text-ink2">{setupMutation.data.message}</p>
              )}
              {setupMutation.data?.deviceId && (
                <p className="mt-2 text-xs font-mono text-ink3">
                  {setupMutation.data.deviceId}
                </p>
              )}
            </div>
          </>
        )}

        {/* Error state */}
        {setupMutation.isError && (
          <>
            <div className="w-14 h-14 rounded-full bg-danger/10 flex items-center justify-center">
              <AlertCircle className="w-7 h-7 text-danger" />
            </div>
            <div>
              <h2 className="text-xl font-semibold tracking-tight text-ink mb-1">
                Discovery failed
              </h2>
              <p className="text-sm text-ink2">
                We could not find a heat pump on your account. Check your credentials and try again.
              </p>
            </div>
            <button
              type="button"
              onClick={() => setupMutation.mutate()}
              className="mt-2 flex items-center gap-2 px-6 py-3 min-h-[44px] rounded-lg bg-purple hover:bg-purple-deep text-white text-sm font-semibold transition-colors"
            >
              <RefreshCw size={16} />
              Retry
            </button>
          </>
        )}
      </div>
    </div>
  )
}

function Spinner() {
  return (
    <div className="w-14 h-14 rounded-full border-[3px] border-purple/20 border-t-purple animate-spin" />
  )
}
