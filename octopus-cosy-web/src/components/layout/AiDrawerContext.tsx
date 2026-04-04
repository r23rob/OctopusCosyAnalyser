import { createContext, useContext, useState, useCallback, type ReactNode } from 'react'

interface AiDrawerState {
  open: boolean
  toggle: () => void
  close: () => void
}

const AiDrawerContext = createContext<AiDrawerState | null>(null)

export function AiDrawerProvider({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false)
  const toggle = useCallback(() => setOpen(o => !o), [])
  const close = useCallback(() => setOpen(false), [])

  return (
    <AiDrawerContext.Provider value={{ open, toggle, close }}>
      {children}
    </AiDrawerContext.Provider>
  )
}

export function useAiDrawer() {
  const ctx = useContext(AiDrawerContext)
  if (!ctx) throw new Error('useAiDrawer must be used within AiDrawerProvider')
  return ctx
}
