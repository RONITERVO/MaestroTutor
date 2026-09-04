// Full application replay against staging, using an isolated browser and a fake microphone.
// Start Vite in staging mode first. Credentials stay in the test process/browser only.
import { chromium } from 'playwright-core';
import { mkdir, writeFile, readFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const base = process.env.MAESTRO_UI_REPLAY_URL || 'http://localhost:5173';
if (!['localhost', '127.0.0.1'].includes(new URL(base).hostname)) throw new Error('Replay requires localhost');
const wav = process.env.MAESTRO_UI_REPLAY_WAV;
if (!wav || !process.env.MAESTRO_FIREBASE_EMAIL || !process.env.MAESTRO_FIREBASE_PASSWORD) throw new Error('Supply replay WAV and staging credentials');
const out = resolve('.maestro-debug/full-ui-replay');
await mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true,
  args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream',
    `--use-file-for-fake-audio-capture=${resolve(wav)}`, '--autoplay-policy=no-user-gesture-required'] });
let page;
try {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, permissions: ['microphone'] });
  page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(base);
  await page.waitForSelector('main', { timeout: 60000 });
  const setup = await page.evaluate(async ({ email, password }) => {
    const fixture = await import('/test-fixtures/browser/liveReplaySetup.ts');
    return fixture.prepare(email, password);
  }, { email: process.env.MAESTRO_FIREBASE_EMAIL.trim(), password: process.env.MAESTRO_FIREBASE_PASSWORD.trim() });
  console.log(JSON.stringify({ phase: 'prepared', ...setup }));
  const preview = page.getByTestId('concealed-speech');
  await preview.waitFor({ timeout: 120000 });
  await page.screenshot({ path: `${out}/concealed.png` });
  const previewText = await preview.innerText();
  const initialMarks = await preview.locator('span').count();
  const progress = [{ at: Date.now(), marks: initialMarks }];
  const previewDeadline = Date.now() + 90000;
  while (await preview.isVisible() && Date.now() < previewDeadline) {
    const marks = await preview.locator('span').count();
    if (marks !== progress.at(-1).marks) progress.push({at: Date.now(), marks});
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  await preview.waitFor({ state: 'hidden', timeout: 1000 });
  let timing;
  const deadline = Date.now() + 120000;
  do {
    timing = await page.evaluate(async () => (await import('/test-fixtures/browser/liveReplaySetup.ts')).evidence());
    if (timing.reports.some(report => report.events.some(event => event.name === 'playback.drained'))) break;
    await new Promise(resolve => setTimeout(resolve, 500));
  } while (Date.now() < deadline);
  await page.screenshot({ path: `${out}/completed.png` });
  await writeFile(`${out}/evidence.json`, JSON.stringify({setup, previewText, initialMarks, progress, timing, errors, persisted: false}, null, 2));
  await page.getByRole('button', {name: /liikenneloki|traffic log/i}).press('Enter');
  const downloadReady = page.waitForEvent('download', {timeout: 15000});
  await page.getByRole('button', {name: 'Export turn timings', exact: true}).press('Enter');
  await (await downloadReady).saveAs(`${out}/export.json`);
  const downloaded = JSON.parse(await readFile(`${out}/export.json`, 'utf8'));
  await page.reload();
  await page.waitForSelector('main');
  const restored = await page.evaluate(async () => (await import('/test-fixtures/browser/liveReplaySetup.ts')).evidence());
  const completedId = timing.reports.find(report => report.events.some(event => event.name === 'playback.drained'))?.turnId;
  const persisted = !!completedId && [restored, downloaded].every(value => value.reports.some(report => report.turnId === completedId));
  await writeFile(`${out}/evidence.json`, JSON.stringify({ setup, previewText, initialMarks, progress, persisted, timing, errors }, null, 2));
  if (previewText.trim() || initialMarks < 3 || errors.length || !persisted || !timing.finalWordPresent || progress.length < 2 || timing.historyMessageCount < 240 || !timing.reports.some(report => report.events.some(event => event.name === 'playback.drained'))) throw new Error('UI replay evidence failed');
  console.log(JSON.stringify({ phase: 'passed', reports: timing.reports.length, evidence: `${out}/evidence.json` }));
} catch (error) {
  if (page) {
    await page.screenshot({path: `${out}/failure.png`}).catch(() => {});
    await writeFile(`${out}/failure.txt`, await page.locator('body').innerText().catch(() => 'Page unavailable'));
  }
  throw error;
} finally { await browser.close(); }
