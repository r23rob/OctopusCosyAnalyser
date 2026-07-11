import { Link, useMatchRoute, useRouterState } from '@tanstack/react-router'
import { Home, Clock, GitCompare, MoreHorizontal } from 'lucide-react'
import { useApiStatus } from '@/hooks/use-api-status'
import { useDevice } from '@/hooks/use-device'
import { useLatestSnapshot } from '@/hooks/use-dashboard'

/* ── connection status ──────────────────────────────────────────────── */

type ConnectionState = {
  color: 'success' | 'warning' | 'danger' | 'ink3'
  label: string
  title: string
}

function connectionState(
  status: ReturnType<typeof useApiStatus>['data'],
  isLoading: boolean,
  minutesAgo: number | null | undefined,
): ConnectionState {
  if (isLoading || !status) return { color: 'ink3', label: 'Checking', title: 'Checking API status…' }
  if (!status.hasSettings) return { color: 'danger', label: 'No Auth', title: 'No Octopus Energy account configured.' }
  if (!status.octopusCredentialsConfigured) return { color: 'danger', label: 'No Auth', title: 'Octopus credentials missing.' }
  if (!status.octopusAuthOk) return { color: 'danger', label: 'Auth Fail', title: status.octopusAuthError ?? 'Octopus auth failed.' }
  if (!status.hasDevice) return { color: 'warning', label: 'Setup', title: 'Authenticated but no heat pump device registered.' }
  if (minutesAgo != null && minutesAgo > 60) {
    return { color: 'danger', label: 'Offline', title: `No worker reading for ${Math.round(minutesAgo)} minutes.` }
  }
  if (minutesAgo != null && minutesAgo > 20) {
    return { color: 'warning', label: 'Stale', title: `Last reading ${Math.round(minutesAgo)} minutes ago.` }
  }
  if (!status.anthropicConfigured) return { color: 'warning', label: 'Live', title: 'Connected. Anthropic AI key not set.' }
  return { color: 'success', label: 'Live', title: 'Connected to Octopus Energy. AI features available.' }
}

const COLOR_CLASSES: Record<ConnectionState['color'], { text: string; bg: string; border: string; dot: string; pulse: boolean }> = {
  success: { text: 'text-success', bg: 'bg-success-bg', border: 'border-[rgba(22,163,74,0.15)]', dot: 'bg-success', pulse: true },
  warning: { text: 'text-warning', bg: 'bg-warning-bg', border: 'border-[rgba(217,119,6,0.18)]', dot: 'bg-warning', pulse: false },
  danger:  { text: 'text-danger',  bg: 'bg-danger-bg',  border: 'border-[rgba(220,38,38,0.18)]',  dot: 'bg-danger',  pulse: false },
  ink3:    { text: 'text-ink3',    bg: 'bg-bg-surface', border: 'border-border-subtle',           dot: 'bg-ink3',    pulse: false },
}

function useConnectionStatus() {
  const { data: status, isLoading: statusLoading } = useApiStatus()
  const { device } = useDevice()
  const { latest } = useLatestSnapshot(device?.deviceId)
  const conn = connectionState(status, statusLoading, latest?.minutesAgo)
  const colors = COLOR_CLASSES[conn.color]
  return { conn, colors }
}

/* ── nav items ──────────────────────────────────────────────────────── */

interface NavItem {
  to: '/' | '/history' | '/compare' | '/more'
  icon: typeof Home
  label: string
  exact: boolean
}

const NAV_ITEMS: NavItem[] = [
  { to: '/', icon: Home, label: 'Home', exact: true },
  { to: '/history', icon: Clock, label: 'History', exact: false },
  { to: '/compare', icon: GitCompare, label: 'Compare', exact: false },
  { to: '/more', icon: MoreHorizontal, label: 'More', exact: false },
]

/* ── page title ─────────────────────────────────────────────────────── */

function usePageTitle(): string {
  const pathname = useRouterState({ select: (s) => s.location.pathname })
  const titles: Record<string, string> = {
    '/': 'Home',
    '/history': 'History',
    '/compare': 'Compare',
    '/more': 'More',
  }
  return titles[pathname] ?? 'Home'
}

/* ── Top bar (both viewports) ──────────────────────────────────────── */

