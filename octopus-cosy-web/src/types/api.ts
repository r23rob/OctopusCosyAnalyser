// Auto-translated from OctopusCosyAnalyser.Shared/Models/

// ── Account Settings ──────────────────────────────────────────────────

export interface AccountSettingsDto {
  id: number
  accountNumber: string
  apiKey: string
  anthropicApiKey?: string | null
  createdAt: string
  updatedAt: string
}

export interface AccountSettingsRequestDto {
  accountNumber: string
  apiKey: string
  anthropicApiKey?: string | null
}

// ── Device ────────────────────────────────────────────────────────────

export interface HeatPumpDeviceDto {
  id: number
  deviceId: string
  accountNumber: string
  meterSerialNumber?: string | null
  mpan?: string | null
  euid?: string | null
  propertyId?: number | null
  isActive: boolean
  createdAt: string
  lastSyncAt?: string | null
}

export interface SetupResponseDto {
  deviceId?: string | null
  mpan?: string | null
  serialNumber?: string | null
  euid?: string | null
  propertyId?: number | null
  message?: string | null
}

// ── Heat Pump Summary ─────────────────────────────────────────────────

export interface HeatPumpValueAndUnitDto {
  value?: string | null
  unit?: string | null
}

export interface HeatPumpConnectivityDto {
  online?: boolean | null
  retrievedAt?: string | null
}

export interface HeatPumpTelemetryDto {
  temperatureInCelsius?: number | null
  humidityPercentage?: number | null
  retrievedAt?: string | null
}

export interface HeatPumpSensorDto {
  code?: string | null
  connectivity?: HeatPumpConnectivityDto | null
  telemetry?: HeatPumpTelemetryDto | null
}

export interface HeatPumpZoneTelemetryDto {
  setpointInCelsius?: number | null
  mode?: string | null
  relaySwitchedOn?: boolean | null
  heatDemand?: boolean | null
  retrievedAt?: string | null
}

export interface HeatPumpZoneStatusDto {
  zone?: string | null
  telemetry?: HeatPumpZoneTelemetryDto | null
}

export interface HeatPumpControllerStatusDto {
  sensors: HeatPumpSensorDto[]
  zones: HeatPumpZoneStatusDto[]
}

export interface HeatPumpAllowableRangeDto {
  minimum?: HeatPumpValueAndUnitDto | null
  maximum?: HeatPumpValueAndUnitDto | null
}

export interface HeatPumpHeatingFlowTemperatureDto {
  currentTemperature?: HeatPumpValueAndUnitDto | null
  allowableRange?: HeatPumpAllowableRangeDto | null
}

export interface HeatPumpWeatherCompensationDto {
  enabled?: boolean | null
  currentRange?: HeatPumpAllowableRangeDto | null
}

export interface HeatPumpDetailsDto {
  serialNumber?: string | null
  model?: string | null
  hardwareVersion?: string | null
  maxWaterSetpoint?: number | null
  minWaterSetpoint?: number | null
  heatingFlowTemperature?: HeatPumpHeatingFlowTemperatureDto | null
  weatherCompensation?: HeatPumpWeatherCompensationDto | null
}

export interface HeatPumpControllerDto {
  state: string[]
  heatPumpTimezone?: string | null
  connected?: boolean | null
}

export interface HeatPumpCurrentOperationDto {
  mode?: string | null
  setpointInCelsius?: number | null
  action?: string | null
  end?: string | null
}

export interface HeatPumpSensorConfigurationDto {
  code?: string | null
  displayName?: string | null
  type?: string | null
  enabled?: boolean | null
  firmwareVersion?: string | null
  boostEnabled?: boolean | null
}

export interface HeatPumpZoneConfigDto {
  code?: string | null
  zoneType?: string | null
  enabled?: boolean | null
  displayName?: string | null
  primarySensor?: string | null
  currentOperation?: HeatPumpCurrentOperationDto | null
  callForHeat?: boolean | null
  heatDemand?: boolean | null
  emergency?: boolean | null
  sensors: HeatPumpSensorConfigurationDto[]
}

