import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ChevronRight,
  Database,
  ExternalLink,
  Eye,
  EyeOff,
  Radio,
  RefreshCw,
  Settings,
  Zap,
} from 'lucide-react'

import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'
import { cn, formatDateTime } from '@/lib/utils'
import { useDevice } from '@/hooks/use-device'
import { useFeatures } from '@/hooks/use-features'
import { ErrorAlert } from '@/components/shared/ErrorAlert'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'

// ── Form schema ──────────────────────────────────────────────────────

const settingsSchema = z
  .object({
    accountNumber: z.string().min(1, 'Account number is required'),
    authMode: z.enum(['apikey', 'password']),
    apiKey: z.string().optional(),
    email: z.string().optional(),
    octopusPassword: z.string().optional(),
    anthropicApiKey: z.string().optional(),
    isExisting: z.boolean().optional(),
  })
  .superRefine((data, ctx) => {
    if (data.authMode === 'apikey') {
      if (!data.isExisting && (!data.apiKey || data.apiKey.trim().length === 0)) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'API key is required', path: ['apiKey'] })
      }
    } else {
      if (!data.email || data.email.trim().length === 0) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Email is required', path: ['email'] })
      } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(data.email)) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Valid email required', path: ['email'] })
      }
      if (!data.isExisting && (!data.octopusPassword || data.octopusPassword.trim().length === 0)) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Password is required', path: ['octopusPassword'] })
      }
    }
  })

type SettingsFormValues = z.infer<typeof settingsSchema>

// ── Main component ───────────────────────────────────────────────────

export function SettingsPage() {
  const [editing, setEditing] = useState(false)

  return (
    <div className="max-w-xl mx-auto px-4 py-6 flex flex-col gap-6 animate-up">
      <h1 className="text-xl font-semibold tracking-tight text-ink">Settings</h1>

      <AccountSection editing={editing} onToggleEdit={() => setEditing((p) => !p)} />
      <DeviceSection />
      <SystemStatusSection />
      <AboutSection />
    </div>
  )
}

// ── Section heading ──────────────────────────────────────────────────

function SectionHeading({ children }: { children: React.ReactNode }) {
  return (
    <h2 className="text-[11px] font-semibold text-ink3 uppercase tracking-widest mb-2">
      {children}
    </h2>
  )
}

// ── Card wrapper ─────────────────────────────────────────────────────

function Card({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={cn('rounded-[var(--radius-lg)] border border-border-subtle bg-white p-4', className)}>
      {children}
    </div>
  )
}

// ── Account section ──────────────────────────────────────────────────

