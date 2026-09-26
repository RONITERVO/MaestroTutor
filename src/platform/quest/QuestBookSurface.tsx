// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import { useMaestroStore } from '../../store';
import { MiniGameViewer, PdfViewer, TextFileViewer, MiniGameErrorBoundary } from '../../features/chat';
import { BookPresentationContext } from './BookPresentationContext';
import { BOOK_LAYOUT_STORAGE_KEY, collectBookArtifacts, readBookLayout, resolveBookArtifact, resolveHistoryPage, type BookCommand, type BookLayout } from './bookModel';
import { installBookBridge, type BookSnapshot } from './bookBridge';
import { selectIsAgentWorking, selectIsListening, selectIsSending, selectIsSpeaking } from '../../store/slices/uiSlice';
import './questBook.css';
import { LibraryBookClient } from './libraryBookBridge';
import { RoomAgentClient } from './roomAgentBridge';
import { RoomWorkspace } from './RoomWorkspace';
import { LibraryBookView } from './LibraryBookView';
import { sessionActivity } from '../browser/sessionActivity';

/** One React root, store, IndexedDB, tutor and audio owner for both page textures. */
export function QuestBookSurface({ children }: React.PropsWithChildren) {
  const [layout, setLayout] = useState<BookLayout>(() => {
    try { return readBookLayout(window.localStorage); } catch { return 'conversation'; }
  });
  const [room] = useState(() => new RoomAgentClient());
  const roomView = useSyncExternalStore(room.subscribe,room.getSnapshot);
  const workspaceOpen = roomView.state?.visible ?? false;
  const [library] = useState(() => new LibraryBookClient());
  const libraryOpen = useSyncExternalStore(library.subscribe, library.getSnapshot).state?.visible ?? false;
  const [overlay,setOverlay] = useState<'library'|'workspace'|null>(null);
  const previousOpen=useRef({library:false,workspace:false});
  useEffect(()=>{
    const previous=previousOpen.current;
    if(workspaceOpen&&!previous.workspace)setOverlay('workspace');
    else if(libraryOpen&&!previous.library)setOverlay('library');
    else if(overlay==='workspace'&&!workspaceOpen||overlay==='library'&&!libraryOpen)setOverlay(null);
    previousOpen.current={library:libraryOpen,workspace:workspaceOpen};
  },[libraryOpen,workspaceOpen,overlay]);
  useEffect(()=>{
    if(overlay==='workspace'&&libraryOpen)library.close();
    if(overlay==='library'&&workspaceOpen&&!roomView.pending)void room.request([{action:'workspace',visible:false}]).catch(()=>{});
  },[overlay,libraryOpen,workspaceOpen,roomView.pending,library,room]);
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
    if (value.type !== 'session.resume') library.close();
    if (value.type !== 'session.resume' && value.type !== 'workspace.open' && workspaceOpen) void room.request([{action:'workspace',visible:false}]).catch(()=>{});
    switch (value.type) {
      case 'workspace.open': setOverlay('workspace'); void room.request([{action:'workspace',visible:true}]).catch(()=>{}); break;
      case 'session.resume': sessionActivity.resume(); break;
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
      audioPaused: !sessionActivity.isActive(),
      layout: latest.layout,
      activity: selectIsSpeaking(state) ? 'speaking' : selectIsListening(state) ? 'listening' : (selectIsSending(state) || selectIsAgentWorking(state)) ? 'thinking' : 'idle',
      bookmarkMessageId: latest.bookmarkMessageId,
      selectedArtifactId: latest.selected?.id ?? null,
      historyStart: latest.page.start, historyEnd: latest.page.end, historyTotal: latest.page.total,
    };
  }, value => stateRef.current.command(value), library, room), [library,room]);

  return (
    <BookPresentationContext.Provider value={presentation}>
      <div className={`quest-book-surface quest-layout-${layout}`} ref={spreadRoot}>
        <div hidden={overlay!==null} inert={overlay!==null} className="quest-tutor-pages">
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
        {overlay==='library' && <LibraryBookView client={library} />}
        {overlay==='workspace' && <RoomWorkspace client={room} />}
      </div>
    </BookPresentationContext.Provider>
  );
}
