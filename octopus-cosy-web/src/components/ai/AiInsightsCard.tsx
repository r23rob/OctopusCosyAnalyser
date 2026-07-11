import { Sparkles, RefreshCw, AlertCircle } from 'lucide-react'
import { useAiSummary } from '@/hooks/use-dashboard'
import { MarkdownRenderer } from '@/components/shared/MarkdownRenderer'
import { cn } from '@/lib/utils'

interface Props {
  deviceId: string | undefined
  className?: string
}

export function AiInsightsCard({ deviceId, className }: Props) {
  const { summary, isLoading, isRefreshing, refresh } = useAiSummary(deviceId)

  const hasContent = summary && (summary.weekSummary || summary.monthSummary || summary.suggestions)

  return (
    <div className={cn('bg-bg-card border border-border-subtle rounded-[var(--radius-lg)] overflow-hidden', className)}>
      {/* Aurora gradient header */}
      <div className="bg-gradient-to-r from-aurora-deep to-aurora-mid px-5 py-3.5 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Sparkles size={16} className="text-white/90" />
          <h3 className="text-sm font-semibold text-white tracking-tight">AI Insights</h3>
        </div>
        <button
          onClick={() => refresh()}
          disabled={!deviceId || isRefreshing}
          className="flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-white/10 hover:bg-white/20 disabled:opacity-40 text-white/90 text-[11px] font-medium transition-colors"
          aria-label="Refresh analysis"
        >
          <RefreshCw size={12} className={cn(isRefreshing && 'animate-spin')} />
          Refresh
        </button>
      </div>

      {/* Card body */}
      <div className="p-5">
        {/* Loading state */}
        {(isLoading || isRefreshing) && (
          <div className="flex items-center gap-1.5 py-6 justify-center">
            <span className="text-xs text-ink3 mr-1">Analysing</span>
            <span className="ai-dot w-1.5 h-1.5 rounded-full bg-purple" />
            <span className="ai-dot w-1.5 h-1.5 rounded-full bg-purple" />
            <span className="ai-dot w-1.5 h-1.5 rounded-full bg-purple" />
          </div>
        )}

        {/* Error state */}
        {!isLoading && !isRefreshing && !hasContent && deviceId && (
          <div className="flex flex-col items-center gap-3 py-6 text-center">
            <AlertCircle size={20} className="text-ink3" />
            <p className="text-xs text-ink3 max-w-[280px]">
              AI analysis is not available right now. This may be because the Anthropic API key is not configured, or there is not enough data yet.
            </p>
            <button
              onClick={() => refresh()}
              className="px-3 py-1.5 rounded-md bg-purple/10 hover:bg-purple/20 text-purple text-xs font-medium transition-colors"
            >
              Try again
            </button>
          </div>
        )}

        {/* No device state */}
        {!deviceId && !isLoading && (
          <p className="text-xs text-ink3 py-4 text-center">
            Set up a device to see AI insights.
          </p>
        )}

        {/* Content */}
        {!isLoading && !isRefreshing && hasContent && (
          <div className="space-y-4">
            {summary.weekSummary && (
              <Section title="This Week">
                <MarkdownRenderer>{summary.weekSummary}</MarkdownRenderer>
              </Section>
            )}
            {summary.monthSummary && (
              <Section title="This Month">
                <MarkdownRenderer>{summary.monthSummary}</MarkdownRenderer>
              </Section>
            )}
            {summary.suggestions && (
              <Section title="Suggestions">
                <MarkdownRenderer>{summary.suggestions}</MarkdownRenderer>
              </Section>
            )}
            {summary.generatedAt && (
              <p className="text-[10px] text-ink3 text-right pt-1">
                Generated {new Date(summary.generatedAt).toLocaleString('en-GB', {
                  day: '2-digit',
                  month: 'short',
                  hour: '2-digit',
                  minute: '2-digit',
                })}
              </p>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h4 className="font-mono text-[11px] tracking-[.08em] uppercase text-ink3 mb-1.5">{title}</h4>
      {children}
    </div>
  )
}
