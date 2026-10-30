// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { getGeminiModels } from '../../../core/config/models';
import { Capacitor } from '@capacitor/core';
import { getAi } from '../../../api/gemini/client';
import { hasCameraConsent } from '../../../core-sdk/media/cameraConsent';
import { beginTurnTiming } from '../../../platform/browser/turnTiming';
import { createLiveUsageTracker } from '../../../shared/utils/costTracker';
import { useMaestroStore } from '../../../store';
import { debugLogService } from '../../diagnostics';
import { AudioCodecWorkerClient } from '../utils/audioCodecWorkerClient';
import { openBrowserPcmCapture } from '../utils/browserPcmCapture';
import {
  waitForLocalSpeechTrigger
} from '../utils/localSpeechTrigger';
import {
  acquireLocalWhisperClient,
  releaseLocalWhisperClient
} from '../utils/localWhisperClient';
import {
  PCM_PLAYBACK_PROCESSOR_NAME,
  PCM_PLAYBACK_PROCESSOR_URL,
} from '../worklets';
import { createBrowserLiveVideo } from './browserVideo';
import type { LiveActivityPorts, LiveRuntimePorts } from './ports';
import { WorkletSpeechOutput } from '../utils/workletSpeechOutput';
import { selectSpeechOutput } from '../utils/selectSpeechOutput';

/** Browser/native adapters. No orchestration policy lives in this composition. */
export function createBrowserLiveRuntime(activity: LiveActivityPorts): LiveRuntimePorts {
  return {
    ...activity, getGeminiModels, getAi, beginTurnTiming, debugLogService, createLiveUsageTracker,
    acquireLocalWhisperClient, releaseLocalWhisperClient, waitForLocalSpeechTrigger, openPcmCapture: openBrowserPcmCapture,
    isNativePlatform: () => Capacitor.isNativePlatform(),
    getAudioContextConstructor: () => window.AudioContext || (window as any).webkitAudioContext,
    createSpeechOutput: async (context, events) => selectSpeechOutput(async () => {
      await context.audioWorklet.addModule(PCM_PLAYBACK_PROCESSOR_URL);
      const node = new AudioWorkletNode(context, PCM_PLAYBACK_PROCESSOR_NAME, {
        numberOfInputs: 0, numberOfOutputs: 1, outputChannelCount: [1],
      });
      return new WorkletSpeechOutput(context, node, events);
    }, events),
    createCanvas: () => document.createElement('canvas'),
    createCodecWorker: () => new AudioCodecWorkerClient(),
    createVideo: state => createBrowserLiveVideo(state, { hasCameraConsent: () => hasCameraConsent(useMaestroStore.getState().settings) }),
    setTimeout: (callback, ms) => window.setTimeout(callback, ms),
    clearTimeout: id => window.clearTimeout(id),
  };
}
