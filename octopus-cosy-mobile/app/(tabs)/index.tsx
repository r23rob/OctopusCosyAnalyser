import { View, Text, ScrollView, StyleSheet, ActivityIndicator, RefreshControl } from 'react-native';
import { useCallback, useState } from 'react';
import { useDevice } from '../../src/hooks/use-device';
import { useDashboard } from '../../src/hooks/use-dashboard';
import { useQueryClient } from '@tanstack/react-query';

function MetricCard({ label, value, unit, accent }: {
  label: string;
  value: string | number | null | undefined;
  unit?: string;
  accent?: boolean;
}) {
  return (
    <View style={[styles.card, accent && styles.cardAccent]}>
      <Text style={styles.cardLabel}>{label}</Text>
      <Text style={[styles.cardValue, accent && styles.cardValueAccent]}>
        {value ?? '—'}
        {unit && <Text style={styles.cardUnit}> {unit}</Text>}
      </Text>
    </View>
  );
}

function CopGauge({ cop }: { cop: number | null }) {
  const display = cop?.toFixed(2) ?? '—';
  const quality = cop == null ? 'unknown' : cop >= 3.5 ? 'great' : cop >= 2.5 ? 'good' : 'low';
  const qualityColor = quality === 'great' ? '#22C55E' : quality === 'good' ? '#06B6D4' : quality === 'low' ? '#F59E0B' : '#A1A1AA';

  return (
    <View style={styles.gaugeContainer}>
      <Text style={styles.gaugeLabel}>Live COP</Text>
      <Text style={[styles.gaugeValue, { color: qualityColor }]}>{display}</Text>
      <Text style={[styles.gaugeQuality, { color: qualityColor }]}>
        {quality === 'unknown' ? '' : quality.toUpperCase()}
      </Text>
    </View>
  );
}

