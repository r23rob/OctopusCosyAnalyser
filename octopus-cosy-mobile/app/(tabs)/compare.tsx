import { View, Text, ScrollView, StyleSheet, ActivityIndicator } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { useDevice } from '../../src/hooks/use-device';
import { api } from '../../src/lib/api-client';

export default function CompareScreen() {
  const { deviceId, isLoading: deviceLoading } = useDevice();

  const now = new Date();
  const thisWeekStart = new Date(now);
  thisWeekStart.setDate(thisWeekStart.getDate() - 7);
  const lastWeekStart = new Date(thisWeekStart);
  lastWeekStart.setDate(lastWeekStart.getDate() - 7);

  const currentPeriod = useQuery({
    queryKey: ['periodSummary', deviceId, 'current'],
    queryFn: () => api.heatpump.getPeriodSummary(deviceId!, thisWeekStart.toISOString(), now.toISOString()),
    enabled: !!deviceId,
  });

  const previousPeriod = useQuery({
    queryKey: ['periodSummary', deviceId, 'previous'],
    queryFn: () => api.heatpump.getPeriodSummary(deviceId!, lastWeekStart.toISOString(), thisWeekStart.toISOString()),
    enabled: !!deviceId,
  });

  if (deviceLoading || currentPeriod.isLoading || previousPeriod.isLoading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color="#06B6D4" />
      </View>
    );
  }

  if (!deviceId) {
    return (
      <View style={styles.center}>
        <Text style={styles.emptyText}>No device configured</Text>
      </View>
    );
  }

  const current = currentPeriod.data;
  const previous = previousPeriod.data;

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <Text style={styles.title}>This Week vs Last Week</Text>

      <CompareRow label="Avg COP" current={current?.avgCop} previous={previous?.avgCop} format={(v) => v.toFixed(2)} higherIsBetter />
      <CompareRow label="Total Input" current={current?.totalInputKwh} previous={previous?.totalInputKwh} format={(v) => `${v.toFixed(1)} kWh`} higherIsBetter={false} />
      <CompareRow label="Total Output" current={current?.totalOutputKwh} previous={previous?.totalOutputKwh} format={(v) => `${v.toFixed(1)} kWh`} higherIsBetter />
      <CompareRow label="Avg Outdoor" current={current?.avgOutdoorTemp} previous={previous?.avgOutdoorTemp} format={(v) => `${v.toFixed(1)}°C`} />
      <CompareRow label="Avg Room" current={current?.avgRoomTemp} previous={previous?.avgRoomTemp} format={(v) => `${v.toFixed(1)}°C`} />
      <CompareRow label="Heating Duty" current={current?.heatingDutyCyclePercent} previous={previous?.heatingDutyCyclePercent} format={(v) => `${v.toFixed(0)}%`} />
    </ScrollView>
  );
}

function CompareRow({ label, current, previous, format, higherIsBetter }: {
  label: string;
  current?: number | null;
  previous?: number | null;
  format?: (v: number) => string;
  higherIsBetter?: boolean;
}) {
  const fmt = format ?? ((v: number) => v.toFixed(1));
  const diff = current != null && previous != null && previous !== 0
    ? ((current - previous) / Math.abs(previous)) * 100
    : null;

  const diffColor = diff == null ? '#A1A1AA'
    : higherIsBetter == null ? '#71717A'
    : (diff > 0) === higherIsBetter ? '#22C55E' : '#EF4444';

  return (
    <View style={styles.compareRow}>
      <Text style={styles.compareLabel}>{label}</Text>
      <View style={styles.compareValues}>
        <Text style={styles.compareValue}>{current != null ? fmt(current) : '—'}</Text>
        <Text style={styles.comparePrev}>{previous != null ? fmt(previous) : '—'}</Text>
        {diff != null && (
          <Text style={[styles.compareDiff, { color: diffColor }]}>
            {diff > 0 ? '+' : ''}{diff.toFixed(1)}%
          </Text>
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F8F8F9' },
  content: { padding: 16, paddingBottom: 32 },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: '#F8F8F9' },
  title: { fontSize: 20, fontWeight: '600', color: '#09090B', marginBottom: 16, letterSpacing: -0.3 },
  emptyText: { fontSize: 14, color: '#71717A', textAlign: 'center', marginTop: 24 },
  compareRow: { backgroundColor: '#FFFFFF', borderRadius: 12, padding: 14, marginBottom: 10, borderWidth: 1, borderColor: '#E4E4E7' },
  compareLabel: { fontSize: 13, color: '#71717A', fontWeight: '500', marginBottom: 6 },
  compareValues: { flexDirection: 'row', alignItems: 'baseline', gap: 12 },
  compareValue: { fontSize: 20, fontWeight: '700', color: '#09090B', fontVariant: ['tabular-nums'] },
  comparePrev: { fontSize: 14, color: '#A1A1AA', fontVariant: ['tabular-nums'] },
  compareDiff: { fontSize: 13, fontWeight: '600', fontVariant: ['tabular-nums'] },
});
