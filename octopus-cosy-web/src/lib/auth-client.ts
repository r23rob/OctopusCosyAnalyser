import { ApiError } from './api-client'

export interface CurrentUser {
  id: string
  email: string
}

interface RawFetchInit extends Omit<RequestInit, 'body'> {
  body?: unknown
}

async function authFetch(path: string, init: RawFetchInit = {}): Promise<Response> {
  const headers = new Headers(init.headers)
  if (init.body !== undefined && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  const body =
    init.body === undefined
      ? undefined
      : typeof init.body === 'string'
        ? init.body
        : JSON.stringify(init.body)

  return fetch(path, {
    ...init,
    headers,
    body,
    credentials: 'include',
  })
}

export const auth = {
  /** Returns the current user, or null when unauthenticated. Never throws on 401. */
  me: async (): Promise<CurrentUser | null> => {
    const res = await authFetch('/api/auth/me')
    if (res.status === 401) return null
    if (!res.ok) {
      const text = await res.text().catch(() => res.statusText)
      throw new ApiError(res.status, text)
    }
    return (await res.json()) as CurrentUser
  },

  /** Sign in with email + password. Throws ApiError on failure. */
  login: async (email: string, password: string): Promise<void> => {
    const res = await authFetch('/api/auth/login?useCookies=true', {
      method: 'POST',
      body: { email, password },
    })
    if (!res.ok) {
      const text = await res.text().catch(() => res.statusText)
      throw new ApiError(res.status, text || 'Invalid email or password')
    }
  },

  /** Create a new account. Throws ApiError on validation/conflict failure. */
  register: async (email: string, password: string): Promise<void> => {
    const res = await authFetch('/api/auth/register', {
      method: 'POST',
      body: { email, password },
    })
    if (!res.ok) {
      const text = await res.text().catch(() => res.statusText)
      throw new ApiError(res.status, text || 'Registration failed')
    }
  },

  /** Clears the auth cookie and signs out. */
  logout: async (): Promise<void> => {
    const res = await authFetch('/api/auth/logout', { method: 'POST' })
    if (!res.ok && res.status !== 401) {
      const text = await res.text().catch(() => res.statusText)
      throw new ApiError(res.status, text)
    }
  },
}
