import { createFileRoute } from '@tanstack/react-router'
import { useDevice } from '@/hooks/use-device'
import { usePeriodData } from '@/hooks/use-period-data'
import { CopScatterChart } from '@/components/charts/ScatterChart'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { periodStart } from '@/lib/utils'

export const Route = createFileRoute('/heatpump/scatter')({
  component: ScatterPage,
})

function ScatterPage() {
  const { device } = useDevice()
  const from = periodStart(90)
  const to = new Date()
  const { snapshots, isLoading } = usePeriodData(device?.deviceId, from, to)

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-lg font-semibold tracking-tight">COP vs temperature</h1>
      </div>

      <div className="bg-white border border-border-subtle rounded-[10px] p-4">
        {isLoading ? (
          <div className="h-[400px] flex items-center justify-center">
            <LoadingSpinner label="Loading data…" />
          </div>
        ) : snapshots.length === 0 ? (
          <div className="h-[400px] flex items-center justify-center text-xs text-ink3">
            No data available
          </div>
        ) : (
          <CopScatterChart
            snapshots={snapshots}
            xKey="outdoorTemperatureCelsius"
            xLabel="Outside temperature"
            xUnit="°C"
            height={400}
          />
        )}
        <p className="font-mono text-[9px] text-ink3 mt-3 leading-[1.8]">
          Each point is one snapshot. Cyan = good (&gt;3.2) · amber = OK (2.5–3.2) · red = low (&lt;2.5). A well-tuned ASHP shows a clear upward trend — warmer outdoor air lifts COP.
        </p>
      </div>
    </div>
  )
}