export interface HeatPumpZoneConfigurationDto {
  configuration?: HeatPumpZoneConfigDto | null
}

export interface HeatPumpControllerConfigurationDto {
  controller?: HeatPumpControllerDto | null
  heatPump?: HeatPumpDetailsDto | null
  zones: HeatPumpZoneConfigurationDto[]
}

export interface HeatPumpLivePerformanceDto {
  coefficientOfPerformance?: string | null
  outdoorTemperature?: HeatPumpValueAndUnitDto | null
  heatOutput?: HeatPumpValueAndUnitDto | null
  powerInput?: HeatPumpValueAndUnitDto | null
  readAt?: string | null
}

export interface HeatPumpLifetimePerformanceDto {
  seasonalCoefficientOfPerformance?: string | null
  heatOutput?: HeatPumpValueAndUnitDto | null
  energyInput?: HeatPumpValueAndUnitDto | null
  readAt?: string | null
}

export interface HeatPumpSummaryDto {
  controllerStatus?: HeatPumpControllerStatusDto | null
  controllerConfiguration?: HeatPumpControllerConfigurationDto | null
  livePerformance?: HeatPumpLivePerformanceDto | null
  lifetimePerformance?: HeatPumpLifetimePerformanceDto | null
}

// ── Snapshots ─────────────────────────────────────────────────────────

export interface HeatPumpSnapshotDto {
  id: number
  deviceId: string
  accountNumber: string
  coefficientOfPerformance?: number | null
  outdoorTemperatureCelsius?: number | null
  heatOutputKilowatt?: number | null
  powerInputKilowatt?: number | null
  seasonalCoefficientOfPerformance?: number | null
  lifetimeHeatOutputKwh?: number | null
  lifetimeEnergyInputKwh?: number | null
  controllerConnected?: boolean | null
  primaryZoneSetpointCelsius?: number | null
  primaryZoneMode?: string | null
  primaryZoneHeatDemand?: boolean | null
  primarySensorTemperatureCelsius?: number | null
  heatingZoneSetpointCelsius?: number | null
  heatingZoneMode?: string | null
  heatingZoneHeatDemand?: boolean | null
  roomTemperatureCelsius?: number | null
  roomHumidityPercentage?: number | null
  roomSensorCode?: string | null
  flowTempMode?: string | null
  weatherCompensationMinCelsius?: number | null
  weatherCompensationMaxCelsius?: number | null
  heatingFlowTemperatureCelsius?: number | null
  heatingFlowTempAllowableMinCelsius?: number | null
  heatingFlowTempAllowableMaxCelsius?: number | null
  controllerState?: string | null
  hotWaterZoneSetpointCelsius?: number | null
  hotWaterZoneMode?: string | null
  hotWaterZoneHeatDemand?: boolean | null
  sensorReadingsJson?: string | null
  snapshotTakenAt: string
  createdAt: string
}

export interface LatestSnapshotDto {
  hasData: boolean
  snapshotTakenAt?: string | null
  minutesAgo?: number | null
}

export interface SnapshotsResponseDto {
  deviceId: string
  from: string
  to: string
  totalCount: number
  count: number
  hasMore: boolean
  snapshots: HeatPumpSnapshotDto[]
}

// ── Period Summary ────────────────────────────────────────────────────

export interface PeriodSummaryDto {
  periodFrom: string
  periodTo: string
  snapshotCount: number
  avgCop?: number | null
  minCop?: number | null
  maxCop?: number | null
  totalInputKwh: number
  totalOutputKwh: number
  avgOutdoorTemp?: number | null
  minOutdoorTemp?: number | null
  maxOutdoorTemp?: number | null
  avgRoomTemp?: number | null
  minRoomTemp?: number | null
  maxRoomTemp?: number | null
  avgRoomHumidity?: number | null
  minRoomHumidity?: number | null
  maxRoomHumidity?: number | null
  avgHotWaterSetpoint?: number | null
  minHotWaterSetpoint?: number | null
  maxHotWaterSetpoint?: number | null
  avgFlowTemp?: number | null
  minFlowTemp?: number | null
  maxFlowTemp?: number | null
  heatingDutyCyclePercent: number
  hotWaterDutyCyclePercent: number
}

