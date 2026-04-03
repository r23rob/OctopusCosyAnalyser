// TanStack Query key factories — keeps cache keys consistent across hooks

export const queryKeys = {
  settings: {
    all: () => ['settings'] as const,
    byAccount: (accountNumber: string) => ['settings', accountNumber] as const,
  },
  devices: {
    all: () => ['devices'] as const,
  },
  heatpump: {
    summary: (deviceId: string) => ['heatpump', 'summary', deviceId] as const,
    latestSnapshot: (deviceId: string) => ['heatpump', 'latest', deviceId] as const,
    snapshots: (deviceId: string, from: string, to: string) =>
      ['heatpump', 'snapshots', deviceId, from, to] as const,
    periodSummary: (deviceId: string, from: string, to: string) =>
      ['heatpump', 'period-summary', deviceId, from, to] as const,
    dailyAggregates: (deviceId: string, from: string, to: string) =>
      ['heatpump', 'daily-aggregates', deviceId, from, to] as const,
    timeSeries: (accountNumber: string, euid: string, from: string, to: string, grouping?: string) =>
      ['heatpump', 'time-series', accountNumber, euid, from, to, grouping] as const,
    storedTimeSeries: (deviceId: string, from: string, to: string) =>
      ['heatpump', 'stored-time-series', deviceId, from, to] as const,
    consumption: (deviceId: string, from: string, to: string) =>
      ['heatpump', 'consumption', deviceId, from, to] as const,
    aiSummary: (deviceId: string) => ['heatpump', 'ai-summary', deviceId] as const,
    aiAnalysis: (deviceId: string, from: string, to: string, question?: string | null) =>
      ['heatpump', 'ai-analysis', deviceId, from, to, question] as const,
    rates: (accountNumber: string, from: string, to: string) =>
      ['heatpump', 'rates', accountNumber, from, to] as const,
    costData: (accountNumber: string, from: string, to: string) =>
      ['heatpump', 'cost', accountNumber, from, to] as const,
    storedCostData: (deviceId: string, from: string, to: string) =>
      ['heatpump', 'cost-stored', deviceId, from, to] as const,
  },
}
