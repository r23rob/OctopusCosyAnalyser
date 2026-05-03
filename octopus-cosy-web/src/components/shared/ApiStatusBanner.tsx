import { Link } from '@tanstack/react-router'
import { AlertTriangle, AlertCircle, X } from 'lucide-react'
import { useState } from 'react'
import { useApiStatus } from '@/hooks/use-api-status'
import { cn } from '@/lib/utils'
import type { ApiStatusDto } from '@/types/api'

type Severity = 'error' | 'warning'

interface BannerMessage {
  id: string
  severity: Severity
  title: string
  detail?: string
  action?: { to: string; label: string }
}

function buildMessages(status: ApiStatusDto): BannerMessage[] {
  const messages: BannerMessage[] = []

  if (!status.hasSettings) {
    messages.push({
      id: 'no-settings',
      severity: 'error',
      title: 'Octopus Energy account not configured',
      detail: 'Add your account number and API key (or email and password) to start collecting data.',
      action: { to: '/settings', label: 'Open Settings' },
    })
  } else if (!status.octopusCredentialsConfigured) {
    messages.push({
      id: 'missing-credentials',
      severity: 'error',
      title: 'Octopus Energy credentials missing',
      detail:
        status.octopusAuthError ??
        (status.authMode === 'password'
          ? 'Email and Octopus password are required for password authentication.'
          : 'Octopus API key is missing.'),
      action: { to: '/settings', label: 'Open Settings' },
    })
  } else if (!status.octopusAuthOk) {
    messages.push({
      id: 'auth-failed',
      severity: 'error',
      title: 'Could not authenticate with Octopus Energy',
      detail: status.octopusAuthError ?? 'Authentication failed. Check your saved credentials.',
      action: { to: '/settings', label: 'Check Settings' },
    })
  } else if (!status.hasDevice) {
    messages.push({
      id: 'no-device',
      severity: 'warning',
      title: 'No heat pump device registered',
      detail: 'Authenticated successfully — run device setup to discover your heat pump.',
      action: { to: '/settings', label: 'Open Settings' },
    })
  }

  if (!status.anthropicConfigured) {
    messages.push({
      id: 'no-anthropic',
      severity: 'warning',
      title: 'Anthropic API key not set',
      detail: 'AI summary and analysis features are disabled. Add a key in Settings or set ANTHROPIC_API_KEY.',
      action: { to: '/settings', label: 'Open Settings' },
    })
  }

  return messages
}

export function ApiStatusBanner() {
  const { data: status, isLoading } = useApiStatus()
  // Dismissals are scoped to a specific set of active message ids — when the set changes
  // (user fixes an issue, a new one appears), dismissals reset automatically.
  const [dismissed, setDismissed] = useState<{ key: string; ids: Set<string> }>({ key: '', ids: new Set() })

  if (isLoading || !status) return null

  const messages = buildMessages(status)
  const key = messages.map((m) => m.id).join('|')
  const activeDismissed = dismissed.key === key ? dismissed.ids : new Set<string>()
  const visible = messages.filter((m) => !activeDismissed.has(m.id))
  if (visible.length === 0) return null

  const dismiss = (id: string) =>
    setDismissed({ key, ids: new Set([...activeDismissed, id]) })

  return (
    <div className="flex flex-col gap-2 px-4 sm:px-8 pt-3 max-w-[1440px] w-full mx-auto">
      {visible.map((msg) => (
        <BannerItem key={msg.id} msg={msg} onDismiss={() => dismiss(msg.id)} />
      ))}
    </div>
  )
}

function BannerItem({ msg, onDismiss }: { msg: BannerMessage; onDismiss: () => void }) {
  const isError = msg.severity === 'error'
  const Icon = isError ? AlertCircle : AlertTriangle
  return (
    <div
      role="alert"
      className={cn(
        'flex items-start gap-3 rounded-lg border px-3.5 py-3 text-sm',
        isError
          ? 'border-danger/30 bg-danger-bg text-danger'
          : 'border-warning/30 bg-warning-bg text-warning',
      )}
    >
      <Icon size={16} className="mt-0.5 flex-shrink-0" />
      <div className="flex-1 min-w-0">
        <div className="font-semibold">{msg.title}</div>
        {msg.detail && <div className="mt-0.5 text-xs opacity-90 break-words">{msg.detail}</div>}
      </div>
      {msg.action && (
        <Link
          to={msg.action.to}
          className={cn(
            'flex-shrink-0 self-center px-2.5 py-1 rounded-md font-mono text-[10px] tracking-[.05em] uppercase border transition-colors',
            isError
              ? 'border-danger/40 hover:bg-danger/10'
              : 'border-warning/40 hover:bg-warning/10',
          )}
        >
          {msg.action.label}
        </Link>
      )}
      <button
        onClick={onDismiss}
        aria-label="Dismiss"
        className="flex-shrink-0 self-start opacity-60 hover:opacity-100 transition-opacity"
      >
        <X size={14} />
      </button>
    </div>
  )
}
