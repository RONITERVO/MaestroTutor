// @vitest-environment jsdom
import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, it, expect, vi } from 'vitest';
const mock = vi.hoisted(() => ({ pending: null as null | ((n: number) => void), flush: vi.fn(async () => {}), close: vi.fn(async () => {}), trigger: vi.fn() }));
vi.mock('../utils/localWhisperClient', () => ({ acquireLocalWhisperClient: () => ({}), releaseLocalWhisperClient: vi.fn() }));
vi.mock('../../../api/gemini/client', () => ({ getAi: () => new Promise(() => {}) }));
vi.mock('../utils/localSpeechTrigger', () => ({ waitForLocalSpeechTrigger: mock.trigger }));
import { useGeminiLiveStt } from './useGeminiLiveStt';
import { sessionActivity } from '../../../platform/browser/sessionActivity';
beforeEach(() => {
  mock.pending = null; mock.flush.mockReset().mockResolvedValue(undefined); mock.close.mockReset().mockResolvedValue(undefined);
  mock.trigger.mockReset().mockImplementation(async (options: any) => {
    mock.pending = options.onPendingSpeechSamples;
    return { transcript: 'unreliable whisper words', pcm: new Int16Array(16000),
      capture: { flush: mock.flush, close: mock.close } };
  });
});
describe('STT concealed speech', () => {
  it('waits for an existing stop before acknowledging host shutdown', async () => {
    const { result, unmount } = renderHook(() => useGeminiLiveStt());
    act(() => { void result.current.start('fi-FI'); });
    await waitFor(() => expect(result.current.speechPreviewProgress).toBe(3));
    let finish!: () => void;
    mock.flush.mockImplementationOnce(() => new Promise<void>(resolve => { finish = resolve; }));
    let stopped!: Promise<void>;
    await act(async () => { stopped = result.current.stop(); await Promise.resolve(); sessionActivity.setSuspended(true); });
    expect(sessionActivity.status().settled).toBe(false);
    await act(async () => { finish(); await stopped; });
    expect(sessionActivity.status().settled).toBe(true);
    act(() => { sessionActivity.setSuspended(false); sessionActivity.resume(); });
    unmount();
  });
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
  it('closes a late trigger without replacing the current microphone owner', async () => {
    let resolveOld!: (value: any) => void;
    mock.trigger.mockImplementationOnce(() => new Promise(resolve => { resolveOld = resolve; }));
    const oldCapture = { close: vi.fn(async () => {}), flush: vi.fn() };
    const { result, unmount } = renderHook(() => useGeminiLiveStt());
    let first!: Promise<void>;
    act(() => { first = result.current.start('en-US'); });
    await waitFor(() => expect(mock.trigger).toHaveBeenCalledOnce());
    act(() => { void result.current.start('en-US'); });
    await waitFor(() => expect(result.current.speechPreviewProgress).toBe(3));
    await act(async () => {
      resolveOld({ pcm: new Int16Array(16000), transcript: 'old', capture: oldCapture }); await first;
    });
    expect(oldCapture.close).toHaveBeenCalledOnce(); expect(mock.close).not.toHaveBeenCalled();
    await act(async () => { await result.current.stop(); });
    expect(mock.close).toHaveBeenCalledOnce(); expect(oldCapture.close).toHaveBeenCalledOnce();
    unmount();
  });

  it('closes microphone ownership even when the flush fails', async () => {
    const warning = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const { result, unmount } = renderHook(() => useGeminiLiveStt());
    act(() => { void result.current.start('en-US'); });
    await waitFor(() => expect(result.current.speechPreviewProgress).toBe(3));
    mock.flush.mockRejectedValueOnce(new Error('capture unavailable'));
    await act(async () => { await result.current.stop(); });
    expect(mock.close).toHaveBeenCalledOnce(); expect(result.current.isListening).toBe(false);
    unmount(); warning.mockRestore();
  });

  it('does not acquire a microphone when Stop wins during startup cleanup', async () => {
    const { result, unmount } = renderHook(() => useGeminiLiveStt());
    await act(async () => {
      const starting = result.current.start('en-US');
      const stopping = result.current.stop();
      await Promise.all([starting, stopping]);
    });
    expect(mock.trigger).not.toHaveBeenCalled(); expect(result.current.isListening).toBe(false);
    unmount();
  });

});
