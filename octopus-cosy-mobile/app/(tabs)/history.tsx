import { View, Text, ScrollView, StyleSheet, ActivityIndicator } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { useDevice } from '../../src/hooks/use-device';
import { api } from '../../src/lib/api-client';

export default function HistoryScreen() {
  const { deviceId, isLoading: deviceLoading } = useDevice();

  const today = new Date();
  const weekAgo = new Date(today);
  weekAgo.setDate(weekAgo.getDate() - 7);

  const { data: aggregates, isLoading } = useQuery({
    queryKey: ['dailyAggregates', deviceId, weekAgo.toISOString(), today.toISOString()],
    queryFn: () => api.heatpump.getDailyAggregates(deviceId!, weekAgo.toISOString(), today.toISOString()),
    enabled: !!deviceId,
  });

  if (deviceLoading || isLoading) {
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

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <Text style={styles.title}>Last 7 Days</Text>

      {aggregates && aggregates.length > 0 ? (
        aggregates.map((day) => {
          const date = new Date(day.date);
          const label = date.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' });
          return (
            <View key={day.date} style={styles.dayCard}>
              <Text style={styles.dayLabel}>{label}</Text>
              <View style={styles.dayMetrics}>
                <View style={styles.dayMetric}>
                  <Text style={styles.metricLabel}>COP</Text>
                  <Text style={styles.metricValue}>{day.avgCopHeating?.toFixed(2) ?? '—'}</Text>
                </View>
                <View style={styles.dayMetric}>
                  <Text style={styles.metricLabel}>kWh In</Text>
                  <Text style={styles.metricValue}>{day.totalElectricityKwh.toFixed(1)}</Text>
                </View>
                <View style={styles.dayMetric}>
                  <Text style={styles.metricLabel}>kWh Out</Text>
                  <Text style={styles.metricValue}>{day.totalHeatOutputKwh.toFixed(1)}</Text>
                </View>
                <View style={styles.dayMetric}>
                  <Text style={styles.metricLabel}>Outdoor</Text>
                  <Text style={styles.metricValue}>{day.avgOutdoorTemp?.toFixed(1) ?? '—'}°</Text>
                </View>
                {day.dailyCostPence != null && (
                  <View style={styles.dayMetric}>
                    <Text style={styles.metricLabel}>Cost</Text>
                    <Text style={styles.metricValue}>£{(day.dailyCostPence / 100).toFixed(2)}</Text>
                  </View>
                )}
              </View>
            </View>
          );
        })
      ) : (
        <Text style={styles.emptyText}>No data for the last 7 days</Text>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F8F8F9' },
  content: { padding: 16, paddingBottom: 32 },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: '#F8F8F9' },
  title: { fontSize: 20, fontWeight: '600', color: '#09090B', marginBottom: 16, letterSpacing: -0.3 },
  emptyText: { fontSize: 14, color: '#71717A', textAlign: 'center', marginTop: 24 },
  dayCard: { backgroundColor: '#FFFFFF', borderRadius: 12, padding: 14, marginBottom: 10, borderWidth: 1, borderColor: '#E4E4E7' },
  dayLabel: { fontSize: 14, fontWeight: '600', color: '#09090B', marginBottom: 10 },
  dayMetrics: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  dayMetric: { minWidth: 60 },
  metricLabel: { fontSize: 11, color: '#A1A1AA', fontWeight: '500', marginBottom: 2 },
  metricValue: { fontSize: 15, fontWeight: '700', color: '#09090B', fontVariant: ['tabular-nums'] },
});
