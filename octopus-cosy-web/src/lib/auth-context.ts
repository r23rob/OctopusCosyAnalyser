import { auth, type CurrentUser } from './auth-client'

/**
 * Router-level auth context. The router is constructed with this object and re-renders
 * whenever `user` changes; route `beforeLoad` guards read `context.auth.user` to decide
 * whether to redirect to /login.
 */
export interface RouterAuth {
  user: CurrentUser | null
  isLoaded: boolean
  refresh: () => Promise<void>
  signOut: () => Promise<void>
}

export function createRouterAuth(onChange: () => void): RouterAuth {
  const state: RouterAuth = {
    user: null,
    isLoaded: false,
    async refresh() {
      try {
        state.user = await auth.me()
      } catch {
        // Network failures — leave user as null but still mark loaded so
        // beforeLoad guards don't hang. The login page will surface the error.
        state.user = null
      } finally {
        state.isLoaded = true
        onChange()
      }
    },
    async signOut() {
      await auth.logout()
      state.user = null
      onChange()
    },
  }
  return state
}
