// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { parseBookCommand, type BookCommand, type BookLayout } from './bookModel';
import { flushSync } from 'react-dom';
import { sessionActivity } from '../browser/sessionActivity';
import type { LibraryBookClient, LibraryRequest } from './libraryBookBridge';
import { createFileSelectionGate } from './fileSelectionGate';

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
  librarySession?: string;
  libraryRevision?: number;
  libraryRequest?: LibraryRequest | null;
}

declare global {
  interface Window {
    maestroBook?: Readonly<{ snapshot: () => BookSnapshot; command: (input: unknown) => boolean; lifecycle: (suspended: boolean) => void; lifecycleState: () => ReturnType<typeof sessionActivity.status>; takeFileSelection: () => boolean; libraryState: (input: unknown) => boolean }>;
  }
}

/** Native polls this top-level document; no JS-to-native object is exposed to iframes. */
export function installBookBridge(target: Window, readSnapshot: () => BookSnapshot, command: (value: BookCommand) => void, library?: LibraryBookClient) {
  const fileSelection = createFileSelectionGate(target);
  const bridge = Object.freeze({
    snapshot: () => ({ ...readSnapshot(), ...library?.snapshot() }),
    libraryState: (input: unknown) => library?.receive(input) ?? false,
    takeFileSelection: () => fileSelection.take(),
    lifecycle(suspended: boolean) {
      if (typeof suspended !== 'boolean') return;
      if (suspended) { fileSelection.clear(); library?.suspend(); }
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
  return () => { fileSelection.dispose(); if (target.maestroBook === bridge) delete target.maestroBook; };
}
