import { useEffect, useState } from 'react'
import { X } from 'lucide-react'

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>
}

const DISMISSED_KEY = 'install-dismissed'

function readDismissed(): boolean {
  if (typeof window === 'undefined') return false
  try {
    return window.localStorage.getItem(DISMISSED_KEY) === '1'
  } catch {
    return false
  }
}

/** PWA install banner driven by the `beforeinstallprompt` event.
 *  Hidden once dismissed (persisted in localStorage) or when running standalone. */
export function InstallBanner() {
  const [deferred, setDeferred] = useState<BeforeInstallPromptEvent | null>(null)
  const [dismissed, setDismissed] = useState(readDismissed)
  const [standalone, setStandalone] = useState(() =>
    typeof window !== 'undefined' && window.matchMedia('(display-mode: standalone)').matches,
  )

  useEffect(() => {
    const onPrompt = (e: Event) => {
      e.preventDefault()
      setDeferred(e as BeforeInstallPromptEvent)
    }
    const onInstalled = () => {
      setDeferred(null)
      setStandalone(true)
    }
    window.addEventListener('beforeinstallprompt', onPrompt)
    window.addEventListener('appinstalled', onInstalled)
    return () => {
      window.removeEventListener('beforeinstallprompt', onPrompt)
      window.removeEventListener('appinstalled', onInstalled)
    }
  }, [])

  if (!deferred || dismissed || standalone) return null

  const onInstall = () => {
    deferred.prompt()
    deferred.userChoice.then(() => setDeferred(null))
  }
  const onDismiss = () => {
    try {
      window.localStorage.setItem(DISMISSED_KEY, '1')
    } catch {
      // Storage blocked (e.g. Safari private mode) — still hide for this session.
    }
    setDismissed(true)
  }

  return (
    <div className="bg-ink text-white rounded-[10px] px-[18px] py-[14px] mb-4 flex items-center gap-3.5 animate-up">
      <div className="w-8 h-8 rounded-[7px] bg-white/[0.06] flex items-center justify-center flex-shrink-0">
        <svg width="14" height="14" viewBox="0 0 11 11" fill="none">
          <path d="M5.5.5C4 2 2 3.3 2 6.3C2 8.8 3.7 10.5 5.5 10.5C7.3 10.5 9 8.8 9 6.3C9 3.3 7 2 5.5.5Z" fill="white" opacity=".88" />
          <circle cx="5.5" cy="7" r="1.3" fill="white" opacity=".38" />
        </svg>
      </div>
      <div className="flex-1 min-w-0">
        <div className="text-[14px] font-medium">Install Octopus Heat Pump on your desktop</div>
        <div className="text-[13px] text-white/55 mt-[1px]">Open faster, get a status pill in your dock, and use it offline.</div>
      </div>
      <button
        onClick={onInstall}
        className="px-3.5 py-2 rounded-[7px] border border-white/20 bg-white/[0.08] text-white text-[13px] font-medium cursor-pointer hover:bg-white/[0.14] transition-colors flex-shrink-0"
      >
        Install
      </button>
      <button
        onClick={onDismiss}
        className="bg-transparent border-none text-white/50 cursor-pointer p-1 hover:text-white transition-colors flex-shrink-0"
        aria-label="Dismiss"
      >
        <X size={15} />
      </button>
    </div>
  )
}
