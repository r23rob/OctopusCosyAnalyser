import { useQuery } from '@tanstack/react-query'
import type { PeriodDays } from '@/components/dashboard/PeriodSelector'
import { CostBarChart } from '@/components/charts/CostBarChart'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { ErrorAlert } from '@/components/shared/ErrorAlert'
import { api } from '@/lib/api-client'
import { queryKeys } from '@/lib/query-keys'
import { fmtDec, fmtKwh, fmtPence, periodStart } from '@/lib/utils'
import { useDailyAggregates } from '@/hooks/use-period-data'

interface Props {
  deviceId: string
  accountNumber: string
  periodDays: PeriodDays
}

interface ParsedRate {
  start: string
  end: string
  ratePence: number
  paymentMethod: string
}

export function CostsTab({ deviceId, accountNumber, periodDays }: Props) {
  const from = periodStart(periodDays)
  const to = new Date()

  const { aggregates, isLoading: aggLoading, isError: aggError } = useDailyAggregates(deviceId, periodDays)

  // Rates data
  const ratesQuery = useQuery({
    queryKey: queryKeys.heatpump.rates(accountNumber, from.toISOString(), to.toISOString()),
    queryFn: () => api.heatpump.getRatesRaw(accountNumber, from, to),
    staleTime: 60 * 60_000,
    retry: false,
  })

  // Parse rates
  const rates = parseRates(ratesQuery.data)

  // Summary metrics from daily aggregates
  const withCost = aggregates.filter((a) => a.dailyCostPence != null)
  const totalCostPounds = withCost.reduce((s, a) => s + (a.dailyCostPence ?? 0), 0) / 100
  const totalKwh = aggregates.reduce((s, a) => s + (a.dailyUsageKwh ?? 0), 0)
  const avgDailyCostPounds = withCost.length > 0 ? totalCostPounds / withCost.length : null
  const avgUnitRate = aggregates.reduce((s, a) => s + (a.avgUnitRatePence ?? 0), 0) /
    aggregates.filter((a) => a.avgUnitRatePence != null).length || null

  const hasCostData = withCost.length > 0

  return (
    <div className="flex flex-col gap-5">
      {/* Summary metrics */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        <MetricCard
          label="Total Cost"
          value={hasCostData ? `£${totalCostPounds.toFixed(2)}` : '—'}
          sub={`${withCost.length} days with data`}
        />
        <MetricCard
          label="Total Usage"
          value={fmtKwh(totalKwh > 0 ? totalKwh : null)}
        />
        <MetricCard
          label="Daily Avg"
          value={avgDailyCostPounds != null ? `£${avgDailyCostPounds.toFixed(2)}` : '—'}
        />
        <MetricCard
          label="Avg Unit Rate"
          value={fmtPence(avgUnitRate)}
        />
      </div>

      {!hasCostData && !aggLoading && (
        <div className="rounded-lg border border-amber-500/20 bg-amber-500/[0.04] px-3 py-2 text-xs text-amber-300">
          Cost data is not yet available. It is populated automatically by the background worker once tariff data is available from Octopus.
        </div>
      )}

      {/* Daily cost chart */}
      {aggLoading && <LoadingSpinner label="Loading cost data…" />}
      {aggError && <ErrorAlert message="Failed to load aggregate data." />}
      {aggregates.length > 0 && (
        <ChartCard title="Daily Cost & Usage">
          <CostBarChart aggregates={aggregates} showCost={hasCostData} showUsage />
        </ChartCard>
      )}

      {/* Tariff rates table */}
      {ratesQuery.isLoading && <LoadingSpinner label="Loading tariff rates…" />}
      {rates.length > 0 && (
        <ChartCard title="Applicable Tariff Rates">
          <div className="overflow-x-auto max-h-64">
            <table className="w-full text-xs">
              <thead className="sticky top-0 bg-[#1e2130]">
                <tr className="border-b border-white/[0.06]">
                  <th className="text-left py-2 text-white/40 font-medium">Start</th>
                  <th className="text-left py-2 text-white/40 font-medium">End</th>
                  <th className="text-right py-2 text-white/40 font-medium">Rate (p/kWh)</th>
                  <th className="text-right py-2 text-white/40 font-medium">Payment</th>
                </tr>
              </thead>
              <tbody>
                {rates.slice(0, 50).map((r, i) => (
                  <tr key={i} className="border-b border-white/[0.04]">
                    <td className="py-1.5 text-white/60">{new Date(r.start).toLocaleDateString('en-GB')}</td>
                    <td className="py-1.5 text-white/60">{new Date(r.end).toLocaleDateString('en-GB')}</td>
                    <td className="py-1.5 text-right text-white/80">{fmtDec(r.ratePence, 4)}</td>
                    <td className="py-1.5 text-right text-white/50">{r.paymentMethod}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ChartCard>
      )}
    </div>
  )
}

function parseRates(raw: string | undefined): ParsedRate[] {
  if (!raw) return []
  try {
    const data = JSON.parse(raw) as Record<string, unknown>
    const root = data['data'] as Record<string, unknown> | null | undefined
    if (!root) return []
    const account = root['account'] as Record<string, unknown> | null | undefined
    if (!account) return []
    const agreements = (account['electricityAgreements'] as Record<string, unknown>[] | null) ?? []
    const rates: ParsedRate[] = []
    for (const ag of agreements) {
      const tariff = ag['tariff'] as Record<string, unknown> | null | undefined
      if (!tariff) continue
      const unitRates = (tariff['unitRates'] as Record<string, unknown>[] | null) ?? []
      for (const ur of unitRates) {
        const edges = (ur['edges'] as Record<string, unknown>[] | null) ?? []
        for (const edge of edges) {
          const node = edge['node'] as Record<string, unknown> | null | undefined
          if (!node) continue
          rates.push({
            start: String(node['validFrom'] ?? ''),
            end: String(node['validTo'] ?? ''),
            ratePence: Number(node['unitRateGrossInPence'] ?? 0),
            paymentMethod: String(node['paymentMethod'] ?? ''),
          })
        }
      }
    }
    return rates
  } catch {
    return []
  }
}

function MetricCard({ label, value, sub }: { label: string; value: string; sub?: string }) {
  return (
    <div className="rounded-lg border border-white/[0.06] bg-white/[0.03] px-3 py-2.5">
      <div className="text-[10px] text-white/40 uppercase tracking-wide mb-1">{label}</div>
      <div className="text-base font-bold text-white/90">{value}</div>
      {sub && <div className="text-[10px] text-white/30 mt-0.5">{sub}</div>}
    </div>
  )
}

function ChartCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-lg border border-white/[0.06] bg-white/[0.02]">
      <div className="px-3 py-2 border-b border-white/[0.06]">
        <span className="text-xs font-medium text-white/60">{title}</span>
      </div>
      <div className="p-3">{children}</div>
    </div>
  )
}
