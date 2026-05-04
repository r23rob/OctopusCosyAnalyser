import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useState, type FormEvent } from 'react'
import { auth } from '@/lib/auth-client'

export const Route = createFileRoute('/signup')({
  component: SignupPage,
})

function SignupPage() {
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
      await auth.register(email, password)
      // Identity register doesn't sign you in — log in immediately so the cookie is set.
      await auth.login(email, password)
      await routerAuth.refresh()
      navigate({ to: '/heatpump' })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Registration failed')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="min-h-screen flex flex-col items-center justify-center bg-bg-base px-4">
      <div className="w-full max-w-md">
        <h1 className="text-2xl font-semibold tracking-tight mb-6 text-center">
          Create account
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
              minLength={8}
              autoComplete="new-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full px-3 py-2 border rounded-md focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
            <span className="block text-xs text-ink2 mt-1">
              Minimum 8 characters with a digit and a lowercase letter.
            </span>
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
            {submitting ? 'Creating…' : 'Create account'}
          </button>
        </form>
        <p className="mt-4 text-center text-sm text-ink2">
          Already have an account?{' '}
          <a href="/login" className="text-cyan-700 hover:underline">
            Sign in
          </a>
        </p>
      </div>
    </div>
  )
}
