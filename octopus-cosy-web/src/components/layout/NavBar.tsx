import { Link, useRouter } from '@tanstack/react-router'
import { LayoutDashboard, ScatterChart, Table2, Sparkles, Settings, Columns2, LogOut } from 'lucide-react'
import { useAiDrawer } from './AiDrawerContext'
import { useApiStatus } from '@/hooks/use-api-status'
import { useDevice } from '@/hooks/use-device'
import { useLatestSnapshot } from '@/hooks/use-dashboard'

type ConnectionState = { color: 'success' | 'warning' | 'danger' | 'ink3'; label: string; title: string }

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

export function NavBar() {
  const { toggle } = useAiDrawer()
  const { data: status, isLoading: statusLoading } = useApiStatus()
  const { device } = useDevice()
  const { latest } = useLatestSnapshot(device?.deviceId)
  const conn = connectionState(status, statusLoading, latest?.minutesAgo)
  const colors = COLOR_CLASSES[conn.color]
  const router = useRouter()
  const auth = router.options.context.auth

  const handleLogout = async () => {
    await auth.signOut()
    router.navigate({ to: '/login' })
  }

  return (
    <>
      {/* Top nav bar */}
      <nav className="sticky top-0 z-[200] h-[64px] bg-white/88 backdrop-blur-[18px] border-b border-[rgba(0,0,0,0.07)] flex items-center px-4 sm:px-[26px] gap-3">
        {/* Brand */}
        <div className="flex items-center gap-2.5 flex-shrink-0">
          <div className="w-[28px] h-[28px] rounded-[7px] bg-ink flex items-center justify-center">
            <svg width="14" height="14" viewBox="0 0 11 11" fill="none">
              <path d="M5.5.5C4 2 2 3.3 2 6.3C2 8.8 3.7 10.5 5.5 10.5C7.3 10.5 9 8.8 9 6.3C9 3.3 7 2 5.5.5Z" fill="white" opacity=".88"/>
              <circle cx="5.5" cy="7" r="1.3" fill="white" opacity=".38"/>
            </svg>
          </div>
          <span className="text-[17px] font-semibold tracking-tight whitespace-nowrap hidden sm:inline">Octopus Heat Pump</span>
          <span className="text-[15px] font-semibold tracking-tight whitespace-nowrap sm:hidden">Octopus</span>
        </div>

        {/* Center nav pills (desktop) */}
        <div className="hidden lg:flex items-center gap-[3px] mx-auto bg-bg-surface border border-border-subtle rounded-[11px] p-1">
          <NavPill to="/heatpump" icon={<LayoutDashboard size={15} />} label="Dashboard" exact />
          <NavPill to="/heatpump/compare" icon={<Columns2 size={15} />} label="Compare" />
          <NavPill to="/heatpump/scatter" icon={<ScatterChart size={15} />} label="Scatter" />
          <NavPill to="/heatpump/data" icon={<Table2 size={15} />} label="Data" />
        </div>

        {/* Right section */}
        <div className="flex items-center gap-2.5 flex-shrink-0 ml-auto lg:ml-0">
          <button
            onClick={toggle}
            className="hidden sm:inline-flex items-center gap-[7px] h-[38px] px-[14px] rounded-[8px] border border-border-subtle bg-white cursor-pointer text-[13px] font-medium text-ink hover:border-border-card transition-all duration-150 whitespace-nowrap"
          >
            <Sparkles size={14} />
            <span>AI Analysis</span>
          </button>
          <button
            onClick={toggle}
            className="sm:hidden w-[38px] h-[38px] rounded-[8px] flex items-center justify-center text-ink2 hover:bg-bg-surface transition-colors"
            aria-label="AI Analysis"
          >
            <Sparkles size={16} />
          </button>
          <Link
            to="/settings"
            className="w-[38px] h-[38px] rounded-[8px] flex items-center justify-center text-ink3 hover:bg-bg-surface hover:text-ink transition-all duration-150"
            activeProps={{ className: 'bg-bg-surface text-ink' }}
          >
            <Settings size={16} />
          </Link>
          <button
            type="button"
            onClick={handleLogout}
            title={auth.user ? `Sign out ${auth.user.email}` : 'Sign out'}
            aria-label="Sign out"
            className="w-[34px] h-[34px] rounded-[7px] flex items-center justify-center text-ink3 hover:bg-white hover:text-ink hover:border-border-subtle border border-transparent transition-all duration-150"
          >
            <LogOut size={15} />
          </button>
          <div
            title={conn.title}
            className={`hidden sm:flex items-center gap-[7px] font-mono text-[12px] tracking-[.05em] px-3 py-[6px] rounded-full border ${colors.text} ${colors.bg} ${colors.border} font-medium`}
          >
            <div className={`w-[7px] h-[7px] rounded-full ${colors.dot} ${colors.pulse ? 'pulse' : ''}`} />
            {conn.label}
          </div>
        </div>
      </nav>

      {/* Mobile bottom tab bar */}
      <div className="lg:hidden fixed bottom-0 left-0 right-0 z-[200] h-14 bg-white/95 backdrop-blur-[18px] border-t border-[rgba(0,0,0,0.07)] flex items-center justify-around px-2 pb-[env(safe-area-inset-bottom)]">
        <MobileTab to="/heatpump" icon={<LayoutDashboard size={20} />} label="Dashboard" exact />
        <MobileTab to="/heatpump/compare" icon={<Columns2 size={20} />} label="Compare" />
        <MobileTab to="/heatpump/scatter" icon={<ScatterChart size={20} />} label="Scatter" />
        <MobileTab to="/heatpump/data" icon={<Table2 size={20} />} label="Data" />
      </div>
    </>
  )
}

function NavPill({ to, icon, label, exact }: { to: string; icon: React.ReactNode; label: string; exact?: boolean }) {
  return (
    <Link
      to={to}
      activeOptions={exact ? { exact: true } : undefined}
      className="h-[38px] px-[14px] rounded-[7px] inline-flex items-center gap-[7px] cursor-pointer text-ink3 hover:text-ink border border-transparent transition-all duration-150 text-[13px] font-medium whitespace-nowrap"
      activeProps={{
        className: 'bg-white text-ink border-[rgba(0,0,0,0.07)] shadow-[0_1px_4px_rgba(0,0,0,0.08)] [&>svg]:text-primary',
      }}
    >
      {icon}
      {label}
    </Link>
  )
}

function MobileTab({ to, icon, label, exact }: { to: string; icon: React.ReactNode; label: string; exact?: boolean }) {
  return (
    <Link
      to={to}
      activeOptions={exact ? { exact: true } : undefined}
      className="flex flex-col items-center gap-0.5 text-ink3 transition-colors flex-1 min-h-[44px] justify-center"
      activeProps={{ className: 'text-primary' }}
    >
      {icon}
      <span className="font-mono text-[10px] tracking-[.05em] uppercase">{label}</span>
    </Link>
  )
}
