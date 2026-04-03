import { PolarAngleAxis, RadialBar, RadialBarChart, ResponsiveContainer } from 'recharts'
import { copColor } from '@/lib/utils'

interface Props {
  value: number | null | undefined
  label: string
  unit: string
  min?: number
  max?: number
  color?: string
  /** If true, uses COP color coding (green/amber/red) */
  copMode?: boolean
}

export function RadialGauge({ value, label, unit, min = 0, max = 10, color, copMode = false }: Props) {
  const safeVal = value ?? 0
  const clamped = Math.min(Math.max(safeVal, min), max)
  const pct = ((clamped - min) / (max - min)) * 100

  const fillColor = copMode ? copColor(safeVal) : (color ?? '#3b82f6')

  const data = [{ value: pct, fill: fillColor }]

  return (
    <div className="flex flex-col items-center gap-1">
      <div className="relative w-32 h-32">
        <ResponsiveContainer width="100%" height="100%">
          <RadialBarChart
            cx="50%"
            cy="50%"
            innerRadius="65%"
            outerRadius="100%"
            startAngle={210}
            endAngle={-30}
            data={data}
            barSize={10}
          >
            <PolarAngleAxis type="number" domain={[0, 100]} angleAxisId={0} tick={false} />
            {/* Background track */}
            <RadialBar
              background={{ fill: 'rgba(255,255,255,0.06)' }}
              dataKey="value"
              angleAxisId={0}
              cornerRadius={5}
            />
          </RadialBarChart>
        </ResponsiveContainer>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span
            className="text-2xl font-bold leading-none"
            style={{ color: fillColor }}
          >
            {value != null ? (unit === '°C' ? safeVal.toFixed(1) : safeVal.toFixed(2)) : '—'}
          </span>
          <span className="text-xs text-white/40 mt-0.5">{unit}</span>
        </div>
      </div>
      <span className="text-xs text-white/60 font-medium text-center">{label}</span>
    </div>
  )
}
