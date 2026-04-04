import { AlertCircle, X } from 'lucide-react'
import { cn } from '@/lib/utils'

interface Props {
  message: string
  onDismiss?: () => void
  className?: string
}

export function ErrorAlert({ message, onDismiss, className }: Props) {
  return (
    <div
      className={cn(
        'flex items-start gap-2 rounded-lg border border-danger/30 bg-danger-bg px-3 py-2.5 text-xs text-danger',
        className,
      )}
    >
      <AlertCircle size={14} className="mt-0.5 flex-shrink-0 text-danger" />
      <span className="flex-1">{message}</span>
      {onDismiss && (
        <button onClick={onDismiss} className="text-danger/60 hover:text-danger transition-colors">
          <X size={12} />
        </button>
      )}
    </div>
  )
}
