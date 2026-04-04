import { useAiDrawer } from '../layout/AiDrawerContext'
import { useDevice } from '../../hooks/use-device'
import { useAiSummary } from '../../hooks/use-dashboard'
import { MarkdownRenderer } from '../shared/MarkdownRenderer'
import { RefreshCw } from 'lucide-react'

export function AiDrawer() {
  const { open, close } = useAiDrawer()
  const { device } = useDevice()
  const { summary, isLoading, isRefreshing, refresh } = useAiSummary(device?.deviceId)

  const findings = buildFindings(summary?.weekSummary, summary?.monthSummary, summary?.yearSummary, summary?.suggestions)

  return (
    <>
      {/* Overlay */}
      <div
        className={`fixed inset-0 z-[400] transition-all duration-250 ${open ? 'bg-black/18 pointer-events-auto' : 'bg-transparent pointer-events-none'}`}
        onClick={close}
      />

      {/* Drawer panel */}
      <div
        className={`fixed top-0 right-0 bottom-0 w-[380px] bg-white border-l border-border-card z-[401] flex flex-col shadow-[-8px_0_32px_rgba(0,0,0,0.08)] transition-transform duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] ${open ? 'translate-x-0' : 'translate-x-full'}`}
      >
        {/* Header */}
        <div className="px-5 pt-[18px] pb-4 border-b border-border-subtle flex items-center justify-between flex-shrink-0">
          <div className="text-[17px] font-semibold tracking-tight flex items-center gap-2">
            AI Analysis
            <span className="inline-flex items-center gap-1 font-mono text-[10px] tracking-[.06em] uppercase px-[8px] py-0.5 rounded bg-gradient-to-br from-[rgba(124,58,237,0.1)] to-[rgba(6,182,212,0.1)] text-primary border border-[rgba(6,182,212,0.2)]">
              Claude
            </span>
          </div>
          <button
            onClick={close}
            aria-label="Close drawer"
            className="w-7 h-7 rounded-[6px] border border-border-subtle bg-transparent cursor-pointer flex items-center justify-center text-ink2 text-base hover:bg-bg-surface hover:text-ink transition-all duration-150"
          >
            ✕
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-5 py-4">
          {isLoading ? (
            <div className="flex flex-col items-center justify-center h-40 gap-3 text-ink3">
              <div className="flex gap-[5px]">
                <div className="w-1.5 h-1.5 rounded-full bg-primary ai-dot" />
                <div className="w-1.5 h-1.5 rounded-full bg-primary ai-dot" />
                <div className="w-1.5 h-1.5 rounded-full bg-primary ai-dot" />
              </div>
              <div className="font-mono text-[11px] text-ink3 tracking-[.05em] uppercase">
                Analysing performance data…
              </div>
            </div>
          ) : findings.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-40 gap-3 text-ink3">
              <div className="font-mono text-[11px] tracking-[.05em] uppercase">
                No analysis data available
              </div>
              <button
                onClick={() => refresh()}
                disabled={isRefreshing}
                className="flex items-center gap-1.5 font-mono text-[10px] tracking-[.06em] uppercase border-none bg-transparent cursor-pointer text-primary hover:text-ink transition-colors duration-150 disabled:opacity-50"
              >
                <RefreshCw size={11} className={isRefreshing ? 'animate-spin' : ''} />
                Generate analysis
              </button>
            </div>
          ) : (
            <>
              {findings.map((f, i) => (
                <div key={i} className={`flex gap-2.5 py-3 ${i < findings.length - 1 ? 'border-b border-border-subtle' : ''}`}>
                  <div
                    className="w-6 h-6 rounded-[6px] flex items-center justify-center flex-shrink-0 mt-[1px] text-[13px]"
                    style={{ background: f.bg, color: f.color }}
                  >
                    {f.icon}
                  </div>
                  <div className="flex-1">
                    <div className="text-[15px] font-medium text-ink mb-[3px]">{f.title}</div>
                    <div className="text-[14px] text-ink2 leading-[1.65]">
                      <MarkdownRenderer>{f.description}</MarkdownRenderer>
                    </div>
                  </div>
                </div>
              ))}

              <button
                onClick={() => refresh()}
                disabled={isRefreshing}
                className="flex items-center gap-1.5 font-mono text-[10px] tracking-[.06em] uppercase pt-2 border-none bg-transparent cursor-pointer text-ink3 hover:text-ink transition-colors duration-150 disabled:opacity-50"
              >
                <RefreshCw size={11} className={isRefreshing ? 'animate-spin' : ''} />
                Regenerate analysis
              </button>
            </>
          )}
        </div>

        {/* Footer */}
        {summary?.generatedAt && (
          <div className="px-5 py-3 border-t border-border-subtle font-mono text-[10px] text-ink3 tracking-[.03em] flex-shrink-0">
            Generated {new Date(summary.generatedAt).toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })}
          </div>
        )}
      </div>
    </>
  )
}

interface Finding {
  icon: string
  color: string
  bg: string
  title: string
  description: string
}

function buildFindings(
  week: string | undefined,
  month: string | undefined,
  year: string | undefined,
  suggestions: string | undefined,
): Finding[] {
  const findings: Finding[] = []
  if (week) findings.push({ icon: '✓', color: '#16A34A', bg: 'rgba(22,163,74,0.08)', title: '7-Day Summary', description: week })
  if (month) findings.push({ icon: '⚠', color: '#D97706', bg: 'rgba(217,119,6,0.08)', title: '30-Day Summary', description: month })
  if (year) findings.push({ icon: '↓', color: '#DC2626', bg: 'rgba(220,38,38,0.08)', title: 'Year Summary', description: year })
  if (suggestions) findings.push({ icon: '✦', color: '#06B6D4', bg: 'rgba(6,182,212,0.1)', title: 'Suggestions', description: suggestions })
  return findings
}
