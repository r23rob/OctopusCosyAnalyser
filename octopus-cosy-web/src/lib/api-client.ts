import type {
  AccountSettingsDto,
  AccountSettingsRequestDto,
  AiAnalysisRequestDto,
  AiAnalysisResponseDto,
  AiSummaryDto,
  ApiStatusDto,
  ConsumptionResponseDto,
  DailyAggregateDto,
  EnergyIntervalDto,
  EnergySummaryResponseDto,
  FeatureAvailability,
  HeatPumpDeviceDto,
  HeatPumpSnapshotDto,
  HeatPumpSummaryDto,
  LatestSnapshotDto,
  PeriodSummaryDto,
  SetupResponseDto,
  SnapshotsResponseDto,
  SyncResult,
  TimeSeriesChartPoint,
  TimeSeriesResult,
} from '@/types/api'

// ── Error class ───────────────────────────────────────────────────────

export class ApiError extends Error {
  readonly status: number
  constructor(status: number, message: string) {
    super(`API ${status}: ${message}`)
    this.status = status
  }
}

// ── Base fetch helpers ────────────────────────────────────────────────

// All API calls send credentials so the auth cookie flows on same-origin AND
// cross-origin (CORS-allowlisted) deployments.
const FETCH_INIT: RequestInit = { credentials: 'include' }

async function get<T>(path: string): Promise<T> {
  const res = await fetch(path, FETCH_INIT)
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new ApiError(res.status, text)
  }
  return res.json() as Promise<T>
}

async function getText(path: string): Promise<string> {
  const res = await fetch(path, FETCH_INIT)
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new ApiError(res.status, text)
  }
  return res.text()
}

async function put<TBody, TResult>(path: string, body: TBody): Promise<TResult> {
  const res = await fetch(path, {
    ...FETCH_INIT,
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new ApiError(res.status, text)
  }
  return res.json() as Promise<TResult>
}

async function post<TBody, TResult>(path: string, body?: TBody): Promise<TResult> {
  const res = await fetch(path, {
    ...FETCH_INIT,
    method: 'POST',
    headers: body != null ? { 'Content-Type': 'application/json' } : {},
    body: body != null ? JSON.stringify(body) : undefined,
  })
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new ApiError(res.status, text)
  }
  return res.json() as Promise<TResult>
}

async function postNoContent(path: string): Promise<void> {
  const res = await fetch(path, { ...FETCH_INIT, method: 'POST' })
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new ApiError(res.status, text)
  }
}

// ── ISO date formatting helpers ───────────────────────────────────────

function toOctopusIso(d: Date): string {
  return d.toISOString().replace('Z', '+00:00').replace(/(\.\d{3})\d*/, '$1000')
}

function toIso(d: Date): string {
  return d.toISOString()
}

function defaultFrom(days: number): Date {
  const d = new Date()
  d.setDate(d.getDate() - days)
  return d
}

// ── Time-series parsing helpers ───────────────────────────────────────

function parseStoredTimeSeriesJson(json: string): TimeSeriesResult {
  const data = JSON.parse(json) as Record<string, unknown>
  const records = data['records']
  if (!Array.isArray(records)) return { points: [], status: 'NoData' }

  const points: TimeSeriesChartPoint[] = []
  for (const item of records as Record<string, unknown>[]) {
    const endAtStr = item['endAt']
    if (typeof endAtStr !== 'string') continue
    const endAt = new Date(endAtStr)
    if (isNaN(endAt.getTime())) continue

    const energyOutputVal = typeof item['energyOutputKwh'] === 'number' ? item['energyOutputKwh'] : 0
    const energyInputVal = typeof item['energyInputKwh'] === 'number' ? item['energyInputKwh'] : 0
    const outdoorTempVal = typeof item['outdoorTemperatureCelsius'] === 'number' ? item['outdoorTemperatureCelsius'] : 0
    const cop = energyInputVal > 0 ? energyOutputVal / energyInputVal : 0

    points.push({ endAt, cop, energyOutputVal, energyInputVal, outdoorTempVal })
  }
  return { points, status: points.length > 0 ? 'Ok' : 'NoData' }
}

