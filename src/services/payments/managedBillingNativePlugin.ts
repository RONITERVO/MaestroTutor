// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { Capacitor, registerPlugin } from '@capacitor/core';

export const isNativeManagedBilling = (
  Capacitor.isNativePlatform() && Capacitor.getPlatform() === 'android'
);

// The native bridge only transports Play catalogue and purchase records. The
// backend remains the authority for product validity, grants and consumption.
const nativePlugin = isNativeManagedBilling
  ? registerPlugin<Record<string, unknown>>('ManagedBilling')
  : null;

export const getManagedBillingNativePlugin = <T>(): T | null => nativePlugin as T | null;
