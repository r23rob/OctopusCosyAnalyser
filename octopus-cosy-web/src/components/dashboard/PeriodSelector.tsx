import { cn } from '@/lib/utils'

export type PeriodDays = 1 | 7 | 30 | 365

const PERIODS: { days: PeriodDays; label: string }[] = [
  { days: 1, label: '1D' },
  { days: 7, label: '1W' },
  { days: 30, label: '1M' },
  { days: 365, label: '1Y' },
]

interface Props {
  value: PeriodDays
  onChange: (days: PeriodDays) => void
  loading?: boolean
}

export function PeriodSelector({ value, onChange, loading = false }: Props) {
  return (
    <div className="flex items-center gap-1">
      {loading && (
        <span className="w-3 h-3 rounded-full border-2 border-white/20 border-t-white/60 animate-spin mr-1" />
      )}
      {PERIODS.map(({ days, label }) => (
        <button
          key={days}
          onClick={() => onChange(days)}
          className={cn(
            'px-2.5 py-1 rounded text-xs font-medium transition-colors',
            value === days
              ? 'bg-blue-500 text-white'
              : 'text-white/50 hover:text-white/80 hover:bg-white/[0.06]',
          )}
        >
          {label}
        </button>
      ))}
    </div>
  )
}
