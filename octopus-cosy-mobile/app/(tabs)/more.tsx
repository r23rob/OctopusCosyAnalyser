import { useState, useEffect, useCallback } from 'react';
import {
  View, Text, TextInput, ScrollView, StyleSheet,
  TouchableOpacity, Alert, ActivityIndicator,
} from 'react-native';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { getApiUrl, setApiUrl, clearApiUrl } from '../../src/lib/storage';
import { setBaseUrl, api } from '../../src/lib/api-client';
import { useDevice } from '../../src/hooks/use-device';
import { useFeatures } from '../../src/hooks/use-features';

export default function MoreScreen() {
  const [serverUrl, setServerUrl] = useState('');
  const [savedUrl, setSavedUrl] = useState<string | null>(null);
  const [testing, setTesting] = useState(false);
  const queryClient = useQueryClient();
  const { device, settings } = useDevice();
  const { data: features } = useFeatures();

  useEffect(() => {
    getApiUrl().then((url) => {
      if (url) {
        setServerUrl(url);
        setSavedUrl(url);
      }
    });
  }, []);

  const saveUrl = useCallback(async () => {
    const trimmed = serverUrl.trim().replace(/\/+$/, '');
    if (!trimmed) {
      Alert.alert('Error', 'Please enter your server URL');
      return;
    }

    setTesting(true);
    try {
      setBaseUrl(trimmed);
      const features = await api.features.getAvailability();
      await setApiUrl(trimmed);
      setSavedUrl(trimmed);
      queryClient.invalidateQueries();
      Alert.alert('Connected', `Server at ${trimmed} is reachable.`);
    } catch (e) {
      Alert.alert('Connection failed', `Could not reach ${trimmed}/api/features. Check the URL and try again.`);
    } finally {
      setTesting(false);
    }
  }, [serverUrl, queryClient]);

  const disconnect = useCallback(async () => {
    await clearApiUrl();
    setServerUrl('');
    setSavedUrl(null);
    setBaseUrl('');
    queryClient.clear();
  }, [queryClient]);

  const setupDevice = useMutation({
    mutationFn: (accountNumber: string) => api.devices.setup(accountNumber),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['devices'] });
      Alert.alert('Device found', `Device ${data.deviceId} registered.`);
    },
    onError: (err) => {
      Alert.alert('Setup failed', (err as Error).message);
    },
  });

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <Text style={styles.title}>Settings</Text>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Server Connection</Text>
        <Text style={styles.hint}>Enter your Cosydays API URL (CloudFront domain)</Text>
        <TextInput
          style={styles.input}
          value={serverUrl}
          onChangeText={setServerUrl}
          placeholder="https://d123abc.cloudfront.net"
          autoCapitalize="none"
          autoCorrect={false}
          keyboardType="url"
        />
        <View style={styles.buttonRow}>
          <TouchableOpacity style={styles.primaryButton} onPress={saveUrl} disabled={testing}>
            {testing ? (
              <ActivityIndicator color="#FFFFFF" size="small" />
            ) : (
              <Text style={styles.primaryButtonText}>Test & Save</Text>
            )}
          </TouchableOpacity>
          {savedUrl && (
            <TouchableOpacity style={styles.dangerButton} onPress={disconnect}>
              <Text style={styles.dangerButtonText}>Disconnect</Text>
            </TouchableOpacity>
          )}
        </View>
      </View>

      {savedUrl && (
        <>
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Status</Text>
            <StatusRow label="Server" value={savedUrl} />
            <StatusRow label="Database" value={features?.database ? 'Connected' : 'Lite mode'} />
            <StatusRow label="Live data" value={features?.liveData ? 'Available' : 'Unavailable'} />
            <StatusRow label="History" value={features?.history ? 'Available' : 'Unavailable'} />
          </View>

          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Device</Text>
            {device ? (
              <>
                <StatusRow label="Device ID" value={device.deviceId} />
                <StatusRow label="Account" value={device.accountNumber} />
                <StatusRow label="EUID" value={device.euid ?? '—'} />
                <StatusRow label="Last sync" value={device.lastSyncAt
                  ? new Date(device.lastSyncAt).toLocaleString()
                  : '—'} />
              </>
            ) : (
              <>
                <Text style={styles.hint}>No device registered yet.</Text>
                {settings?.accountNumber && (
                  <TouchableOpacity
                    style={styles.primaryButton}
                    onPress={() => setupDevice.mutate(settings.accountNumber)}
                    disabled={setupDevice.isPending}
                  >
                    <Text style={styles.primaryButtonText}>
                      {setupDevice.isPending ? 'Setting up...' : 'Discover Device'}
                    </Text>
                  </TouchableOpacity>
                )}
              </>
            )}
          </View>
        </>
      )}

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>About</Text>
        <StatusRow label="App" value="Cosydays v1.0.0" />
        <StatusRow label="Platform" value="Expo React Native" />
      </View>
    </ScrollView>
  );
}

function StatusRow({ label, value }: { label: string; value?: string | null }) {
  return (
    <View style={styles.statusRow}>
      <Text style={styles.statusLabel}>{label}</Text>
      <Text style={styles.statusValue} numberOfLines={1}>{value ?? '—'}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F8F8F9' },
  content: { padding: 16, paddingBottom: 48 },
  title: { fontSize: 20, fontWeight: '600', color: '#09090B', marginBottom: 20, letterSpacing: -0.3 },
  section: { backgroundColor: '#FFFFFF', borderRadius: 12, padding: 16, marginBottom: 16, borderWidth: 1, borderColor: '#E4E4E7' },
  sectionTitle: { fontSize: 15, fontWeight: '600', color: '#09090B', marginBottom: 10 },
  hint: { fontSize: 13, color: '#71717A', marginBottom: 10, lineHeight: 18 },
  input: { backgroundColor: '#F4F4F5', borderRadius: 8, padding: 12, fontSize: 14, color: '#09090B', borderWidth: 1, borderColor: '#E4E4E7', marginBottom: 12 },
  buttonRow: { flexDirection: 'row', gap: 10 },
  primaryButton: { flex: 1, backgroundColor: '#06B6D4', borderRadius: 8, paddingVertical: 12, alignItems: 'center', justifyContent: 'center', minHeight: 44 },
  primaryButtonText: { color: '#FFFFFF', fontWeight: '600', fontSize: 14 },
  dangerButton: { backgroundColor: '#FEE2E2', borderRadius: 8, paddingVertical: 12, paddingHorizontal: 16, alignItems: 'center', justifyContent: 'center', minHeight: 44 },
  dangerButtonText: { color: '#DC2626', fontWeight: '600', fontSize: 14 },
  statusRow: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 6 },
  statusLabel: { fontSize: 13, color: '#71717A' },
  statusValue: { fontSize: 13, fontWeight: '500', color: '#09090B', maxWidth: '60%' },
});
