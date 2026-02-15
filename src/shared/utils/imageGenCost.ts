// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

const STORAGE_KEY_COUNT = 'maestro_imageGenCount';
const STORAGE_KEY_WARNING_SHOWN = 'maestro_imageGenCostWarningShown';

export const IMAGE_GEN_COST_PER_IMAGE = 0.05;
export const GOOGLE_BILLING_URL = 'https://console.cloud.google.com/billing';

export const getImageGenCount = (): number => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY_COUNT);
    return raw ? parseInt(raw, 10) || 0 : 0;
  } catch {
    return 0;
  }
};

export const incrementImageGenCount = (): number => {
  const next = getImageGenCount() + 1;
  try {
    localStorage.setItem(STORAGE_KEY_COUNT, String(next));
  } catch { /* ignore */ }
  return next;
};

export const hasShownImageGenCostWarning = (): boolean => {
  try {
    return localStorage.getItem(STORAGE_KEY_WARNING_SHOWN) === '1';
  } catch {
    return false;
  }
};

export const setImageGenCostWarningShown = (): void => {
  try {
    localStorage.setItem(STORAGE_KEY_WARNING_SHOWN, '1');
  } catch { /* ignore */ }
};
