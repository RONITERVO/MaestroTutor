// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';
import {
  type LocalSpeechTriggerPhase
} from '../utils/localSpeechTrigger';
import type { LiveActivityPorts } from './ports';
import type { LiveSessionData } from './state';
import { type LiveSessionState } from './types';

export function createLiveActivity(state: Pick<LiveSessionData, 'speechTriggerActivityTokenRef' | 'vadActivityTokenRef' | 'callbacksRef'>, ports: LiveActivityPorts) {
  const { speechTriggerActivityTokenRef, vadActivityTokenRef, callbacksRef } = state;
  const { setState, addActivityToken, removeActivityToken } = ports;
  const updateState = (s: LiveSessionState) => {
    setState(s);
    callbacksRef.current.onStateChange?.(s);
  };

  const setLocalSpeechTriggerPhase = (phase: LocalSpeechTriggerPhase | null) => {
    if (speechTriggerActivityTokenRef.current) {
      removeActivityToken(speechTriggerActivityTokenRef.current);
      speechTriggerActivityTokenRef.current = null;
    }
    callbacksRef.current.onLocalSpeechTriggerPhaseChange?.(phase);
    if (!phase) return;
    const token = phase === 'whisper-loading'
      ? addActivityToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_OBSERVER_LOADING)
      : (phase === 'whisper-checking'
        ? addActivityToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_OBSERVER_CHECKING)
        : (phase === 'speech-confirmed'
          ? addActivityToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_OBSERVER_TRIGGERED)
          : addActivityToken(TOKEN_CATEGORY.VAD, TOKEN_SUBTYPE.VAD_OBSERVER_LISTEN)));
    speechTriggerActivityTokenRef.current = token;
  };

  const setVadActivity = (active: boolean, observer: boolean) => {
    const current = vadActivityTokenRef.current;
    if (current && (!active || current.observer !== observer)) {
      removeActivityToken(current.token);
      vadActivityTokenRef.current = null;
    }
    if (!active || vadActivityTokenRef.current) return;
    vadActivityTokenRef.current = {
      observer,
      token: addActivityToken(
        TOKEN_CATEGORY.VAD,
        observer ? TOKEN_SUBTYPE.VAD_OBSERVER_ACTIVE : TOKEN_SUBTYPE.VAD_ACTIVE,
      ),
    };
  };
  return { updateState, setLocalSpeechTriggerPhase, setVadActivity };
}
