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
    <div className="max-w-screen-2xl mx-auto">
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-xl font-semibold tracking-tight">COP vs temperature</h1>
      </div>

      <div className="bg-white border border-border-subtle rounded-[10px] p-4 md:p-6">
        {isLoading ? (
          <div className="h-[400px] md:h-[520px] flex items-center justify-center">
            <LoadingSpinner label="Loading data…" />
          </div>
        ) : snapshots.length === 0 ? (
          <div className="h-[400px] md:h-[520px] flex items-center justify-center text-sm text-ink3">
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
        <p className="text-[12px] md:text-[13px] text-ink2 mt-4 leading-relaxed max-w-3xl">
          Each point is one snapshot. <span className="text-success font-medium">Cyan</span> = good (&gt;3.2) · <span className="text-warning font-medium">amber</span> = OK (2.5–3.2) · <span className="text-danger font-medium">red</span> = low (&lt;2.5). A well-tuned ASHP shows a clear upward trend — warmer outdoor air lifts COP.
        </p>
      </div>
    </div>
  )
}
