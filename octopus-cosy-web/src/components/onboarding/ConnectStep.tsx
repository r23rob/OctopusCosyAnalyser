import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { api } from '@/lib/api-client'
import { ErrorAlert } from '@/components/shared/ErrorAlert'

const schema = z.object({
  accountNumber: z.string().min(1, 'Account number is required'),
  apiKey: z.string().min(1, 'API key is required'),
})

type FormValues = z.infer<typeof schema>

interface Props {
  onNext: (accountNumber: string) => void
  onBack: () => void
}

export function ConnectStep({ onNext, onBack }: Props) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { accountNumber: '', apiKey: '' },
  })

  const saveMutation = useMutation({
    mutationFn: (values: FormValues) =>
      api.settings.upsert({
        accountNumber: values.accountNumber,
        apiKey: values.apiKey,
        authMode: 'apikey',
      }),
    onSuccess: (_data, variables) => {
      onNext(variables.accountNumber)
    },
  })

  const onSubmit = handleSubmit((values) => saveMutation.mutate(values))

  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-bg-base px-4 sm:px-6">
      <div className="w-full max-w-md">
        {/* Back button */}
        <button
          type="button"
          onClick={onBack}
          className="mb-6 flex items-center gap-1.5 text-sm text-ink2 hover:text-ink transition-colors min-h-[44px]"
        >
          <ArrowLeft size={16} />
          Back
        </button>

        {/* Heading */}
        <h2 className="text-xl font-semibold tracking-tight text-ink mb-1">
          Connect your account
        </h2>
        <p className="text-sm text-ink2 mb-6">
          Enter your Octopus Energy credentials to get started.
        </p>

        {saveMutation.isError && (
          <ErrorAlert
            message="Failed to save credentials. Please check your details and try again."
            className="mb-4"
          />
        )}

        <form onSubmit={onSubmit} className="flex flex-col gap-4">
          <div className="rounded-[10px] border border-border-subtle bg-white p-5 flex flex-col gap-4">
            {/* Account Number */}
            <div className="flex flex-col gap-1.5">
              <label htmlFor="accountNumber" className="text-xs font-medium text-ink2">
                Account Number
              </label>
              <input
                id="accountNumber"
                {...register('accountNumber')}
                placeholder="A-XXXXXXXX"
                className={inputCls(!!errors.accountNumber)}
              />
              {errors.accountNumber && (
                <p className="text-[11px] text-danger">{errors.accountNumber.message}</p>
              )}
            </div>

            {/* API Key */}
            <div className="flex flex-col gap-1.5">
              <label htmlFor="apiKey" className="text-xs font-medium text-ink2">
                API Key
              </label>
              <input
                id="apiKey"
                {...register('apiKey')}
                type="password"
                placeholder="sk_live_..."
                className={inputCls(!!errors.apiKey)}
              />
              <p className="text-[11px] text-ink3">
                Find this at{' '}
                <span className="font-mono text-[11px]">
                  octopus.energy/dashboard/developer-settings
                </span>
              </p>
              {errors.apiKey && (
                <p className="text-[11px] text-danger">{errors.apiKey.message}</p>
              )}
            </div>
          </div>

          {/* Submit */}
          <button
            type="submit"
            disabled={saveMutation.isPending}
            className="w-full min-h-[44px] rounded-lg bg-purple hover:bg-purple-deep disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-semibold transition-colors"
          >
            {saveMutation.isPending ? 'Connecting...' : 'Connect'}
          </button>
        </form>
      </div>
    </div>
  )
}

function inputCls(hasError: boolean) {
  return `w-full rounded-lg border ${hasError ? 'border-danger/50' : 'border-border-subtle'} bg-bg-base px-3 py-2.5 min-h-[44px] text-sm text-ink placeholder:text-ink3 focus:outline-none focus:border-purple/50 transition-colors`
}
