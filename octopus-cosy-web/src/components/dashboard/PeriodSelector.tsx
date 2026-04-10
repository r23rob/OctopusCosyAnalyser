import { ChevronLeft, ChevronRight } from 'lucide-react'
import type { PeriodType } from '@/lib/utils'

const PERIODS: { type: PeriodType; label: string }[] = [
  { type: 'day', label: 'Day' },
  { type: 'week', label: 'Week' },
  { type: 'month', label: 'Month' },
  { type: 'year', label: 'Year' },
]

interface Props {
  periodType: PeriodType
  onPeriodChange: (type: PeriodType) => void
  label: string
  subtitle: string
  onPrev: () => void
  onNext: () => void
  canGoNext: boolean
}

export function PeriodSelector({ periodType, onPeriodChange, label, subtitle, onPrev, onNext, canGoNext }: Props) {
  return (
    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-5 animate-up">
      <div>
        <h1 className="text-[28px] font-semibold tracking-tight">Overview</h1>
        <div className="font-mono text-[12px] text-ink3 tracking-[.05em] uppercase mt-[3px]">{subtitle}</div>
      </div>

      <div className="flex items-center gap-2.5">
        {/* Period pills */}
        <div className="flex bg-white border border-border-card rounded-[9px] p-[3px] gap-0.5">
          {PERIODS.map(({ type, label: lbl }) => (
            <button
              key={type}
              onClick={() => onPeriodChange(type)}
              className={`font-mono text-[12px] tracking-[.07em] uppercase px-[16px] py-[7px] rounded-[6px] border-none cursor-pointer transition-all duration-150 ${
                periodType === type
                  ? 'bg-ink text-white'
                  : 'bg-transparent text-ink3 hover:text-ink'
              }`}
            >
              {lbl}
            </button>
          ))}
        </div>

        {/* Date navigation */}
        <div className="flex items-center gap-1.5">
          <button
            onClick={onPrev}
            className="w-[36px] h-[36px] border border-border-card rounded-[6px] bg-white cursor-pointer flex items-center justify-center text-ink2 hover:bg-bg-surface hover:text-ink transition-all duration-100"
          >
            <ChevronLeft size={15} />
          </button>
          <div className="font-mono text-[13px] text-ink2 min-w-[170px] text-center tracking-[.02em]">
            {label}
          </div>
          <button
            onClick={onNext}
            disabled={!canGoNext}
            className="w-[36px] h-[36px] border border-border-card rounded-[6px] bg-white cursor-pointer flex items-center justify-center text-ink2 hover:bg-bg-surface hover:text-ink transition-all duration-100 disabled:opacity-30 disabled:cursor-default"
          >
            <ChevronRight size={15} />
          </button>
        </div>
      </div>
    </div>
  )
}
