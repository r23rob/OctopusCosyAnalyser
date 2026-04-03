import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

// ── Date helpers ──────────────────────────────────────────────────────

export function periodStart(days: number): Date {
  const d = new Date()
  d.setDate(d.getDate() - days)
  return d
}

export function toIsoString(d: Date): string {
  return d.toISOString()
}

export function formatDate(d: Date | string): string {
  const date = typeof d === 'string' ? new Date(d) : d
  return date.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

export function formatDateTime(d: Date | string): string {
  const date = typeof d === 'string' ? new Date(d) : d
  return date.toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
}

export function formatTime(d: Date | string): string {
  const date = typeof d === 'string' ? new Date(d) : d
  return date.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
}

export function shortDate(d: Date | string): string {
  const date = typeof d === 'string' ? new Date(d) : d
  return date.toLocaleDateString('en-GB', { day: '2-digit', month: 'short' })
}

// ── Number formatting ─────────────────────────────────────────────────

export function fmtDec(value: number | null | undefined, places = 1): string {
  if (value == null) return '—'
  return value.toFixed(places)
}

export function fmtTemp(value: number | null | undefined): string {
  if (value == null) return '—'
  return `${value.toFixed(1)}°C`
}

export function fmtKw(value: number | null | undefined): string {
  if (value == null) return '—'
  return `${value.toFixed(2)} kW`
}

export function fmtKwh(value: number | null | undefined): string {
  if (value == null) return '—'
  return `${value.toFixed(2)} kWh`
}

export function fmtPence(value: number | null | undefined): string {
  if (value == null) return '—'
  return `${value.toFixed(1)}p`
}

export function fmtPercent(value: number | null | undefined): string {
  if (value == null) return '—'
  return `${value.toFixed(1)}%`
}

// ── COP helpers ───────────────────────────────────────────────────────

export function copColorClass(cop: number | null | undefined): string {
  if (cop == null) return 'text-white/60'
  if (cop >= 3.5) return 'text-green-400'
  if (cop >= 2.5) return 'text-amber-400'
  return 'text-red-400'
}

export function copColor(cop: number | null | undefined): string {
  if (cop == null) return 'rgba(255,255,255,0.38)'
  if (cop >= 3.5) return '#22c55e'
  if (cop >= 2.5) return '#f59e0b'
  return '#ef4444'
}

// ── Snapshot downsampling ─────────────────────────────────────────────

export function downsample<T>(items: T[], maxPoints: number): T[] {
  if (items.length <= maxPoints) return items
  const step = Math.ceil(items.length / maxPoints)
  return items.filter((_, i) => i % step === 0)
}

// ── Snap parsing helpers ──────────────────────────────────────────────

export function parseFloatSafe(s: string | null | undefined): number | null {
  if (!s) return null
  const n = parseFloat(s)
  return isNaN(n) ? null : n
}
