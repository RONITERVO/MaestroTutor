// @vitest-environment jsdom
import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
const mock = vi.hoisted(() => ({ pending: null as null | ((n: number) => void) }));
vi.mock('../utils/localWhisperClient', () => ({ acquireLocalWhisperClient: () => ({}), releaseLocalWhisperClient: vi.fn() }));
vi.mock('../utils/captureWorkletMessaging', () => ({ flushCaptureWorkletNode: async () => {} }));
vi.mock('../../../api/gemini/client', () => ({ getAi: () => new Promise(() => {}) }));
vi.mock('../utils/localSpeechTrigger', () => ({ waitForLocalSpeechTrigger: async (options: any) => {
  mock.pending = options.onPendingSpeechSamples;
  return { transcript: 'unreliable whisper words', pcm: new Int16Array(16000),
    microphoneStream: { getTracks: () => [] }, capture: {
      audioContext: { close: async () => {} }, workletNode: { port: {}, disconnect: () => {} }, close: async () => {},
    } };
} }));
import { useGeminiLiveStt } from './useGeminiLiveStt';
describe('STT concealed speech', () => {
  it('conceals Whisper, accumulates one mark per captured second, and clears on stop', async () => {
    const { result, unmount } = renderHook(() => useGeminiLiveStt());
    act(() => { void result.current.start('fi-FI'); });
    await waitFor(() => expect(result.current.speechPreviewProgress).toBe(3));
    expect(result.current.transcript).toBe('');
    act(() => mock.pending!(15999));
    expect(result.current.speechPreviewProgress).toBe(3);
    act(() => mock.pending!(1));
    expect(result.current.speechPreviewProgress).toBe(4);
    act(() => mock.pending!(32000));
    expect(result.current.speechPreviewProgress).toBe(6);
    expect(result.current.transcript).toBe('');
    await act(async () => { await result.current.stop(); });
    expect(result.current.speechPreviewProgress).toBe(0);
    act(() => mock.pending!(16000));
    expect(result.current.speechPreviewProgress).toBe(0);
    unmount();
  });
});
