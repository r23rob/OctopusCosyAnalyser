import { getApiUrl } from './storage';
import type {
  AccountSettingsDto,
  AccountSettingsRequestDto,
  ApiStatusDto,
  DailyAggregateDto,
  FeatureAvailability,
  HeatPumpDeviceDto,
  HeatPumpSnapshotDto,
  HeatPumpSummaryDto,
  LatestSnapshotDto,
  PeriodSummaryDto,
  SetupResponseDto,
  SnapshotsResponseDto,
  AiSummaryDto,
  AiAnalysisRequestDto,
  AiAnalysisResponseDto,
  EnergyIntervalDto,
  EnergySummaryResponseDto,
} from '../types/api';

class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

let baseUrl = '';

async function ensureBaseUrl(): Promise<string> {
  if (baseUrl) return baseUrl;
  const stored = await getApiUrl();
  if (stored) {
    baseUrl = stored.replace(/\/+$/, '');
    return baseUrl;
  }
  throw new ApiError(0, 'API URL not configured. Go to Settings to set your server URL.');
}

export function setBaseUrl(url: string) {
  baseUrl = url.replace(/\/+$/, '');
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const base = await ensureBaseUrl();
  const res = await fetch(`${base}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  });
  if (!res.ok) {
    const body = await res.text().catch(() => '');
    throw new ApiError(res.status, body || `HTTP ${res.status}`);
  }
  return res.json();
}

async function get<T>(path: string): Promise<T> {
  return request<T>(path);
}

async function post<TBody, TResult>(path: string, body: TBody): Promise<TResult> {
  return request<TResult>(path, {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

async function put<TBody, TResult>(path: string, body: TBody): Promise<TResult> {
  return request<TResult>(path, {
    method: 'PUT',
    body: JSON.stringify(body),
  });
}

function dateParams(from?: string, to?: string): string {
  const params = new URLSearchParams();
  if (from) params.set('from', from);
  if (to) params.set('to', to);
  const qs = params.toString();
  return qs ? `?${qs}` : '';
}

export const api = {
  features: {
    getAvailability: () => get<FeatureAvailability>('/api/features'),
  },
  status: {
    get: () => get<ApiStatusDto>('/api/status'),
  },
  settings: {
    getAll: () => get<AccountSettingsDto[]>('/api/settings'),
    upsert: (body: AccountSettingsRequestDto) => put<AccountSettingsRequestDto, AccountSettingsDto>('/api/settings', body),
  },
  devices: {
    getAll: () => get<HeatPumpDeviceDto[]>('/api/heatpump/devices'),
    setup: (accountNumber: string) => post<null, SetupResponseDto>(`/api/heatpump/setup?accountNumber=${encodeURIComponent(accountNumber)}`, null),
  },
  heatpump: {
    getSummary: (deviceId: string) => get<HeatPumpSummaryDto>(`/api/heatpump/summary/${deviceId}`),
    getLatestSnapshot: (deviceId: string) => get<LatestSnapshotDto>(`/api/heatpump/snapshots/${deviceId}/latest`),
    getSnapshots: (deviceId: string, from?: string, to?: string) =>
      get<SnapshotsResponseDto>(`/api/heatpump/snapshots/${deviceId}${dateParams(from, to)}`),
    getPeriodSummary: (deviceId: string, from?: string, to?: string) =>
      get<PeriodSummaryDto>(`/api/heatpump/period-summary/${deviceId}${dateParams(from, to)}`),
    getDailyAggregates: (deviceId: string, from?: string, to?: string) =>
      get<DailyAggregateDto[]>(`/api/heatpump/daily-aggregates/${deviceId}${dateParams(from, to)}`),
    getAiSummary: (deviceId: string) => get<AiSummaryDto>(`/api/heatpump/ai-summary/${deviceId}`),
    refreshAiSummary: (deviceId: string) => get<AiSummaryDto>(`/api/heatpump/ai-summary/${deviceId}/refresh`),
    getAiAnalysis: (deviceId: string, body: AiAnalysisRequestDto) =>
      post<AiAnalysisRequestDto, AiAnalysisResponseDto>(`/api/heatpump/ai-analysis/${deviceId}`, body),
    getEnergyIntervals: (deviceId: string, from?: string, to?: string) =>
      get<EnergyIntervalDto[]>(`/api/heatpump/energy-intervals/${deviceId}${dateParams(from, to)}`),
    getEnergySummary: (deviceId: string, from?: string, to?: string, grouping?: string) => {
      const params = new URLSearchParams();
      if (from) params.set('from', from);
      if (to) params.set('to', to);
      if (grouping) params.set('grouping', grouping);
      const qs = params.toString();
      return get<EnergySummaryResponseDto>(`/api/heatpump/energy-summary/${deviceId}${qs ? `?${qs}` : ''}`);
    },
  },
};
