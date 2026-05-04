import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useState, type FormEvent } from 'react'
import { auth } from '@/lib/auth-client'
import { GoogleSignInButton } from '@/components/shared/GoogleSignInButton'

interface LoginSearch {
  redirect?: string
}

const DEFAULT_LANDING = '/heatpump'

/**
 * Only allow same-origin relative paths starting with a single `/`. Rejects:
 *  - protocol-relative URLs (`//attacker.com/path`)
 *  - absolute URLs of any scheme (`https://attacker.com`, `javascript:...`)
 *  - empty / undefined
 * Returns the safe relative target otherwise.
 */
function sanitizeRedirect(target: string | undefined): string {
  if (!target) return DEFAULT_LANDING
  if (!target.startsWith('/')) return DEFAULT_LANDING
  if (target.startsWith('//')) return DEFAULT_LANDING
  return target
}

export const Route = createFileRoute('/login')({
  validateSearch: (search: Record<string, unknown>): LoginSearch =>
    typeof search['redirect'] === 'string'
      ? { redirect: search['redirect'] }
      : {},
  component: LoginPage,
})

function LoginPage() {
  const { redirect } = Route.useSearch()
  const navigate = useNavigate()
  const { auth: routerAuth } = Route.useRouteContext()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await auth.login(email, password)
      await routerAuth.refresh()
      // After refresh the gate in __root will let us through.
      navigate({ to: sanitizeRedirect(redirect) as '/heatpump' })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="min-h-screen flex flex-col items-center justify-center bg-bg-base px-4">
      <div className="w-full max-w-md">
        <h1 className="text-2xl font-semibold tracking-tight mb-6 text-center">
          Sign in
        </h1>
        <form onSubmit={onSubmit} className="space-y-4 bg-white border rounded-lg p-6 shadow-sm">
          <label className="block">
            <span className="block text-sm font-medium mb-1">Email</span>
            <input
              type="email"
              required
              autoComplete="email"
              autoFocus
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full px-3 py-2 border rounded-md focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </label>
          <label className="block">
            <span className="block text-sm font-medium mb-1">Password</span>
            <input
              type="password"
              required
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full px-3 py-2 border rounded-md focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </label>
          {error && (
            <div role="alert" className="text-sm text-red-600">
              {error}
            </div>
          )}
          <button
            type="submit"
            disabled={submitting}
            className="w-full min-h-[44px] py-2 bg-cyan-500 hover:bg-cyan-600 disabled:opacity-60 text-white rounded-md font-medium transition-colors"
          >
            {submitting ? 'Signing in…' : 'Sign in'}
          </button>
          <div className="flex items-center gap-3 text-xs text-ink2">
            <span className="flex-1 border-t" aria-hidden="true" />
            <span>or</span>
            <span className="flex-1 border-t" aria-hidden="true" />
          </div>
          <GoogleSignInButton
            returnUrl={sanitizeRedirect(redirect)}
            label="Continue with Google"
          />
        </form>
        <p className="mt-4 text-center text-sm text-ink2">
          New here?{' '}
          <a href="/signup" className="text-cyan-700 hover:underline">
            Create an account
          </a>
        </p>
      </div>
    </div>
  )
}
