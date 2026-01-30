// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { Capacitor } from '@capacitor/core';
import { Browser } from '@capacitor/browser';

export const openExternalUrl = async (url: string) => {
  if (typeof url !== 'string' || !url.trim()) return;
  const safeUrl = url.trim();

  let parsed: URL;
  try {
    parsed = new URL(safeUrl);
  } catch {
    return;
  }
  if (parsed.protocol !== 'https:' && parsed.protocol !== 'http:') {
    return;
  }

  if (Capacitor.isNativePlatform()) {
    try {
      await Browser.open({ url: safeUrl });
      return;
    } catch {
      // Fall back to window.open below
    }
  }

  if (typeof window !== 'undefined') {
    window.open(safeUrl, '_blank', 'noopener,noreferrer');
  }
};
