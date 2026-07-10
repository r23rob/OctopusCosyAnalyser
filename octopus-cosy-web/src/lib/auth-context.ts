import type { CurrentUser } from './auth-client'

export interface RouterAuth {
  user: CurrentUser | null
  isLoaded: boolean
  refresh: () => Promise<void>
  signOut: () => Promise<void>
}

export function createRouterAuth(onChange: () => void): RouterAuth {
  const state: RouterAuth = {
    user: { id: 'rob', email: 'Rob@hutchin.co.uk' },
    isLoaded: true,
    async refresh() {
      onChange()
    },
    async signOut() {
      // no-op — auth disabled
    },
  }
  return state
}