function AccountSection({ editing, onToggleEdit }: { editing: boolean; onToggleEdit: () => void }) {
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
  } = useForm<SettingsFormValues>({
    resolver: zodResolver(settingsSchema),
    values: existing
      ? {
          accountNumber: existing.accountNumber,
          authMode: existing.authMode ?? 'apikey',
          apiKey: '',
          email: existing.email ?? '',
          octopusPassword: '',
          anthropicApiKey: '',
          isExisting: true,
        }
      : {
          accountNumber: '',
          authMode: 'apikey',
          apiKey: '',
          email: '',
          octopusPassword: '',
          anthropicApiKey: '',
          isExisting: false,
        },
  })

  const authMode = useWatch({ control, name: 'authMode' })

  const saveMutation = useMutation({
    mutationFn: (values: SettingsFormValues) =>
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
        apiKey: '',
        email: saved?.email ?? '',
        octopusPassword: '',
        anthropicApiKey: '',
        isExisting: true,
      })
      onToggleEdit()
    },
  })

  const onSubmit = handleSubmit((values) => saveMutation.mutate(values))

  if (settingsQuery.isLoading) {
    return (
      <section>
        <SectionHeading>Account</SectionHeading>
        <Card className="flex items-center justify-center py-8">
          <LoadingSpinner label="Loading settings..." />
        </Card>
      </section>
    )
  }

  // View mode
  if (!editing && existing) {
    return (
      <section>
        <SectionHeading>Account</SectionHeading>
        <Card>
          <div className="flex flex-col gap-3">
            <Row label="Account" value={existing.accountNumber} />
            <Row label="Auth" value={existing.authMode === 'password' ? 'Email & Password' : 'API Key'} />
            {existing.authMode === 'apikey' && (
              <Row label="API Key" value={existing.hasApiKey ? 'Configured' : 'Not set'} mono />
            )}
            {existing.authMode === 'password' && (
              <>
                <Row label="Email" value={existing.email ?? 'Not set'} />
                <Row label="Password" value={existing.hasOctopusPassword ? 'Configured' : 'Not set'} mono />
              </>
            )}
            <Row label="Anthropic" value={existing.hasAnthropicApiKey ? 'Configured' : 'Not set'} mono />

            <div className="pt-1">
              <button
                type="button"
                onClick={onToggleEdit}
                className="flex items-center gap-2 min-h-[44px] px-4 py-2.5 rounded-[var(--radius-md)] border border-border-subtle text-sm font-medium text-ink2 hover:bg-bg-surface transition-colors"
              >
                <Settings size={14} />
                Update Credentials
              </button>
            </div>
          </div>
        </Card>
      </section>
    )
  }

  // Edit mode (or no settings yet)
  return (
    <section>
      <SectionHeading>Account</SectionHeading>
      <Card>
        {saveMutation.isError && (
          <ErrorAlert
            message="Failed to save settings. Please check your credentials and try again."
            className="mb-4"
          />
        )}

        <form onSubmit={onSubmit} className="flex flex-col gap-4">
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
            <div className="flex rounded-[var(--radius-md)] border border-border-subtle overflow-hidden">
              <button
                type="button"
                onClick={() => setValue('authMode', 'apikey', { shouldDirty: true })}
                className={cn(
                  'flex-1 min-h-[44px] px-3 py-2.5 text-sm font-medium transition-colors',
                  authMode === 'apikey' ? 'bg-ink text-white' : 'bg-bg-base text-ink2 hover:bg-bg-surface',
                )}
              >
                API Key
              </button>
              <button
                type="button"
                onClick={() => setValue('authMode', 'password', { shouldDirty: true })}
                className={cn(
                  'flex-1 min-h-[44px] px-3 py-2.5 text-sm font-medium transition-colors',
                  authMode === 'password' ? 'bg-ink text-white' : 'bg-bg-base text-ink2 hover:bg-bg-surface',
                )}
              >
                Email & Password
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
              <PasswordInput
                {...register('apiKey')}
                placeholder={existing?.hasApiKey ? 'Stored securely -- leave blank to keep' : 'sk_live_...'}
                hasError={!!errors.apiKey}
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
              <Field label="Password" error={errors.octopusPassword?.message} hint="Re-enter each time you save.">
                <PasswordInput
                  {...register('octopusPassword')}
                  placeholder={
                    existing?.hasOctopusPassword
                      ? 'Stored securely -- leave blank to keep'
                      : 'Your Octopus account password'
                  }
                  hasError={!!errors.octopusPassword}
                />
              </Field>
            </>
          )}

          <div className="border-t border-border-subtle my-1" />

          <Field label="Anthropic API Key (optional)" hint="Required for AI analysis features">
            <PasswordInput
              {...register('anthropicApiKey')}
              placeholder={existing?.hasAnthropicApiKey ? 'Stored securely -- leave blank to keep' : 'sk-ant-...'}
              hasError={false}
            />
          </Field>

          <div className="flex gap-3 pt-1">
            <button
              type="submit"
              disabled={saveMutation.isPending || !isDirty}
              className="min-h-[44px] px-5 py-2.5 rounded-[var(--radius-md)] bg-ink hover:bg-ink2 disabled:opacity-40 disabled:cursor-not-allowed text-white text-sm font-medium transition-colors"
            >
              {saveMutation.isPending ? 'Saving...' : 'Save'}
            </button>
            {existing && (
              <button
                type="button"
                onClick={() => {
                  reset()
                  onToggleEdit()
                }}
                className="min-h-[44px] px-4 py-2.5 rounded-[var(--radius-md)] border border-border-subtle text-sm text-ink2 hover:bg-bg-surface transition-colors"
              >
                Cancel
              </button>
            )}
          </div>
        </form>
      </Card>
    </section>
  )
}

