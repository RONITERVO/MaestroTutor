// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { getGeminiModels } from '../../../core-sdk/modelRegistry';
import type { TokenCategory } from '../../../core/config/activityTokens';
import type { LiveSessionState } from './types';
export interface LiveTimerPorts { setTimeout(callback: (...args: any[]) => void, ms: number): number; clearTimeout(id: number): void }
export interface LiveActivityPorts { setState(state: LiveSessionState): void; addActivityToken(category: TokenCategory, subtype: string): string; removeActivityToken(token: string): void }

import type { getAi } from '../../../api/gemini/client';
import type { beginTurnTiming } from '../../../platform/browser/turnTiming';
import type { createLiveUsageTracker } from '../../../shared/utils/costTracker';
import type { debugLogService } from '../../diagnostics';
import type { AudioCodecWorkerClient } from '../utils/audioCodecWorkerClient';
import type { flushCaptureWorkletNode } from '../utils/captureWorkletMessaging';
import type { waitForLocalSpeechTrigger } from '../utils/localSpeechTrigger';
import type { acquireLocalWhisperClient, releaseLocalWhisperClient } from '../utils/localWhisperClient';
import type { createBrowserLiveVideo } from './browserVideo';
import type { LiveSessionData } from './state';

/** Browser and transport capabilities supplied by the React application shell. */
export interface LiveRuntimePorts extends LiveTimerPorts, LiveActivityPorts {
  getGeminiModels: typeof getGeminiModels;
  getAi: typeof getAi;
  beginTurnTiming: typeof beginTurnTiming;
  debugLogService: Pick<typeof debugLogService, 'logRequest'>;
  createLiveUsageTracker: typeof createLiveUsageTracker;
  acquireLocalWhisperClient: typeof acquireLocalWhisperClient;
  releaseLocalWhisperClient: typeof releaseLocalWhisperClient;
  waitForLocalSpeechTrigger: typeof waitForLocalSpeechTrigger;
  flushCaptureWorkletNode: typeof flushCaptureWorkletNode;
  isNativePlatform(): boolean;
  getAudioContextConstructor(): typeof AudioContext;
  getUserMedia(constraints: MediaStreamConstraints): Promise<MediaStream>;
  createAudioWorkletNode(context: AudioContext, name: string, options: AudioWorkletNodeOptions): AudioWorkletNode;
  createCanvas(): HTMLCanvasElement;
  createCodecWorker(): AudioCodecWorkerClient;
  createVideo(state: LiveSessionData): ReturnType<typeof createBrowserLiveVideo>;
  FLOAT_TO_INT16_PROCESSOR_URL: string;
  FLOAT_TO_INT16_PROCESSOR_NAME: string;
  PCM_PLAYBACK_PROCESSOR_URL: string;
  PCM_PLAYBACK_PROCESSOR_NAME: string;
}
