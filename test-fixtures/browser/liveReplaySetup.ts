// Development-only setup for the full-app audio replay. Never imported by the app.
import { signInWithEmailAndPassword } from 'firebase/auth';
import { maestroFirebaseService } from '../../src/services/firebase/maestroFirebaseService';
import { googleAuthService } from '../../src/services/auth/googleAuthService';
import { useMaestroStore } from '../../src/store';
import { exportTurnTimings } from '../../src/core-sdk/turnTiming';

export async function prepare(email: string, password: string) {
  if (!import.meta.env.DEV || import.meta.env.VITE_FIREBASE_PROJECT_ID !== 'chatwithmaestro-staging') {
    throw new Error('This fixture requires a local development server configured for staging.');
  }
  const readyDeadline = performance.now() + 10000;
  while (!useMaestroStore.getState().isSettingsLoaded && performance.now() < readyDeadline) await new Promise(resolve => setTimeout(resolve, 25));
  const store = useMaestroStore.getState();
  const pair = store.languagePairs.find(pair => pair.targetLanguageCode.startsWith('cmn') && pair.nativeLanguageCode.startsWith('fi'));
  if (!pair) throw new Error('Finnish/Chinese language pair unavailable');
  store.setSettings(previous => ({ ...previous, selectedLanguagePairId: pair.id,
    sendWithSnapshotEnabled: false, enableGoogleSearch: false, stt: { ...previous.stt, enabled: false } }));
  store.setNeedsLanguageSelection(false);
  const messages = Array.from({ length: 240 }, (_, index) => ({
    id: `latency-fixture-${index}`, role: index % 2 ? 'assistant' as const : 'user' as const,
    timestamp: Date.now() - (241 - index) * 1000,
    text: index % 2
      ? 'Liu Bang perusti Han-dynastian. Xiang Yu oli hänen kilpailijansa. Keskustelemme Kiinan historiasta ja harjoittelemme kiinaa.'
      : 'Kerro lisää Kiinan historiasta, Liu Bangista ja Xiang Yusta. Selitä asiat lyhyesti suomeksi ja kiinaksi.',
  }));
  await signInWithEmailAndPassword(await maestroFirebaseService.getAuth(), email, password);
  await googleAuthService.restoreManagedSession();
  await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  const deadline = performance.now() + 10000;
  while (useMaestroStore.getState().isLoadingHistory && performance.now() < deadline) await new Promise(resolve => setTimeout(resolve, 25));
  store.setMessages(messages);
  return { historyMessages: useMaestroStore.getState().messages.length };
}

export const evidence = () => ({ ...JSON.parse(exportTurnTimings()), historyMessageCount: useMaestroStore.getState().messages.length, finalWordPresent: useMaestroStore.getState().messages.some(message => message.role === 'user' && !message.id.startsWith('latency-fixture-') && /minulle/i.test(message.text || '')) });
