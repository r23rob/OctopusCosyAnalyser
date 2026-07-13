import * as SecureStore from 'expo-secure-store';

const API_URL_KEY = 'cosydays_api_url';

export async function getApiUrl(): Promise<string | null> {
  return SecureStore.getItemAsync(API_URL_KEY);
}

export async function setApiUrl(url: string): Promise<void> {
  await SecureStore.setItemAsync(API_URL_KEY, url);
}

export async function clearApiUrl(): Promise<void> {
  await SecureStore.deleteItemAsync(API_URL_KEY);
}
