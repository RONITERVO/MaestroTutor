// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {openDB} from '../../../core/db';

type ResetGuard = {
  unchanged: () => boolean;
  subscribe: (changed: () => void) => () => void;
};

/** Clear every store in one transaction, and settle only on its terminal event.
 * Unlike deleteDatabase, this cannot leave a delayed deletion waiting for a tab. */
export async function resetLocalData(guard?: ResetGuard): Promise<void> {
  const db = await openDB();
  try {
    await new Promise<void>((resolve, reject) => {
      const names = Array.from(db.objectStoreNames);
      const tx = db.transaction(names, 'readwrite');
      let failure: unknown;
      let failed = false;
      let unsubscribe: (() => void) | undefined;
      const cleanup = () => { try { unsubscribe?.(); } catch { /* Already terminal. */ } };
      const abort = (reason: unknown) => {
        if (!failed) failure = reason;
        failed = true;
        // An abort may arrive after commit started. Its terminal event, rather
        // than this exception, determines whether the data was cleared.
        try { tx.abort(); } catch { /* Wait for complete/abort. */ }
      };
      const check = () => {
        try {
          if (guard && !guard.unchanged()) {
            abort(new Error('Your conversation or settings changed. Nothing was reset.'));
          }
        } catch (error) { abort(error); }
      };
      tx.oncomplete = () => {
        cleanup();
        if (failed) reject(new Error('The reset finished before it could be stopped. Your backup was saved; reload the app before continuing.'));
        else resolve();
      };
      tx.onabort = () => {
        cleanup();
        reject(failure ?? new Error(tx.error?.message || 'Reset failed. Your data was not cleared.'));
      };
      try {
        unsubscribe = guard?.subscribe(check);
        check();
        if (failed) return;
        for (const name of names) tx.objectStore(name).clear();
      } catch (error) {
        // A synchronous clear/setup error must roll back previously queued clears.
        abort(error);
      }
    });
  } finally { db.close(); }
}
