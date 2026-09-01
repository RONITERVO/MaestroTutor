// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createCoreEventJournal } from '../../core-sdk/events';
import { createManagedAccountController } from '../../core-sdk/managedAccount';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { googleAuthService } from '../auth/googleAuthService';
import { maestroBackendService } from '../backend/maestroBackendService';

export const maestroCoreEventJournal = createCoreEventJournal();

export const maestroManagedAccountController = createManagedAccountController({
  backend: maestroBackendService,
  identity: {
    beginSignIn: () => googleAuthService.beginSignIn(),
    signOut: () => googleAuthService.signOutManagedSession(),
  },
  navigation: {
    navigate: async url => {
      if (Capacitor.isNativePlatform()) {
        await Browser.open({ url });
        return;
      }
      if (typeof window === 'undefined') {
        throw new Error('Hosted checkout navigation is unavailable outside a browser adapter.');
      }
      window.location.assign(url);
    },
  },
  runtime: {
    clock: {
      now: Date.now,
      sleep: milliseconds => new Promise(resolve => globalThis.setTimeout(resolve, milliseconds)),
      setInterval: (callback, milliseconds) => window.setInterval(callback, milliseconds),
      clearInterval: handle => window.clearInterval(handle as number),
    },
    ids: {
      create: prefix => `${prefix}-${globalThis.crypto.randomUUID()}`,
    },
    events: maestroCoreEventJournal,
  },
});