// ── Daily Aggregates ──────────────────────────────────────────────────

export interface DailyAggregateDto {
  date: string
  snapshotCount: number
  avgCopHeating?: number | null
  avgCopHotWater?: number | null
  avgCopSpaceHeatingOnly?: number | null
  totalElectricityKwh: number
  totalHeatOutputKwh: number
  avgOutdoorTemp?: number | null
  minOutdoorTemp?: number | null
  maxOutdoorTemp?: number | null
  avgFlowTemp?: number | null
  avgRoomTemp?: number | null
  avgSetpoint?: number | null
  heatingDutyCyclePercent: number
  hotWaterDutyCyclePercent: number
  flowTempMode?: string | null
  weatherCompMin?: number | null
  weatherCompMax?: number | null
  flowTempAllowableMin?: number | null
  flowTempAllowableMax?: number | null
  controllerStateTransitions: number
  dailyCostPence?: number | null
  dailyUsageKwh?: number | null
  avgUnitRatePence?: number | null
  costPerKwhHeatPence?: number | null
  hotWaterRunCount: number
  hotWaterTotalMinutes: number
  avgHotWaterSetpoint?: number | null
}

// ── Consumption ───────────────────────────────────────────────────────

export interface ConsumptionReadingDto {
  id: number
  deviceId: string
  readAt: string
  consumption: number
  consumptionDelta?: number | null
  demand?: number | null
  createdAt: string
}

export interface ConsumptionResponseDto {
  deviceId: string
  from: string
  to: string
  totalCount: number
  count: number
  hasMore: boolean
  readings: ConsumptionReadingDto[]
}

// ── Time Series ───────────────────────────────────────────────────────

export interface TimeSeriesPointDto {
  startAt: string
  endAt: string
  coefficientOfPerformance?: string | null
  energyOutput?: string | null
  energyInput?: string | null
  outdoorTemperature?: string | null
}

export interface TimeSeriesResponseDto {
  accountNumber: string
  euid: string
  from: string
  to: string
  grouping: string
  points: TimeSeriesPointDto[]
}

export interface TimeRangedResponseDto {
  accountNumber: string
  euid: string
  from: string
  to: string
  coefficientOfPerformance?: string | null
  energyOutput?: string | null
  energyInput?: string | null
}

// ── AI ────────────────────────────────────────────────────────────────

export interface AiAnalysisRequestDto {
  from: string
  to: string
  question?: string | null
}

export interface AiAnalysisResponseDto {
  analysis: string
  from: string
  to: string
  daysAnalysed: number
  totalSnapshots: number
  totalTimeSeriesRecords: number
  costDataStatus?: string | null
}

export interface AiSummaryDto {
  weekSummary: string
  monthSummary: string
  yearSummary: string
  suggestions: string
  generatedAt: string
}

// ── View Models (client-side only) ────────────────────────────────────

export interface TimeSeriesChartPoint {
  endAt: Date
  cop: number
  energyOutputVal: number
  energyInputVal: number
  outdoorTempVal: number
}

export type TimeSeriesStatus = 'Ok' | 'NoData' | 'UnexpectedFormat'

export interface TimeSeriesResult {
  points: TimeSeriesChartPoint[]
  status: TimeSeriesStatus
}

export interface SyncResult {
  synced: number
  skipped: number
}

export const FlowTempMode = {
  WeatherCompensation: 'WeatherCompensation',
  FixedFlow: 'FixedFlow',
} as const
