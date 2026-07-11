import { useNavigate } from '@tanstack/react-router'

export function DoneStep() {
  const navigate = useNavigate()

  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-gradient-to-b from-aurora-deep via-aurora-mid to-aurora-light px-6 text-center">
      <div className="flex flex-col items-center gap-6 max-w-sm">
        {/* Checkmark */}
        <div className="w-16 h-16 rounded-full bg-white/15 flex items-center justify-center">
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth={2.5}
            strokeLinecap="round"
            strokeLinejoin="round"
            className="w-8 h-8 text-white"
          >
            <polyline points="20 6 9 17 4 12" />
          </svg>
        </div>

        {/* Heading */}
        <div>
          <h2 className="text-3xl sm:text-4xl font-semibold tracking-tight text-white mb-2">
            You're all set!
          </h2>
          <p className="text-base text-white/70">
            Your heat pump is connected and data collection has started.
          </p>
        </div>

        {/* CTA */}
        <button
          type="button"
          onClick={() => navigate({ to: '/' })}
          className="mt-6 px-8 py-3 min-h-[44px] rounded-full bg-white text-purple font-semibold text-sm sm:text-base transition-colors hover:bg-white/90 active:bg-white/80"
        >
          Go to Dashboard
        </button>
      </div>
    </div>
  )
}
