import { Outlet } from '@tanstack/react-router'
import { TopBar, LeftRail, BottomTabs } from './NavBar'
import { AiDrawerProvider } from './AiDrawerContext'
import { AiDrawer } from '../dashboard/AiDrawer'
import { ApiStatusBanner } from '../shared/ApiStatusBanner'

export function AppLayout() {
  return (
    <AiDrawerProvider>
      <div className="flex min-h-screen bg-bg-base">
        {/* Desktop left rail */}
        <LeftRail />

        {/* Main content column */}
        <div className="flex flex-col flex-1 min-w-0">
          <TopBar />
          <ApiStatusBanner />
          <main className="flex-1 px-4 md:px-6 py-5 pb-20 md:pb-5 max-w-[1440px] w-full mx-auto">
            <Outlet />
          </main>
        </div>

        {/* Mobile bottom tabs */}
        <BottomTabs />
        <AiDrawer />
      </div>
    </AiDrawerProvider>
  )
}
