// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { parseBookCommand, type BookCommand, type BookLayout } from './bookModel';
import { flushSync } from 'react-dom';
import { sessionActivity } from '../browser/sessionActivity';
import type { LibraryBookClient, LibraryRequest } from './libraryBookBridge';
import { RoomAgentClient, registerRoomAgent } from './roomAgentBridge';
import { createFileSelectionGate } from './fileSelectionGate';
import { createBookFileExport } from './bookFileExport';
import { QuestIntegrityClient, registerQuestIntegrity, type QuestIntegrityRequest } from './questIntegrityBridge';

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
  integrityRequest?: QuestIntegrityRequest;
}

declare global {
  interface Window {
    maestroBook?: Readonly<{ integrityResult: (input: unknown) => boolean; snapshot: () => BookSnapshot; roomSnapshot: () => ReturnType<RoomAgentClient['snapshot']>; roomState: (input: unknown) => boolean; command: (input: unknown) => boolean; lifecycle: (suspended: boolean) => void; lifecycleState: () => ReturnType<typeof sessionActivity.status>; takeFileSelection: () => boolean; fileExportPoll: () => unknown; fileExportResult: (value:unknown) => boolean; libraryState: (input: unknown) => boolean }>;
  }
}

/** Native polls this top-level document; no JS-to-native object is exposed to iframes. */
export function installBookBridge(target: Window, readSnapshot: () => BookSnapshot, command: (value: BookCommand) => void, library?: LibraryBookClient, room = new RoomAgentClient()) {
  const integrity = new QuestIntegrityClient();
  const unregisterIntegrity = registerQuestIntegrity(integrity);
  const fileSelection = createFileSelectionGate(target);
  const fileExport = createBookFileExport(target);
  const unregisterRoom = registerRoomAgent(room);
  const bridge = Object.freeze({
    roomSnapshot: room.snapshot, roomState: room.receive,
    snapshot: () => ({ ...readSnapshot(), ...library?.snapshot(), ...(integrity.snapshot() ? { integrityRequest: integrity.snapshot() } : {}) }),
    integrityResult: integrity.receive,
    libraryState: (input: unknown) => library?.receive(input) ?? false,
    takeFileSelection: () => fileSelection.take(),
    fileExportPoll:fileExport.poll,fileExportResult:fileExport.receive,
    lifecycle(suspended: boolean) {
      if (typeof suspended !== 'boolean') return;
      fileExport.lifecycle(suspended);
      if (suspended) { integrity.cancel(); fileSelection.clear(); library?.suspend(); room.cancel(); }
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
  return () => { unregisterIntegrity(); unregisterRoom(); fileExport.dispose(); fileSelection.dispose(); if (target.maestroBook === bridge) delete target.maestroBook; };
}
