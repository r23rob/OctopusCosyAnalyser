import { cn } from '@/lib/utils'

interface Props {
  from: Date
  to: Date
  onChange: (from: Date, to: Date) => void
  className?: string
}

function toInputValue(d: Date): string {
  // datetime-local input expects "YYYY-MM-DDTHH:mm"
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function DateRangePicker({ from, to, onChange, className }: Props) {
  return (
    <div className={cn('flex items-center gap-2 text-xs', className)}>
      <label className="text-white/40">From</label>
      <input
        type="datetime-local"
        value={toInputValue(from)}
        onChange={(e) => {
          const d = new Date(e.target.value)
          if (!isNaN(d.getTime())) onChange(d, to)
        }}
        className="rounded border border-white/10 bg-white/[0.04] px-2 py-1 text-white/80 text-xs focus:outline-none focus:border-blue-500/50"
      />
      <label className="text-white/40">To</label>
      <input
        type="datetime-local"
        value={toInputValue(to)}
        onChange={(e) => {
          const d = new Date(e.target.value)
          if (!isNaN(d.getTime())) onChange(from, d)
        }}
        className="rounded border border-white/10 bg-white/[0.04] px-2 py-1 text-white/80 text-xs focus:outline-none focus:border-blue-500/50"
      />
    </div>
  )
}
