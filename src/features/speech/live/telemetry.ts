// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {
  type RealtimePcmPacketizerStats
} from '../../../core-sdk/media/realtimePcmPacketizer';
import type { LiveSessionData } from './state';
import { createEmptyInputAudioTelemetry, createEmptyPlaybackTelemetry } from './types';

export function createLiveTelemetry(state: Pick<LiveSessionData, 'inputPacketizerRef' | 'inputAudioTelemetryRef' | 'playbackTelemetryRef'>) {
  const { inputPacketizerRef, inputAudioTelemetryRef, playbackTelemetryRef } = state;

  const resetAudioTelemetry = () => {
    inputAudioTelemetryRef.current = createEmptyInputAudioTelemetry();
    playbackTelemetryRef.current = createEmptyPlaybackTelemetry();
  };

  const getInputPacketizerStats = (): RealtimePcmPacketizerStats => (
    inputPacketizerRef.current?.getStats() ?? {
      totalInputSamples: 0,
      totalOutputSamples: 0,
      packetsSent: 0,
      partialPacketsSent: 0,
      timerFlushes: 0,
      explicitFlushes: 0,
      maxBufferedSamples: 0,
      maxPacketSamples: 0,
      pacedOutput: false,
      outputPacingWaitMs: 0,
      outputPacingElapsedMs: 0,
    }
  );

  const getAudioTelemetrySnapshot = () => ({
    input: {
      packetizer: getInputPacketizerStats(),
      ...inputAudioTelemetryRef.current,
    },
    playback: {
      ...playbackTelemetryRef.current,
    },
  });
  return { resetAudioTelemetry, getInputPacketizerStats, getAudioTelemetrySnapshot };
}
