// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import { createCoreEventJournal } from '../events';
import type { CoreGeminiClient } from '../managedGeminiClient';
import { createCoreRuntime } from '../runtime';
import { createSyntheticPcmSource } from './pcmInput';
import { runSyntheticLiveJourney } from './syntheticLiveJourney';
import { LIVE_OPEN_TRIGGER } from '../../../shared/liveOpenReason';

describe('synthetic Live journey', () => {
  it('captures through a delayed UI handoff and drains model audio at playback pace', async () => {
    const sent: any[] = [];
    let callbacks: Record<string, (...args: any[]) => void> = {};
    let finishConnect: ((session: any) => void) | undefined;
    const modelPcmBase64 = btoa(String.fromCharCode(...new Uint8Array(4_800)));
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        sent.push(message);
        if (message.audioStreamEnd) queueMicrotask(() => callbacks.onmessage?.({
          serverContent: {
            inputTranscription: { text: 'the final words are preserved' },
            outputTranscription: { text: 'I heard the final words.' },
            modelTurn: { parts: [{ inlineData: { mimeType: 'audio/pcm;rate=24000', data: modelPcmBase64 } }] },
            turnComplete: true,
          },
        }));
      }),
      close: vi.fn(),
    };
    const ai = {
      models: {} as CoreGeminiClient['models'],
      live: {
        connect: vi.fn((request: any) => {
          callbacks = request.callbacks;
          return new Promise<any>(resolve => { finishConnect = resolve; });
        }),
        music: { connect: vi.fn() },
      },
    } as CoreGeminiClient;
    let now = 1_000;
    const sleeps: number[] = [];
    const runtime = createCoreRuntime({
      clock: {
        now: () => now,
        sleep: async (ms) => {
          sleeps.push(ms);
          now += ms;
        },
        setInterval: () => 0,
        clearInterval: () => undefined,
      },
    });
    const pcm = new Int16Array(32_000).fill(7_000);
    const journey = runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({ pcm, sampleRate: 16_000, pace: true, runtime }),
      gateInputOnSpeech: true,
      semanticSpeech: true,
      simulateUiSpeechHandoff: true,
      requireRealtimeInputPacing: true,
      playModelAudioRealtime: true,
    }, { runtime });

    await vi.waitFor(() => expect(ai.live.connect).toHaveBeenCalledOnce());
    expect(sent).toHaveLength(0);
    finishConnect?.(session);
    const result = await journey;

    expect(result.inputTranscript).toContain('final words');
    expect(result.sentSamples).toBe(pcm.length);
    expect(result.timing.uiSpeechHandoff).toBe(true);
    expect(result.timing.connectionHandoffSamples).toBeGreaterThanOrEqual(19_200);
    expect(result.timing.inputCaptureElapsedMs).toBeGreaterThanOrEqual(1_900);
    expect(result.timing.modelPlaybackRealtime).toBe(true);
    expect(result.timing.modelAudioDurationMs).toBe(100);
    expect(result.timing.modelPlaybackElapsedMs).toBe(100);
    expect(sleeps.some(ms => ms === 100)).toBe(true);
    expect(result.realtimeEvidence).toEqual({
      required: true,
      inputPacingPassed: true,
      providerInputPacingPassed: true,
      modelPlaybackPassed: true,
      uiSpeechHandoffPassed: true,
      passed: true,
    });
  });

  it('does not open Live when the UI-style pre-connect capture never confirms speech', async () => {
    const ai = {
      models: {} as CoreGeminiClient['models'],
      live: {
        connect: vi.fn(),
        music: { connect: vi.fn() },
      },
    } as CoreGeminiClient;
    const silence = new Int16Array(16_000);

    await expect(runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({ pcm: silence, sampleRate: 16_000, pace: false }),
      gateInputOnSpeech: true,
      semanticSpeech: true,
      simulateUiSpeechHandoff: true,
    })).rejects.toThrow('ended before semantic speech was confirmed');
    expect(ai.live.connect).not.toHaveBeenCalled();
  });

  it('attaches the paid Live request ID and provider evidence to a timeout', async () => {
    vi.useFakeTimers();
    try {
      const session = { sendRealtimeInput: vi.fn(), close: vi.fn() };
      const ai = {
        models: {} as CoreGeminiClient['models'],
        live: {
          connect: vi.fn(async (request: any) => {
            request.callbacks.onmessage?.({ setupComplete: {} });
            return session;
          }),
          music: { connect: vi.fn() },
        },
      } as CoreGeminiClient;
      const failure = runSyntheticLiveJourney(ai, {
        liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
        source: createSyntheticPcmSource({
          pcm: new Int16Array(16_000).fill(6_000),
          sampleRate: 16_000,
          pace: false,
        }),
        gateInputOnSpeech: false,
        timeoutMs: 1_000,
      }).catch(error => error as Error & {
        operationId?: string;
        liveDiagnostics?: Record<string, unknown>;
      });

      await vi.advanceTimersByTimeAsync(1_100);
      const error = await failure as Error & {
        operationId?: string;
        liveDiagnostics?: Record<string, unknown>;
      };
      expect(error.message).toContain('with 1 server messages (setupComplete)');
      expect(error.operationId).toMatch(/^synthetic-live-/);
      expect(error.liveDiagnostics).toMatchObject({
        serverMessageCount: 1,
        serverMessageKinds: ['setupComplete'],
        inputTranscriptLength: 0,
        outputTranscriptLength: 0,
        modelAudioSampleCount: 0,
      });
    } finally {
      vi.useRealTimers();
    }
  });

  it('routes PCM through capture, packetizer, speech gate and the real Live client contract', async () => {
    const sent: any[] = [];
    let callbacks: Record<string, (...args: any[]) => void> = {};
    let completed = false;
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        sent.push(message);
        if (message.audioStreamEnd && !completed) {
          completed = true;
          queueMicrotask(() => callbacks.onmessage?.({
            serverContent: {
              inputTranscription: { text: 'hola' },
              outputTranscription: { text: 'Hola.' },
              modelTurn: { parts: [{ inlineData: { mimeType: 'audio/pcm;rate=24000', data: 'AA==' } }] },
              turnComplete: true,
            },
          }));
        }
      }),
      close: vi.fn(),
    };
    const ai: CoreGeminiClient = {
      models: {
        generateContent: vi.fn(),
        generateContentStream: vi.fn(),
      },
      live: {
        connect: vi.fn(async (request: any) => {
          callbacks = request.callbacks;
          callbacks.onopen?.();
          return session;
        }),
        music: { connect: vi.fn() },
      },
    };
    const events = createCoreEventJournal({ now: () => 1_000 });
    const runtime = createCoreRuntime({
      events,
      clock: {
        now: () => 1_000,
        sleep: async () => undefined,
        setInterval: () => 0,
        clearInterval: () => undefined,
      },
    });
    const pcm = new Int16Array(32_000);
    pcm.fill(6_000);
    const source = createSyntheticPcmSource({ pcm, sampleRate: 16_000, pace: false, runtime });

    const result = await runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source,
      gateInputOnSpeech: true,
      semanticSpeech: true,
      includeModelAudio: true,
      videoFrames: [{ dataBase64: 'data:image/png;base64,AA==' }],
    }, { runtime });

    expect(result.transcript).toBe('Hola.');
    expect(result.sentSamples).toBeGreaterThan(0);
    expect(result.packetizer.totalInputSamples).toBe(32_000);
    expect(result.gate.gatedPackets).toBeGreaterThan(0);
    expect(result.modelAudioChunksBase64).toEqual(['AA==']);
    expect(ai.live.connect).toHaveBeenCalledWith(expect.objectContaining({
      liveOpenReason: expect.objectContaining({ trigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE }),
    }));
    expect(sent.some(message => message.audio?.mimeType === 'audio/pcm;rate=16000')).toBe(true);
    expect(sent.some(message => message.video?.mimeType === 'image/png')).toBe(true);
    expect(events.snapshot().some(event => event.phase === 'input.frame' && event.data?.source === 'synthetic')).toBe(true);
  });

  it('does not send a sub-1.2-second utterance to the Live provider', async () => {
    let callbacks: Record<string, (...args: any[]) => void> = {};
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        if (message.audioStreamEnd) queueMicrotask(() => callbacks.onmessage?.({
          serverContent: {
            outputTranscription: { text: 'No audio received.' },
            turnComplete: true,
          },
        }));
      }),
      close: vi.fn(),
    };
    const ai = {
      models: {} as CoreGeminiClient['models'],
      live: {
        connect: vi.fn(async (request: any) => {
          callbacks = request.callbacks;
          return session;
        }),
        music: { connect: vi.fn() },
      },
    } as CoreGeminiClient;
    const pcm = new Int16Array(16_000);
    pcm.fill(6_000, 0, 8_000);

    const result = await runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({ pcm, sampleRate: 16_000, pace: false }),
      gateInputOnSpeech: true,
      semanticSpeech: true,
    });

    expect(result.sentSamples).toBe(0);
    expect(session.sendRealtimeInput).not.toHaveBeenCalledWith(expect.objectContaining({ audio: expect.anything() }));
  });

  it('keeps a final spoken clause after a natural one-second pause in the same turn', async () => {
    let callbacks: Record<string, (...args: any[]) => void> = {};
    const sent: any[] = [];
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        sent.push(message);
        if (message.audioStreamEnd) queueMicrotask(() => callbacks.onmessage?.({
          serverContent: {
            inputTranscription: { text: 'Hello, how are you doing? I am doing great.' },
            outputTranscription: { text: 'I am glad you are doing great.' },
            turnComplete: true,
          },
        }));
      }),
      close: vi.fn(),
    };
    const ai = {
      models: {} as CoreGeminiClient['models'],
      live: {
        connect: vi.fn(async (request: any) => {
          callbacks = request.callbacks;
          return session;
        }),
        music: { connect: vi.fn() },
      },
    } as CoreGeminiClient;
    const pcm = new Int16Array(64_000);
    pcm.fill(6_000, 0, 22_400);
    pcm.fill(6_000, 40_000, 51_200);

    const result = await runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({ pcm, sampleRate: 16_000, pace: false }),
      gateInputOnSpeech: true,
      semanticSpeech: true,
    });

    expect(result.inputTranscript).toContain('doing great');
    expect(result.sentSamples).toBe(64_000);
    expect(result.gate.streamEnds).toBe(1);
    expect(sent.filter(message => message.audioStreamEnd)).toHaveLength(1);
  });

  it('streams every packet across a two-second pause under one manual activity boundary', async () => {
    let callbacks: Record<string, (...args: any[]) => void> = {};
    const sent: any[] = [];
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        sent.push(message);
        if (message.audioStreamEnd) queueMicrotask(() => callbacks.onmessage?.({
          serverContent: {
            inputTranscription: { text: 'The first clause and the second clause.' },
            outputTranscription: { text: 'The first clause and the second clause.' },
            turnComplete: true,
          },
        }));
      }),
      close: vi.fn(),
    };
    const ai = {
      models: {} as CoreGeminiClient['models'],
      live: {
        connect: vi.fn(async (params: any) => {
          callbacks = params.callbacks;
          return session;
        }),
        music: { connect: vi.fn() },
      },
    } as CoreGeminiClient;
    const pcm = new Int16Array(96_000);
    pcm.fill(6_000, 0, 24_000);
    pcm.fill(6_000, 56_000, 72_000);

    const result = await runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({ pcm, sampleRate: 16_000, pace: false }),
      gateInputOnSpeech: true,
      semanticSpeech: true,
    });

    expect(result.sentSamples).toBe(pcm.length);
    expect(sent.filter(message => message.activityStart)).toHaveLength(1);
    expect(sent.filter(message => message.activityEnd)).toHaveLength(1);
    expect(sent.filter(message => message.audioStreamEnd)).toHaveLength(1);
    expect(ai.live.connect).toHaveBeenCalledWith(expect.objectContaining({
      config: expect.objectContaining({
        realtimeInputConfig: expect.objectContaining({
          automaticActivityDetection: { disabled: true },
        }),
      }),
    }));
  });

  it('keeps one Live connection across six audio-video turns and sums cumulative provider usage', async () => {
    let callbacks: Record<string, (...args: any[]) => void> = {};
    const sent: any[] = [];
    let completedTurns = 0;
    const modelPcmBase64 = btoa(String.fromCharCode(...new Uint8Array(4_800)));
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        sent.push(message);
        if (!message.audioStreamEnd) return;
        completedTurns += 1;
        const turn = completedTurns;
        queueMicrotask(() => callbacks.onmessage?.({
          usageMetadata: {
            promptTokenCount: turn * 100,
            responseTokenCount: 10,
            totalTokenCount: turn * 100 + 10,
          },
          serverContent: {
            inputTranscription: { text: `Input ${turn}.` },
            outputTranscription: { text: `Reply ${turn}.` },
            modelTurn: {
              parts: [{ inlineData: { mimeType: 'audio/pcm;rate=24000', data: modelPcmBase64 } }],
            },
            turnComplete: true,
          },
        }));
      }),
      close: vi.fn(),
    };
    const ai = {
      models: {} as CoreGeminiClient['models'],
      live: {
        connect: vi.fn(async (params: any) => {
          callbacks = params.callbacks;
          return session;
        }),
        music: { connect: vi.fn() },
      },
    } as CoreGeminiClient;
    let now = 1_000;
    const runtime = createCoreRuntime({
      clock: {
        now: () => now,
        sleep: async (ms) => { now += ms; },
        setInterval: () => 0,
        clearInterval: () => undefined,
      },
    });
    const first = new Int16Array(32_000).fill(6_000);
    const shortFollowup = new Int16Array(24_000);
    shortFollowup.fill(6_000, 9_600, 14_400);

    const result = await runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({ pcm: first, sampleRate: 16_000, pace: true, runtime }),
      additionalSources: Array.from(
        { length: 5 },
        () => createSyntheticPcmSource({ pcm: shortFollowup, sampleRate: 16_000, pace: true, runtime }),
      ),
      gateInputOnSpeech: true,
      semanticSpeech: true,
      simulateUiSpeechHandoff: true,
      // The single-turn pacing test above owns strict microphone-clock proof.
      // This deterministic clock also advances for provider output, so this
      // test focuses on the connected-turn and playback-order invariants.
      requireRealtimeInputPacing: false,
      playModelAudioRealtime: true,
      videoFramesByTurn: Array.from(
        { length: 6 },
        () => [{ dataBase64: 'data:image/png;base64,AA==' }],
      ),
    }, { runtime });

    expect(ai.live.connect).toHaveBeenCalledOnce();
    expect(result.connectedTurnCount).toBe(6);
    expect(result.turns).toHaveLength(6);
    expect(result.turns[5]).toMatchObject({
      inputTranscript: 'Input 6.',
      outputTranscript: 'Reply 6.',
      sentSamples: shortFollowup.length,
      sentVideoFrameCount: 1,
      playbackCompletedAfterLastByte: true,
    });
    expect(result.providerTurnUsage).toHaveLength(6);
    expect(result.providerUsageMetadata).toMatchObject({
      promptTokenCount: 2_100,
      responseTokenCount: 60,
      totalTokenCount: 2_160,
    });
    expect(sent.filter(message => message.activityStart)).toHaveLength(6);
    expect(sent.filter(message => message.activityEnd)).toHaveLength(6);
    expect(sent.filter(message => message.audioStreamEnd)).toHaveLength(6);
    expect(sent.filter(message => message.video)).toHaveLength(6);
    expect(session.close).toHaveBeenCalledOnce();
  });

  it('waits for every model byte to play when more audio arrives during playback', async () => {
    let callbacks: Record<string, (...args: any[]) => void> = {};
    const playbackSleeps: Array<{ ms: number; resolve: () => void }> = [];
    let now = 1_000;
    const runtime = createCoreRuntime({
      clock: {
        now: () => now,
        sleep: (ms) => new Promise<void>(resolve => {
          playbackSleeps.push({ ms, resolve: () => {
            now += ms;
            resolve();
          } });
        }),
        setInterval: () => 0,
        clearInterval: () => undefined,
      },
    });
    const pcmChunk = btoa(String.fromCharCode(...new Uint8Array(4_800)));
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        if (!message.audioStreamEnd) return;
        callbacks.onmessage?.({
          serverContent: {
            modelTurn: { parts: [{ inlineData: { mimeType: 'audio/pcm;rate=24000', data: pcmChunk } }] },
            outputTranscription: { text: 'One ' },
          },
        });
        queueMicrotask(() => callbacks.onmessage?.({
          serverContent: {
            modelTurn: { parts: [{ inlineData: { mimeType: 'audio/pcm;rate=24000', data: pcmChunk } }] },
            outputTranscription: { text: 'two.' },
            turnComplete: true,
          },
        }));
      }),
      close: vi.fn(),
    };
    const ai = {
      models: {} as CoreGeminiClient['models'],
      live: {
        connect: vi.fn(async (params: any) => {
          callbacks = params.callbacks;
          return session;
        }),
        music: { connect: vi.fn() },
      },
    } as CoreGeminiClient;
    let settled = false;
    const journey = runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({
        pcm: new Int16Array(16_000).fill(6_000),
        sampleRate: 16_000,
        pace: false,
        runtime,
      }),
      gateInputOnSpeech: false,
      playModelAudioRealtime: true,
    }, { runtime }).finally(() => { settled = true; });

    await vi.waitFor(() => expect(playbackSleeps.some(item => item.ms === 100)).toBe(true));
    expect(settled).toBe(false);
    playbackSleeps.splice(playbackSleeps.findIndex(item => item.ms === 100), 1)[0]?.resolve();
    await vi.waitFor(() => expect(playbackSleeps.some(item => item.ms === 100)).toBe(true));
    expect(settled).toBe(false);
    playbackSleeps.splice(playbackSleeps.findIndex(item => item.ms === 100), 1)[0]?.resolve();
    await vi.waitFor(() => expect(playbackSleeps.some(item => item.ms === 250)).toBe(true));
    playbackSleeps.splice(playbackSleeps.findIndex(item => item.ms === 250), 1)[0]?.resolve();

    const result = await journey;
    expect(result.modelAudioSampleCount).toBe(4_800);
    expect(result.timing.modelPlaybackElapsedMs).toBe(200);
    expect(result.turns[0]).toMatchObject({
      modelAudioSampleCount: 4_800,
      playbackCompletedAfterLastByte: true,
    });
  });

  it('retains transcription and audio callbacks dispatched after turnComplete', async () => {
    let callbacks: Record<string, (...args: any[]) => void> = {};
    const pcmChunk = btoa(String.fromCharCode(...new Uint8Array(2_400)));
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        if (!message.audioStreamEnd) return;
        callbacks.onmessage?.({
          serverContent: {
            inputTranscription: { text: 'The first half ' },
            outputTranscription: { text: 'One ' },
            modelTurn: { parts: [{ inlineData: { mimeType: 'audio/pcm;rate=24000', data: pcmChunk } }] },
            turnComplete: true,
          },
        });
        setTimeout(() => callbacks.onmessage?.({
          serverContent: {
            inputTranscription: { text: 'and the final words.' },
            outputTranscription: { text: 'two.' },
            modelTurn: { parts: [{ inlineData: { mimeType: 'audio/pcm;rate=24000', data: pcmChunk } }] },
          },
        }), 10);
      }),
      close: vi.fn(),
    };
    const ai = {
      models: {} as CoreGeminiClient['models'],
      live: {
        connect: vi.fn(async (params: any) => {
          callbacks = params.callbacks;
          return session;
        }),
        music: { connect: vi.fn() },
      },
    } as CoreGeminiClient;

    const result = await runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({
        pcm: new Int16Array(1_600).fill(6_000),
        sampleRate: 16_000,
        pace: false,
      }),
      gateInputOnSpeech: false,
      playModelAudioRealtime: true,
    });

    expect(result.inputTranscript).toBe('The first half and the final words.');
    expect(result.outputTranscript).toBe('One two.');
    expect(result.modelAudioSampleCount).toBe(2_400);
    expect(result.realtimeEvidence.modelPlaybackPassed).toBe(true);
    expect(result.turns[0]).toMatchObject({
      inputTranscript: 'The first half and the final words.',
      outputTranscript: 'One two.',
      modelAudioSampleCount: 2_400,
      playbackCompletedAfterLastByte: true,
    });
  });

  it('keeps closed-turn silence out of the packetizer pacing queue', async () => {
    let callbacks: Record<string, (...args: any[]) => void> = {};
    const sent: any[] = [];
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        sent.push(message);
        if (message.audioStreamEnd) queueMicrotask(() => callbacks.onmessage?.({
          serverContent: {
            inputTranscription: { text: 'One complete sentence.' },
            outputTranscription: { text: 'One complete sentence.' },
            turnComplete: true,
          },
        }));
      }),
      close: vi.fn(),
    };
    const ai = {
      models: {} as CoreGeminiClient['models'],
      live: {
        connect: vi.fn(async (params: any) => {
          callbacks = params.callbacks;
          return session;
        }),
        music: { connect: vi.fn() },
      },
    } as CoreGeminiClient;
    const pcm = new Int16Array(104_000);
    pcm.fill(6_000, 0, 24_000);

    const result = await runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({ pcm, sampleRate: 16_000, pace: false }),
      gateInputOnSpeech: true,
      semanticSpeech: true,
    });

    expect(result.packetizer.totalInputSamples).toBeLessThan(pcm.length);
    expect(result.packetizer.totalInputSamples).toBe(result.sentSamples);
    expect(sent.filter(message => message.audioStreamEnd)).toHaveLength(1);
  });

  it('does not send a second empty boundary after the gate already closed', async () => {
    let callbacks: Record<string, (...args: any[]) => void> = {};
    const sent: any[] = [];
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        sent.push(message);
        if (message.audioStreamEnd) queueMicrotask(() => callbacks.onmessage?.({
          serverContent: {
            outputTranscription: { text: 'I heard the completed turn.' },
            turnComplete: true,
          },
        }));
      }),
      close: vi.fn(),
    };
    const ai = {
      models: {} as CoreGeminiClient['models'],
      live: {
        connect: vi.fn(async (request: any) => {
          callbacks = request.callbacks;
          return session;
        }),
        music: { connect: vi.fn() },
      },
    } as CoreGeminiClient;
    const pcm = new Int16Array(64_000);
    pcm.fill(6_000, 0, 24_000);

    const result = await runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({ pcm, sampleRate: 16_000, pace: false }),
      gateInputOnSpeech: true,
      semanticSpeech: true,
    });

    expect(result.gate.streamEnds).toBe(1);
    expect(sent.filter(message => message.audioStreamEnd)).toHaveLength(1);
  });

  it('rejects a provider turn that completes without model output', async () => {
    let callbacks: Record<string, (...args: any[]) => void> = {};
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        if (message.audioStreamEnd) queueMicrotask(() => callbacks.onmessage?.({
          serverContent: { inputTranscription: { text: 'Play' }, turnComplete: true },
        }));
      }),
      close: vi.fn(),
    };
    const ai = {
      models: {} as CoreGeminiClient['models'],
      live: {
        connect: vi.fn(async (request: any) => {
          callbacks = request.callbacks;
          return session;
        }),
        music: { connect: vi.fn() },
      },
    } as CoreGeminiClient;
    const pcm = new Int16Array(16_000);
    pcm.fill(6_000);

    await expect(runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({ pcm, sampleRate: 16_000, pace: false }),
      gateInputOnSpeech: false,
    })).rejects.toThrow('Live turn completed without model output');
  });
});
