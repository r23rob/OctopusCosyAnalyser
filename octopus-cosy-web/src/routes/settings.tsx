import { createFileRoute } from '@tanstack/react-router'
import { useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'
import { ErrorAlert } from '@/components/shared/ErrorAlert'

export const Route = createFileRoute('/settings')({
  component: SettingsPage,
})

const schema = z
  .object({
    accountNumber: z.string().min(1, 'Account number is required'),
    authMode: z.enum(['apikey', 'password']),
    apiKey: z.string().optional(),
    email: z.string().optional(),
    octopusPassword: z.string().optional(),
    anthropicApiKey: z.string().optional(),
  })
  .superRefine((data, ctx) => {
    if (data.authMode === 'apikey') {
      if (!data.apiKey || data.apiKey.trim().length === 0) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'API key is required', path: ['apiKey'] })
      }
    } else {
      if (!data.email || data.email.trim().length === 0) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Email is required', path: ['email'] })
      } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(data.email)) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Valid email required', path: ['email'] })
      }
      if (!data.octopusPassword || data.octopusPassword.trim().length === 0) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Password is required', path: ['octopusPassword'] })
      }
    }
  })

type FormValues = z.infer<typeof schema>

function SettingsPage() {
  const queryClient = useQueryClient()

  const settingsQuery = useQuery({
    queryKey: queryKeys.settings.all(),
    queryFn: () => api.settings.getAll(),
  })

  const existing = settingsQuery.data?.[0]

  const {
    register,
    handleSubmit,
    formState: { errors, isDirty },
    reset,
    control,
    setValue,
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: existing
      ? {
          accountNumber: existing.accountNumber,
          authMode: existing.authMode ?? 'apikey',
          apiKey: existing.apiKey,
          email: '',
          octopusPassword: '',
          anthropicApiKey: existing.anthropicApiKey ?? '',
        }
      : { accountNumber: '', authMode: 'apikey', apiKey: '', email: '', octopusPassword: '', anthropicApiKey: '' },
  })

  const authMode = useWatch({ control, name: 'authMode' })

  const saveMutation = useMutation({
    mutationFn: (values: FormValues) =>
      api.settings.upsert({
        accountNumber: values.accountNumber,
        authMode: values.authMode,
        apiKey: values.authMode === 'apikey' ? (values.apiKey || null) : null,
        email: values.authMode === 'password' ? (values.email || null) : null,
        octopusPassword: values.authMode === 'password' ? (values.octopusPassword || null) : null,
        anthropicApiKey: values.anthropicApiKey || null,
      }),
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKeys.settings.all(), saved ? [saved] : [])
      reset({
        accountNumber: saved?.accountNumber ?? '',
        authMode: saved?.authMode ?? 'apikey',
        apiKey: saved?.apiKey ?? '',
        email: '',
        octopusPassword: '',
        anthropicApiKey: saved?.anthropicApiKey ?? '',
      })
    },
  })

  const setupMutation = useMutation({
    mutationFn: (accountNumber: string) => api.devices.setup(accountNumber),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.devices.all() })
    },
  })

  const onSubmit = handleSubmit((values) => saveMutation.mutate(values))

  return (
    <div className="max-w-xl">
      <h1 className="text-lg font-semibold mb-1">Account Settings</h1>
      <p className="text-sm text-ink2 mb-6">
        Your Octopus Energy credentials for accessing the heat pump API.
      </p>

      {saveMutation.isError && (
        <ErrorAlert message="Failed to save settings. Please check your credentials and try again." className="mb-4" />
      )}
      {saveMutation.isSuccess && !isDirty && (
        <div className="mb-4 rounded-lg border border-success/30 bg-success-bg px-3 py-2 text-xs text-success">
          Settings saved successfully.
        </div>
      )}

      <form onSubmit={onSubmit} className="flex flex-col gap-4">
        <div className="rounded-[10px] border border-border-subtle bg-white p-5 flex flex-col gap-4">
          <h2 className="text-sm font-medium text-ink2">Octopus Energy</h2>

          <Field label="Account Number" error={errors.accountNumber?.message}>
            <input
              {...register('accountNumber')}
              placeholder="A-XXXXXXXX"
              className={inputCls(!!errors.accountNumber)}
            />
          </Field>

          {/* Auth mode toggle */}
          <div className="flex flex-col gap-1.5">
            <label className="text-xs font-medium text-ink2">Authentication Method</label>
            <div className="flex rounded-lg border border-border-subtle overflow-hidden">
              <button
                type="button"
                onClick={() => setValue('authMode', 'apikey', { shouldDirty: true })}
                className={`flex-1 px-3 py-2 text-sm font-medium transition-colors ${
                  authMode === 'apikey'
                    ? 'bg-ink text-white'
                    : 'bg-bg-base text-ink2 hover:bg-bg-surface'
                }`}
              >
                API Key
              </button>
              <button
                type="button"
                onClick={() => setValue('authMode', 'password', { shouldDirty: true })}
                className={`flex-1 px-3 py-2 text-sm font-medium transition-colors ${
                  authMode === 'password'
                    ? 'bg-ink text-white'
                    : 'bg-bg-base text-ink2 hover:bg-bg-surface'
                }`}
              >
                Email &amp; Password
              </button>
            </div>
            <p className="text-[11px] text-ink3">
              {authMode === 'apikey'
                ? 'Find your API key at octopus.energy/dashboard/developer-settings'
                : 'Use your Octopus Energy login credentials'}
            </p>
          </div>

          {authMode === 'apikey' && (
            <Field label="API Key" error={errors.apiKey?.message}>
              <input
                {...register('apiKey')}
                type="password"
                placeholder="sk_live_..."
                className={inputCls(!!errors.apiKey)}
              />
            </Field>
          )}

          {authMode === 'password' && (
            <>
              <Field label="Email" error={errors.email?.message}>
                <input
                  {...register('email')}
                  type="email"
                  placeholder="you@example.com"
                  className={inputCls(!!errors.email)}
                />
              </Field>

              <Field label="Password" error={errors.octopusPassword?.message} hint="Re-enter each time you save settings.">
                <input
                  {...register('octopusPassword')}
                  type="password"
                  placeholder="Your Octopus account password"
                  className={inputCls(!!errors.octopusPassword)}
                />
              </Field>
            </>
          )}
        </div>

        <div className="rounded-[10px] border border-border-subtle bg-white p-5 flex flex-col gap-4">
          <h2 className="text-sm font-medium text-ink2">AI Features (optional)</h2>
          <Field label="Anthropic API Key" hint="Required for AI analysis and dashboard summaries">
            <input
              {...register('anthropicApiKey')}
              type="password"
              placeholder="sk-ant-..."
              className={inputCls(false)}
            />
          </Field>
        </div>

        <div className="flex gap-3">
          <button
            type="submit"
            disabled={saveMutation.isPending || !isDirty}
            className="px-4 py-2 rounded-lg bg-ink hover:bg-ink2 disabled:opacity-40 disabled:cursor-not-allowed text-white text-sm font-medium transition-colors"
          >
            {saveMutation.isPending ? 'Saving…' : 'Save Settings'}
          </button>

          {existing && (
            <button
              type="button"
              onClick={() => setupMutation.mutate(existing.accountNumber)}
              disabled={setupMutation.isPending}
              className="px-4 py-2 rounded-lg border border-border-card hover:bg-bg-surface disabled:opacity-40 text-ink2 text-sm transition-colors"
            >
              {setupMutation.isPending ? 'Setting up…' : 'Setup / Re-discover Device'}
            </button>
          )}
        </div>

        {setupMutation.isSuccess && (
          <div className="rounded-lg border border-success/30 bg-success-bg px-3 py-2 text-xs text-success">
            Device setup complete: {setupMutation.data?.message ?? setupMutation.data?.deviceId}
          </div>
        )}
        {setupMutation.isError && (
          <ErrorAlert message="Device setup failed. Check your account number and try again." />
        )}
      </form>
    </div>
  )
}

function inputCls(hasError: boolean) {
  return `w-full rounded-lg border ${hasError ? 'border-danger/50' : 'border-border-subtle'} bg-bg-base px-3 py-2 text-sm text-ink placeholder:text-ink3 focus:outline-none focus:border-primary/50 transition-colors`
}

function Field({
  label,
  hint,
  error,
  children,
}: {
  label: string
  hint?: string
  error?: string
  children: React.ReactNode
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label className="text-xs font-medium text-ink2">{label}</label>
      {children}
      {hint && <p className="text-[11px] text-ink3">{hint}</p>}
      {error && <p className="text-[11px] text-danger">{error}</p>}
    </div>
  )
}
