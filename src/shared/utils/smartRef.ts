// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { MutableRefObject } from 'react';

/**
 * Smart Ref utility for avoiding stale closure issues.
 * 
 * This is in a separate file to avoid circular dependencies with the store.
 */

let hasWarnedOnWrite = false;

/**
 * Creates a "smart ref" that reads directly from the Zustand store.
 * This eliminates stale closure issues without manual useEffect syncing.
 * 
 * @example
 * const settingsRef = useMemo(() => createSmartRef(
 *   useMaestroStore.getState,
 *   state => state.settings
 * ), []);
 * // settingsRef.current always returns the fresh value
 * 
 * @param getState - The store's getState function (e.g., useMaestroStore.getState)
 * @param selector - A function that selects state from the store
 * @returns A ref-like object with a getter that always returns fresh state
 */
export const createSmartRef = <TStore, T>(
  getState: () => TStore,
  selector: (state: TStore) => T
): MutableRefObject<T> => ({
  get current() {
    return selector(getState());
  },
  set current(_value: T) {
    if (!hasWarnedOnWrite && typeof process !== 'undefined' && process.env?.NODE_ENV !== 'production') {
      hasWarnedOnWrite = true;
      console.warn('[createSmartRef] Writes to .current are ignored. Use store actions to update state.');
    }
  },
});

/**
 * Creates a "smart ref" that reads from the store and writes via the provided setter.
 * Useful when hook logic wants ref-like semantics with proper store updates.
 */
export const createWritableSmartRef = <TStore, T>(
  getState: () => TStore,
  selector: (state: TStore) => T,
  setValue: (value: T) => void
): MutableRefObject<T> => ({
  get current() {
    return selector(getState());
  },
  set current(value: T) {
    setValue(value);
  },
});
