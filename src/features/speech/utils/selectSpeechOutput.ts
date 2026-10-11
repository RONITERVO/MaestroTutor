// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { SpeechOutput, SpeechOutputEvents } from '../../../core-sdk/media/speechOutput';
import { isNativeQuestBook } from '../../../platform/quest/questIntegrityBridge';
import { createBookSpeechOutput } from '../../../platform/quest/speechBookBridge';

/** Select once. A failed Quest output must never start a second dry browser copy. */
export function selectSpeechOutput<T extends SpeechOutput | Promise<SpeechOutput>>(
  browser: () => T, events?: SpeechOutputEvents,
): SpeechOutput | T {
  return isNativeQuestBook() ? createBookSpeechOutput(events) : browser();
}
