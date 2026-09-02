// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom

import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import ManagedAccessPanel from './ManagedAccessPanel';
import { ServiceHttpError } from '../../../services/shared/serviceErrors';

const mocks = vi.hoisted(() => ({
  addListener: vi.fn(),
  browserFinished: null as null | (() => void),
  startStripeCheckout: vi.fn(async () => ({ url: 'https://checkout.stripe.test', sessionId: 'cs_test_1' })),
  startStripeReturnPolling: vi.fn(),
  signIn: vi.fn(async () => undefined),
}));

vi.mock('@capacitor/core', () => ({ Capacitor: { isNativePlatform: () => true } }));
vi.mock('@capacitor/browser', () => ({ Browser: { addListener: mocks.addListener } }));
vi.mock('../../../shared/hooks/useAppTranslations', () => ({
  useAppTranslations: () => ({ t: (key: string) => ({
    'managedAccess.buyCredits': 'Buy credits',
    'managedAccess.purchasing': 'Purchasing',
    'managedAccess.checkoutReturn': 'Checkout returned',
    'managedAccess.accountAction': 'Managed account',
    'managedAccess.detailsAction': 'Managed access details',
    'managedAccess.signIn': 'Sign in with Google',
    'managedAccess.description': 'Sign in to buy and use Maestro credits.',
    'managedAccess.appCheckFailed': 'Google could not verify this app on this device.',
    'managedAccess.refresh': 'Refresh balance',
  } as Record<string, string>)[key] || key }),
}));
vi.mock('../../../services/payments/maestroPaymentsService', () => ({
  maestroPaymentsService: {
    getManagedCreditPackIds: () => ['pack_1000'],
    isAndroidExternalCheckoutEnabled: () => true,
  },
}));
vi.mock('../../../services/backend/maestroBackendService', () => ({
  maestroBackendService: { isConfigured: () => true },
}));
vi.mock('../../../services/account/maestroManagedAccountController', () => ({
  maestroManagedAccountController: {
    refreshAccount: vi.fn(async () => undefined),
    signIn: mocks.signIn,
    signOut: vi.fn(),
    deleteAccount: vi.fn(),
    startStripeCheckout: mocks.startStripeCheckout,
    startStripeReturnPolling: mocks.startStripeReturnPolling,
  },
}));
vi.mock('./ManagedAccountActivityModal', () => ({ default: () => null }));

// Vitest runs without globals here, so Testing Library never registers its own
// auto-cleanup and one test's modal would still be mounted during the next.
afterEach(cleanup);

const session = {
  firebaseIdToken: 'token',
  user: { id: 'user-1', email: 'maintainer@example.test' },
  billingSummary: { availableCredits: 100, lifetimeSpentCredits: 0, lifetimeSpentUsd: 0 },
};

describe('ManagedAccessPanel native checkout listener', () => {
  beforeEach(() => {
    mocks.browserFinished = null;
    mocks.addListener.mockReset();
    mocks.startStripeCheckout.mockClear();
    mocks.startStripeReturnPolling.mockReset();
    mocks.addListener.mockImplementation(async (_event: string, callback: () => void) => {
      mocks.browserFinished = callback;
      return { remove: vi.fn(async () => undefined) };
    });
    mocks.startStripeReturnPolling.mockImplementation(() => ({
      cancel: vi.fn(),
      completion: Promise.resolve(null),
    }));
  });

  it('ignores unrelated browser closes and cancels the prior Stripe poll before a later checkout', async () => {
    render(<ManagedAccessPanel session={session as any} />);
    await waitFor(() => expect(mocks.browserFinished).toBeTypeOf('function'));

    act(() => mocks.browserFinished?.());
    expect(mocks.startStripeReturnPolling).not.toHaveBeenCalled();

    // Purchasing now lives behind the card's account button, which keeps the
    // Stripe listener mounted on the card while the details are closed.
    fireEvent.click(screen.getByRole('button', { name: 'Managed account' }));
    fireEvent.click(screen.getByRole('button', { name: 'Buy credits' }));
    await waitFor(() => expect(mocks.startStripeCheckout).toHaveBeenCalledTimes(1));
    act(() => mocks.browserFinished?.());
    expect(mocks.startStripeReturnPolling).toHaveBeenCalledTimes(1);
    const firstPoll = mocks.startStripeReturnPolling.mock.results[0].value;

    fireEvent.click(screen.getByRole('button', { name: 'Buy credits' }));
    await waitFor(() => expect(mocks.startStripeCheckout).toHaveBeenCalledTimes(2));
    act(() => mocks.browserFinished?.());
    expect(firstPoll.cancel).toHaveBeenCalledOnce();
    expect(mocks.startStripeReturnPolling).toHaveBeenCalledTimes(2);
  });

  it('removes a listener that resolves after the panel has unmounted', async () => {
    let resolveListener!: (listener: { remove: () => Promise<void> }) => void;
    const remove = vi.fn(async () => undefined);
    mocks.addListener.mockReturnValueOnce(new Promise(resolve => { resolveListener = resolve; }));

    const { unmount } = render(<ManagedAccessPanel session={session as any} />);
    unmount();
    await act(async () => resolveListener({ remove }));

    expect(remove).toHaveBeenCalledOnce();
  });
});

describe('ManagedAccessPanel card footprint', () => {
  beforeEach(() => {
    mocks.addListener.mockResolvedValue({ remove: vi.fn(async () => undefined) });
    mocks.signIn.mockReset();
  });

  it('keeps the signed-out card to two controls and no prose', () => {
    render(<ManagedAccessPanel session={null} />);

    expect(screen.getAllByRole('button').map(button => button.textContent || button.getAttribute('aria-label')))
      .toEqual(['Sign in with Google', 'Managed access details']);
    expect(screen.queryByText('Sign in to buy and use Maestro credits.')).toBeNull();
  });

  it('keeps the signed-in card to the account row and its two icon buttons', () => {
    render(<ManagedAccessPanel session={session as any} />);

    expect(screen.getAllByRole('button').map(button => button.getAttribute('aria-label')))
      .toEqual(['Refresh balance', 'Managed account']);
    // The balance is a preview on the button that opens the rest, not prose.
    expect(screen.getByRole('button', { name: 'Managed account' }).textContent).toBe('100');
  });

  it('explains an attestation failure instead of quoting the backend', async () => {
    mocks.signIn.mockRejectedValue(new ServiceHttpError(
      'Missing Firebase App Check token.',
      401,
      'app-check/unavailable',
    ));
    render(<ManagedAccessPanel session={null} />);

    fireEvent.click(screen.getByRole('button', { name: 'Sign in with Google' }));

    // The failure opens the one surface with room to explain it.
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('Google could not verify this app on this device.');
    expect(screen.queryByText('Missing Firebase App Check token.')).toBeNull();
  });
});
