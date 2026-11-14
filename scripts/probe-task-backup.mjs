// Real browser/IndexedDB backup checks. Isolated profile; no account, provider or device access.
import { chromium } from 'playwright-core';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';
const base = process.env.MAESTRO_HANDOFF_FIXTURE_URL || 'http://127.0.0.1:5184';
if (!['localhost', '127.0.0.1'].includes(new URL(base).hostname)) throw new Error('Local fixture required.');
const out = resolve('.quest-evidence/task-backup'); await mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
try {
  const context = await browser.newContext();
  await context.route('**/*', route => ['localhost', '127.0.0.1'].includes(new URL(route.request().url()).hostname) ? route.continue() : route.abort());
  await context.route(`${base}/_task-backup`, route => route.fulfill({ contentType: 'text/html', body: `<!doctype html><title>Task backup verification</title><div id="root"></div><script type="module">
import RefreshRuntime from '/@react-refresh';
RefreshRuntime.injectIntoGlobalHook(window); window.$RefreshReg$ = () => {}; window.$RefreshSig$ = () => type => type;
window.__vite_plugin_react_preamble_installed__ = true;
</script>` }));
  const page = await context.newPage(), errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${base}/_task-backup`);
  const exported = await page.evaluate(async () => {
    const { openDB } = await import('/src/core/db/index.ts');
    const { roomTaskStore } = await import('/src/features/chat/services/roomTaskStore.ts');
    const chats = await import('/src/features/chat/services/chatHistory.ts');
    const { hideRoomTaskMessage } = await import('/src/features/chat/services/roomTaskSummaries.ts');
    const { LiveInputContext } = await import('/src/core-sdk/media/liveInputContext.ts');
    const { useMaestroStore } = await import('/src/store/index.ts');
    const { subscribeRoomTaskResults } = await import('/src/features/chat/services/roomTaskResults.ts');
    const history = [{ id: 'u', role: 'user', text: 'Make a robot.', timestamp: 1 }, { id: 'a', role: 'assistant', text: 'Delegating.', timestamp: 2 }];
    const media = new LiveInputContext(() => 0); media.recordAudio('AAA='); media.recordFrame('/9j/2Q==');
    const record = { version: 1, id: 'task', phase: 'completed', note: 'Finished.', startedAt: 3, updatedAt: 4,
      handoff: { version: 1, id: 'task', sourceUserId: 'u', sourceAssistantId: 'a', conversationId: 'pair', nativeSession: 'original-room', accessScope: 'original-scope',
        input: { prompt: 'Make a robot.', model: 'fixture', systemInstruction: '🤖'.repeat(140000), history: [], nativeLanguageCode: 'en', liveInputMedia: media.finish() } },
      operations: [{ commands: [{ action: 'workspace', visible: true }], sceneRevision: 1, receipt: { ok: true, status: 'Applied', sceneRevision: 2 } }],
      reply: { rawResponse: 'Robot ready.', parsed: { visibleText: 'Robot ready.', translations: [], hasSkippedNonLanguageContent: false } } };
    await chats.saveChatHistoryDB('pair', history);
    await chats.saveChatHistoryDB('other', [{ id: 'other-user', role: 'user', text: 'Other conversation', timestamp: 1 }]);
    await chats.setChatMetaDB('pair', { bookmarkMessageId: 'a' });
    const originals = [];
    for (const id of ['task', 'hidden-task', 'unfinished-task']) {
      const item = structuredClone(record); item.id = item.handoff.id = id;
      if (id === 'unfinished-task') { item.phase = 'working'; delete item.reply; }
      await roomTaskStore.claim(item); originals.push(item);
    }
    await hideRoomTaskMessage('hidden-task');
    const { setGlobalProfileDB } = await import('/src/features/session/services/globalProfile.ts');
    const { setMaestroProfileImageDB } = await import('/src/core/db/assets.ts');
    await setGlobalProfileDB('Original profile'); await setMaestroProfileImageDB({ dataUrl: 'data:image/png;base64,fixture', uri: 'device-local-path' });
    useMaestroStore.setState({ messages: await chats.getChatHistoryDB('pair'), isLoadingHistory: false,
      settings: { ...useMaestroStore.getState().settings, selectedLanguagePairId: 'pair' } });
    window.announcements = 0; subscribeRoomTaskResults(() => window.announcements++);
    window.alerts = []; window.alert = text => window.alerts.push(text);
    window.savedFiles = []; window.showSaveFilePicker = async () => ({ createWritable: async () => {
      const lines = []; return { write: async line => lines.push(line), close: async () => window.savedFiles.push(lines.join('')) };
    } });
    const React = await import('/node_modules/.vite/deps/react.js');
    const dom = await import('/node_modules/.vite/deps/react-dom_client.js');
    const createRoot = dom.createRoot || dom.default.createRoot;
    const { useDataBackup } = await import('/src/features/session/hooks/useDataBackup.ts');
    const Component = () => { window.backupActions = useDataBackup({ t: (key, params) => key + JSON.stringify(params || {}) }); return null; };
    createRoot(document.getElementById('root')).render((React.createElement || React.default.createElement)(Component));
    while (!window.backupActions) await new Promise(resolve => setTimeout(resolve, 10));
    await window.backupActions.handleSaveAllChats();
    const file = window.savedFiles.at(-1); if (!file) throw new Error('Export failed: ' + window.alerts.join(';'));
    window.backupFixture = { file, originals, history };
    const rows = file.trim().split('\n').map(JSON.parse);
    const db = await openDB(); const version = db.version; db.close();
    return { version, end: rows.at(-1), chunkCount: rows.filter(row => row.type === 'agentTaskChunk').length, announcements: window.announcements };
  });
  assert.equal(exported.version, 11); assert.deepEqual(exported.end, { type: 'end', chats: 2, tasks: 3 });
  assert.ok(exported.chunkCount > 3); assert.equal(exported.announcements, 0);
  const restore = await page.evaluate(async () => {
    const chats = await import('/src/features/chat/services/chatHistory.ts');
    const { roomTaskStore } = await import('/src/features/chat/services/roomTaskStore.ts');
    const { useMaestroStore } = await import('/src/store/index.ts');
    const { getGlobalProfileDB } = await import('/src/features/session/services/globalProfile.ts');
    const { getMaestroProfileImageDB } = await import('/src/core/db/assets.ts');
    const { file, originals } = window.backupFixture;
    await chats.clearAndSaveAllHistoriesDB({ obsolete: [{ id: 'old', role: 'user', timestamp: 1, text: 'Obsolete' }] });
    useMaestroStore.setState({ messages: [] });
    await window.backupActions.handleLoadAllChats(new File([file], 'fixture.ndjson'));
    const records = await Promise.all(originals.map(record => roomTaskStore.get(record.id)));
    const preserved = records.every((record, index) => JSON.stringify({ ...record, readOnly: undefined }) === JSON.stringify(originals[index]));
    let lateWriteRejected = false;
    try { await roomTaskStore.save({ ...originals[0], note: 'Late callback' }); } catch { lateWriteRejected = true; }
    const all = await chats.getAllChatHistoriesDB();
    return { preserved, readOnly: records.every(record => record.readOnly), lateWriteRejected, ids: all.pair.map(message => message.id).sort(),
      pairs: Object.keys(all).sort(), bookmark: useMaestroStore.getState().settings.historyBookmarkMessageId,
      profile: (await getGlobalProfileDB()).text, strippedUri: !(await getMaestroProfileImageDB()).uri,
      duplicateClaimed: (await roomTaskStore.claim(originals[0])).claimed, announcements: window.announcements,
      uiIds: useMaestroStore.getState().messages.map(message => message.id).sort(), loading: useMaestroStore.getState().isLoadingHistory };
  });
  assert.equal(restore.preserved, true); assert.equal(restore.readOnly, true); assert.equal(restore.lateWriteRejected, true);
  assert.deepEqual(restore.ids, ['a', 'task', 'u', 'unfinished-task']); assert.deepEqual(restore.uiIds, restore.ids);
  assert.deepEqual(restore.pairs, ['other', 'pair']); assert.equal(restore.bookmark, 'a'); assert.equal(restore.profile, 'Original profile');
  assert.equal(restore.strippedUri, true); assert.equal(restore.duplicateClaimed, false); assert.equal(restore.announcements, 0); assert.equal(restore.loading, false);
  const failures = await page.evaluate(async () => {
    const backup = await import('/src/features/session/services/backupArchive.ts');
    const codec = await import('/src/core-sdk/backup/archive.ts');
    const { openDB } = await import('/src/core/db/index.ts');
    const stores = ['chatHistories', 'chatMetas', 'globalProfile', 'appAssets', 'agentTasks', 'agentTaskSummaries'];
    const snapshot = async () => { const db = await openDB(); return new Promise((resolve, reject) => {
      const tx = db.transaction(stores, 'readonly'), requests = stores.map(name => tx.objectStore(name).getAll());
      tx.oncomplete = () => { db.close(); resolve(JSON.stringify(requests.map(request => request.result))); }; tx.onabort = () => reject(tx.error);
    }); };
    const stage = text => backup.stageBackup(async visit => { for (const line of text.trim().split('\n')) await visit(line); });
    const before = await snapshot(); const file = window.backupFixture.file; const results = {};
    for (const [label, text] of [['truncated', file.slice(0, file.lastIndexOf('{"type":"end"'))], ['corrupt', file.replace('Original context', 'tamper') + '{']]) {
      let rejected = false; try { await stage(text); } catch { rejected = true; }
      results[label] = rejected && await snapshot() === before;
    }
    const sourceMissing = structuredClone(window.backupFixture.originals[0]); sourceMissing.handoff.sourceUserId = 'absent-source';
    const rows = file.trim().split('\n').map(JSON.parse).filter(row => row.type !== 'agentTaskChunk' && row.type !== 'end');
    const invalidLines = rows.map(row => JSON.stringify(row));
    for await (const line of codec.encodeRoomTaskArchive({ version: 1, hidden: false, record: sourceMissing })) invalidLines.push(line.trim());
    invalidLines.push(JSON.stringify({ type: 'end', chats: 2, tasks: 1 }));
    const missing = await stage(invalidLines.join('\n'));
    let rejected = false; try { await backup.commitBackupStage(missing); } catch { rejected = true; }
    results.missingSourceAtomic = rejected && await snapshot() === before; await backup.discardBackupStage(missing.batch);
    const staged = await stage(file), add = IDBObjectStore.prototype.add;
    IDBObjectStore.prototype.add = function (...args) { const request = Reflect.apply(add, this, args);
      if (this.name === 'agentTaskSummaries') request.addEventListener('success', () => this.transaction.abort(), { once: true }); return request; };
    rejected = false; try { await backup.commitBackupStage(staged); } catch { rejected = true; } finally { IDBObjectStore.prototype.add = add; }
    results.storageFailureAtomic = rejected && await snapshot() === before; await backup.discardBackupStage(staged.batch);
    IDBObjectStore.prototype.add = function (...args) { const request = Reflect.apply(add, this, args);
      if (this.name === 'backupStaging') request.addEventListener('success', () => this.transaction.abort(), { once: true }); return request; };
    rejected = false; try { await stage(file); } catch { rejected = true; } finally { IDBObjectStore.prototype.add = add; }
    results.stagingFailurePreserves = rejected && await snapshot() === before;
    const db = await openDB(); const count = await new Promise(resolve => { const request = db.transaction('backupStaging').objectStore('backupStaging').count(); request.onsuccess = () => resolve(request.result); }); db.close();
    results.stagingCleared = count === 0; return results;
  });
  for (const [name, passed] of Object.entries(failures)) assert.equal(passed, true, name);
  const append = await page.evaluate(async () => {
    const chats = await import('/src/features/chat/services/chatHistory.ts');
    const { roomTaskStore } = await import('/src/features/chat/services/roomTaskStore.ts');
    const { useMaestroStore } = await import('/src/store/index.ts');
    const original = await roomTaskStore.get('task'), other = await chats.getChatHistoryDB('other');
    const text = window.backupFixture.file;
    await window.backupActions.handleAppendToCurrentChat(new File([text], 'combine.ndjson'));
    await window.backupActions.handleAppendToCurrentChat(new File([text], 'combine.ndjson'));
    await window.backupActions.handleSaveCurrentChat();
    const rows = window.savedFiles.at(-1).trim().split('\n').map(JSON.parse);
    return { sameTask: JSON.stringify(await roomTaskStore.get('task')) === JSON.stringify(original), sameOther: JSON.stringify(await chats.getChatHistoryDB('other')) === JSON.stringify(other),
      ids: useMaestroStore.getState().messages.map(message => message.id).sort(), end: rows.at(-1), announcements: window.announcements };
  });
  assert.equal(append.sameTask, true); assert.equal(append.sameOther, true); assert.deepEqual(append.ids, restore.ids);
  assert.deepEqual(append.end, { type: 'end', chats: 1, tasks: 3 }); assert.equal(append.announcements, 0);
  assert.deepEqual(errors, []);
  await writeFile(`${out}/receipt.json`, JSON.stringify({ exported, restore, failures, append, errors, provider: 'not called', native: 'not called', deviceAccess: false }, null, 2));
  console.log(JSON.stringify({ passed: true, evidence: out }));
} finally { await browser.close(); }