// ── Device section ───────────────────────────────────────────────────

function DeviceSection() {
  const queryClient = useQueryClient()
  const { device, settings, isLoading, hasDevice } = useDevice()

  const setupMutation = useMutation({
    mutationFn: (accountNumber: string) => api.devices.setup(accountNumber),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.devices.all() })
    },
  })

  if (isLoading) {
    return (
      <section>
        <SectionHeading>Device</SectionHeading>
        <Card className="flex items-center justify-center py-8">
          <LoadingSpinner label="Loading device..." />
        </Card>
      </section>
    )
  }

  return (
    <section>
      <SectionHeading>Device</SectionHeading>
      <Card>
        {hasDevice && device ? (
          <div className="flex flex-col gap-3">
            <Row label="Device ID" value={device.deviceId} mono />
            <Row label="Account" value={device.accountNumber} />
            {device.euid && <Row label="EUID" value={device.euid} mono />}
            {device.mpan && <Row label="MPAN" value={device.mpan} mono />}
            {device.lastSyncAt && (
              <Row label="Last sync" value={formatDateTime(device.lastSyncAt)} />
            )}
            <div className="flex items-center gap-1.5 pt-1">
              <StatusDot variant={device.isActive ? 'success' : 'default'} />
              <span className="text-xs text-ink2">
                {device.isActive ? 'Active' : 'Inactive'}
              </span>
            </div>
          </div>
        ) : (
          <div className="flex flex-col items-center gap-2 py-4 text-center">
            <Radio size={20} className="text-ink3" />
            <p className="text-sm text-ink2">No device registered</p>
            <p className="text-xs text-ink3">Run device setup to discover your heat pump.</p>
          </div>
        )}

        {settings && (
          <div className="pt-3 mt-3 border-t border-border-subtle">
            <button
              type="button"
              onClick={() => setupMutation.mutate(settings.accountNumber)}
              disabled={setupMutation.isPending}
              className="flex items-center gap-2 min-h-[44px] px-4 py-2.5 rounded-[var(--radius-md)] border border-border-subtle text-sm font-medium text-ink2 hover:bg-bg-surface disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              <RefreshCw size={14} className={cn(setupMutation.isPending && 'animate-spin')} />
              {setupMutation.isPending ? 'Discovering...' : hasDevice ? 'Re-discover Device' : 'Setup Device'}
            </button>

            {setupMutation.isSuccess && (
              <div className="mt-3 rounded-[var(--radius-sm)] border border-success/30 bg-success-bg px-3 py-2 text-xs text-success">
                Device setup complete: {setupMutation.data?.message ?? setupMutation.data?.deviceId}
              </div>
            )}
            {setupMutation.isError && (
              <ErrorAlert
                message="Device setup failed. Check your account number and credentials."
                className="mt-3"
              />
            )}
          </div>
        )}
      </Card>
    </section>
  )
}

// ── System status section ────────────────────────────────────────────

function SystemStatusSection() {
  const { features, isLoading: featuresLoading, hasDatabase } = useFeatures()
  const { device } = useDevice()

  const latestQuery = useQuery({
    queryKey: queryKeys.heatpump.latestSnapshot(device?.deviceId ?? ''),
    queryFn: () => api.heatpump.getLatestSnapshot(device!.deviceId),
    enabled: !!device,
    staleTime: 60_000,
  })

  const latest = latestQuery.data

  return (
    <section>
      <SectionHeading>System Status</SectionHeading>
      <Card>
        {featuresLoading ? (
          <LoadingSpinner label="Checking status..." />
        ) : (
          <div className="flex flex-col gap-3">
            <StatusRow
              icon={<Database size={14} />}
              label="Database"
              status={hasDatabase}
              detail={hasDatabase ? 'Connected' : 'Unavailable'}
            />
            {!hasDatabase && (
              <p className="text-xs text-ink3 pl-6 -mt-1">
                Running in live-only mode -- connect a database for historical data.
              </p>
            )}

            <StatusRow
              icon={<Zap size={14} />}
              label="Live data"
              status={features.liveData}
              detail={features.liveData ? 'Available' : 'Unavailable'}
            />

            <StatusRow
              icon={<Radio size={14} />}
              label="Snapshot worker"
              status={latest?.hasData ?? false}
              detail={
                latest?.hasData && latest.minutesAgo != null
                  ? `Last: ${Math.round(latest.minutesAgo)}m ago`
                  : 'No snapshots'
              }
            />
          </div>
        )}
      </Card>
    </section>
  )
}