// ── API client ────────────────────────────────────────────────────────

export const api = {
  // Feature availability
  features: {
    getAvailability: async (): Promise<FeatureAvailability> => {
      const res = await fetch('/api/features', FETCH_INIT)
      if (!res.ok) {
        // If the endpoint doesn't exist (old API), assume full features
        return { database: true, history: true, efficiency: true, liveData: true }
      }
      return res.json() as Promise<FeatureAvailability>
    },
  },

  // Connection / API status
  status: {
    get: () => get<ApiStatusDto>('/api/status'),
  },

  // Settings
  settings: {
    getAll: () => get<AccountSettingsDto[]>('/api/settings'),
    getByAccount: (accountNumber: string) => get<AccountSettingsDto>(`/api/settings/${accountNumber}`),
    upsert: (body: AccountSettingsRequestDto) => put<AccountSettingsRequestDto, AccountSettingsDto>('/api/settings', body),
  },

  // Devices
  devices: {
    getAll: () => get<HeatPumpDeviceDto[]>('/api/heatpump/devices'),
    setup: (accountNumber: string) => post<object, SetupResponseDto>(`/api/heatpump/setup?accountNumber=${encodeURIComponent(accountNumber)}`, {}),
  },

  // Heat pump data
  heatpump: {
    getSummary: (deviceId: string) => get<HeatPumpSummaryDto>(`/api/heatpump/summary/${deviceId}`),

    getLatestSnapshot: (deviceId: string) => get<LatestSnapshotDto>(`/api/heatpump/snapshots/${deviceId}/latest`),

    getSnapshots: (deviceId: string, from?: Date, to?: Date, cursor?: string): Promise<SnapshotsResponseDto> => {
      const fromStr = encodeURIComponent(toIso(from ?? defaultFrom(7)))
      const toStr = encodeURIComponent(toIso(to ?? new Date()))
      let url = `/api/heatpump/snapshots/${deviceId}?from=${fromStr}&to=${toStr}`
      if (cursor) url += `&cursor=${encodeURIComponent(cursor)}`
      return get<SnapshotsResponseDto>(url)
    },

    getPeriodSummary: (deviceId: string, from?: Date, to?: Date): Promise<PeriodSummaryDto> => {
      const fromStr = encodeURIComponent(toIso(from ?? defaultFrom(7)))
      const toStr = encodeURIComponent(toIso(to ?? new Date()))
      return get<PeriodSummaryDto>(`/api/heatpump/period-summary/${deviceId}?from=${fromStr}&to=${toStr}`)
    },

    getDailyAggregates: async (deviceId: string, from?: Date, to?: Date): Promise<DailyAggregateDto[]> => {
      const fromStr = encodeURIComponent(toIso(from ?? defaultFrom(30)))
      const toStr = encodeURIComponent(toIso(to ?? new Date()))
      const json = await getText(`/api/heatpump/daily-aggregates/${deviceId}?from=${fromStr}&to=${toStr}`)
      const data = JSON.parse(json) as Record<string, unknown>
      const aggregates = data['aggregates']
      if (!Array.isArray(aggregates)) return []
      return aggregates as DailyAggregateDto[]
    },

    getStoredTimeSeries: async (deviceId: string, from: Date, to: Date): Promise<TimeSeriesResult> => {
      const fromStr = encodeURIComponent(toIso(from))
      const toStr = encodeURIComponent(toIso(to))
      const json = await getText(`/api/heatpump/timeseries/${deviceId}?from=${fromStr}&to=${toStr}`)
      return parseStoredTimeSeriesJson(json)
    },

    syncTimeSeries: async (deviceId: string, from?: Date, to?: Date): Promise<SyncResult> => {
      const defaultFromDate = new Date()
      defaultFromDate.setMonth(defaultFromDate.getMonth() - 12)
      const fromStr = encodeURIComponent(toIso(from ?? defaultFromDate))
      const toStr = encodeURIComponent(toIso(to ?? new Date()))
      const json = await getText(`/api/heatpump/sync-timeseries/${deviceId}?from=${fromStr}&to=${toStr}`)
      const data = JSON.parse(json) as Record<string, unknown>
      return {
        synced: typeof data['synced'] === 'number' ? data['synced'] : 0,
        skipped: typeof data['skipped'] === 'number' ? data['skipped'] : 0,
      }
    },

    getConsumption: (deviceId: string, from?: Date, to?: Date, cursor?: string): Promise<ConsumptionResponseDto> => {
      const fromStr = encodeURIComponent(toIso(from ?? defaultFrom(7)))
      const toStr = encodeURIComponent(toIso(to ?? new Date()))
      let url = `/api/heatpump/consumption/${deviceId}?from=${fromStr}&to=${toStr}`
      if (cursor) url += `&cursor=${encodeURIComponent(cursor)}`
      return get<ConsumptionResponseDto>(url)
    },

    syncConsumption: (deviceId: string, from?: Date, to?: Date): Promise<void> => {
      const fromStr = encodeURIComponent(toIso(from ?? defaultFrom(7)))
      const toStr = encodeURIComponent(toIso(to ?? new Date()))
      return postNoContent(`/api/heatpump/sync/${deviceId}?from=${fromStr}&to=${toStr}`)
    },

    getAiSummary: (deviceId: string) => get<AiSummaryDto>(`/api/heatpump/ai-summary/${deviceId}`),

    refreshAiSummary: (deviceId: string) => get<AiSummaryDto>(`/api/heatpump/ai-summary/${deviceId}/refresh`),

    getAiAnalysis: (deviceId: string, body: AiAnalysisRequestDto) =>
      post<AiAnalysisRequestDto, AiAnalysisResponseDto>(`/api/heatpump/ai-analysis/${deviceId}`, body),

    getRatesRaw: async (accountNumber: string, from?: Date, to?: Date): Promise<string> => {
      const fromStr = encodeURIComponent(toOctopusIso(from ?? defaultFrom(1)))
      const toStr = encodeURIComponent(toOctopusIso(to ?? new Date()))
      return getText(`/api/heatpump/rates/${accountNumber}?from=${fromStr}&to=${toStr}`)
    },

    getCostRaw: async (accountNumber: string, from?: Date, to?: Date): Promise<string> => {
      const fromStr = encodeURIComponent(toOctopusIso(from ?? defaultFrom(7)))
      const toStr = encodeURIComponent(toOctopusIso(to ?? new Date()))
      return getText(`/api/heatpump/cost/${accountNumber}?from=${fromStr}&to=${toStr}`)
    },

    getStoredCostRaw: async (deviceId: string, from?: Date, to?: Date): Promise<string> => {
      const fromStr = encodeURIComponent(toOctopusIso(from ?? defaultFrom(30)))
      const toStr = encodeURIComponent(toOctopusIso(to ?? new Date()))
      return getText(`/api/heatpump/cost-stored/${deviceId}?from=${fromStr}&to=${toStr}`)
    },

    getSnapshotsList: async (deviceId: string, from?: Date, to?: Date): Promise<HeatPumpSnapshotDto[]> => {
      const all: HeatPumpSnapshotDto[] = []
      let cursor: string | undefined
      do {
        const resp = await api.heatpump.getSnapshots(deviceId, from, to, cursor)
        all.push(...resp.snapshots)
        cursor = resp.cursor ?? undefined
      } while (cursor)
      return all
    },

    getEnergyIntervals: (deviceId: string, from?: Date, to?: Date) => {
      const fromStr = encodeURIComponent(toOctopusIso(from ?? defaultFrom(7)))
      const toStr = encodeURIComponent(toOctopusIso(to ?? new Date()))
      return get<EnergyIntervalDto[]>(`/api/heatpump/energy-intervals/${deviceId}?from=${fromStr}&to=${toStr}`)
    },

    getEnergySummary: (deviceId: string, from?: Date, to?: Date, grouping = 'day') => {
      const fromStr = encodeURIComponent(toOctopusIso(from ?? defaultFrom(7)))
      const toStr = encodeURIComponent(toOctopusIso(to ?? new Date()))
      return get<EnergySummaryResponseDto>(`/api/heatpump/energy-summary/${deviceId}?from=${fromStr}&to=${toStr}&grouping=${grouping}`)
    },
  },
}
