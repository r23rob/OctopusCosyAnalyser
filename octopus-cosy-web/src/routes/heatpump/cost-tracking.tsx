import { createFileRoute } from '@tanstack/react-router'
import { PoundSterling } from 'lucide-react'

export const Route = createFileRoute('/heatpump/cost-tracking')({
  component: CostTrackingPage,
})

function CostTrackingPage() {
  return (
    <div className="max-w-md mt-16 flex flex-col items-center gap-4">
      <div className="w-12 h-12 rounded-full bg-amber-500/10 border border-amber-500/20 flex items-center justify-center">
        <PoundSterling size={20} className="text-amber-400" />
      </div>
      <h1 className="text-lg font-semibold text-white/90">Cost Tracking</h1>
      <span className="px-2.5 py-1 rounded-full text-xs font-medium border border-amber-500/30 bg-amber-500/10 text-amber-300">
        Coming Soon
      </span>
      <p className="text-sm text-white/40 text-center">
        Detailed daily cost tracking with tariff rates will be available here.
        Cost data is already being collected via the Costs tab on the Dashboard.
      </p>
    </div>
  )
}
