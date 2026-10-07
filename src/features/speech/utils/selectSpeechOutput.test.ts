// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, expect, it, vi } from 'vitest';
import { SpeechBookClient, registerBookSpeech } from '../../../platform/quest/speechBookBridge';
import { isNativeQuestBook } from '../../../platform/quest/questIntegrityBridge';
import { selectSpeechOutput } from './selectSpeechOutput';
vi.mock('../../../platform/quest/questIntegrityBridge', () => ({ isNativeQuestBook: vi.fn() }));
afterEach(() => vi.resetAllMocks());
it('uses the normal browser output outside the native book and never falls back inside it', () => {
  const browser = vi.fn(); vi.mocked(isNativeQuestBook).mockReturnValue(false);
  selectSpeechOutput(browser); expect(browser).toHaveBeenCalledOnce(); browser.mockClear();
  vi.mocked(isNativeQuestBook).mockReturnValue(true);
  expect(() => selectSpeechOutput(browser)).toThrow(); expect(browser).not.toHaveBeenCalled();
  const client = new SpeechBookClient(), unregister = registerBookSpeech(client);
  try {
    const idle = client.exchange(null)!;
    client.exchange({ ...idle, host: 'a'.repeat(32), status: 'ready', acceptedSequence: 0, submittedSamples: 0, playedSamples: 0 });
    const output = selectSpeechOutput(browser); output.write(new Int16Array([7]));
    expect(output.microphonePolicy).toBe('suppress-during-playback');
    expect(client.exchange(null)?.chunks).toHaveLength(1); expect(browser).not.toHaveBeenCalled(); output.dispose();
  } finally { unregister(); }
});
