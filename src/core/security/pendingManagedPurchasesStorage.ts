// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { Capacitor } from '@capacitor/core';
import { SecureStorage } from '@aparajita/capacitor-secure-storage';
import type { GooglePlayPurchaseRecord } from '../contracts/integrations';

const STORAGE_KEY = 'maestro.pendingManagedPurchases.v1';

interface PendingManagedPurchaseRecord {
  purchase: GooglePlayPurchaseRecord;
  consumed: boolean;
  updatedAt: number;
}

let cachedRecords: PendingManagedPurchaseRecord[] | undefined;

const isNative = Capacitor.isNativePlatform();

const safeWindow = () => (typeof window !== 'undefined' ? window : undefined);

const readLocalStorage = (): PendingManagedPurchaseRecord[] => {
  try {
    const raw = safeWindow()?.localStorage?.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed as PendingManagedPurchaseRecord[] : [];
  } catch {
    return [];
  }
};

const writeLocalStorage = (records: PendingManagedPurchaseRecord[]) => {
  try {
    safeWindow()?.localStorage?.setItem(STORAGE_KEY, JSON.stringify(records));
  } catch {
    // Ignore persistence failures.
  }
};

const removeLocalStorage = () => {
  try {
    safeWindow()?.localStorage?.removeItem(STORAGE_KEY);
  } catch {
    // Ignore persistence failures.
  }
};

const loadSecureStorage = async (): Promise<PendingManagedPurchaseRecord[]> => {
  try {
    const raw = await SecureStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed as PendingManagedPurchaseRecord[] : [];
  } catch {
    return [];
  }
};

const saveSecureStorage = async (records: PendingManagedPurchaseRecord[]): Promise<void> => {
  await SecureStorage.setItem(STORAGE_KEY, JSON.stringify(records));
};

const removeSecureStorage = async (): Promise<void> => {
  try {
    await SecureStorage.removeItem(STORAGE_KEY);
  } catch {
    // Ignore persistence failures.
  }
};

const persistRecords = async (records: PendingManagedPurchaseRecord[]): Promise<void> => {
  cachedRecords = records;
  if (records.length === 0) {
    if (isNative) {
      await removeSecureStorage();
    } else {
      removeLocalStorage();
    }
    return;
  }

  if (isNative) {
    await saveSecureStorage(records);
  } else {
    writeLocalStorage(records);
  }
};

const loadRecords = async (): Promise<PendingManagedPurchaseRecord[]> => {
  if (cachedRecords !== undefined) return cachedRecords;
  cachedRecords = isNative ? await loadSecureStorage() : readLocalStorage();
  return cachedRecords;
};

const upsertPendingManagedPurchase = async (
  purchase: GooglePlayPurchaseRecord,
  options?: { consumed?: boolean }
): Promise<PendingManagedPurchaseRecord> => {
  const records = await loadRecords();
  const nextRecord: PendingManagedPurchaseRecord = {
    purchase,
    consumed: Boolean(options?.consumed),
    updatedAt: Date.now(),
  };

  const index = records.findIndex(record => record.purchase.purchaseToken === purchase.purchaseToken);
  const nextRecords = [...records];
  if (index >= 0) {
    nextRecords[index] = {
      ...records[index],
      purchase,
      consumed: records[index].consumed || nextRecord.consumed,
      updatedAt: Date.now(),
    };
  } else {
    nextRecords.push(nextRecord);
  }

  await persistRecords(nextRecords);
  return nextRecords[index >= 0 ? index : nextRecords.length - 1];
};

const markPendingManagedPurchaseConsumed = async (purchaseToken: string): Promise<void> => {
  const records = await loadRecords();
  const nextRecords = records.map(record => (
    record.purchase.purchaseToken === purchaseToken
      ? { ...record, consumed: true, updatedAt: Date.now() }
      : record
  ));
  await persistRecords(nextRecords);
};

const removePendingManagedPurchase = async (purchaseToken: string): Promise<void> => {
  const records = await loadRecords();
  const nextRecords = records.filter(record => record.purchase.purchaseToken !== purchaseToken);
  await persistRecords(nextRecords);
};

const clearPendingManagedPurchases = async (): Promise<void> => {
  await persistRecords([]);
};

export {
  clearPendingManagedPurchases,
  loadRecords as loadPendingManagedPurchases,
  markPendingManagedPurchaseConsumed,
  removePendingManagedPurchase,
  upsertPendingManagedPurchase,
};
