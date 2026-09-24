// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { resolveSttTurnDestination } from '../../core-sdk/media/sttTurnRouting';
import type { SttTurnHandler, SttTurnPorts } from './contracts';

/** Deliver completed speech to the captured/current destination, preserving
 * busy guards and the fresh-state check before restarting the microphone. */
export const createSttTurnHandler = ({
  readState, stopListening, startListening, clearTranscript, handleCreateSuggestion,
  handleSendMessageInternal, logSttFlow, warnSttFlow, warn,
}: SttTurnPorts): SttTurnHandler => async (turn) => {
  const turnText = (turn.turnTranscript || turn.committedTranscript || '').trim();
  logSttFlow('app.turnComplete.received', {
    turnId: turn.turnId,
    textLength: turnText.length,
    committedLength: turn.committedTranscript.length,
    audioSamples: turn.audioSamples,
  });
  if (turnText.length < 2) {
    warnSttFlow('app.turnComplete.skip.short', {
      turnId: turn.turnId,
      textLength: turnText.length,
    });
    return;
  }

  const state = readState();
  if (!state.settings.stt.enabled) {
    warnSttFlow('app.turnComplete.skip.sttDisabled', {
      turnId: turn.turnId,
    });
    return;
  }
  if (state.responsePending || state.speaking) {
    warnSttFlow('app.turnComplete.skip.busy', {
      turnId: turn.turnId,
      responsePending: state.responsePending,
      speaking: state.speaking,
    });
    return;
  }

  const destination = resolveSttTurnDestination(
    turn.destination,
    state.settings.isSuggestionMode,
  );

  if (destination === 'translation') {
    logSttFlow('app.turnComplete.suggestionMode.start', {
      turnId: turn.turnId,
      textLength: turnText.length,
    });
    try {
      await Promise.resolve(stopListening());
    } catch (error) {
      warn('Failed to stop STT before creating suggestion', error);
    }

    clearTranscript();
    await handleCreateSuggestion(turnText);

    const nextState = readState();
    if (
      nextState.settings.stt.enabled &&
      !nextState.responsePending &&
      !nextState.speaking &&
      !nextState.listening
    ) {
      logSttFlow('app.turnComplete.suggestionMode.restartStt', {
        turnId: turn.turnId,
      });
      startListening(nextState.settings.stt.language);
    }
    logSttFlow('app.turnComplete.suggestionMode.done', {
      turnId: turn.turnId,
    });
    return;
  }

  logSttFlow('app.turnComplete.send.start', {
    turnId: turn.turnId,
    textLength: turnText.length,
    hasAttachedImage: Boolean(state.attachedImageBase64),
  });
  const sendResult = await handleSendMessageInternal(
    turnText,
    state.attachedImageBase64 || undefined,
    state.attachedImageMimeType || undefined,
    'user',
    { triggeredByStt: true }
  );
  logSttFlow('app.turnComplete.send.done', {
    turnId: turn.turnId,
    sendResult,
  });

};
