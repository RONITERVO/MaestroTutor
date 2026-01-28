// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * Smart Ref utility for avoiding stale closure issues.
 * 
 * This is in a separate file to avoid circular dependencies with the store.
 */

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
): React.MutableRefObject<T> => ({
  get current() {
    return selector(getState());
  },
  set current(_value: T) {
    // no-op: use store actions to update state
  },
});
