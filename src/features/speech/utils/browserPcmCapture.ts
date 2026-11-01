// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { OpenPcmCapture, PcmCapture } from '../../../core-sdk/media/pcmCapture';
import { acquireUserMedia } from '../../../platform/browser/sessionActivity';
import { FLOAT_TO_INT16_PROCESSOR_NAME, FLOAT_TO_INT16_PROCESSOR_URL } from '../worklets';
import { flushCaptureWorkletNode } from './captureWorkletMessaging';

const aborted = () => Object.assign(new Error('Microphone capture was stopped.'), { name: 'AbortError' });
const stopTracks = (stream: MediaStream) => {
  for (const track of stream.getTracks()) {
    try { track.stop(); } catch { /* Release every track even if one fails. */ }
  }
};

/** The browser adapter is the sole owner of its stream, context and worklet.
 * Speech gating and provider connections remain in the existing consumers.
 */
export const openBrowserPcmCapture: OpenPcmCapture = async ({ signal } = {}) => {
  if (signal?.aborted) throw aborted();
  let stream: MediaStream | null = null;
  let context: AudioContext | null = null;
  let node: AudioWorkletNode | null = null;
  let source: MediaStreamAudioSourceNode | null = null;
  let closed = false;
  let started = false;
  let closing: Promise<void> | null = null;
  let flushing: Promise<void> | null = null;
  const close = (): Promise<void> => {
    if (closing) return closing;
    closed = true;
    if (node) {
      node.port.onmessage = null;
      try { node.disconnect(); } catch { /* Already disconnected. */ }
    }
    try { source?.disconnect(); } catch { /* Already disconnected. */ }
    if (stream) stopTracks(stream);
    const ownedContext = context;
    // Publish before calling any asynchronous resource, so all callers await
    // the same teardown. Tracks and delivery are already stopped synchronously.
    closing = Promise.resolve().then(async () => {
      if (ownedContext && ownedContext.state !== 'closed') {
        try { await ownedContext.close(); } catch { /* Device may already be gone. */ }
      }
      node = null; source = null; stream = null; context = null;
    });
    return closing;
  };
  let rejectAbort!: (reason: Error) => void;
  const cancellation = new Promise<never>((_, reject) => { rejectAbort = reject; });
  const onAbort = () => { void close(); rejectAbort(aborted()); };
  const wait = <T>(operation: Promise<T>) => Promise.race([operation, cancellation]);
  signal?.addEventListener('abort', onAbort, { once: true });
  try {
    const microphone = acquireUserMedia({ audio: {
      sampleRate: 16000, channelCount: 1, echoCancellation: true, noiseSuppression: true,
    } });
    // getUserMedia cannot be cancelled. A late result belongs only to this
    // acquisition; never install it into a replacement session.
    const accepted = microphone.then(late => {
      if (closed) { stopTracks(late); throw aborted(); }
      stream = late;
      return late;
    });
    await wait(accepted);
    if (closed || signal?.aborted) throw aborted();
    const AudioContextCtor = window.AudioContext || (window as any).webkitAudioContext;
    context = new AudioContextCtor({ sampleRate: 16000 });
    const ownedContext = context!;
    if (!ownedContext.audioWorklet) throw new Error('AudioWorklet is required for microphone capture.');
    await wait(ownedContext.audioWorklet.addModule(FLOAT_TO_INT16_PROCESSOR_URL));
    if (closed) throw aborted();
    if (ownedContext.state === 'suspended') {
      await wait(ownedContext.resume());
      if (closed) throw aborted();
      if (ownedContext.state === 'suspended') throw new Error('Could not start the microphone AudioContext.');
    }
    node = new AudioWorkletNode(ownedContext, FLOAT_TO_INT16_PROCESSOR_NAME, {
      numberOfInputs: 1, numberOfOutputs: 0,
    });
    source = ownedContext.createMediaStreamSource(stream!);
    const ownedNode = node;
    const ownedSource = source;
    const capture: PcmCapture = {
      sampleRate: 16000,
      start(sink) {
        if (closed) throw new Error('Microphone capture is closed.');
        if (started) throw new Error('Microphone capture already has a consumer.');
        started = true;
        ownedNode.port.onmessage = event => {
          if (!closed && event.data instanceof Int16Array && event.data.length) sink(event.data);
        };
        try { ownedSource.connect(ownedNode); }
        catch (error) { void close(); throw error; }
      },
      flush() {
        if (closed) return closing ?? Promise.resolve();
        if (!started) return Promise.resolve();
        if (!flushing) flushing = flushCaptureWorkletNode(ownedNode).finally(() => { flushing = null; });
        return flushing;
      },
      close,
    };
    return capture;
  } catch (error) {
    await close();
    throw error;
  } finally {
    signal?.removeEventListener('abort', onAbort);
  }
};
