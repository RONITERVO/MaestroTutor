// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { parseBookCommand, type BookCommand, type BookLayout } from './bookModel';

export interface BookSnapshot {
  version: 1;
  layout: BookLayout;
  activity: 'idle' | 'listening' | 'thinking' | 'speaking';
  bookmarkMessageId: string | null;
  selectedArtifactId: string | null;
  historyStart: number;
  historyEnd: number;
  historyTotal: number;
}

declare global {
  interface Window {
    maestroBook?: Readonly<{ snapshot: () => BookSnapshot; command: (input: unknown) => boolean }>;
  }
}

/** Native polls this top-level document; no JS-to-native object is exposed to iframes. */
export function installBookBridge(target: Window, readSnapshot: () => BookSnapshot, command: (value: BookCommand) => void) {
  const bridge = Object.freeze({
    snapshot: readSnapshot,
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
