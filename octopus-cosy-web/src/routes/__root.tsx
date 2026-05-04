import {
  createRootRouteWithContext,
  Outlet,
  redirect,
  useLocation,
} from '@tanstack/react-router'
import { AppLayout } from '@/components/layout/AppLayout'
import type { RouterAuth } from '@/lib/auth-context'

interface RouterContext {
  auth: RouterAuth
}

const PUBLIC_ROUTES = new Set(['/login', '/signup'])

export const Route = createRootRouteWithContext<RouterContext>()({
  // Auth gate. Runs before every route loads. Redirects to /login when no user
  // is signed in, except for the public routes themselves.
  beforeLoad: ({ context, location }) => {
    if (PUBLIC_ROUTES.has(location.pathname)) return
    if (!context.auth.user) {
      throw redirect({
        to: '/login',
        search: { redirect: location.href },
      })
    }
  },
  component: RootRoute,
})

function RootRoute() {
  const pathname = useLocation({ select: (loc) => loc.pathname })
  // Public routes (login, signup) render bare — no NavBar, no AiDrawer.
  if (PUBLIC_ROUTES.has(pathname)) {
    return <Outlet />
  }
  return <AppLayout />
}
