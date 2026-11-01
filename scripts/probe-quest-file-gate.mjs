// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Run with a local Vite server; no account, API key or user browser profile is used.
import assert from 'node:assert/strict';
import { chromium } from 'playwright-core';
const base = process.env.MAESTRO_TEST_URL || 'http://127.0.0.1:5178';
const browser = await chromium.launch({ ...(process.env.MAESTRO_CHROME ? { executablePath: process.env.MAESTRO_CHROME } : { channel: 'chrome' }), headless: true });
try {
  const page = await browser.newPage();
  await page.route('**/__file-gate-probe', route => route.fulfill({ contentType: 'text/html', body: '<button id="choose">Choose</button><input id="file" type="file" hidden><iframe sandbox="allow-scripts" srcdoc="<input id=file type=file>"></iframe>' }));
  await page.goto(new URL('/__file-gate-probe',base).href);
  await page.evaluate(async () => {
    const { createFileSelectionGate } = await import('/src/platform/quest/fileSelectionGate.ts');
    window.probeGate = createFileSelectionGate(window);
    document.querySelector('#choose').onclick = () => document.querySelector('#file').click();
  });
  let chooser = await Promise.all([page.waitForEvent('filechooser'), page.locator('#choose').click()]).then(value => value[0]);
  assert.equal(await page.evaluate(() => window.probeGate.take()), true);
  await chooser.setFiles([]);
  await page.evaluate(() => document.querySelector('#file').click());
  assert.equal(await page.evaluate(() => window.probeGate.take()), false);
  chooser = await Promise.all([page.waitForEvent('filechooser'), page.frameLocator('iframe').locator('#file').click()]).then(value => value[0]);
  assert.equal(await page.evaluate(() => window.probeGate.take()), false);
  await chooser.setFiles([]);
  chooser = await Promise.all([page.waitForEvent('filechooser'), page.locator('#choose').click()]).then(value => value[0]);
  assert.equal(await page.evaluate(() => window.probeGate.take()), true);
  await chooser.setFiles([]);
  console.log('Chrome file gate passed: real top-level click accepted, reused synthetic click rejected, sandboxed frame rejected, next real click accepted.');
} finally { await browser.close(); }