export function TopBar() {
  const title = usePageTitle()
  const { conn, colors } = useConnectionStatus()

  return (
    <header className="sticky top-0 z-[200] h-12 bg-white/88 backdrop-blur-[18px] border-b border-border-subtle flex items-center px-4 md:px-6">
      <h1 className="text-[15px] font-semibold tracking-tight">{title}</h1>
      <div className="ml-auto">
        <div
          title={conn.title}
          className={`flex items-center gap-[7px] font-mono text-[11px] tracking-[.05em] px-2.5 py-[5px] rounded-full border ${colors.text} ${colors.bg} ${colors.border} font-medium`}
        >
          <div className={`w-[6px] h-[6px] rounded-full ${colors.dot} ${colors.pulse ? 'pulse' : ''}`} />
          {conn.label.toUpperCase()}
        </div>
      </div>
    </header>
  )
}

/* ── Desktop left rail (md: and above) ─────────────────────────────── */

export function LeftRail() {
  return (
    <nav className="hidden md:flex flex-col w-[72px] min-w-[72px] h-screen sticky top-0 bg-white border-r border-border-subtle z-[200]">
      {/* Brand — aligned with top bar height */}
      <div className="flex items-center justify-center h-12 border-b border-border-subtle shrink-0">
        <span className="text-[11px] font-semibold tracking-tight text-ink2">
          cosydays
        </span>
      </div>

      {/* Nav items */}
      <div className="flex flex-col items-center gap-1 pt-3 flex-1">
        {NAV_ITEMS.map((item) => (
          <RailItem key={item.to} {...item} />
        ))}
      </div>
    </nav>
  )
}

function RailItem({ to, icon: Icon, label, exact }: NavItem) {
  const matchRoute = useMatchRoute()
  const isActive = !!matchRoute({ to, fuzzy: !exact })

  return (
    <Link
      to={to}
      className="group relative flex items-center justify-center w-full py-0.5"
    >
      <div
        className={`w-11 h-11 rounded-[10px] flex items-center justify-center transition-colors duration-[var(--dur-base)] ${
          isActive ? 'bg-purple-bg' : 'hover:bg-bg-surface'
        }`}
      >
        <Icon
          size={20}
          className={`transition-colors duration-[var(--dur-base)] ${
            isActive ? 'text-purple' : 'text-ink3 group-hover:text-ink'
          }`}
        />
      </div>
      {/* Tooltip on hover */}
      <span className="absolute left-[calc(100%+4px)] px-2.5 py-1.5 rounded-md bg-ink text-white text-[11px] font-medium whitespace-nowrap opacity-0 pointer-events-none group-hover:opacity-100 transition-opacity duration-[var(--dur-fast)] z-[300] shadow-lg">
        {label}
      </span>
    </Link>
  )
}

/* ── Mobile bottom tabs (below md:) ────────────────────────────────── */

export function BottomTabs() {
  return (
    <nav
      className="md:hidden fixed bottom-0 left-0 right-0 z-[200] bg-white border-t border-border-subtle flex items-center justify-around"
      style={{
        height: 'calc(56px + env(safe-area-inset-bottom, 0px))',
        paddingBottom: 'env(safe-area-inset-bottom, 0px)',
      }}
    >
      {NAV_ITEMS.map((item) => (
        <MobileTab key={item.to} {...item} />
      ))}
    </nav>
  )
}

function MobileTab({ to, icon: Icon, label, exact }: NavItem) {
  const matchRoute = useMatchRoute()
  const isActive = !!matchRoute({ to, fuzzy: !exact })

  return (
    <Link
      to={to}
      className="flex flex-col items-center justify-center gap-0.5 flex-1 min-h-[44px]"
    >
      <div
        className={`flex items-center justify-center w-10 h-7 rounded-full transition-colors duration-[var(--dur-base)] ${
          isActive ? 'bg-purple-bg' : ''
        }`}
      >
        <Icon
          size={20}
          className={`transition-colors duration-[var(--dur-base)] ${
            isActive ? 'text-purple' : 'text-ink3'
          }`}
        />
      </div>
      <span
        className={`text-[10px] font-medium tracking-[.03em] transition-colors duration-[var(--dur-base)] ${
          isActive ? 'text-purple' : 'text-ink3'
        }`}
      >
        {label}
      </span>
    </Link>
  )
}
