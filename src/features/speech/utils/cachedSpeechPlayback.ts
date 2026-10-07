// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { SpeechOutput } from '../../../core-sdk/media/speechOutput';
import { ScheduledSpeechOutput } from './scheduledSpeechOutput';
import { selectSpeechOutput } from './selectSpeechOutput';

const RATE = 24000, MAX_SECONDS = 120, MAX_ENCODED = 16 * 1024 * 1024;
const CANCELLED = Symbol('cancelled');

/** Decode a local speech cache without connecting it to an audible browser node.
 * Offline decoding resamples even when the physical device runs at 48 kHz. */
export async function decodeCachedSpeech(dataUrl: string): Promise<Int16Array> {
  if (dataUrl.length > MAX_ENCODED) throw new Error('Saved speech is too large.');
  const comma = dataUrl.indexOf(',');
  if (comma < 0 || !/^data:audio\/[\w.+-]+;base64$/i.test(dataUrl.slice(0, comma)))
    throw new Error('Saved speech must be an inline audio recording.');
  const binary = atob(dataUrl.slice(comma + 1));
  const bytes = Uint8Array.from(binary, char => char.charCodeAt(0));
  const decoder = new OfflineAudioContext(1, 1, RATE);
  const audio = await decoder.decodeAudioData(bytes.buffer);
  if (audio.sampleRate !== RATE || audio.length < 1 || audio.length > RATE * MAX_SECONDS
    || ![1, 2].includes(audio.numberOfChannels)) throw new Error('Saved speech has an unsupported duration or format.');
  const channels = Array.from({ length: audio.numberOfChannels }, (_, index) => audio.getChannelData(index));
  const pcm = new Int16Array(audio.length);
  for (let i = 0; i < pcm.length; i++) {
    const sample = channels.reduce((sum, channel) => sum + channel[i], 0) / channels.length;
    if (!Number.isFinite(sample)) throw new Error('Saved speech contains invalid samples.');
    pcm[i] = Math.max(-32768, Math.min(32767, Math.round(sample * 32768)));
  }
  return pcm;
}

/** One owned replay. Stop also fences uncancellable decoder/context promises;
 * their eventual completion cannot open or dispose a newer voice. */
export async function playCachedSpeech(options: {
  audioDataUrl: string;
  getAudioContext: () => Promise<AudioContext>;
  signal: AbortSignal;
}): Promise<'drained' | 'cancelled'> {
  const { signal } = options;
  if (signal.aborted) return 'cancelled';
  let output: SpeechOutput | undefined, finished = false;
  let cancel!: (value: typeof CANCELLED) => void, fail!: (error: Error) => void;
  const cancellation = new Promise<typeof CANCELLED>(resolve => { cancel = resolve; });
  const failure = new Promise<never>((_, reject) => { fail = reject; });
  // Also handle an adapter that reports a synchronous failure during creation.
  void failure.catch(() => undefined);
  const abort = () => { output?.dispose(); cancel(CANCELLED); };
  signal.addEventListener('abort', abort, { once: true });
  const phase = async <T,>(operation: Promise<T>, milliseconds: number): Promise<T | typeof CANCELLED> => {
    let timer: ReturnType<typeof setTimeout> | undefined;
    try {
      return await Promise.race([operation, cancellation, failure, new Promise<never>((_, reject) => {
        timer = setTimeout(() => reject(new Error('Saved speech playback stopped responding.')), milliseconds);
      })]);
    } finally { clearTimeout(timer); }
  };
  try {
    // Invoke synchronously so a browser AudioContext can resume in the click's
    // user activation. Quest never creates that audible browser context.
    const pending = Promise.resolve(selectSpeechOutput(async () =>
      new ScheduledSpeechOutput(await options.getAudioContext()), { onError: fail }));
    void pending.then(value => { if (finished || signal.aborted) value.dispose(); }, () => undefined);
    const selected = await phase(pending, 10000);
    if (selected === CANCELLED) return 'cancelled';
    output = selected;
    if (signal.aborted) return 'cancelled';
    const pcm = await phase(decodeCachedSpeech(options.audioDataUrl), 10000);
    if (pcm === CANCELLED || signal.aborted) return 'cancelled';
    output.write(pcm);
    const result = await phase(output.drain(), pcm.length / RATE * 1000 + 5000);
    return result === CANCELLED ? 'cancelled' : result;
  } finally {
    finished = true; signal.removeEventListener('abort', abort); output?.dispose();
  }
}
