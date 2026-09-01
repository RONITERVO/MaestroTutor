// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { Capacitor, registerPlugin } from '@capacitor/core';

export const isNativeAndroidBilling = (
  Capacitor.isNativePlatform() && Capacitor.getPlatform() === 'android'
);

// One registration for the one native plugin. Theme purchases and managed
// credit purchases expose different typed views over this same Capacitor
// bridge; registering each view separately produces duplicate listeners and a
// warning even on web.
const nativePlugin = isNativeAndroidBilling
  ? registerPlugin<Record<string, unknown>>('ThemeBilling')
  : null;

export const getThemeBillingNativePlugin = <T>(): T | null => nativePlugin as T | null;
