import { createFileRoute } from '@tanstack/react-router'
import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { api } from '@/lib/api-client'
import { useDevice } from '@/hooks/use-device'
import { MarkdownRenderer } from '@/components/shared/MarkdownRenderer'
import { ErrorAlert } from '@/components/shared/ErrorAlert'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { periodStart } from '@/lib/utils'

export const Route = createFileRoute('/heatpump/ai-analysis')({
  component: AiAnalysisPage,
})

interface FormValues {
  from: string
  to: string
  question: string
}

function toInputDate(d: Date): string {
  return d.toISOString().slice(0, 10)
}

function AiAnalysisPage() {
  const { device } = useDevice()
  const [result, setResult] = useState<{ analysis: string; meta: string } | null>(null)

  const defaultFrom = periodStart(30)
  const defaultTo = new Date()

  const { register, handleSubmit } = useForm<FormValues>({
    defaultValues: {
      from: toInputDate(defaultFrom),
      to: toInputDate(defaultTo),
      question: '',
    },
  })

  const analysisMutation = useMutation({
    mutationFn: (values: FormValues) =>
      api.heatpump.getAiAnalysis(device!.deviceId, {
        from: new Date(values.from).toISOString(),
        to: new Date(values.to).toISOString(),
        question: values.question || null,
      }),
    onSuccess: (data) => {
      const meta = `${data.daysAnalysed} days · ${data.totalSnapshots} snapshots · ${data.totalTimeSeriesRecords} time-series records`
      setResult({ analysis: data.analysis, meta })
    },
  })

  const onSubmit = handleSubmit((values) => {
    setResult(null)
    analysisMutation.mutate(values)
  })

  return (
    <div className="max-w-3xl">
      <h1 className="text-lg font-semibold text-white/90 mb-1">AI Analysis</h1>
      <p className="text-sm text-white/50 mb-6">
        Ask Claude to analyse your heat pump data for a specific date range.
        This may take up to a few minutes.
      </p>

      {!device && (
        <ErrorAlert message="No heat pump device registered. Go to Settings to set up your device first." className="mb-4" />
      )}

      <form onSubmit={onSubmit} className="flex flex-col gap-4">
        <div className="rounded-xl border border-white/[0.08] bg-[#1e2130] p-5 flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-white/60">From</label>
              <input
                type="date"
                {...register('from')}
                className="rounded-lg border border-white/10 bg-white/[0.04] px-3 py-2 text-sm text-white/85 focus:outline-none focus:border-blue-500/50"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-white/60">To</label>
              <input
                type="date"
                {...register('to')}
                className="rounded-lg border border-white/10 bg-white/[0.04] px-3 py-2 text-sm text-white/85 focus:outline-none focus:border-blue-500/50"
              />
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <label className="text-xs font-medium text-white/60">
              Custom question <span className="text-white/30">(optional)</span>
            </label>
            <textarea
              {...register('question')}
              placeholder="e.g. Is my weather compensation curve optimised for the current outdoor temperatures?"
              rows={3}
              className="rounded-lg border border-white/10 bg-white/[0.04] px-3 py-2 text-sm text-white/85 placeholder:text-white/25 focus:outline-none focus:border-blue-500/50 resize-none"
            />
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            type="submit"
            disabled={analysisMutation.isPending || !device}
            className="px-4 py-2 rounded-lg bg-violet-600 hover:bg-violet-500 disabled:opacity-40 disabled:cursor-not-allowed text-white text-sm font-medium transition-colors"
          >
            {analysisMutation.isPending ? 'Analysing…' : 'Run Analysis'}
          </button>
          {analysisMutation.isPending && (
            <LoadingSpinner label="This can take a few minutes…" />
          )}
        </div>
      </form>

      {analysisMutation.isError && (
        <ErrorAlert
          message="Analysis failed. Check your Anthropic API key is configured in Settings."
          className="mt-4"
        />
      )}

      {result && (
        <div className="mt-6 rounded-xl border border-violet-500/20 bg-violet-500/[0.04]">
          <div className="px-4 py-3 border-b border-white/[0.06] flex items-center justify-between">
            <span className="text-sm font-medium text-violet-300">Analysis Result</span>
            <span className="text-[11px] text-white/30">{result.meta}</span>
          </div>
          <div className="p-4">
            <MarkdownRenderer>{result.analysis}</MarkdownRenderer>
          </div>
        </div>
      )}
    </div>
  )
}
