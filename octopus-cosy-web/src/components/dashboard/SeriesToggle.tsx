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
      className={`flex items-center gap-1 px-[7px] py-[3px] border rounded-full cursor-pointer font-mono text-[7.5px] tracking-[.06em] uppercase transition-all duration-150 select-none ${
        active
          ? 'bg-white text-ink2 border-border-subtle hover:border-border-card hover:text-ink'
          : 'opacity-[0.22] bg-white text-ink2 border-border-subtle'
      }`}
    >
      <span
        className="w-[7px] h-[2.5px] rounded-sm flex-shrink-0"
        style={{ background: color }}
      />
      {label}
    </button>
  )
}
