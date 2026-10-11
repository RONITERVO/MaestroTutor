// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Run against a local Vite server. Synthetic fixture, no provider/network login.
import { chromium } from 'playwright-core';
import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
const base = process.env.MAESTRO_ACCOUNT_UI_URL || 'http://127.0.0.1:5182';
if (!['localhost', '127.0.0.1'].includes(new URL(base).hostname)) throw new Error('Local fixture required');
const out = '.quest-evidence/quest-account-ui'; await mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const errors = [], requests = [];
try {
  const context = await browser.newContext({ locale: 'en-US', viewport: { width: 390, height: 844 } });
  await context.route('**/*', route => {
    const url = new URL(route.request().url());
    if (['http:', 'https:'].includes(url.protocol) && url.origin !== new URL(base).origin) { requests.push(url.origin); return route.abort(); }
    return route.continue();
  });
  const page = await context.newPage(); page.setDefaultTimeout(60000); page.on('pageerror', error => errors.push(error.message));
  const fixture = base + '/test-fixtures/browser/quest-account.html';
  await page.goto(fixture, { waitUntil: 'domcontentloaded' }); await page.getByText('learner@example.test', { exact: true }).waitFor();
  const submit = page.getByRole('button', { name: 'Link my Quest book' }); assert(await submit.isDisabled());
  await page.getByLabel('Code from your book').fill('ABCDE-FGHJK'); assert(await submit.isDisabled());
  await page.getByRole('checkbox').check(); assert(await submit.isEnabled());
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
  await page.screenshot({ path: out + '/approval-phone.png', fullPage: true });
  await submit.click(); await page.getByRole('heading', { name: 'Your book is approved' }).waitFor();
  await page.screenshot({ path: out + '/approval-success.png', fullPage: true });
  await page.goto(fixture + '?signedout'); await page.getByRole('button', { name: 'Sign in with Google' }).waitFor();
  await page.getByRole('button', { name: 'Sign in with Google' }).click(); await page.getByText('learner@example.test', { exact: true }).waitFor();
  await page.goto(fixture + '?book'); await page.getByText('ABCDE-FGHJK', { exact: true }).waitFor();
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
  assert(await page.getByText(/Stripe Checkout/).count() === 0);
  await page.screenshot({ path: out + '/book-pairing-phone.png', fullPage: true });
  await page.setViewportSize({ width: 1024, height: 1536 });
  await page.screenshot({ path: out + '/book-pairing-page.png', fullPage: true });
  await page.getByRole('button', { name: 'Cancel sign-in', exact: true }).click();
  assert(await page.getByText('ABCDE-FGHJK', { exact: true }).count() === 0);
  await page.goto(base + '/quest-link.html'); await page.getByText('Account linking is not configured on this page yet.').waitFor();
  assert.deepEqual(errors, []); assert.deepEqual(requests, []);
  await writeFile(out + '/browser-ui.json', JSON.stringify({ passed: true, errors, externalRequests: requests, viewports: ['390x844', '1024x1536'], syntheticIdentity: true, realProviderVerified: false }, null, 2));
  console.log('Quest approval and book dialog: browser UI checks passed. No external requests.');
} finally { await browser.close(); }
