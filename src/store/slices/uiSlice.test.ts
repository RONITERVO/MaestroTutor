// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE, buildToken } from '../../core/config/activityTokens';
import { selectBlocksSilentObserver } from './uiSlice';

describe('silent observer activity blocking', () => {
  it('ignores its own local trigger and observer transport phases', () => {
    const ownTokens = new Set([
      buildToken(TOKEN_CATEGORY.VAD, TOKEN_SUBTYPE.VAD_OBSERVER_LISTEN),
      buildToken(TOKEN_CATEGORY.VAD, TOKEN_SUBTYPE.VAD_OBSERVER_ACTIVE),
      buildToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_OBSERVER_CHECKING),
      buildToken(TOKEN_CATEGORY.LIVE, TOKEN_SUBTYPE.OBSERVER_CONNECTING),
    ]);

    expect(selectBlocksSilentObserver({ activityTokens: ownTokens })).toBe(false);
  });

  it('lets reply-suggestion post-processing run while observer audio drains', () => {
    const observerAndSuggestions = new Set([
      buildToken(TOKEN_CATEGORY.LIVE, TOKEN_SUBTYPE.OBSERVER_SESSION),
      buildToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.SUGGESTIONS),
    ]);

    expect(selectBlocksSilentObserver({ activityTokens: observerAndSuggestions })).toBe(false);
  });

  it('still yields to foreground speech, generation, translation creation, and user Live activity', () => {
    for (const token of [
      buildToken(TOKEN_CATEGORY.STT, TOKEN_SUBTYPE.LISTEN),
      buildToken(TOKEN_CATEGORY.VAD, TOKEN_SUBTYPE.VAD_LISTEN),
      buildToken(TOKEN_CATEGORY.VAD, TOKEN_SUBTYPE.VAD_ACTIVE),
      buildToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_CHECKING),
      buildToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.RESPONSE),
      buildToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.CREATE_SUGGESTION),
      buildToken(TOKEN_CATEGORY.LIVE, TOKEN_SUBTYPE.CONNECTING),
    ]) {
      expect(selectBlocksSilentObserver({ activityTokens: new Set([token]) })).toBe(true);
    }
  });
});
