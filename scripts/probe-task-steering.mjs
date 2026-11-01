// Isolated Chromium, actual task runner/dispatcher/IndexedDB/UI; simulated model and room ports.
import { chromium } from 'playwright-core';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';
const base = process.env.MAESTRO_HANDOFF_FIXTURE_URL || 'http://127.0.0.1:5184';
if (!['localhost', '127.0.0.1'].includes(new URL(base).hostname)) throw new Error('Local fixture required.');
const out = resolve('.quest-evidence/task-steering'); await mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
try {
  const context = await browser.newContext({ viewport: { width: 1100, height: 850 } });
  await context.route('**/*', route => ['localhost', '127.0.0.1'].includes(new URL(route.request().url()).hostname) ? route.continue() : route.abort());
  await context.route(`${base}/_task-steering`, route => route.fulfill({ contentType: 'text/html', body: `<!doctype html><title>Conversational task control verification</title><div id="root"></div><script type="module">
import RefreshRuntime from '/@react-refresh';
RefreshRuntime.injectIntoGlobalHook(window); window.$RefreshReg$ = () => {}; window.$RefreshSig$ = () => type => type;
window.__vite_plugin_react_preamble_installed__ = true;
</script>` }));
  const page = await context.newPage(), errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${base}/_task-steering`);
  const result = await page.evaluate(async () => {
    const prior = { version: 1, id: 'legacy', phase: 'completed', note: 'Earlier work', startedAt: 1, updatedAt: 2, operations: [],
      handoff: { version: 1, id: 'legacy', sourceUserId: 'legacy-u', sourceAssistantId: 'legacy-a', conversationId: 'pair', nativeSession: 'room', accessScope: 'fixture',
        input: { model: 'fixture', prompt: 'Earlier request', systemInstruction: 'Fixture', history: [], nativeLanguageCode: 'en' } } };
    await new Promise((resolve, reject) => {
      const request = indexedDB.open('GeminiLanguageTutorDB', 10);
      request.onupgradeneeded = () => {
        for (const [name, keyPath] of [['chatHistories', 'pairId'], ['chatMetas', 'pairId'], ['globalProfile', 'key'], ['appSettings', 'key'], ['appAssets', 'key'], ['agentTasks', 'id'], ['agentTaskSummaries', 'id']]) {
          const store = request.result.createObjectStore(name, { keyPath }); if (name === 'agentTaskSummaries') store.createIndex('conversationId', 'conversationId');
        }
        const stage = request.result.createObjectStore('backupStaging', { keyPath: ['batch', 'kind', 'id'] }); stage.createIndex('batch', 'batch'); stage.createIndex('createdAt', 'createdAt');
      };
      request.onsuccess = () => {
        const db = request.result, tx = db.transaction(['agentTasks', 'agentTaskSummaries'], 'readwrite'); tx.objectStore('agentTasks').put(prior);
        tx.objectStore('agentTaskSummaries').put({ id: 'legacy', conversationId: 'pair', hidden: true });
        tx.oncomplete = () => { db.close(); resolve(); }; tx.onabort = () => reject(tx.error);
      }; request.onerror = () => reject(request.error);
    });
    const { RoomTaskHandoff } = await import('/src/core-sdk/room/roomTaskHandoff.ts');
    const { runRoomActionTask } = await import('/src/core-sdk/room/roomAgent.ts');
    const { roomTaskStore } = await import('/src/features/chat/services/roomTaskStore.ts');
    const { loadRoomTaskSummaries } = await import('/src/features/chat/services/roomTaskSummaries.ts');
    const { saveChatHistoryDB, getChatHistoryDB } = await import('/src/features/chat/services/chatHistory.ts');
    const { normalizeSuggestionCreatorToolRequest } = await import('/src/core-sdk/chat/suggestionAftersteps.ts');
    const { createAssistantTools } = await import('/src/features/chat/coordinators/assistantTools.ts');
    const { openDB } = await import('/src/core/db/index.ts');
    const migrated = (await loadRoomTaskSummaries('pair'))[0];
    const db = await openDB(); const version = db.version; db.close();
    let scene = { version: 1, session: 'room', revision: 1, sceneRevision: 1, ack: 0, ok: true, status: 'Ready', objects: [], created: [], canUndo: false, canRedo: false, physicsRunning: false };
    const history = [{ id: 'legacy-u', role: 'user', text: 'Earlier request', timestamp: 1 }, { id: 'legacy-a', role: 'assistant', text: 'Earlier response', timestamp: 2 }];
    const records = [], calls = [], plans = []; let acknowledge, began;
    const started = new Promise(resolve => { began = resolve; });
    const robotId = 'b'.repeat(32);
    const execute = async (commands, _revision, _objects, signal) => {
      calls.push(commands);
      if (commands[0].action === 'create') return new Promise(resolve => {
        acknowledge = () => { scene = { ...scene, revision: 2, sceneRevision: 2, ack: 1, status: 'Robot created before cancellation', created: [robotId],
          objects: [{ id: robotId, name: 'Robot', kind: 'boxRobot', position: { x: 0, y: 1, z: 0 }, scale: 1, color: { r: 1, g: 1, b: 1, a: 1 }, animated: true }] }; resolve(scene); };
        signal.addEventListener('abort', acknowledge, { once: true }); began();
      });
      scene = { ...scene, revision: scene.revision + 1, sceneRevision: scene.sceneRevision + 1, ack: scene.ack + 1, status: 'Robot painted blue',
        objects: scene.objects.map(object => ({ ...object, color: commands[0].color })) }; return scene;
    };
    const manager = new RoomTaskHandoff({ store: roomTaskStore, lease: () => ({ valid: () => true, state: () => scene, execute }), now: Date.now,
      activity: () => {}, changed: record => { records.push(structuredClone(record)); },
      run: (input, lease, control) => {
        let planned = false;
        const ai = { models: { generateContentStream: async request => {
          plans.push(request);
          const commands = planned ? [] : input.prompt.includes('blue') ? [{ action: 'paint', target: robotId, color: { r: 0, g: 0, b: 1, a: 1 } }]
            : [{ action: 'create', reference: 'r', name: 'Robot', kind: 'boxRobot' }]; planned = true;
          return (async function* () { yield { text: JSON.stringify({ commands }) }; })();
        } } };
        return runRoomActionTask(input, { aiClient: ai }, lease, () => {}, control);
      },
      reply: async (_input, result) => { const text = result.relatedTask?.action === 'stop' ? 'The task has stopped. The robot remains in the room.' : 'The robot is blue.';
        return { rawResponse: text, parsed: { visibleText: text, translations: [], hasSkippedNonLanguageContent: false } }; },
    });
    const capture = async (id, prompt, targets = []) => {
      history.push({ id: id + '-u', role: 'user', text: prompt, timestamp: Date.now() }, { id: id + '-a', role: 'assistant', text: 'I will ask the agent.', timestamp: Date.now() + 1 });
      await saveChatHistoryDB('pair', history);
      manager.capture({ version: 1, id, sourceUserId: id + '-u', sourceAssistantId: id + '-a', conversationId: 'pair', nativeSession: 'room', accessScope: 'fixture',
        input: { model: 'fixture', prompt, history: [], systemInstruction: 'Fixture', nativeLanguageCode: 'en' } }, async () => true, targets);
    };
    await capture('parent', 'Make a robot.'); const parent = manager.start('parent-a'); await started;
    const target = { id: 'parent', phase: 'working', requestPreview: 'Make a robot.', replyPreview: '', running: true };
    await capture('stop', 'Stop that task.', [target]);
    const normalized = normalizeSuggestionCreatorToolRequest({ tool: 'agent', task: { action: 'stop', taskId: 'parent' } }, '', { allowAgent: true, agentTargets: [target] });
    const dispatcher = createAssistantTools({ messagesRef: { current: history }, updateMessage: () => {}, runAgentTask: (id, directive) => manager.start(id, directive) });
    await dispatcher.executeAssistantToolRequest('stop-a', normalized); await parent;
    const stop = await roomTaskStore.get('stop'); const parentStopped = await roomTaskStore.get('parent');
    const executionsAfterStop = calls.length;
    await dispatcher.executeAssistantToolRequest('stop-a', normalized); const duplicateExecutions = calls.length;
    await capture('revision', 'Make that robot blue.', [{ ...target, phase: 'stopped', running: false }]);
    await dispatcher.executeAssistantToolRequest('revision-a', { tool: 'agent', task: { action: 'revise', taskId: 'parent' } });
    const revision = await roomTaskStore.get('revision');
    const { AgentTaskStatus } = await import('/src/features/chat/components/AgentTaskStatus.tsx');
    const React = await import('/node_modules/.vite/deps/react.js'), dom = await import('/node_modules/.vite/deps/react-dom_client.js');
    const element = React.createElement || React.default.createElement;
    (dom.createRoot || dom.default.createRoot)(document.getElementById('root')).render(element('main', { style: { maxWidth: 850, margin: '24px auto', font: '18px system-ui' } },
      element('h1', {}, 'Conversational task control — simulated room'),
      ...[parentStopped, stop, revision].map(record => element('article', { key: record.id }, element('p', {}, record.handoff.input.prompt),
        element(AgentTaskStatus, { task: { id: record.id, phase: record.phase, note: record.note }, controls: manager }), element('p', {}, record.reply?.parsed.visibleText || record.note)))));
    return { version, migratedHidden: migrated.hidden, migratedScope: migrated.taskScope,
      parentPhase: parentStopped.phase, stopPhase: stop.phase, stopRelated: stop.relatedTask, executionsAfterStop, duplicateExecutions,
      revisionPhase: revision.phase, revisionRequests: revision.relatedTask.requests, revisionOperations: revision.operations.length,
      calls: calls.map(batch => batch.map(command => command.action)), color: scene.objects[0].color,
      historyTasks: (await getChatHistoryDB('pair')).filter(message => message.agentTask).map(message => message.id).sort(),
      priorEvidenceInPlanner: JSON.stringify(plans.at(-2)).includes('Robot created before cancellation') };
  });
  assert.equal(result.version, 11); assert.equal(result.migratedHidden, true); assert.equal(result.migratedScope.nativeSession, 'room');
  assert.equal(result.parentPhase, 'stopped'); assert.equal(result.stopPhase, 'completed'); assert.equal(result.stopRelated.wasRunning, true);
  assert.equal(result.stopRelated.unconfirmed, false); assert.equal(result.executionsAfterStop, 1); assert.equal(result.duplicateExecutions, 1);
  assert.equal(result.revisionPhase, 'completed'); assert.deepEqual(result.revisionRequests, ['Make a robot.']); assert.equal(result.revisionOperations, 1);
  assert.deepEqual(result.calls, [['create'], ['paint']]); assert.deepEqual(result.color, { r: 0, g: 0, b: 1, a: 1 });
  assert.deepEqual(result.historyTasks, ['parent', 'revision', 'stop']); assert.equal(result.priorEvidenceInPlanner, true);
  await page.getByText('Task details', { exact: true }).last().click();
  await page.getByText('Request: Revise earlier work.', { exact: true }).waitFor();
  await page.screenshot({ path: `${out}/task-control.png`, fullPage: true });
  assert.deepEqual(errors, []);
  await writeFile(`${out}/receipt.json`, JSON.stringify({ ...result, errors, provider: 'simulated', native: 'simulated', storage: 'real Chromium IndexedDB', deviceAccess: false }, null, 2));
  console.log(JSON.stringify({ passed: true, evidence: out }));
} finally { await browser.close(); }
