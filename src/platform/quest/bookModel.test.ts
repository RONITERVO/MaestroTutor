// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';
import type { ChatMessage } from '../../core/types';
import { collectBookArtifacts, parseBookCommand, resolveBookArtifact, resolveHistoryPage } from './bookModel';
import { installBookBridge, type BookSnapshot } from './bookBridge';

const message = (id: string, overrides: Partial<ChatMessage> = {}): ChatMessage => ({ id, role: 'assistant', timestamp: 1, ...overrides });
const html = '<html><body><button>Practice</button><script>void 0;</script></body></html>';
const src = `data:text/html;base64,${btoa(html)}`;

describe('book conversation projection', () => {
  it('preserves actual HTML and message identity, excludes unfinished artifacts', () => {
    const artifacts = collectBookArtifacts([
      message('first', { imageUrl: src, imageMimeType: 'text/html', attachmentName: 'practice.html' }),
      message('loading', { imageUrl: src, imageMimeType: 'text/html', isLoadingArtifact: true }),
      message('picture', { storageOptimizedImageUrl: 'data:image/png;base64,AA==', storageOptimizedImageMimeType: 'image/png' }),
    ]);
    expect(artifacts.map(a => [a.id, a.kind])).toEqual([['first', 'html'], ['picture', 'image']]);
    expect(artifacts[0].sourceCode).toBe(html);
    expect(resolveBookArtifact(artifacts, null)?.id).toBe('picture');
    expect(resolveBookArtifact(artifacts, 'first')?.id).toBe('first');
    expect(resolveBookArtifact(artifacts, 'deleted')?.id).toBe('picture');
  });

  it('keeps a history page at its message after new replies and earlier history arrive', () => {
    const ids = Array.from({ length: 20 }, (_, i) => `m${i}`);
    expect(resolveHistoryPage(ids, null).ids).toEqual(ids.slice(12));
    const earlier = resolveHistoryPage(ids, resolveHistoryPage(ids, null).previousAnchor);
    expect(earlier.ids).toEqual(ids.slice(4, 12));
    expect(resolveHistoryPage(['older', ...ids, 'new'], earlier.ids[0]).ids).toEqual(earlier.ids);
    expect(resolveHistoryPage(ids, 'm7').ids[0]).toBe('m7');
    expect(resolveHistoryPage(ids, 'deleted').isLatest).toBe(true);
    expect(resolveHistoryPage([], null).ids).toEqual([]);
  });
});

describe('native book command boundary', () => {
  it('rejects unknown versions, malformed IDs and arbitrary native operations', () => {
    for (const value of [null, [], {}, { version: 2, type: 'bookmark.jump' }, { version: 1, type: 'eval', script: 'alert(1)' }, { version: 1, type: 'history.step', direction: 3 }, { version: 1, type: 'artifact.select', messageId: 'x'.repeat(257) }]) {
      expect(parseBookCommand(value)).toBeNull();
    }
    expect(parseBookCommand({ version: 1, type: 'history.step', direction: -1, injected: true })).toEqual({ version: 1, type: 'history.step', direction: -1 });
  });

  it('cleans up only its own bridge and returns a minimal snapshot', () => {
    const target = window;
    const snapshot: BookSnapshot = { version: 1, layout: 'conversation', activity: 'idle', bookmarkMessageId: 'm7', selectedArtifactId: null, historyStart: 0, historyEnd: 8, historyTotal: 20 };
    const commands: unknown[] = [];
    const uninstall = installBookBridge(target, () => snapshot, command => commands.push(command));
    expect(target.maestroBook!.snapshot()).toMatchObject({ ...snapshot, camera: { pulse: 1, requestId: '', acknowledged: '' } });
    expect(target.maestroBook!.command({ version: 1, type: 'bookmark.jump' })).toBe(true);
    expect(target.maestroBook!.command({ version: 1, type: 'openFile', path: '/private' })).toBe(false);
    expect(commands).toEqual([{ version: 1, type: 'bookmark.jump' }]);
    const replacement = installBookBridge(target, () => snapshot, () => {});
    uninstall();
    expect(target.maestroBook).toBeDefined();
    replacement();
    expect(target.maestroBook).toBeUndefined();
  });
});