export default function DashboardScreen() {
  const { device, deviceId, isLoading: deviceLoading } = useDevice();
  const { summary, latestSnapshot, isLoading, isError, error } = useDashboard(deviceId);
  const queryClient = useQueryClient();
  const [refreshing, setRefreshing] = useState(false);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    await queryClient.invalidateQueries({ queryKey: ['summary'] });
    await queryClient.invalidateQueries({ queryKey: ['latestSnapshot'] });
    setRefreshing(false);
  }, [queryClient]);

  if (deviceLoading || isLoading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color="#06B6D4" />
        <Text style={styles.loadingText}>Loading dashboard...</Text>
      </View>
    );
  }

  if (!device || !deviceId) {
    return (
      <View style={styles.center}>
        <Text style={styles.emptyTitle}>No device configured</Text>
        <Text style={styles.emptySubtitle}>
          Go to Settings to connect your API and set up your heat pump device.
        </Text>
      </View>
    );
  }

  if (isError) {
    return (
      <View style={styles.center}>
        <Text style={styles.errorText}>Failed to load data</Text>
        <Text style={styles.emptySubtitle}>{(error as Error)?.message}</Text>
      </View>
    );
  }

  const live = summary?.livePerformance;
  const lifetime = summary?.lifetimePerformance;
  const cop = live?.coefficientOfPerformance ? parseFloat(live.coefficientOfPerformance) : null;
  const outdoorTemp = live?.outdoorTemperature?.value ? parseFloat(live.outdoorTemperature.value) : null;
  const heatOutput = live?.heatOutput?.value ? parseFloat(live.heatOutput.value) : null;
  const powerInput = live?.powerInput?.value ? parseFloat(live.powerInput.value) : null;
  const seasonalCop = lifetime?.seasonalCoefficientOfPerformance
    ? parseFloat(lifetime.seasonalCoefficientOfPerformance) : null;

  const controllerStates = summary?.controllerConfiguration?.controller?.state ?? [];
  const stateLabel = controllerStates.length > 0 ? controllerStates.join(', ') : 'Unknown';

  const freshMinutes = latestSnapshot?.minutesAgo;
  const freshLabel = freshMinutes != null
    ? freshMinutes < 2 ? 'Just now' : `${Math.round(freshMinutes)}m ago`
    : null;

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor="#06B6D4" />}
    >
      <View style={styles.header}>
        <Text style={styles.title}>Cosydays</Text>
        {freshLabel && (
          <View style={styles.freshPill}>
            <View style={styles.freshDot} />
            <Text style={styles.freshText}>{freshLabel}</Text>
          </View>
        )}
      </View>

      <View style={styles.statusRow}>
        <View style={styles.statusPill}>
          <Text style={styles.statusText}>{stateLabel}</Text>
        </View>
      </View>

      <CopGauge cop={cop} />

      <View style={styles.metricsGrid}>
        <MetricCard label="Outdoor" value={outdoorTemp?.toFixed(1)} unit="°C" />
        <MetricCard label="Heat Output" value={heatOutput?.toFixed(2)} unit="kW" />
        <MetricCard label="Power Input" value={powerInput?.toFixed(2)} unit="kW" />
        <MetricCard label="Seasonal COP" value={seasonalCop?.toFixed(2)} accent />
      </View>

      {summary?.controllerStatus?.zones?.map((zone, i) => (
        <View key={i} style={styles.zoneCard}>
          <Text style={styles.zoneTitle}>{zone.zone ?? `Zone ${i + 1}`}</Text>
          <View style={styles.zoneRow}>
            <Text style={styles.zoneLabel}>Setpoint</Text>
            <Text style={styles.zoneValue}>
              {zone.telemetry?.setpointInCelsius?.toFixed(1) ?? '—'}°C
            </Text>
          </View>
          <View style={styles.zoneRow}>
            <Text style={styles.zoneLabel}>Mode</Text>
            <Text style={styles.zoneValue}>{zone.telemetry?.mode ?? '—'}</Text>
          </View>
          <View style={styles.zoneRow}>
            <Text style={styles.zoneLabel}>Heat demand</Text>
            <Text style={[styles.zoneValue, zone.telemetry?.heatDemand ? styles.demandOn : styles.demandOff]}>
              {zone.telemetry?.heatDemand ? 'YES' : 'No'}
            </Text>
          </View>
        </View>
      ))}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F8F8F9' },
  content: { padding: 16, paddingBottom: 32 },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 32, backgroundColor: '#F8F8F9' },
  loadingText: { marginTop: 12, color: '#71717A', fontSize: 14 },
  emptyTitle: { fontSize: 18, fontWeight: '600', color: '#09090B', marginBottom: 8 },
  emptySubtitle: { fontSize: 14, color: '#71717A', textAlign: 'center', lineHeight: 20 },
  errorText: { fontSize: 16, fontWeight: '600', color: '#EF4444', marginBottom: 8 },
  header: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 },
  title: { fontSize: 24, fontWeight: '700', color: '#09090B', letterSpacing: -0.5 },
  freshPill: { flexDirection: 'row', alignItems: 'center', backgroundColor: '#ECFDF5', paddingHorizontal: 10, paddingVertical: 4, borderRadius: 12 },
  freshDot: { width: 6, height: 6, borderRadius: 3, backgroundColor: '#22C55E', marginRight: 6 },
  freshText: { fontSize: 12, color: '#059669', fontWeight: '500' },
  statusRow: { marginBottom: 16 },
  statusPill: { alignSelf: 'flex-start', backgroundColor: '#E4E4E7', paddingHorizontal: 12, paddingVertical: 4, borderRadius: 8 },
  statusText: { fontSize: 12, fontWeight: '600', color: '#52525B', textTransform: 'uppercase', letterSpacing: 0.5 },
  gaugeContainer: { alignItems: 'center', paddingVertical: 24, marginBottom: 16 },
  gaugeLabel: { fontSize: 13, color: '#71717A', fontWeight: '500', marginBottom: 4 },
  gaugeValue: { fontSize: 56, fontWeight: '700', fontVariant: ['tabular-nums'], letterSpacing: -2 },
  gaugeQuality: { fontSize: 12, fontWeight: '600', letterSpacing: 1, marginTop: 2 },
  metricsGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 10, marginBottom: 16 },
  card: { flex: 1, minWidth: '45%', backgroundColor: '#FFFFFF', borderRadius: 12, padding: 14, borderWidth: 1, borderColor: '#E4E4E7' },
  cardAccent: { borderColor: '#06B6D4', borderWidth: 1.5 },
  cardLabel: { fontSize: 12, color: '#71717A', fontWeight: '500', marginBottom: 4 },
  cardValue: { fontSize: 22, fontWeight: '700', color: '#09090B', fontVariant: ['tabular-nums'] },
  cardValueAccent: { color: '#06B6D4' },
  cardUnit: { fontSize: 13, fontWeight: '500', color: '#A1A1AA' },
  zoneCard: { backgroundColor: '#FFFFFF', borderRadius: 12, padding: 16, marginBottom: 10, borderWidth: 1, borderColor: '#E4E4E7' },
  zoneTitle: { fontSize: 14, fontWeight: '600', color: '#09090B', marginBottom: 10, textTransform: 'capitalize' },
  zoneRow: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 4 },
  zoneLabel: { fontSize: 13, color: '#71717A' },
  zoneValue: { fontSize: 13, fontWeight: '600', color: '#09090B', fontVariant: ['tabular-nums'] },
  demandOn: { color: '#06B6D4' },
  demandOff: { color: '#A1A1AA' },
});
