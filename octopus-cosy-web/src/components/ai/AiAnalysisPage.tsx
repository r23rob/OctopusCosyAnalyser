import { Link } from '@tanstack/react-router'
import { ArrowLeft, Zap, Thermometer, Flame, Droplets, Activity, Timer } from 'lucide-react'

import { useDevice } from '@/hooks/use-device'
import { usePeriodData } from '@/hooks/use-period-data'
import { useAiSummary } from '@/hooks/use-dashboard'
import { AiInsightsCard } from '@/components/ai/AiInsightsCard'
import { MarkdownRenderer } from '@/components/shared/MarkdownRenderer'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { fmtDec, fmtKwh, fmtTemp, fmtPercent } from '@/lib/utils'

export function AiAnalysisPage() {
  const { device, isLoading: deviceLoading, hasDevice } = useDevice()
  const deviceId = device?.deviceId

  // Last 7 days for performance summary
  const sevenDaysAgo = new Date()
  sevenDaysAgo.setDate(sevenDaysAgo.getDate() - 7)
  const now = new Date()

  const { periodSummary, isLoading: periodLoading } = usePeriodData(deviceId, sevenDaysAgo, now)
  const { summary: aiSummary } = useAiSummary(deviceId)

  if (deviceLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <LoadingSpinner size="lg" label="Loading..." />
      </div>
    )
  }

  if (!hasDevice) {
    return (
      <div className="max-w-md mx-auto mt-16 text-center flex flex-col items-center gap-4">
        <h2 className="text-lg font-semibold">No Device</h2>
        <p className="text-sm text-ink2">Set up a heat pump device to use AI analysis.</p>
        <Link to="/" className="px-4 py-2 rounded-lg bg-ink hover:bg-ink2 text-white text-sm font-medium transition-colors">
          Go Home
        </Link>
      </div>
    )
  }

  // Extract recommendations from the AI suggestions if available
  const recommendations = aiSummary?.suggestions ?? null

  return (
    <div className="max-w-screen-2xl mx-auto">
      {/* Back link */}
      <div className="mb-4">
        <Link to="/" className="inline-flex items-center gap-1.5 text-xs text-ink3 hover:text-ink2 transition-colors">
          <ArrowLeft size={14} />
          Back to Dashboard
        </Link>
      </div>

      {/* Page header */}
      <h1 className="text-xl font-semibold tracking-tight mb-1">AI Analysis</h1>
      <p className="text-sm text-ink2 mb-6">Claude-powered insights about your heat pump performance.</p>

      {/* Main content: two-column on large screens */}
      <div className="grid grid-cols-1 lg:grid-cols-[1fr_340px] gap-4">
        {/* Left column: AI Insights */}
        <div className="space-y-4">
          <AiInsightsCard deviceId={deviceId} />

          {/* Recommendations section */}
          {recommendations && (
            <div className="bg-bg-card border border-border-subtle rounded-[var(--radius-lg)] p-5 hover:border-border-card transition-colors duration-150">
              <div className="flex items-center gap-2 mb-3">
                <div className="w-6 h-6 rounded-full bg-purple/10 flex items-center justify-center">
                  <Activity size={13} className="text-purple" />
                </div>
                <h2 className="text-sm font-semibold tracking-tight">Recommendations</h2>
              </div>
              <MarkdownRenderer className="[&_li]:marker:text-purple">
                {recommendations}
              </MarkdownRenderer>
            </div>
          )}
        </div>

        {/* Right column: Performance Summary */}
        <div className="space-y-4">
          <div className="bg-bg-card border border-border-subtle rounded-[var(--radius-lg)] p-5 hover:border-border-card transition-colors duration-150">
            <div className="font-mono text-[11px] tracking-[.1em] uppercase text-ink3 mb-3">
              Last 7 days
            </div>

            {periodLoading ? (
              <div className="flex justify-center py-6">
                <LoadingSpinner size="sm" label="Loading metrics..." />
              </div>
            ) : periodSummary ? (
              <div className="space-y-3">
                <MetricRow
                  icon={<Zap size={14} className="text-primary" />}
                  label="Avg COP"
                  value={fmtDec(periodSummary.avgCop, 2)}
                  sublabel={periodSummary.minCop != null && periodSummary.maxCop != null
                    ? `${fmtDec(periodSummary.minCop, 1)} – ${fmtDec(periodSummary.maxCop, 1)} range`
                    : undefined}
                />
                <MetricRow
                  icon={<Thermometer size={14} className="text-danger" />}
                  label="Avg outdoor"
                  value={fmtTemp(periodSummary.avgOutdoorTemp)}
                  sublabel={periodSummary.minOutdoorTemp != null && periodSummary.maxOutdoorTemp != null
                    ? `${fmtTemp(periodSummary.minOutdoorTemp)} – ${fmtTemp(periodSummary.maxOutdoorTemp)}`
                    : undefined}
                />
                <MetricRow
                  icon={<Flame size={14} className="text-chart-1" />}
                  label="Heat output"
                  value={fmtKwh(periodSummary.totalOutputKwh)}
                />
                <MetricRow
                  icon={<Zap size={14} className="text-warning" />}
                  label="Electricity in"
                  value={fmtKwh(periodSummary.totalInputKwh)}
                />
                <MetricRow
                  icon={<Droplets size={14} className="text-info" />}
                  label="Avg flow temp"
                  value={fmtTemp(periodSummary.avgFlowTemp)}
                />
                <MetricRow
                  icon={<Thermometer size={14} className="text-success" />}
                  label="Avg room temp"
                  value={fmtTemp(periodSummary.avgRoomTemp)}
                  sublabel={periodSummary.avgRoomHumidity != null
                    ? `${fmtPercent(periodSummary.avgRoomHumidity)} humidity`
                    : undefined}
                />
                <MetricRow
                  icon={<Timer size={14} className="text-purple" />}
                  label="Heating duty"
                  value={fmtPercent(periodSummary.heatingDutyCyclePercent)}
                  sublabel={`Hot water: ${fmtPercent(periodSummary.hotWaterDutyCyclePercent)}`}
                />
                <div className="pt-2 border-t border-border-subtle">
                  <div className="font-mono text-[10px] text-ink3">
                    {periodSummary.snapshotCount} snapshots analysed
                  </div>
                </div>
              </div>
            ) : (
              <p className="text-xs text-ink3 py-4">No data available for this period.</p>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

interface MetricRowProps {
  icon: React.ReactNode
  label: string
  value: string
  sublabel?: string
}

function MetricRow({ icon, label, value, sublabel }: MetricRowProps) {
  return (
    <div className="flex items-start gap-2.5 py-1">
      <div className="mt-0.5 flex-shrink-0">{icon}</div>
      <div className="flex-1 min-w-0">
        <div className="font-mono text-[11px] tracking-[.05em] text-ink3">{label}</div>
        {sublabel && (
          <div className="font-mono text-[10px] text-ink4 mt-0.5">{sublabel}</div>
        )}
      </div>
      <div className="font-mono text-[13px] text-ink tabular-nums">{value}</div>
    </div>
  )
}
