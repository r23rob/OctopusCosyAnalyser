import { cn } from '@/lib/utils'

interface Props {
  color: string
  label: string
  active: boolean
  onToggle: () => void
}

export function SeriesToggle({ color, label, active, onToggle }: Props) {
  return (
    <button
      onClick={onToggle}
      className={cn(
        'flex items-center gap-1.5 px-2 py-1 rounded text-xs transition-opacity',
        active ? 'opacity-100' : 'opacity-40',
      )}
    >
      <span className="w-2 h-2 rounded-full flex-shrink-0" style={{ background: color }} />
      <span className="text-white/70">{label}</span>
    </button>
  )
}
