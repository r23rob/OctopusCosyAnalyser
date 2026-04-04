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
  if (cop == null) return 'text-ink3'
  if (cop >= 3.2) return 'text-[#16A34A]'
  if (cop >= 2.5) return 'text-[#D97706]'
  return 'text-[#DC2626]'
}

export function copColor(cop: number | null | undefined): string {
  if (cop == null) return '#A1A1AA'
  if (cop >= 3.2) return '#16A34A'
  if (cop >= 2.5) return '#D97706'
  return '#DC2626'
}

export function copLabel(cop: number | null | undefined): string {
  if (cop == null) return '—'
  if (cop >= 3.2) return 'Running well'
  if (cop >= 2.5) return 'Running OK'
  return 'Low efficiency'
}

export function copCls(cop: number | null | undefined): 'g' | 'w' | 'r' {
  if (cop == null) return 'r'
  if (cop >= 3.2) return 'g'
  if (cop >= 2.5) return 'w'
  return 'r'
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

// ── Period label formatting ───────────────────────────────────────────

export type PeriodType = 'day' | 'week' | 'month' | 'year'

export function periodLabel(type: PeriodType, from: Date, to: Date): string {
  switch (type) {
    case 'day':
      return from.toLocaleDateString('en-GB', { weekday: 'short', day: '2-digit', month: 'short', year: 'numeric' })
    case 'week': {
      const s = from.toLocaleDateString('en-GB', { day: '2-digit', month: 'short' })
      const e = to.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: '2-digit' })
      return `${s} – ${e}`
    }
    case 'month':
      return from.toLocaleDateString('en-GB', { month: 'long', year: 'numeric' })
    case 'year':
      return from.getFullYear().toString()
  }
}

export function periodDaysFromType(type: PeriodType): number {
  switch (type) {
    case 'day': return 1
    case 'week': return 7
    case 'month': return 30
    case 'year': return 365
  }
}

export function vsLabel(type: PeriodType): string {
  switch (type) {
    case 'day': return 'vs yesterday'
    case 'week': return 'vs last week'
    case 'month': return 'vs last month'
    case 'year': return 'vs last year'
  }
}

export function periodSubtitle(type: PeriodType): string {
  switch (type) {
    case 'day': return '24-hour view'
    case 'week': return '7-day view'
    case 'month': return 'Monthly view'
    case 'year': return 'Annual view'
  }
}
