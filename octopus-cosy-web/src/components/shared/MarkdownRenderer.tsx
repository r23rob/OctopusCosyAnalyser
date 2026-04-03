import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import rehypeSanitize from 'rehype-sanitize'
import { cn } from '@/lib/utils'

interface Props {
  children: string
  className?: string
}

export function MarkdownRenderer({ children, className }: Props) {
  return (
    <div className={cn('prose prose-invert prose-sm max-w-none', className)}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeSanitize]}
        components={{
          // Override to match our dark theme sizing
          h1: ({ children }) => <h1 className="text-base font-semibold text-white/90 mt-3 mb-1">{children}</h1>,
          h2: ({ children }) => <h2 className="text-sm font-semibold text-white/85 mt-3 mb-1">{children}</h2>,
          h3: ({ children }) => <h3 className="text-xs font-semibold text-white/80 mt-2 mb-1">{children}</h3>,
          p: ({ children }) => <p className="text-white/70 text-xs leading-relaxed mb-2">{children}</p>,
          ul: ({ children }) => <ul className="text-white/70 text-xs pl-4 mb-2 space-y-0.5 list-disc">{children}</ul>,
          ol: ({ children }) => <ol className="text-white/70 text-xs pl-4 mb-2 space-y-0.5 list-decimal">{children}</ol>,
          li: ({ children }) => <li className="leading-relaxed">{children}</li>,
          strong: ({ children }) => <strong className="text-white/90 font-semibold">{children}</strong>,
          code: ({ children }) => (
            <code className="text-[10px] bg-white/[0.06] text-cyan-300 px-1 py-0.5 rounded font-mono">{children}</code>
          ),
          table: ({ children }) => (
            <div className="overflow-x-auto mb-2">
              <table className="text-[11px] border-collapse w-full">{children}</table>
            </div>
          ),
          th: ({ children }) => (
            <th className="border border-white/10 px-2 py-1 text-left text-white/60 font-medium bg-white/[0.04]">{children}</th>
          ),
          td: ({ children }) => (
            <td className="border border-white/10 px-2 py-1 text-white/70">{children}</td>
          ),
        }}
      >
        {children}
      </ReactMarkdown>
    </div>
  )
}
