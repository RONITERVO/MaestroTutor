// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { parseBookCommand, type BookCommand, type BookLayout } from './bookModel';
import { flushSync } from 'react-dom';
import { sessionActivity } from '../browser/sessionActivity';

export interface BookSnapshot {
  version: 1;
  layout: BookLayout;
  activity: 'idle' | 'listening' | 'thinking' | 'speaking';
  bookmarkMessageId: string | null;
  selectedArtifactId: string | null;
  historyStart: number;
  historyEnd: number;
  historyTotal: number;
  audioPaused?: boolean;
}

declare global {
  interface Window {
    maestroBook?: Readonly<{ snapshot: () => BookSnapshot; command: (input: unknown) => boolean; lifecycle: (suspended: boolean) => void; lifecycleState: () => ReturnType<typeof sessionActivity.status> }>;
  }
}

/** Native polls this top-level document; no JS-to-native object is exposed to iframes. */
export function installBookBridge(target: Window, readSnapshot: () => BookSnapshot, command: (value: BookCommand) => void) {
  const bridge = Object.freeze({
    snapshot: readSnapshot,
    lifecycle(suspended: boolean) {
      if (typeof suspended !== 'boolean') return;
      // Commit iframe removal before native pauses JavaScript timers.
      flushSync(() => sessionActivity.setSuspended(suspended));
    },
    lifecycleState: () => sessionActivity.status(),
    command(input: unknown) {
      const parsed = parseBookCommand(input);
      if (!parsed) return false;
      command(parsed);
      return true;
    },
  });
  target.maestroBook = bridge;
  return () => { if (target.maestroBook === bridge) delete target.maestroBook; };
}
