import { useState } from 'react'
import { Link } from '@tanstack/react-router'
import { Activity, Bot, PoundSterling, Settings, Menu, X, Zap } from 'lucide-react'

interface NavLinkProps {
  to: string
  icon: React.ReactNode
  label: string
  onClick?: () => void
}

function NavLink({ to, icon, label, onClick }: NavLinkProps) {
  return (
    <Link
      to={to}
      onClick={onClick}
      className="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-white/60 hover:text-white hover:bg-white/[0.06] transition-colors"
      activeProps={{ className: 'text-white bg-white/[0.08] border-l-2 border-blue-500 rounded-l-none pl-[10px]' }}
    >
      <span className="w-4 h-4 flex-shrink-0">{icon}</span>
      {label}
    </Link>
  )
}

export function NavBar() {
  const [mobileOpen, setMobileOpen] = useState(false)
  const close = () => setMobileOpen(false)

  const links = (
    <nav className="flex flex-col gap-1 p-3 flex-1">
      <NavLink to="/heatpump" icon={<Activity size={16} />} label="Dashboard" onClick={close} />
      <NavLink to="/heatpump/ai-analysis" icon={<Bot size={16} />} label="AI Analysis" onClick={close} />
      <NavLink to="/heatpump/cost-tracking" icon={<PoundSterling size={16} />} label="Cost Tracking" onClick={close} />
      <div className="mt-4 mb-1 px-3 text-[10px] uppercase tracking-widest text-white/25 font-medium">
        Settings
      </div>
      <NavLink to="/settings" icon={<Settings size={16} />} label="Account Settings" onClick={close} />
    </nav>
  )

  return (
    <>
      {/* Desktop sidebar */}
      <aside className="hidden md:flex flex-col w-56 flex-shrink-0 border-r border-white/[0.06] bg-[#1a1d27] min-h-screen">
        <div className="flex items-center gap-2 px-4 py-4 border-b border-white/[0.06]">
          <Zap size={18} className="text-blue-400" />
          <span className="font-semibold text-white/90 text-sm">Cosy Analyser</span>
        </div>
        {links}
      </aside>

      {/* Mobile top bar */}
      <header className="md:hidden flex items-center justify-between px-4 py-3 border-b border-white/[0.06] bg-[#1a1d27]">
        <div className="flex items-center gap-2">
          <Zap size={16} className="text-blue-400" />
          <span className="font-semibold text-white/90 text-sm">Cosy Analyser</span>
        </div>
        <button onClick={() => setMobileOpen((o) => !o)} className="text-white/60 hover:text-white p-1">
          {mobileOpen ? <X size={20} /> : <Menu size={20} />}
        </button>
      </header>

      {/* Mobile nav drawer */}
      {mobileOpen && (
        <div className="md:hidden absolute inset-0 z-50 flex">
          <div className="flex flex-col w-56 bg-[#1a1d27] border-r border-white/[0.06] min-h-screen pt-2">
            <div className="flex items-center justify-between px-4 py-3 border-b border-white/[0.06]">
              <div className="flex items-center gap-2">
                <Zap size={16} className="text-blue-400" />
                <span className="font-semibold text-white/90 text-sm">Cosy Analyser</span>
              </div>
              <button onClick={close} className="text-white/60">
                <X size={18} />
              </button>
            </div>
            {links}
          </div>
          <div className="flex-1 bg-black/50" onClick={close} />
        </div>
      )}
    </>
  )
}
