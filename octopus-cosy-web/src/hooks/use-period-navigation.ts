import { useState, useMemo, useCallback } from 'react'
import type { PeriodType } from '@/lib/utils'
import { periodLabel } from '@/lib/utils'

export function usePeriodNavigation() {
  const [periodType, setPeriodType] = useState<PeriodType>('week')
  const [offset, setOffset] = useState(0)

  const { from, to } = useMemo(() => {
    const now = new Date()
    switch (periodType) {
      case 'day': {
        const d = new Date(now)
        d.setDate(d.getDate() + offset)
        d.setHours(0, 0, 0, 0)
        const end = new Date(d)
        end.setHours(23, 59, 59, 999)
        return { from: d, to: end }
      }
      case 'week': {
        const d = new Date(now)
        // Start of current week (Monday)
        const day = d.getDay()
        const diff = day === 0 ? -6 : 1 - day
        d.setDate(d.getDate() + diff + offset * 7)
        d.setHours(0, 0, 0, 0)
        const end = new Date(d)
        end.setDate(end.getDate() + 6)
        end.setHours(23, 59, 59, 999)
        return { from: d, to: end }
      }
      case 'month': {
        const d = new Date(now.getFullYear(), now.getMonth() + offset, 1)
        const end = new Date(d.getFullYear(), d.getMonth() + 1, 0, 23, 59, 59, 999)
        return { from: d, to: end }
      }
      case 'year': {
        const year = now.getFullYear() + offset
        return {
          from: new Date(year, 0, 1),
          to: new Date(year, 11, 31, 23, 59, 59, 999),
        }
      }
    }
  }, [periodType, offset])

  const label = useMemo(() => periodLabel(periodType, from, to), [periodType, from, to])

  const prev = useCallback(() => setOffset(o => o - 1), [])
  const next = useCallback(() => setOffset(o => o + 1), [])

  const canGoNext = offset < 0

  const changePeriod = useCallback((type: PeriodType) => {
    setPeriodType(type)
    setOffset(0)
  }, [])

  return { periodType, setPeriodType: changePeriod, offset, from, to, label, prev, next, canGoNext }
}
