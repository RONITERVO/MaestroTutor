// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useEffect, useState } from 'react';
import { LibraryBookClient, type LibraryState } from './libraryBookBridge';

export function MotionLibraryManagement({ client, state, waiting }: { client: LibraryBookClient; state: LibraryState; waiting: boolean }) {
  const [confirm, setConfirm] = useState<'download' | 'forget' | null>(null);
  const selected = state.selected;
  useEffect(() => setConfirm(null), [selected?.id, selected?.archived, selected?.removed, state.session, state.canRemoveDownload, state.canForgetMotion]);
  if (!selected || !state.usage || selected.archived === undefined) return null;
  const usage = state.usage, disabled = waiting || state.readOnly;
  const page = (usagePage: number) => client.request('select', { motionId: selected.id, usagePage, sourceIndex: state.sourceIndex, termsPage: state.termsPage });
  return <details className="quest-library-source quest-library-management"><summary>Usage and local storage</summary>
    <p>{selected.removed ? 'Download removed. Its identity, name, tags and source terms are retained.' : selected.downloaded === false ? 'Local download is missing. Import the original export and use Save motions to repair it.' : `${((selected.bytes ?? 0) / 1024).toFixed(1)} KiB local motion download.${selected.archived ? ' Archived motions still work in existing assignments.' : ''}`}</p>
    <p>{usage.total ? `Assigned in ${usage.total} place${usage.total === 1 ? '' : 's'}:` : 'No current walking, action or tutor-state assignments.'}</p>
    <ul aria-label="Motion uses">{usage.uses.map((label, index) => <li key={index}>{label}</li>)}</ul>
    {usage.pages > 1 && <div className="quest-library-actions"><button disabled={waiting || usage.page === 0} onClick={() => page(usage.page - 1)}>Previous uses</button><span>{usage.page + 1}/{usage.pages}</span><button disabled={waiting || usage.page + 1 >= usage.pages} onClick={() => page(usage.page + 1)}>Next uses</button></div>}
    <p>To replace an assignment, select the replacement motion and use Walking, the selected action, or Tutor-state motions. Select an action on the physical rules tray; load another avatar to edit its tutor states.</p>
    {usage.history && <p>Protected by Undo or Redo history.</p>}
    {usage.saved && <p>Protected by a retained save or recovery backup.</p>}
    {usage.playing && <p>Playback or loading is using this download.</p>}
    {usage.protection && <p>{usage.protection}</p>}
    <div className="quest-library-actions">
      <button disabled={disabled || selected.removed} onClick={() => client.request(selected.archived ? 'restore' : 'archive', { motionId: selected.id })}>{selected.archived ? 'Restore to library' : 'Archive motion'}</button>
      {selected.archived && <button disabled={disabled || !state.canRemoveDownload} onClick={() => setConfirm('download')}>Remove local download</button>}
      {selected.removed && <button disabled={disabled || !state.canForgetMotion} onClick={() => setConfirm('forget')}>Forget motion details</button>}
    </div>
    {!selected.archived && <p>Archive hides this motion from the main list and keeps assignments working. Find it again with the Archived filter.</p>}
    {confirm === 'download' && <div role="group" aria-label="Confirm download removal"><p>Remove only the local download of “{selected.name}”? Keep your original export: importing it again restores this motion under the same identity. This does not delete your source file.</p>
      <div className="quest-library-actions"><button disabled={disabled || !state.canRemoveDownload} onClick={() => { if (client.request('removeDownload', { motionId: selected.id })) setConfirm(null); }}>Confirm remove download</button><button onClick={() => setConfirm(null)}>Keep download</button></div>
    </div>}
    {confirm === 'forget' && <div role="group" aria-label="Confirm forgetting motion"><p>Forget the name, tags, source records and saved identity for “{selected.name}”? A later import creates a new motion. Your original source file is unchanged.</p>
      <div className="quest-library-actions"><button disabled={disabled || !state.canForgetMotion} onClick={() => { if (client.request('forgetMotion', { motionId: selected.id })) setConfirm(null); }}>Confirm forget details</button><button onClick={() => setConfirm(null)}>Keep details</button></div>
    </div>}
    {selected.removed && <p>To restore: use Import on the physical tray, select the original GLB/VRM export, then Save motions; or use Animation batches with the original ZIP. A changed animation is added separately so existing references are never redirected silently.</p>}
  </details>;
}
