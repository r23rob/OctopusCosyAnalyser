import type { HeatPumpSummaryDto } from '@/types/api'
import { FlowTempMode } from '@/types/api'
import { fmtDec, fmtTemp } from '@/lib/utils'
import { Wifi, WifiOff } from 'lucide-react'

interface Props {
  summary: HeatPumpSummaryDto | null | undefined
  isLoading?: boolean
}

export function ComfortTab({ summary, isLoading }: Props) {
  if (isLoading) {
    return (
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="h-20 rounded-lg bg-white/[0.03] border border-white/[0.06] animate-pulse" />
        ))}
      </div>
    )
  }

  if (!summary) return <p className="text-xs text-white/40">No data available.</p>

  const config = summary.controllerConfiguration
  const status = summary.controllerStatus
  const heatPump = config?.heatPump
  const controller = config?.controller
  const zones = config?.zones ?? []

  // Find room sensor (Cosy Pod with telemetry)
  const roomSensor = status?.sensors.find((s) => s.telemetry?.temperatureInCelsius != null)
  const roomTemp = roomSensor?.telemetry?.temperatureInCelsius
  const roomHumidity = roomSensor?.telemetry?.humidityPercentage

  // Flow temperature info
  const flowTemp = heatPump?.heatingFlowTemperature
  const wc = heatPump?.weatherCompensation
  const wcEnabled = wc?.enabled === true
  const flowMode = wcEnabled ? FlowTempMode.WeatherCompensation : FlowTempMode.FixedFlow

  return (
    <div className="flex flex-col gap-5">
      {/* Live sensor cards */}
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {roomTemp != null && (
          <SensorCard label="Room Temperature" value={fmtTemp(roomTemp)} color="text-violet-400" />
        )}
        {roomHumidity != null && (
          <SensorCard label="Room Humidity" value={`${fmtDec(roomHumidity)}%`} color="text-cyan-400" />
        )}
        {flowTemp?.currentTemperature?.value != null && (
          <SensorCard
            label="Flow Temperature"
            value={`${flowTemp.currentTemperature.value}°C`}
            color="text-cyan-400"
            sub={flowMode === FlowTempMode.WeatherCompensation ? 'Weather Comp' : 'Fixed Flow'}
          />
        )}
        {flowTemp?.allowableRange?.minimum?.value != null && (
          <SensorCard
            label="Flow Range"
            value={`${flowTemp.allowableRange.minimum.value}–${flowTemp.allowableRange.maximum?.value ?? '?'}°C`}
            color="text-white/70"
          />
        )}
        {wc?.currentRange?.minimum?.value != null && (
          <SensorCard
            label="WC Curve"
            value={`${wc.currentRange.minimum.value}–${wc.currentRange.maximum?.value ?? '?'}°C`}
            color="text-cyan-400"
            sub="Min–Max flow"
          />
        )}
        {controller?.connected != null && (
          <SensorCard
            label="Controller"
            value={controller.connected ? 'Connected' : 'Disconnected'}
            color={controller.connected ? 'text-green-400' : 'text-red-400'}
            icon={controller.connected ? <Wifi size={14} /> : <WifiOff size={14} />}
          />
        )}
      </div>

      {/* Heat pump details */}
      {heatPump && (
        <Section title="Heat Pump Details">
          <dl className="grid grid-cols-2 gap-x-6 gap-y-1.5 text-xs">
            {heatPump.model && <Row label="Model" value={heatPump.model} />}
            {heatPump.serialNumber && <Row label="Serial" value={heatPump.serialNumber} />}
            {heatPump.hardwareVersion && <Row label="Hardware" value={heatPump.hardwareVersion} />}
            {heatPump.maxWaterSetpoint != null && (
              <Row label="Max Setpoint" value={`${heatPump.maxWaterSetpoint}°C`} />
            )}
            {heatPump.minWaterSetpoint != null && (
              <Row label="Min Setpoint" value={`${heatPump.minWaterSetpoint}°C`} />
            )}
            <Row label="Flow Mode" value={wcEnabled ? 'Weather Compensation' : 'Fixed Flow'} />
          </dl>
        </Section>
      )}

      {/* Controller state */}
      {controller?.state && controller.state.length > 0 && (
        <Section title="Controller State">
          <div className="flex flex-wrap gap-2">
            {controller.state.map((s) => (
              <span
                key={s}
                className={`px-2.5 py-1 rounded-full text-xs font-medium border ${
                  s === 'HEATING'
                    ? 'border-green-500/40 bg-green-500/10 text-green-300'
                    : s === 'IDLE'
                      ? 'border-white/10 bg-white/[0.04] text-white/50'
                      : 'border-amber-500/30 bg-amber-500/10 text-amber-300'
                }`}
              >
                {s}
              </span>
            ))}
          </div>
        </Section>
      )}

      {/* Zone configuration table */}
      {zones.length > 0 && (
        <Section title="Zone Configuration">
          <div className="overflow-x-auto">
            <table className="w-full text-xs">
              <thead>
                <tr className="border-b border-white/[0.06]">
                  <th className="text-left py-1.5 text-white/40 font-medium">Zone</th>
                  <th className="text-left py-1.5 text-white/40 font-medium">Type</th>
                  <th className="text-left py-1.5 text-white/40 font-medium">Mode</th>
                  <th className="text-right py-1.5 text-white/40 font-medium">Setpoint</th>
                  <th className="text-right py-1.5 text-white/40 font-medium">Demand</th>
                </tr>
              </thead>
              <tbody>
                {zones.map((z, i) => {
                  const c = z.configuration
                  if (!c) return null
                  const zoneStatus = status?.zones.find((zs) => zs.zone === c.code)
                  return (
                    <tr key={i} className="border-b border-white/[0.04]">
                      <td className="py-1.5 text-white/80">{c.displayName ?? c.code}</td>
                      <td className="py-1.5 text-white/50">{c.zoneType}</td>
                      <td className="py-1.5 text-white/50">{c.currentOperation?.mode ?? '—'}</td>
                      <td className="py-1.5 text-right text-white/70">
                        {c.currentOperation?.setpointInCelsius != null
                          ? `${c.currentOperation.setpointInCelsius}°C`
                          : zoneStatus?.telemetry?.setpointInCelsius != null
                            ? `${zoneStatus.telemetry.setpointInCelsius}°C`
                            : '—'}
                      </td>
                      <td className="py-1.5 text-right">
                        {c.heatDemand === true ? (
                          <span className="text-amber-400">Yes</span>
                        ) : (
                          <span className="text-white/30">No</span>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </Section>
      )}

      {/* All sensors */}
      {status?.sensors && status.sensors.length > 0 && (
        <Section title="Sensors">
          <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
            {status.sensors.map((s, i) => (
              <div key={i} className="rounded-lg border border-white/[0.06] px-3 py-2 bg-white/[0.02]">
                <div className="flex items-center gap-1.5 mb-1">
                  {s.connectivity?.online ? (
                    <Wifi size={11} className="text-green-400" />
                  ) : (
                    <WifiOff size={11} className="text-white/30" />
                  )}
                  <span className="text-[11px] text-white/60 font-medium truncate">{s.code}</span>
                </div>
                {s.telemetry?.temperatureInCelsius != null && (
                  <div className="text-sm font-bold text-white/85">
                    {fmtTemp(s.telemetry.temperatureInCelsius)}
                  </div>
                )}
                {s.telemetry?.humidityPercentage != null && (
                  <div className="text-xs text-cyan-400">{fmtDec(s.telemetry.humidityPercentage)}% RH</div>
                )}
              </div>
            ))}
          </div>
        </Section>
      )}
    </div>
  )
}

function SensorCard({
  label,
  value,
  color,
  sub,
  icon,
}: {
  label: string
  value: string
  color: string
  sub?: string
  icon?: React.ReactNode
}) {
  return (
    <div className="rounded-lg border border-white/[0.06] bg-white/[0.03] px-3 py-2.5">
      <div className="text-[10px] text-white/40 uppercase tracking-wide mb-1">{label}</div>
      <div className={`flex items-center gap-1.5 text-base font-bold ${color}`}>
        {icon}
        {value}
      </div>
      {sub && <div className="text-[10px] text-white/30 mt-0.5">{sub}</div>}
    </div>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-lg border border-white/[0.06] bg-white/[0.02]">
      <div className="px-3 py-2 border-b border-white/[0.06]">
        <span className="text-xs font-medium text-white/60">{title}</span>
      </div>
      <div className="p-3">{children}</div>
    </div>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <>
      <dt className="text-white/40">{label}</dt>
      <dd className="text-white/80 text-right">{value}</dd>
    </>
  )
}
