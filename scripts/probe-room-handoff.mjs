// Local browser verification. No accounts, provider requests or headset access.
import { chromium } from 'playwright-core';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';
const base = process.env.MAESTRO_HANDOFF_FIXTURE_URL || 'http://127.0.0.1:5184';
if (!['localhost', '127.0.0.1'].includes(new URL(base).hostname)) throw new Error('Local fixture required.');
const out = resolve('.quest-evidence/agent-handoff');
await mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const errors = [];
let probePage;
try {
  const context = await browser.newContext({ viewport: { width: 1024, height: 768 } });
  await context.route('**/*', route => ['localhost', '127.0.0.1'].includes(new URL(route.request().url()).hostname) ? route.continue() : route.abort());
  const page = await context.newPage(); probePage = page; page.setDefaultTimeout(15000); page.on('pageerror', error => { errors.push(error.message); console.error(error.message); });
  const evidence = () => page.evaluate(() => window.agentTaskFixture.evidence());
  await context.route(`${base}/_agent-storage-seed`, route => route.fulfill({ contentType: 'text/html', body: '<!doctype html><title>Storage migration setup</title>' }));
  await page.goto(`${base}/_agent-storage-seed`);
  await page.evaluate(() => new Promise((resolve, reject) => {
    const request = indexedDB.open('GeminiLanguageTutorDB', 7);
    request.onupgradeneeded = () => {
      for (const [name, keyPath] of [['chatHistories', 'pairId'], ['chatMetas', 'pairId'], ['globalProfile', 'key'], ['appSettings', 'key'], ['appAssets', 'key']]) request.result.createObjectStore(name, { keyPath });
    };
    request.onerror = () => reject(request.error);
    request.onsuccess = () => {
      const db = request.result, tx = db.transaction('chatHistories', 'readwrite');
      tx.objectStore('chatHistories').put({ pairId: 'legacy', messages: [{ id: 'legacy-user', role: 'user', text: 'Existing conversation', timestamp: 1 }] });
      tx.oncomplete = () => { db.close(); resolve(); }; tx.onabort = () => reject(tx.error);
    };
  }));
  await page.goto(`${base}/test-fixtures/browser/agent-task.html`, { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'Start simulated handoff' }).click();
  await page.getByText('Applying the next action.', { exact: true }).waitFor();
  const working = await evidence(); assert.equal(working.agentWorking, true); assert.equal(working.inputBlocked, false);
  await page.getByRole('textbox', { name: 'Chat message' }).fill('Can we practise Spanish while you work?');
  await page.screenshot({ path: `${out}/working.png` });
  await page.getByRole('button', { name: 'Acknowledge simulated action' }).click();
  await page.getByText('Your robot is ready.', { exact: true }).waitFor();
  await page.getByText('Task details', { exact: true }).click();
  await page.getByText('create: Created robot', { exact: true }).waitFor();
  await page.screenshot({ path: `${out}/completed.png` });
  const completed = await evidence(); assert.equal(completed.executions, 1); assert.equal(completed.agentWorking, false);
  await page.reload();
  await page.getByText('Your robot is ready.', { exact: true }).waitFor();
  await page.getByRole('button', { name: 'Start simulated handoff' }).click();
  assert.equal((await evidence()).executions, 0, 'Reloaded task was replayed');
  assert.deepEqual(await page.evaluate(() => window.agentTaskFixture.pruneAndCheck()), { removed: true, lateSaveRejected: true, atomicClaims: 1 });
  const legacy = await page.evaluate(async () => (await import('/src/features/chat/services/chatHistory.ts')).getChatHistoryDB('legacy'));
  assert.equal(legacy[0].text, 'Existing conversation');
  await context.close();
  const stoppedContext = await browser.newContext({ viewport: { width: 1024, height: 768 } });
  await stoppedContext.route('**/*', route => ['localhost', '127.0.0.1'].includes(new URL(route.request().url()).hostname) ? route.continue() : route.abort());
  const stopped = await stoppedContext.newPage(); stopped.on('pageerror', error => errors.push(error.message));
  await stopped.goto(`${base}/test-fixtures/browser/agent-task.html`, { waitUntil: 'domcontentloaded' });
  await stopped.getByRole('button', { name: 'Start simulated handoff' }).click();
  await stopped.getByText('Applying the next action.', { exact: true }).waitFor();
  await stopped.getByRole('button', { name: 'Stop task', exact: true }).click();
  await stopped.getByText('Stopped with an unconfirmed action. Inspect the room before trying again.', { exact: true }).waitFor();
  await stopped.getByText('Task details', { exact: true }).click();
  await stopped.getByText('create: Outcome unconfirmed; do not automatically repeat.', { exact: true }).waitFor();
  await stopped.screenshot({ path: `${out}/stopped.png` });
  assert.deepEqual(errors, []);
  await writeFile(`${out}/receipt.json`, JSON.stringify({ working, completed, reloadedWithoutReplay: true, historyPruned: true, lateSaveRejected: true, atomicClaims: 1, migratedLegacyHistory: true, stoppedUnconfirmed: true, errors,
    provider: 'simulated', native: 'simulated', storage: 'real IndexedDB', deviceAccess: false }, null, 2));
  console.log(JSON.stringify({ passed: true, evidence: out }));
} catch (error) {
  if (probePage && !probePage.isClosed()) {
    await probePage.screenshot({ path: `${out}/failure.png` });
    console.error(await probePage.locator('body').innerText());
    console.error(await probePage.evaluate(async () => (await import('/src/features/chat/services/roomTaskStore.ts')).roomTaskStore.get('fixture-agent-task')));
  }
  throw error;
} finally { await browser.close(); }
