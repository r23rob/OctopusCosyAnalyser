import { Outlet } from '@tanstack/react-router'
import { NavBar } from './NavBar'

export function AppLayout() {
  return (
    <div className="flex min-h-screen bg-[#0f1117]">
      <NavBar />
      <main className="flex-1 flex flex-col min-w-0">
        <div className="flex-1 p-4 md:p-6 max-w-[1400px] w-full mx-auto">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
