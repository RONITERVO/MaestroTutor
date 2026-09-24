// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useMaestroStore } from '../store';
import { selectIsListening, selectIsResponsePending, selectIsSpeaking } from '../store/slices/uiSlice';
import type { SpeechRoutingState } from './coordinators/contracts';

/** Read at the handoff, including again after async work. Do not cache this
 * snapshot in a render: speech completion can arrive after the UI changes. */
export const readSpeechRoutingState = (): SpeechRoutingState => {
  const state = useMaestroStore.getState();
  return {
    settings: state.settings,
    responsePending: selectIsResponsePending(state),
    speaking: selectIsSpeaking(state),
    listening: selectIsListening(state),
    attachedImageBase64: state.attachedImageBase64,
    attachedImageMimeType: state.attachedImageMimeType,
    liveSessionState: state.liveSessionState,
  };
};
