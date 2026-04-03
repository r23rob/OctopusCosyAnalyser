import { useState } from 'react'
import { ChevronDown, ChevronUp, RefreshCw } from 'lucide-react'
import type { AiSummaryDto } from '@/types/api'
import { MarkdownRenderer } from '@/components/shared/MarkdownRenderer'
import { formatDateTime } from '@/lib/utils'

interface Props {
  summary: AiSummaryDto | null | undefined
  isLoading?: boolean
  onRefresh?: () => void
  isRefreshing?: boolean
}

export function AiSummaryPanel({ summary, isLoading, onRefresh, isRefreshing }: Props) {
  const [expanded, setExpanded] = useState(false)
  const [activeSection, setActiveSection] = useState<'week' | 'month' | 'year' | 'suggestions'>('week')

  if (isLoading) {
    return (
      <div className="rounded-lg border border-white/[0.08] bg-white/[0.03] p-3">
        <div className="flex items-center gap-2 text-white/40 text-sm">
          <span className="w-3 h-3 rounded-full border-2 border-white/20 border-t-white/50 animate-spin" />
          Loading AI summary…
        </div>
      </div>
    )
  }

  if (!summary) {
    return (
      <div className="rounded-lg border border-white/[0.08] bg-white/[0.03] p-3 text-xs text-white/40">
        No AI summary available.{' '}
        {onRefresh && (
          <button onClick={onRefresh} className="text-blue-400 hover:text-blue-300 underline">
            Generate one
          </button>
        )}
      </div>
    )
  }

  const sections = [
    { key: 'week' as const, label: '7 days', content: summary.weekSummary },
    { key: 'month' as const, label: '30 days', content: summary.monthSummary },
    { key: 'year' as const, label: '1 year', content: summary.yearSummary },
    { key: 'suggestions' as const, label: 'Suggestions', content: summary.suggestions },
  ]

  return (
    <div className="rounded-lg border border-violet-500/20 bg-violet-500/[0.04]">
      <button
        onClick={() => setExpanded((e) => !e)}
        className="w-full flex items-center justify-between px-3 py-2.5 text-left"
      >
        <div className="flex items-center gap-2">
          <span className="text-violet-400 text-xs font-semibold uppercase tracking-wider">AI Summary</span>
          {summary.generatedAt && (
            <span className="text-white/30 text-[10px]">{formatDateTime(summary.generatedAt)}</span>
          )}
        </div>
        <div className="flex items-center gap-2">
          {onRefresh && (
            <button
              onClick={(e) => { e.stopPropagation(); onRefresh() }}
              disabled={isRefreshing}
              className="text-white/30 hover:text-white/60 transition-colors p-0.5"
              title="Refresh AI summary"
            >
              <RefreshCw size={12} className={isRefreshing ? 'animate-spin' : ''} />
            </button>
          )}
          {expanded ? <ChevronUp size={14} className="text-white/40" /> : <ChevronDown size={14} className="text-white/40" />}
        </div>
      </button>

      {expanded && (
        <div className="px-3 pb-3">
          <div className="flex gap-1 mb-3">
            {sections.map((s) => (
              <button
                key={s.key}
                onClick={() => setActiveSection(s.key)}
                className={`px-2 py-0.5 rounded text-[11px] transition-colors ${
                  activeSection === s.key
                    ? 'bg-violet-500/30 text-violet-300'
                    : 'text-white/40 hover:text-white/60'
                }`}
              >
                {s.label}
              </button>
            ))}
          </div>
          <div className="text-xs text-white/70">
            <MarkdownRenderer>
              {sections.find((s) => s.key === activeSection)?.content ?? ''}
            </MarkdownRenderer>
          </div>
        </div>
      )}
    </div>
  )
}
