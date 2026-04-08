import { createFileRoute } from '@tanstack/react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Settings } from 'lucide-react'
import { Link } from '@tanstack/react-router'

import { useDevice } from '@/hooks/use-device'
import { useDashboard } from '@/hooks/use-dashboard'
import { usePeriodData } from '@/hooks/use-period-data'
import { usePeriodNavigation } from '@/hooks/use-period-navigation'
import { useSnapshotMetrics } from '@/hooks/use-snapshot-metrics'

import { PeriodSelector } from '@/components/dashboard/PeriodSelector'
import { MetricsStrip } from '@/components/dashboard/MetricsStrip'
import { ChartSection } from '@/components/dashboard/ChartSection'
import { CopGaugeCard } from '@/components/dashboard/CopGaugeCard'
import { RoomTempsCard } from '@/components/dashboard/RoomTempsCard'
import { AvgPowerCard } from '@/components/dashboard/AvgPowerCard'
import { EfficiencySplitCard } from '@/components/dashboard/EfficiencySplitCard'
import { CopByTempCard } from '@/components/dashboard/CopByTempCard'

import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { ErrorAlert } from '@/components/shared/ErrorAlert'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'
import { downsample, periodSubtitle, vsLabel } from '@/lib/utils'

export const Route = createFileRoute('/heatpump/')({
  component: DashboardPage,
})

function DashboardPage() {
  const period = usePeriodNavigation()
  const { device, settings, isLoading: deviceLoading, hasDevice } = useDevice()
  const deviceId = device?.deviceId

  const { summary, isError: summaryError } = useDashboard(deviceId)
  const { snapshots, periodSummary, isLoading: periodLoading } = usePeriodData(deviceId, period.from, period.to)

  const displayed = downsample(snapshots, 500)
  const { roomTemps, avgPowerIn, avgPowerOut } = useSnapshotMetrics(snapshots, periodSummary, summary)

  const queryClient = useQueryClient()
  const setupMutation = useMutation({
    mutationFn: () => api.devices.setup(settings!.accountNumber),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.devices.all() }),
  })

  if (deviceLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <LoadingSpinner size="lg" label="Loading…" />
      </div>
    )
  }

  if (!settings) {
    return (
      <div className="max-w-md mx-auto mt-16 text-center flex flex-col items-center gap-4">
        <div className="w-12 h-12 rounded-full bg-primary/10 border border-primary/20 flex items-center justify-center">
          <Settings size={20} className="text-primary" />
        </div>
        <h2 className="text-lg font-semibold">Welcome to Ecodan Monitor</h2>
        <p className="text-sm text-ink2">Enter your Octopus Energy credentials to get started.</p>
        <Link
          to="/settings"
          className="px-4 py-2 rounded-lg bg-ink hover:bg-ink2 text-white text-sm font-medium transition-colors"
        >
          Go to Settings
        </Link>
      </div>
    )
  }

  if (!hasDevice) {
    return (
      <div className="max-w-md mx-auto mt-16 text-center flex flex-col items-center gap-4">
        <h2 className="text-lg font-semibold">No Device Registered</h2>
        <p className="text-sm text-ink2">Your credentials are saved. Now discover your heat pump device.</p>
        <button
          onClick={() => setupMutation.mutate()}
          disabled={setupMutation.isPending}
          className="px-4 py-2 rounded-lg bg-ink hover:bg-ink2 disabled:opacity-40 text-white text-sm font-medium transition-colors"
        >
          {setupMutation.isPending ? 'Setting up…' : 'Setup Device'}
        </button>
        {setupMutation.isError && (
          <ErrorAlert message="Setup failed. Check your account number in Settings." />
        )}
      </div>
    )
  }

  return (
    <div>
      <PeriodSelector
        periodType={period.periodType}
        onPeriodChange={period.setPeriodType}
        label={period.label}
        subtitle={periodSubtitle(period.periodType)}
        onPrev={period.prev}
        onNext={period.next}
        canGoNext={period.canGoNext}
      />

      {summaryError && (
        <ErrorAlert message="Could not load live data. The API may be unavailable." />
      )}

      <MetricsStrip
        periodSummary={periodSummary}
        vsLabel={vsLabel(period.periodType)}
      />

      <div className="grid grid-cols-1 lg:grid-cols-[1fr_260px] gap-3 mb-3 animate-up" style={{ animationDelay: '0.2s' }}>
        <ChartSection snapshots={displayed} isLoading={periodLoading} />

        <div className="flex flex-col gap-3">
          <CopGaugeCard
            cop={periodSummary?.avgCop ?? (summary?.livePerformance?.coefficientOfPerformance != null ? parseFloat(summary.livePerformance.coefficientOfPerformance) : null)}
            flowTemp={periodSummary?.avgFlowTemp ?? null}
            setpointTemp={null}
          />
          <RoomTempsCard rooms={roomTemps} />
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-3 animate-up" style={{ animationDelay: '0.28s' }}>
        <AvgPowerCard
          avgPowerIn={avgPowerIn}
          avgPowerOut={avgPowerOut}
          snapshots={snapshots}
        />
        <EfficiencySplitCard snapshots={snapshots} />
        <CopByTempCard snapshots={snapshots} />
      </div>
    </div>
  )
}
