import { Link } from '@tanstack/react-router'
import { LayoutDashboard, ScatterChart, Table2, Sparkles, Settings } from 'lucide-react'
import { useAiDrawer } from './AiDrawerContext'

export function NavBar() {
  const { toggle } = useAiDrawer()

  return (
    <>
      {/* Top nav bar */}
      <nav className="sticky top-0 z-[200] h-[56px] bg-white/88 backdrop-blur-[18px] border-b border-[rgba(0,0,0,0.07)] flex items-center px-4 sm:px-[22px]">
        {/* Brand */}
        <div className="flex items-center gap-2 flex-shrink-0">
          <div className="w-[22px] h-[22px] rounded-[6px] bg-ink flex items-center justify-center">
            <svg width="11" height="11" viewBox="0 0 11 11" fill="none">
              <path d="M5.5.5C4 2 2 3.3 2 6.3C2 8.8 3.7 10.5 5.5 10.5C7.3 10.5 9 8.8 9 6.3C9 3.3 7 2 5.5.5Z" fill="white" opacity=".88"/>
              <circle cx="5.5" cy="7" r="1.3" fill="white" opacity=".38"/>
            </svg>
          </div>
          <span className="text-[15px] font-semibold tracking-tight">Ecodan</span>
          <span className="hidden sm:inline font-mono text-[10px] text-ink3 tracking-[.07em] uppercase ml-0.5">· Thermal Monitor</span>
        </div>

        {/* Center nav pills (desktop) */}
        <div className="hidden sm:flex items-center gap-0.5 mx-auto bg-bg-surface border border-border-subtle rounded-[10px] p-[3px]">
          <NavPill to="/heatpump" icon={<LayoutDashboard size={15} />} tip="Dashboard" exact />
          <NavPill to="/heatpump/scatter" icon={<ScatterChart size={15} />} tip="COP vs Temp" />
          <NavPill to="/heatpump/data" icon={<Table2 size={15} />} tip="Data & Export" />
        </div>

        {/* Right section */}
        <div className="flex items-center gap-2 flex-shrink-0 ml-auto sm:ml-0">
          <button
            onClick={toggle}
            className="flex items-center gap-1.5 px-3 py-[6px] rounded-[7px] border border-border-subtle bg-white cursor-pointer font-mono text-[10px] tracking-[.06em] uppercase text-ink2 hover:border-border-card hover:text-ink transition-all duration-150"
          >
            <Sparkles size={13} />
            <span className="hidden sm:inline">AI Analysis</span>
          </button>
          <Link
            to="/settings"
            className="w-[34px] h-[34px] rounded-[7px] flex items-center justify-center text-ink3 hover:bg-white hover:text-ink hover:border-border-subtle border border-transparent transition-all duration-150"
          >
            <Settings size={15} />
          </Link>
          <div className="hidden sm:flex items-center gap-[5px] font-mono text-[10px] text-success tracking-[.05em] uppercase px-[10px] py-1 rounded-full bg-success-bg border border-[rgba(22,163,74,0.15)]">
            <div className="w-[6px] h-[6px] rounded-full bg-success pulse" />
            Live
          </div>
        </div>
      </nav>

      {/* Mobile bottom tab bar */}
      <div className="sm:hidden fixed bottom-0 left-0 right-0 z-[200] h-12 bg-white/95 backdrop-blur-[18px] border-t border-[rgba(0,0,0,0.07)] flex items-center justify-around px-4">
        <MobileTab to="/heatpump" icon={<LayoutDashboard size={20} />} label="Dashboard" exact />
        <MobileTab to="/heatpump/scatter" icon={<ScatterChart size={20} />} label="Scatter" />
        <MobileTab to="/heatpump/data" icon={<Table2 size={20} />} label="Data" />
      </div>
    </>
  )
}

function NavPill({ to, icon, tip, exact }: { to: string; icon: React.ReactNode; tip: string; exact?: boolean }) {
  return (
    <Link
      to={to}
      activeOptions={exact ? { exact: true } : undefined}
      className="w-[34px] h-[34px] rounded-[7px] flex items-center justify-center cursor-pointer text-ink3 hover:bg-white hover:text-ink hover:border-[rgba(0,0,0,0.07)] border border-transparent transition-all duration-150 relative group"
      activeProps={{
        className: 'bg-white text-ink border-[rgba(0,0,0,0.07)] shadow-[0_1px_4px_rgba(0,0,0,0.08)] [&>svg]:text-primary',
      }}
    >
      {icon}
      <div className="absolute top-[calc(100%+8px)] left-1/2 -translate-x-1/2 bg-ink text-white font-mono text-[10px] tracking-[.05em] px-2.5 py-[4px] rounded-[5px] whitespace-nowrap opacity-0 pointer-events-none group-hover:opacity-100 transition-opacity duration-150 z-[300] before:content-[''] before:absolute before:bottom-full before:left-1/2 before:-translate-x-1/2 before:border-4 before:border-transparent before:border-b-ink">
        {tip}
      </div>
    </Link>
  )
}

function MobileTab({ to, icon, label, exact }: { to: string; icon: React.ReactNode; label: string; exact?: boolean }) {
  return (
    <Link
      to={to}
      activeOptions={exact ? { exact: true } : undefined}
      className="flex flex-col items-center gap-0.5 text-ink3 transition-colors"
      activeProps={{ className: 'text-primary' }}
    >
      {icon}
      <span className="font-mono text-[9px] tracking-[.05em] uppercase">{label}</span>
    </Link>
  )
}
