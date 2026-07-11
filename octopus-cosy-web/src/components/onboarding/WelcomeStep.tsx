interface Props {
  onNext: () => void
}

export function WelcomeStep({ onNext }: Props) {
  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-gradient-to-b from-aurora-deep via-aurora-mid to-aurora-light px-6 text-center">
      <div className="flex flex-col items-center gap-6 max-w-sm">
        {/* Wordmark */}
        <h1 className="text-4xl sm:text-5xl font-semibold tracking-tight text-white">
          cosydays
        </h1>

        {/* Tagline */}
        <p className="text-base sm:text-lg text-white/70">
          Your heat pump companion
        </p>

        {/* CTA */}
        <button
          type="button"
          onClick={onNext}
          className="mt-8 px-8 py-3 min-h-[44px] rounded-full bg-white text-purple font-semibold text-sm sm:text-base transition-colors hover:bg-white/90 active:bg-white/80"
        >
          Get Started
        </button>
      </div>
    </div>
  )
}
