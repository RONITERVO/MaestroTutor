// Verify the built app entry, not a separate welcome-screen fixture.
// Run npm run build first. No account, AI or real headset is used.
import { chromium } from 'playwright-core';
import { preview } from 'vite';
import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
const out = '.quest-evidence/quest-privacy/audience';
await mkdir(out, { recursive: true });
const server = await preview({ preview: { host: '127.0.0.1', port: 5191, strictPort: true } });
let browser;
try {
  browser = await chromium.launch({ channel: 'chrome', headless: true });
  const context = await browser.newContext({ viewport: { width: 1024, height: 768 } });
  const external = [], errors = [];
  await context.route('**/*', route => {
    const url = new URL(route.request().url());
    if (url.hostname === '127.0.0.1') return route.continue();
    external.push({ host: url.hostname, method: route.request().method() });
    return route.abort();
  });
  const page = await context.newPage();
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('http://127.0.0.1:5191/?surface=quest-book');
  const open = page.getByRole('button', { name: 'Open my Maestro book' });
  await open.waitFor();
  assert.equal(await open.isDisabled(), true);
  const startup = await page.evaluate(() => ({ snapshot: window.maestroBook.snapshot(), state: window.maestroBook.lifecycleState(), splash: !!document.getElementById('splash-screen') }));
  assert.equal(startup.splash, false); assert.equal(startup.state.active, false);
  assert.equal(startup.snapshot.historyTotal, 0);
  await page.screenshot({ path: `${out}/book-1024x768.png` });
  const geometry = await page.locator('.quest-audience-page').evaluateAll(pages => pages.map(p => ({ width: p.clientWidth, scrollWidth: p.scrollWidth, height: p.clientHeight, scrollHeight: p.scrollHeight })));
  assert.equal(geometry.length, 2); assert.ok(geometry.every(p => p.width >= p.scrollWidth));
  const links = await page.getByRole('link').evaluateAll(links => links.map(link => link.href));
  assert.deepEqual(links, ['https://chatwithmaestro.com/privacy.html', 'https://ai.google.dev/gemini-api/terms']);
  await page.evaluate(() => window.maestroBook.lifecycle(true));
  await page.waitForFunction(() => window.maestroBook.lifecycleState().settled);
  assert.deepEqual(await page.evaluate(() => window.maestroBook.lifecycleState()), { suspended: true, settled: true, active: false });
  await page.evaluate(() => window.maestroBook.lifecycle(false));
  assert.equal(await page.evaluate(() => window.maestroBook.command({ version: 1, type: 'session.resume' })), false);
  assert.equal(await open.isDisabled(), true);
  // Separate pages can scroll at enlarged UI sizes without horizontal clipping.
  await page.setViewportSize({ width: 800, height: 600 });
  await page.screenshot({ path: `${out}/book-800x600.png` });
  assert.ok((await page.locator('.quest-audience-page').evaluateAll(pages => pages.map(p => p.scrollWidth <= p.clientWidth))).every(Boolean));
  await page.getByRole('button', { name: 'I am under 18' }).click();
  assert.match(await page.getByRole('status').innerText(), /No AI session has started/);
  assert.equal(await open.count(), 0);
  const beforeEntry = external.slice();
  assert.ok(beforeEntry.every(r => ['fonts.googleapis.com', 'fonts.gstatic.com'].includes(r.host) && r.method === 'GET'), JSON.stringify(beforeEntry));
  await page.reload(); await open.waitFor();
  assert.equal(await open.isDisabled(), true);
  await page.getByRole('checkbox').check();
  assert.equal(await page.locator('.quest-book-surface').count(), 0);
  await open.click();
  await page.waitForFunction(() => !document.querySelector('.quest-audience-spread') && !!window.maestroBook?.roomSnapshot().clientId);
  assert.equal(await page.locator('.quest-book-surface').count(), 1);
  await page.screenshot({ path: `${out}/accepted.png` });
  await page.reload(); await open.waitFor(); assert.equal(await open.isDisabled(), true);
  // The same production entry retains ordinary phone/web access.
  const phone = await context.newPage();
  await phone.goto('http://127.0.0.1:5191/');
  await phone.waitForFunction(() => document.getElementById('root').childElementCount > 0);
  assert.equal(await phone.locator('.quest-audience-spread').count(), 0);
  assert.equal(errors.length, 0, JSON.stringify(errors));
  await writeFile(`${out}/evidence.json`, JSON.stringify({ passed: true, startup, geometry, links, beforeEntry, errors, realHeadset: false }, null, 2));
  console.log('Quest audience production-entry checks passed. Evidence: ' + out);
} finally {
  await browser?.close();
  await new Promise(resolve => server.httpServer.close(resolve));
}
