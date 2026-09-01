// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { spawn } from 'node:child_process';
import { access, mkdir } from 'node:fs/promises';
import { join } from 'node:path';
import { chromium, type BrowserContext, type Frame, type Locator, type Page } from 'playwright-core';

const DEFAULT_TIMEOUT_MS = 120_000;
const ALLOWED_STRIPE_HOST = 'checkout.stripe.com';

export interface StripeTestCheckoutInput {
  checkoutUrl: string;
  sessionId: string;
  profileDirectory: string;
  email: string;
  cardholderName?: string;
  countryCode?: string;
  headless?: boolean;
  timeoutMs?: number;
  executablePath?: string;
}

const firstExistingPath = async (paths: string[]): Promise<string | undefined> => {
  for (const path of paths) {
    try {
      await access(path);
      return path;
    } catch {
      // Try the next platform-default browser path.
    }
  }
  return undefined;
};

const resolveBrowserExecutable = async (override?: string): Promise<string | undefined> => {
  if (override?.trim()) return override.trim();
  if (process.env.MAESTRO_BROWSER_EXECUTABLE?.trim()) return process.env.MAESTRO_BROWSER_EXECUTABLE.trim();
  if (process.platform === 'win32') {
    return firstExistingPath([
      'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
      'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
      'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
      'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    ]);
  }
  return undefined;
};

const runNativeGoogleHandoff = async (
  executablePath: string,
  profileDirectory: string,
  appUrl: URL,
  timeoutMs: number,
): Promise<void> => {
  const userDataDirectory = join(profileDirectory, 'hosted-browser');
  await mkdir(userDataDirectory, { recursive: true });
  process.stderr.write(
    'Complete Google sign-in in the normal browser window, then close that window so Maestro can verify the saved session.\n',
  );
  await new Promise<void>((resolve, reject) => {
    const child = spawn(executablePath, [
      `--user-data-dir=${userDataDirectory}`,
      '--new-window',
      '--no-first-run',
      '--no-default-browser-check',
      '--disable-background-mode',
      appUrl.href,
    ], { stdio: 'ignore' });
    const timer = setTimeout(() => {
      child.kill();
      reject(new Error('Timed out waiting for the maintainer to close the Google sign-in browser window.'));
    }, timeoutMs);
    child.once('error', error => {
      clearTimeout(timer);
      reject(error);
    });
    child.once('exit', () => {
      clearTimeout(timer);
      resolve();
    });
  });
};

const visibleLocator = async (page: Page, matcher: RegExp): Promise<Locator | null> => {
  const frames: Frame[] = page.frames();
  for (const frame of frames) {
    for (const locator of [
      frame.getByLabel(matcher).first(),
      frame.getByPlaceholder(matcher).first(),
    ]) {
      if (await locator.isVisible().catch(() => false)) return locator;
    }
  }
  return null;
};

const fillIfPresent = async (page: Page, matcher: RegExp, value: string): Promise<boolean> => {
  const locator = await visibleLocator(page, matcher);
  if (!locator) return false;
  await locator.fill(value);
  return true;
};

const waitForCheckoutForm = async (page: Page, timeoutMs: number): Promise<void> => {
  const deadline = Date.now() + timeoutMs;
  const cardPaymentMethod = page.getByRole('radio', { name: /^card$/i }).first();
  const cardPaymentButton = page.getByRole('button', { name: /pay with card/i }).first();
  while (Date.now() < deadline) {
    if (await visibleLocator(page, /card number/i)) return;
    if (await cardPaymentMethod.count()
      && !await cardPaymentMethod.isChecked().catch(() => false)) {
      await cardPaymentMethod.check({ force: true }).catch(() => undefined);
    } else if (await cardPaymentButton.count()) {
      await cardPaymentButton.click({ force: true }).catch(() => undefined);
    }
    await page.waitForTimeout(250);
  }
  const bodyText = await page.locator('body').innerText().catch(() => '');
  const safeExcerpt = bodyText
    .replace(/[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}/g, '[email]')
    .replace(/cs_(?:test|live)_[A-Za-z0-9]+/g, '[checkout-session]')
    .replace(/\s+/g, ' ')
    .trim()
    .slice(0, 360);
  throw new Error(
    `Stripe Checkout did not expose a card-number field before the timeout. `
    + `frames=${page.frames().length}; radioCount=${await cardPaymentMethod.count()}; `
    + `buttonCount=${await cardPaymentButton.count()}; page="${safeExcerpt}"`,
  );
};

export const launchHostedBrowserContext = async (
  profileDirectory: string,
  input: Pick<StripeTestCheckoutInput, 'headless' | 'executablePath'>,
): Promise<BrowserContext> => {
  const executablePath = await resolveBrowserExecutable(input.executablePath);
  return chromium.launchPersistentContext(join(profileDirectory, 'hosted-browser'), {
    ...(executablePath ? { executablePath } : { channel: 'chrome' }),
    headless: input.headless ?? true,
    locale: 'en-US',
    viewport: { width: 1280, height: 900 },
  });
};

export const completeStripeTestCheckout = async (input: StripeTestCheckoutInput) => {
  const checkoutUrl = new URL(input.checkoutUrl);
  if (checkoutUrl.protocol !== 'https:' || checkoutUrl.hostname !== ALLOWED_STRIPE_HOST) {
    throw new Error('Refusing to automate a checkout outside checkout.stripe.com.');
  }
  if (!input.sessionId.startsWith('cs_test_') || !checkoutUrl.href.includes(input.sessionId)) {
    throw new Error('Refusing to enter test payment data unless the Checkout session is provably in test mode.');
  }
  if (!input.email.trim()) throw new Error('A disposable staging email is required for Stripe Checkout.');

  const timeoutMs = Math.max(10_000, input.timeoutMs ?? DEFAULT_TIMEOUT_MS);
  const context = await launchHostedBrowserContext(input.profileDirectory, input);
  const page = context.pages()[0] || await context.newPage();
  page.setDefaultTimeout(Math.min(timeoutMs, 30_000));
  try {
    await page.goto(checkoutUrl.href, { waitUntil: 'domcontentloaded', timeout: timeoutMs });
    await waitForCheckoutForm(page, timeoutMs);
    await fillIfPresent(page, /^email|email address$/i, input.email.trim());
    const cardFilled = await fillIfPresent(page, /card number/i, '4242424242424242');
    const expiryFilled = await fillIfPresent(page, /expiration|expiry|mm\s*\/\s*yy/i, '1234');
    const cvcFilled = await fillIfPresent(page, /cvc|security code/i, '123');
    if (!cardFilled || !expiryFilled || !cvcFilled) {
      throw new Error('Stripe Checkout payment fields did not match the supported test-card form.');
    }
    await fillIfPresent(page, /name on card|cardholder name/i, input.cardholderName || 'Maestro Headless CI');
    const country = await visibleLocator(page, /country or region|country/i);
    if (country) await country.selectOption(input.countryCode || 'FI').catch(() => undefined);
    const agentDisclosure = page.getByRole('checkbox', {
      name: /AI agent acting on behalf of someone else/i,
    }).first();
    if (await agentDisclosure.count()
      && !await agentDisclosure.isChecked().catch(() => false)) {
      await agentDisclosure.evaluate((element: HTMLInputElement) => element.click());
      if (!await agentDisclosure.isChecked().catch(() => false)) {
        throw new Error('Stripe Checkout did not accept the required AI-agent disclosure.');
      }
    }

    const payButton = page.getByRole('button', { name: /^(?:pay|submit|complete)$/i }).last();
    if (!await payButton.isVisible().catch(() => false)) {
      throw new Error('Stripe Checkout did not expose a visible payment button.');
    }
    await Promise.all([
      page.waitForURL(url => !url.hostname.endsWith('stripe.com'), { timeout: timeoutMs, waitUntil: 'domcontentloaded' }),
      payButton.click(),
    ]);
    const returnUrl = page.url();
    if (new URL(returnUrl).searchParams.get('billing') !== 'success') {
      throw new Error(`Stripe Checkout returned without the billing success marker (${new URL(returnUrl).origin}).`);
    }
    return { sessionId: input.sessionId, returnOrigin: new URL(returnUrl).origin, completed: true as const };
  } finally {
    await context.close();
  }
};

export interface HostedGoogleSignInInput {
  appUrl?: string;
  profileDirectory: string;
  headless?: boolean;
  timeoutMs?: number;
  executablePath?: string;
}

export const verifyHostedGoogleSignIn = async (input: HostedGoogleSignInInput) => {
  const appUrl = new URL(input.appUrl || process.env.MAESTRO_HOSTED_APP_URL || 'https://chatwithmaestro-staging.web.app');
  const local = appUrl.hostname === 'localhost' || appUrl.hostname === '127.0.0.1';
  if (appUrl.protocol !== 'https:' && !(local && appUrl.protocol === 'http:')) {
    throw new Error('Hosted Google verification requires HTTPS or a local development origin.');
  }
  if (!local && appUrl.hostname !== 'chatwithmaestro-staging.web.app') {
    throw new Error('Refusing to run the Google hosted journey outside the dedicated staging app.');
  }
  const timeoutMs = Math.max(10_000, input.timeoutMs ?? 240_000);
  if (input.headless !== true) {
    const executablePath = await resolveBrowserExecutable(input.executablePath);
    if (!executablePath) {
      throw new Error('A system Chrome or Edge executable is required for the manual Google sign-in handoff.');
    }
    await runNativeGoogleHandoff(executablePath, input.profileDirectory, appUrl, timeoutMs);
  }

  // Google rejects automation-controlled login pages. Playwright only inspects
  // the session after a maintainer has completed the provider flow in a normal
  // browser, or reuses an already prepared profile in headless mode.
  const context = await launchHostedBrowserContext(input.profileDirectory, {
    ...input,
    headless: true,
  });
  const page = context.pages()[0] || await context.newPage();
  page.setDefaultTimeout(Math.min(timeoutMs, 30_000));
  try {
    await page.goto(appUrl.href, { waitUntil: 'domcontentloaded', timeout: timeoutMs });
    const signOut = page.getByRole('button', { name: /sign out/i }).first();
    if (await signOut.isVisible().catch(() => false)) {
      return {
        signedIn: true as const,
        reusedProviderSession: input.headless === true,
        appOrigin: appUrl.origin,
      };
    }
    throw new Error(
      input.headless === true
        ? 'The named browser profile has no reusable Google session. Run the headed handoff first.'
        : 'Google sign-in did not persist in the named browser profile before the window was closed.',
    );
  } finally {
    await context.close();
  }
};
