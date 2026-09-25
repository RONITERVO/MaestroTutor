// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useMaestroStore } from '../../store';
import MiniGameViewer from '../../features/chat/components/MiniGameViewer';
import PdfViewer from '../../features/chat/components/PdfViewer';
import TextFileViewer from '../../features/chat/components/TextFileViewer';
import MiniGameErrorBoundary from '../../features/chat/components/MiniGameErrorBoundary';
import { BookPresentationContext } from './BookPresentationContext';
import { BOOK_LAYOUT_STORAGE_KEY, collectBookArtifacts, readBookLayout, resolveBookArtifact, resolveHistoryPage, type BookCommand, type BookLayout } from './bookModel';
import { installBookBridge, type BookSnapshot } from './bookBridge';
import { selectIsListening, selectIsSending, selectIsSpeaking } from '../../store/slices/uiSlice';
import './questBook.css';

/** One React root, store, IndexedDB, tutor and audio owner for both page textures. */
export function QuestBookSurface({ children }: React.PropsWithChildren) {
  const [layout, setLayout] = useState<BookLayout>(() => {
    try { return readBookLayout(window.localStorage); } catch { return 'conversation'; }
  });
  const spreadRoot = useRef<HTMLDivElement>(null);
  const [earlierPageTarget, setEarlierPageTarget] = useState<HTMLDivElement | null>(null);
  useEffect(() => { try { window.localStorage.setItem(BOOK_LAYOUT_STORAGE_KEY, layout); } catch { /* Session choice still works if storage is unavailable. */ } }, [layout]);
  const messages = useMaestroStore(state => state.messages);
  const artifacts = useMemo(() => collectBookArtifacts(messages), [messages]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [historyAnchor, setHistoryAnchor] = useState<string | null>(null);
  const bookmarkMessageId = useMaestroStore(state => state.settings.historyBookmarkMessageId ?? null);
  const pairId = useMaestroStore(state => state.settings.selectedLanguagePairId);
  const historyIds = useMemo(() => messages.filter(message => message.role !== 'system_selection').map(message => message.id), [messages]);
  const page = useMemo(() => resolveHistoryPage(historyIds, historyAnchor), [historyIds, historyAnchor]);
  const visibleMessageIds = useMemo(() => new Set(page.ids), [page]);
  const earlierMessageIds = useMemo(() => new Set(page.earlierIds), [page]);
  useEffect(() => { setSelectedId(null); setHistoryAnchor(null); setPosters(new Map()); }, [pairId]);
  const selected = resolveBookArtifact(artifacts, selectedId);
  const [posters, setPosters] = useState<ReadonlyMap<string, string>>(new Map());
  const selectedMessageId = selected?.id;
  const receivePoster = useCallback((dataUrl: string) => {
    if (!selectedMessageId) return;
    setPosters(previous => {
      const next = new Map(previous);
      next.delete(selectedMessageId);
      next.set(selectedMessageId, dataUrl);
      while (next.size > 4) next.delete(next.keys().next().value!);
      return next;
    });
  }, [selectedMessageId]);
  const historyPageKey = JSON.stringify(page.ids);
  const presentation = useMemo(() => ({ layout, spreadRoot, earlierPageTarget, earlierMessageIds, visibleMessageIds, historyPageKey, isLatestPage: page.isLatest, selectedId: selected?.id ?? null, selectArtifact: setSelectedId, posters }), [layout, earlierPageTarget, earlierMessageIds, selected?.id, posters, visibleMessageIds, historyPageKey, page.isLatest]);
  const command = (value: BookCommand) => {
    switch (value.type) {
      case 'layout.set': setLayout(value.layout); break;
      case 'history.step':
        if (value.direction < 0 && page.previousAnchor) setHistoryAnchor(page.previousAnchor);
        if (value.direction > 0) setHistoryAnchor(page.nextAnchor);
        break;
      case 'history.latest': setHistoryAnchor(null); break;
      case 'bookmark.jump': if (bookmarkMessageId && historyIds.includes(bookmarkMessageId)) setHistoryAnchor(bookmarkMessageId); break;
      case 'artifact.latest': setSelectedId(null); break;
      case 'artifact.select': if (artifacts.some(artifact => artifact.id === value.messageId)) setSelectedId(value.messageId); break;
    }
  };
  const stateRef = useRef({ page, bookmarkMessageId, selected, command, layout });
  stateRef.current = { page, bookmarkMessageId, selected, command, layout };
  useEffect(() => installBookBridge(window, (): BookSnapshot => {
    const state = useMaestroStore.getState();
    const latest = stateRef.current;
    return {
      version: 1,
      layout: latest.layout,
      activity: selectIsSpeaking(state) ? 'speaking' : selectIsListening(state) ? 'listening' : selectIsSending(state) ? 'thinking' : 'idle',
      bookmarkMessageId: latest.bookmarkMessageId,
      selectedArtifactId: latest.selected?.id ?? null,
      historyStart: latest.page.start, historyEnd: latest.page.end, historyTotal: latest.page.total,
    };
  }, value => stateRef.current.command(value)), []);

  return (
    <BookPresentationContext.Provider value={presentation}>
      <div className={`quest-book-surface quest-layout-${layout}`} ref={spreadRoot}>
        <section className="quest-chat-page" aria-label="Conversation page">
          <div className="quest-chat-document">{children}</div>
        </section>
        {layout === 'conversation' ? <section className="quest-earlier-page" aria-label="Earlier conversation page">
          <div className="quest-earlier-messages bg-page-bg notebook-lines" ref={setEarlierPageTarget} />
        </section> : <section className="quest-practice-page" aria-label="Artifact page">
          {selected && <>
            <div className="quest-artifact-content" key={selected.id}>
              <MiniGameErrorBoundary failedText="This artifact could not be displayed." retryText="Try again">
                {selected.kind === 'html' && <MiniGameViewer embedId={`book-${selected.id}`} sourceCode={selected.sourceCode!} fileName={selected.title} mimeType={selected.mimeType} variant="preview" activeOnBook onBookPoster={receivePoster} />}
                {selected.kind === 'image' && <img src={selected.src} alt={selected.title} />}
                {selected.kind === 'audio' && <audio src={selected.src} controls />}
                {selected.kind === 'video' && <video src={selected.src} controls playsInline />}
                {selected.kind === 'pdf' && <PdfViewer src={selected.src} variant="preview" embedId={`book-${selected.id}`} activeOnBook />}
                {selected.kind === 'text' && <TextFileViewer src={selected.src} fileName={selected.title} mimeType={selected.mimeType} variant="preview" />}
              </MiniGameErrorBoundary>
            </div>
          </>}
        </section>}
      </div>
    </BookPresentationContext.Provider>
  );
}