// ── About section ────────────────────────────────────────────────────

function AboutSection() {
  return (
    <section>
      <SectionHeading>About</SectionHeading>
      <Card>
        <div className="flex flex-col gap-3">
          <div>
            <p className="text-sm font-semibold text-ink tracking-tight">Cosydays</p>
            <p className="text-xs text-ink3 mt-0.5">
              Heat pump monitoring for Octopus Energy Cosy customers
            </p>
          </div>

          <a
            href="https://github.com/r23rob/OctopusCosyAnalyser"
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center gap-2 min-h-[44px] px-4 py-2.5 rounded-[var(--radius-md)] border border-border-subtle text-sm text-ink2 hover:bg-bg-surface transition-colors w-fit"
          >
            <ExternalLink size={14} />
            View on GitHub
            <ChevronRight size={14} className="text-ink4" />
          </a>
        </div>
      </Card>
    </section>
  )
}

// ── Shared sub-components ────────────────────────────────────────────

function Row({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span className="text-xs text-ink3 flex-shrink-0">{label}</span>
      <span
        className={cn(
          'text-sm text-ink text-right truncate',
          mono && 'font-mono text-[13px]',
        )}
      >
        {value}
      </span>
    </div>
  )
}

function StatusRow({
  icon,
  label,
  status,
  detail,
}: {
  icon: React.ReactNode
  label: string
  status: boolean
  detail: string
}) {
  return (
    <div className="flex items-center gap-2">
      <span className="text-ink3">{icon}</span>
      <span className="text-sm text-ink flex-1">{label}</span>
      <StatusDot variant={status ? 'success' : 'default'} />
      <span className="text-xs text-ink2">{detail}</span>
    </div>
  )
}

function StatusDot({ variant }: { variant: 'success' | 'warning' | 'danger' | 'default' }) {
  const colors = {
    success: 'bg-success',
    warning: 'bg-warning',
    danger: 'bg-danger',
    default: 'bg-ink4',
  }
  return <span className={cn('w-2 h-2 rounded-full flex-shrink-0', colors[variant])} />
}

import { forwardRef } from 'react'

interface PasswordInputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  hasError: boolean
}

const PasswordInput = forwardRef<HTMLInputElement, PasswordInputProps>(
  function PasswordInput({ hasError, className, ...props }, ref) {
    const [visible, setVisible] = useState(false)

    return (
      <div className="relative">
        <input
          ref={ref}
          type={visible ? 'text' : 'password'}
          className={cn(inputCls(hasError), 'pr-10', className)}
          {...props}
        />
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          className="absolute right-2 top-1/2 -translate-y-1/2 p-1.5 text-ink3 hover:text-ink2 transition-colors"
          tabIndex={-1}
          aria-label={visible ? 'Hide password' : 'Show password'}
        >
          {visible ? <EyeOff size={14} /> : <Eye size={14} />}
        </button>
      </div>
    )
  },
)

function inputCls(hasError: boolean) {
  return cn(
    'w-full min-h-[44px] rounded-[var(--radius-md)] border bg-bg-base px-3 py-2.5 text-sm text-ink placeholder:text-ink3 focus:outline-none focus:border-primary/50 transition-colors',
    hasError ? 'border-danger/50' : 'border-border-subtle',
  )
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
