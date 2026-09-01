// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ManagedAccessPanel from './ManagedAccessPanel';

const mocks = vi.hoisted(() => ({
  addListener: vi.fn(),
  browserFinished: null as null | (() => void),
  startStripeCheckout: vi.fn(async () => ({ url: 'https://checkout.stripe.test', sessionId: 'cs_test_1' })),
  startStripeReturnPolling: vi.fn(),
}));

vi.mock('@capacitor/core', () => ({ Capacitor: { isNativePlatform: () => true } }));
vi.mock('@capacitor/browser', () => ({ Browser: { addListener: mocks.addListener } }));
vi.mock('../../../shared/hooks/useAppTranslations', () => ({
  useAppTranslations: () => ({ t: (key: string) => ({
    'managedAccess.buyCredits': 'Buy credits',
    'managedAccess.purchasing': 'Purchasing',
    'managedAccess.checkoutReturn': 'Checkout returned',
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
    signIn: vi.fn(),
    signOut: vi.fn(),
    deleteAccount: vi.fn(),
    startStripeCheckout: mocks.startStripeCheckout,
    startStripeReturnPolling: mocks.startStripeReturnPolling,
  },
}));
vi.mock('./ManagedAccountActivityModal', () => ({ default: () => null }));

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
