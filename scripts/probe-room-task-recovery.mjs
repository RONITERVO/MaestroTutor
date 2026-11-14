// Local browser verification with real IndexedDB and app history loading.
// No accounts, provider requests, native dispatch, or headset access.
import { chromium } from 'playwright-core';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';
const base = process.env.MAESTRO_HANDOFF_FIXTURE_URL || 'http://127.0.0.1:5184';
if (!['localhost', '127.0.0.1'].includes(new URL(base).hostname)) throw new Error('Local fixture required.');
const out = resolve('.quest-evidence/agent-task-recovery'); await mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
try {
  const context = await browser.newContext();
  await context.route('**/*', route => ['localhost', '127.0.0.1'].includes(new URL(route.request().url()).hostname) ? route.continue() : route.abort());
  await context.route(`${base}/_task-recovery`, route => route.fulfill({ contentType: 'text/html', body: `<!doctype html><title>Task recovery verification</title><script type="module">
import RefreshRuntime from '/@react-refresh';
RefreshRuntime.injectIntoGlobalHook(window); window.$RefreshReg$ = () => {}; window.$RefreshSig$ = () => type => type;
window.__vite_plugin_react_preamble_installed__ = true;
</script>` }));
  const page = await context.newPage(), errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${base}/_task-recovery`);
  const migrated = await page.evaluate(async () => {
    const history = [{ id: 'u', role: 'user', text: 'Make a robot.', timestamp: 1 },
      { id: 'a', role: 'assistant', text: 'I will ask the agent.', timestamp: 2 },
      { id: 'later', role: 'user', text: 'Continue the lesson.', timestamp: 5 }];
    const record = { version: 1, id: 'task', phase: 'completed', note: 'Finished.', startedAt: 3, updatedAt: 4,
      handoff: { version: 1, id: 'task', sourceUserId: 'u', sourceAssistantId: 'a', conversationId: 'pair', nativeSession: 'private-native', accessScope: 'private-scope',
        input: { prompt: 'Make a robot.', model: 'fixture', systemInstruction: 'private-instruction', history: [], nativeLanguageCode: 'en',
          liveInputMedia: { privateFixturePayload: 'private-frame-and-audio' } } },
      operations: [{ commands: [{ action: 'create', kind: 'boxRobot' }], sceneRevision: 1 }],
      reply: { rawResponse: 'Robot ready.', parsed: { visibleText: 'Robot ready.', translations: [], hasSkippedNonLanguageContent: false } } };
    window.recoveryFixture = { history, record };
    await new Promise((resolve, reject) => {
      const request = indexedDB.open('GeminiLanguageTutorDB', 8);
      request.onupgradeneeded = () => {
        for (const [name, keyPath] of [['chatHistories', 'pairId'], ['chatMetas', 'pairId'], ['globalProfile', 'key'], ['appSettings', 'key'], ['appAssets', 'key'], ['agentTasks', 'id']]) request.result.createObjectStore(name, { keyPath });
      };
      request.onsuccess = () => {
        const db = request.result, tx = db.transaction(['chatHistories', 'agentTasks', 'chatMetas'], 'readwrite');
        tx.objectStore('chatHistories').put({ pairId: 'pair', messages: history });
        tx.objectStore('chatHistories').put({ pairId: 'other', messages: [{ id: 'other-user', role: 'user', text: 'Other conversation', timestamp: 1 }] });
        tx.objectStore('chatMetas').put({ pairId: 'pair', meta: { bookmarkMessageId: 'a' } });
        tx.objectStore('agentTasks').put(record);
        tx.objectStore('agentTasks').put({ ...record, id: 'unfinished', phase: 'working', note: 'Applying action.', reply: undefined });
        tx.oncomplete = () => { db.close(); resolve(); }; tx.onabort = () => reject(tx.error);
      };
      request.onerror = () => reject(request.error);
    });
    const { getChatHistoryDB } = await import('/src/features/chat/services/chatHistory.ts');
    const { openDB } = await import('/src/core/db/index.ts');
    const messages = await getChatHistoryDB('pair');
    const db = await openDB();
    const summary = await new Promise((resolve, reject) => {
      const tx = db.transaction('agentTaskSummaries', 'readonly'), req = tx.objectStore('agentTaskSummaries').get('task');
      tx.oncomplete = () => resolve(req.result); tx.onabort = () => reject(tx.error);
    });
    const version = db.version; db.close();
    return { version, ids: messages.map(message => message.id), completed: messages.find(message => message.id === 'task'),
      unfinished: messages.find(message => message.id === 'unfinished'), privateDataInSummary: ['private-instruction', 'private-frame-and-audio'].some(value => JSON.stringify(summary).includes(value)) };
  });
  assert.equal(migrated.version, 11); assert.equal(migrated.completed.text, 'Robot ready.');
  assert.equal(migrated.unfinished.agentTask.phase, 'working'); assert.equal(migrated.privateDataInSummary, false);
  assert.deepEqual([...migrated.ids].sort(), ['a', 'later', 'task', 'u', 'unfinished'].sort());

  const recovery = await page.evaluate(async () => {
    const { roomTaskStore } = await import('/src/features/chat/services/roomTaskStore.ts');
    const chats = await import('/src/features/chat/services/chatHistory.ts');
    const { useMaestroStore } = await import('/src/store/index.ts');
    const { subscribeRoomTaskResults } = await import('/src/features/chat/services/roomTaskResults.ts');
    const { history, record } = window.recoveryFixture;
    let announcements = 0; const unsubscribe = subscribeRoomTaskResults(() => { announcements++; });
    const select = async pair => {
      useMaestroStore.setState({ settings: { ...useMaestroStore.getState().settings, selectedLanguagePairId: pair } });
      await useMaestroStore.getState().loadHistoryForPair(pair, key => key);
      return useMaestroStore.getState().messages;
    };
    await select('other');
    const next = structuredClone(record); next.reply.parsed.visibleText = 'Robot checked and ready.'; next.updatedAt++;
    await roomTaskStore.save(next);
    const otherUnchanged = useMaestroStore.getState().messages[0].text === 'Other conversation';
    await chats.saveChatHistoryDB('pair', history); // Stale autosave has no result at all.
    const loaded = await select('pair');
    const result = loaded.find(message => message.id === 'task');
    const bookmark = useMaestroStore.getState().settings.historyBookmarkMessageId;
    const all = await chats.getAllChatHistoriesDB();
    const duplicate = await roomTaskStore.claim(record);
    const privateJournalRetained = (await roomTaskStore.get('task')).handoff.input.liveInputMedia.privateFixturePayload === 'private-frame-and-audio';
    unsubscribe();
    return { otherUnchanged, result, bookmark, exportResult: all.pair.find(message => message.id === 'task').text,
      duplicateClaimed: duplicate.claimed, announcements, privateJournalRetained };
  });
  assert.equal(recovery.otherUnchanged, true); assert.equal(recovery.result.text, 'Robot checked and ready.');
  assert.equal(recovery.exportResult, recovery.result.text); assert.equal(recovery.bookmark, 'a');
  assert.equal(recovery.duplicateClaimed, false); assert.equal(recovery.announcements, 0); assert.equal(recovery.privateJournalRetained, true);

  const atomic = await page.evaluate(async () => {
    const { roomTaskStore } = await import('/src/features/chat/services/roomTaskStore.ts');
    const { getChatHistoryDB } = await import('/src/features/chat/services/chatHistory.ts');
    const before = await roomTaskStore.get('task');
    const next = structuredClone(before); next.reply.parsed.visibleText = 'Must not commit';
    const put = IDBObjectStore.prototype.put;
    IDBObjectStore.prototype.put = function (...args) {
      const request = Reflect.apply(put, this, args);
      if (this.name === 'agentTaskSummaries') request.addEventListener('success', () => this.transaction.abort(), { once: true });
      return request;
    };
    let rejected = false;
    try { await roomTaskStore.save(next); } catch { rejected = true; }
    finally { IDBObjectStore.prototype.put = put; }
    return { rejected, unchangedJournal: JSON.stringify(await roomTaskStore.get('task')) === JSON.stringify(before),
      unchangedChat: (await getChatHistoryDB('pair')).find(message => message.id === 'task').text === before.reply.parsed.visibleText };
  });
  assert.deepEqual(atomic, { rejected: true, unchangedJournal: true, unchangedChat: true });

  const hidden = await page.evaluate(async () => {
    const { useMaestroStore } = await import('/src/store/index.ts');
    const { hideRoomTaskMessage } = await import('/src/features/chat/services/roomTaskSummaries.ts');
    const { roomTaskStore } = await import('/src/features/chat/services/roomTaskStore.ts');
    const chats = await import('/src/features/chat/services/chatHistory.ts');
    useMaestroStore.getState().deleteMessage('task'); // Actual user deletion path.
    await hideRoomTaskMessage('task'); // Await visibility persistence before inspecting it.
    const record = await roomTaskStore.get('task'); await roomTaskStore.save(record);
    await chats.saveChatHistoryDB('pair', window.recoveryFixture.history);
    const messages = await chats.getChatHistoryDB('pair');
    return { visible: messages.some(message => message.id === 'task'), claimRetained: !(await roomTaskStore.claim(record)).claimed };
  });
  assert.deepEqual(hidden, { visible: false, claimRetained: true });
  await page.reload();
  const afterReload = await page.evaluate(async () => {
    const chats = await import('/src/features/chat/services/chatHistory.ts');
    const { roomTaskStore } = await import('/src/features/chat/services/roomTaskStore.ts');
    const { openDB } = await import('/src/core/db/index.ts');
    const messages = await chats.getChatHistoryDB('pair'), record = await roomTaskStore.get('task');
    const hiddenAfterReload = !messages.some(message => message.id === 'task');
    const { useMaestroStore } = await import('/src/store/index.ts');
    useMaestroStore.setState({ messages, isLoadingHistory: false, settings: { ...useMaestroStore.getState().settings, selectedLanguagePairId: 'pair' } });
    useMaestroStore.getState().deleteMessage('u');
    const sourceDeletionClearedVisibleResults = !useMaestroStore.getState().messages.some(message => message.agentTask);
    await chats.saveChatHistoryDB('pair', useMaestroStore.getState().messages);
    let lateSaveRejected = false, newClaimRejected = false;
    try { await roomTaskStore.save(record); } catch { lateSaveRejected = true; }
    try { await roomTaskStore.claim({ ...record, id: 'late-new-claim' }); } catch { newClaimRejected = true; }
    const journalRemoved = !(await roomTaskStore.get('task'));
    const db = await openDB();
    const count = await new Promise((resolve, reject) => {
      const tx = db.transaction('agentTaskSummaries', 'readonly'), request = tx.objectStore('agentTaskSummaries').count();
      tx.oncomplete = () => resolve(request.result); tx.onabort = () => reject(tx.error);
    }); db.close();
    await chats.clearAndSaveAllHistoriesDB({ restored: [{ id: 'restored-user', role: 'user', timestamp: 1, text: 'Restored history' }] });
    const restored = await chats.getChatHistoryDB('restored');
    return { hiddenAfterReload, sourceDeletionClearedVisibleResults, lateSaveRejected, newClaimRejected, journalRemoved, summariesAfterSourceDeletion: count, restored: restored[0].text };
  });
  assert.deepEqual(afterReload, { hiddenAfterReload: true, sourceDeletionClearedVisibleResults: true, lateSaveRejected: true, newClaimRejected: true, journalRemoved: true, summariesAfterSourceDeletion: 0, restored: 'Restored history' });
  assert.deepEqual(errors, []);
  await writeFile(`${out}/receipt.json`, JSON.stringify({ migrated, recovery, atomic, hidden, afterReload, errors,
    provider: 'not called', native: 'not called', storage: 'real IndexedDB', deviceAccess: false }, null, 2));
  console.log(JSON.stringify({ passed: true, evidence: out }));
} finally { await browser.close(); }
