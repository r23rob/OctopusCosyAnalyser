import { createFileRoute } from '@tanstack/react-router'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'
import { ErrorAlert } from '@/components/shared/ErrorAlert'

export const Route = createFileRoute('/settings')({
  component: SettingsPage,
})

const schema = z.object({
  accountNumber: z.string().min(1, 'Account number is required'),
  apiKey: z.string().min(1, 'API key is required'),
  anthropicApiKey: z.string().optional(),
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
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: existing
      ? {
          accountNumber: existing.accountNumber,
          apiKey: existing.apiKey,
          anthropicApiKey: existing.anthropicApiKey ?? '',
        }
      : undefined,
  })

  const saveMutation = useMutation({
    mutationFn: (values: FormValues) =>
      api.settings.upsert({
        accountNumber: values.accountNumber,
        apiKey: values.apiKey,
        anthropicApiKey: values.anthropicApiKey || null,
      }),
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKeys.settings.all(), saved ? [saved] : [])
      reset({
        accountNumber: saved?.accountNumber ?? '',
        apiKey: saved?.apiKey ?? '',
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
      <h1 className="text-lg font-semibold text-white/90 mb-1">Account Settings</h1>
      <p className="text-sm text-white/50 mb-6">
        Your Octopus Energy credentials for accessing the heat pump API.
      </p>

      {saveMutation.isError && (
        <ErrorAlert message="Failed to save settings. Please check your credentials and try again." className="mb-4" />
      )}
      {saveMutation.isSuccess && !isDirty && (
        <div className="mb-4 rounded-lg border border-green-500/30 bg-green-500/10 px-3 py-2 text-xs text-green-300">
          Settings saved successfully.
        </div>
      )}

      <form onSubmit={onSubmit} className="flex flex-col gap-4">
        <div className="rounded-xl border border-white/[0.08] bg-[#1e2130] p-5 flex flex-col gap-4">
          <h2 className="text-sm font-medium text-white/70">Octopus Energy</h2>

          <Field label="Account Number" error={errors.accountNumber?.message}>
            <input
              {...register('accountNumber')}
              placeholder="A-XXXXXXXX"
              className={inputCls(!!errors.accountNumber)}
            />
          </Field>

          <Field label="API Key" error={errors.apiKey?.message}>
            <input
              {...register('apiKey')}
              type="password"
              placeholder="sk_live_..."
              className={inputCls(!!errors.apiKey)}
            />
          </Field>
        </div>

        <div className="rounded-xl border border-white/[0.08] bg-[#1e2130] p-5 flex flex-col gap-4">
          <h2 className="text-sm font-medium text-white/70">AI Features (optional)</h2>
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
            className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 disabled:opacity-40 disabled:cursor-not-allowed text-white text-sm font-medium transition-colors"
          >
            {saveMutation.isPending ? 'Saving…' : 'Save Settings'}
          </button>

          {existing && (
            <button
              type="button"
              onClick={() => setupMutation.mutate(existing.accountNumber)}
              disabled={setupMutation.isPending}
              className="px-4 py-2 rounded-lg border border-white/10 hover:bg-white/[0.06] disabled:opacity-40 text-white/70 text-sm transition-colors"
            >
              {setupMutation.isPending ? 'Setting up…' : 'Setup / Re-discover Device'}
            </button>
          )}
        </div>

        {setupMutation.isSuccess && (
          <div className="rounded-lg border border-green-500/30 bg-green-500/10 px-3 py-2 text-xs text-green-300">
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
  return `w-full rounded-lg border ${hasError ? 'border-red-500/50' : 'border-white/10'} bg-white/[0.04] px-3 py-2 text-sm text-white/85 placeholder:text-white/25 focus:outline-none focus:border-blue-500/50 transition-colors`
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
      <label className="text-xs font-medium text-white/60">{label}</label>
      {children}
      {hint && <p className="text-[11px] text-white/35">{hint}</p>}
      {error && <p className="text-[11px] text-red-400">{error}</p>}
    </div>
  )
}
