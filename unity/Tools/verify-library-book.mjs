// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Run after Verify-Quest.ps1 -RenderImports, with the local Vite fixture server running.
import { chromium } from 'playwright-core';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
const output = path.resolve('.quest-evidence/book-library');
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const errors = [];
try {
  const page = await browser.newPage({ viewport: { width: 1024, height: 768 }, deviceScaleFactor: 2 });
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('http://127.0.0.1:5178/test-fixtures/browser/library-book.html');
  await page.getByRole('heading', { name: 'Animations', exact: true }).waitFor();
  await page.screenshot({ path: path.join(output, 'library-spread.png') });
  const overflow = await page.locator('.quest-library-page').evaluateAll(pages => pages.some(page => page.scrollWidth > page.clientWidth + 1));
  if (overflow) throw new Error('A library page overflows horizontally');
  await page.getByLabel('Search names and tags').fill('walk');
  await page.getByRole('button', { name: 'Search', exact: true }).click();
  await page.getByText('1–1 of 1', { exact: true }).waitFor();
  await page.getByRole('button', { name: /Everyday walk/ }).click();
  await page.getByLabel('Name', { exact: true }).fill('My daily walk');
  await page.getByRole('button', { name: 'Save details', exact: true }).click();
  await page.getByRole('heading', { name: 'My daily walk', exact: true }).waitFor();
  if (await page.locator('.quest-library-results button').count() !== 1) throw new Error('Filtered list changed after editing');
  await page.screenshot({ path: path.join(output, 'library-edited.png') });
  await page.getByRole('button', { name: 'Back to chat', exact: true }).click();
  await page.locator('.quest-library-spread').waitFor({ state: 'detached' });
  if (!await page.locator('.quest-chat-page').isVisible()) throw new Error('Chat did not return');
  await page.frameLocator('iframe').getByRole('button', { name: 'Try the phrase', exact: true }).waitFor({ timeout: 10000 });
  await page.frameLocator('iframe').getByRole('button', { name: 'Try the phrase', exact: true }).click();
  await page.frameLocator('iframe').getByText('Un café, por favor.', { exact: true }).waitFor();
  await page.screenshot({ path: path.join(output, 'returned-conversation.png') });
  if (errors.length) throw new Error(errors.join('\n'));
  await writeFile(path.join(output, 'visual-check.json'), JSON.stringify({ viewport: '1024x768', scale: 2, overflow: false, search: 'passed', edit: 'passed', returnToChat: 'passed', artifactAfterReturn: 'passed', pageErrors: errors, scope: 'Native synthetic state; simulated fixture acknowledgements. Native operations tested separately in Unity.' }, null, 2));
  console.log('Library visual checks passed: search, metadata edit, return to chat and page bounds. '+output);
} finally { await browser.close(); }
