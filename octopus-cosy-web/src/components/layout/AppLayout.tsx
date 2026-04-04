import { Outlet } from '@tanstack/react-router'
import { NavBar } from './NavBar'
import { AiDrawerProvider } from './AiDrawerContext'
import { AiDrawer } from '../dashboard/AiDrawer'

export function AppLayout() {
  return (
    <AiDrawerProvider>
      <div className="flex flex-col min-h-screen bg-bg-base">
        <NavBar />
        <main className="flex-1 px-4 sm:px-6 py-5 pb-16 sm:pb-5 max-w-[1300px] w-full mx-auto">
          <Outlet />
        </main>
        <AiDrawer />
      </div>
    </AiDrawerProvider>
  )
}
