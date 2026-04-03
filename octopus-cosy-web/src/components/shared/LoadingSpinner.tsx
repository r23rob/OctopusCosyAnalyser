import { cn } from '@/lib/utils'

interface Props {
  className?: string
  size?: 'sm' | 'md' | 'lg'
  label?: string
}

export function LoadingSpinner({ className, size = 'md', label }: Props) {
  const sizes = { sm: 'w-3 h-3 border-2', md: 'w-5 h-5 border-2', lg: 'w-8 h-8 border-2' }
  return (
    <div className={cn('flex items-center gap-2', className)}>
      <span
        className={cn(
          'rounded-full border-white/20 border-t-white/60 animate-spin',
          sizes[size],
        )}
      />
      {label && <span className="text-xs text-white/40">{label}</span>}
    </div>
  )
}
