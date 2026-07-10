import { createRootRouteWithContext } from '@tanstack/react-router'
import { AppLayout } from '@/components/layout/AppLayout'
import type { RouterAuth } from '@/lib/auth-context'

interface RouterContext {
  auth: RouterAuth
}

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootRoute,
})

function RootRoute() {
  return <AppLayout />
}
