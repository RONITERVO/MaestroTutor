// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useEffect, useState, useSyncExternalStore, type FormEvent } from 'react';
import { LibraryBookClient, type LibraryState } from './libraryBookBridge';

export function LibraryBookView({ client }: { client: LibraryBookClient }) {
  const { state, pending } = useSyncExternalStore(client.subscribe, client.getSnapshot);
  const [query, setQuery] = useState('');
  const [name, setName] = useState('');
  const [tags, setTags] = useState('');
  const [favourite, setFavourite] = useState(false);
  const [loop, setLoop] = useState(false);
  const [error, setError] = useState('');
  const selected = state?.selected;
  useEffect(() => { setQuery(state?.query ?? ''); }, [state?.query, state?.session]);
  useEffect(() => { setName(selected?.name ?? ''); setTags(selected?.tags.join(', ') ?? ''); setFavourite(selected?.favourite ?? false); setError(''); }, [selected?.id, selected?.name, selected?.tags.join(','), selected?.favourite]);
  if (!state?.visible) return null;
  const waiting = pending || state.busy;
  const filter = (changes: Partial<Pick<LibraryState, 'compatibleOnly' | 'favouritesOnly' | 'includeShort' | 'offset'>> = {}) => client.request('query', {
    query, compatibleOnly: state.compatibleOnly, favouritesOnly: state.favouritesOnly, includeShort: state.includeShort, offset: 0, ...changes,
  });
  const source = (sourceIndex: number, termsPage: number) => client.request('select', { motionId: selected!.id, sourceIndex, termsPage });
  const save = (event: FormEvent) => {
    event.preventDefault();
    const values = tags.split(',').map(tag => tag.trim()).filter(Boolean);
    if (!name.trim() || /[\x00-\x1f\x7f]/.test(name) || values.length > 16 || values.some(tag => tag.length > 32 || /[\x00-\x1f\x7f]/.test(tag))) {
      setError('Enter a name and up to 16 tags, each no longer than 32 characters.'); return;
    }
    setError(''); client.request('save', { motionId: selected!.id, name: name.trim(), tags: values, favourite });
  };
  return <div className="quest-library-spread" aria-label="Animation library">
    <section className="quest-library-page quest-library-list" aria-label="Find animations">
      <div className="quest-library-title"><h1>Animations</h1><button onClick={() => client.close()}>Back to chat</button></div>
      <form className="quest-library-search" onSubmit={event => { event.preventDefault(); filter(); }}>
        <label htmlFor="motion-search">Search names and tags</label>
        <div><input id="motion-search" value={query} maxLength={80} onChange={event => setQuery(event.target.value)} placeholder="Walk, greeting, dance…" /><button disabled={waiting}>Search</button></div>
      </form>
      <fieldset disabled={waiting} className="quest-library-filters"><legend className="sr-only">Filters</legend>
        <label><input type="checkbox" checked={state.compatibleOnly} onChange={event => filter({ compatibleOnly: event.target.checked })} />For this Maestro</label>
        <label><input type="checkbox" checked={state.favouritesOnly} onChange={event => filter({ favouritesOnly: event.target.checked })} />Favourites</label>
        <label><input type="checkbox" checked={state.includeShort} onChange={event => filter({ includeShort: event.target.checked })} />Short clips</label>
      </fieldset>
      <div className="quest-library-results" aria-label="Saved animations">
        {state.entries.map(motion => <button key={motion.id} disabled={waiting} aria-pressed={selected?.id === motion.id} onClick={() => client.request('select', { motionId: motion.id })}>
          <span>{motion.favourite ? '★ ' : ''}{motion.name}</span>
          <small>{motion.duration.toFixed(2)} s{motion.shortClip ? ' · Short export clip' : ''}{motion.compatible ? '' : ' · Different rig'}{motion.tags.length ? ` · ${motion.tags.join(', ')}` : ''}</small>
        </button>)}
        {!state.entries.length && <p className="quest-library-empty">No matching animations. Try another search or turn off a filter. Use Import, then Save motions on the physical tray to add a GLB or VRM export.</p>}
      </div>
      <div className="quest-library-paging"><button disabled={waiting || state.offset === 0} onClick={() => filter({ offset: Math.max(0, state.offset - state.pageSize) })}>Previous</button>
        <span>{state.total ? `${state.offset + 1}–${Math.min(state.offset + state.pageSize, state.total)} of ${state.total}` : '0 results'}</span>
        <button disabled={waiting || state.offset + state.pageSize >= state.total} onClick={() => filter({ offset: state.offset + state.pageSize })}>Next</button></div>
    </section>
    <section className="quest-library-page quest-library-detail" aria-label="Animation details">
      <div className="quest-library-detail-scroll">
        {selected ? <>
          <h2>{selected.name}</h2><p>{selected.duration.toFixed(2)} seconds · {selected.compatible ? 'Matches this Maestro' : 'Requires a matching rig'}</p>
          {selected.shortClip && <p>Very short export clip. It may be a setup pose or helper; preview before assigning it.</p>}
          <div className="quest-library-actions"><button disabled={waiting || !state.canPreview} onClick={() => client.request('preview', { motionId: selected.id, loop })}>Preview on Maestro</button><button onClick={() => client.request('stop')}>Stop motion</button></div>
          <label className="quest-library-check"><input type="checkbox" checked={loop} onChange={event => setLoop(event.target.checked)} />Loop preview</label>
          <div className="quest-library-actions"><button disabled={waiting || !state.canWalk} onClick={() => client.request('walk', { motionId: selected.id })}>Use for walking</button>
            <button disabled={waiting || !state.canAssign || !state.ruleId} onClick={() => client.request('rule', { motionId: selected.id, ruleId: state.ruleId!, stepIndex: state.stepIndex })}>Use in selected action</button></div>
          <p className="quest-library-hint">{state.ruleName ? `${state.ruleName}, step ${state.stepIndex + 1}. Its target and triggers stay the same.` : 'Create or select an action on the physical rules tray to assign this motion.'} Walking stays within room navigation.</p>
          <form onSubmit={save} className="quest-library-edit"><fieldset disabled={waiting || state.readOnly}><legend>Library details</legend>
            <label>Name<input value={name} maxLength={100} onChange={event => setName(event.target.value)} /></label>
            <label>Tags, separated by commas<input value={tags} maxLength={542} onChange={event => setTags(event.target.value)} placeholder="Greeting, talking, calm" /></label>
            <label className="quest-library-check"><input type="checkbox" checked={favourite} onChange={event => setFavourite(event.target.checked)} />Favourite</label>
            <button>Save details</button>
          </fieldset>{error && <p role="alert">{error}</p>}</form>
          <details className="quest-library-source"><summary>Source and use terms ({state.sourceCount})</summary><p>{state.sourceName}</p><pre>{state.attribution || 'No source terms supplied. Review your original export.'}</pre>
            <div className="quest-library-actions"><button disabled={waiting || state.termsPage === 0} onClick={() => source(state.sourceIndex, state.termsPage - 1)}>Earlier terms</button><span>{state.termsPage + 1}/{state.termsPages}</span><button disabled={waiting || state.termsPage + 1 >= state.termsPages} onClick={() => source(state.sourceIndex, state.termsPage + 1)}>More terms</button></div>
            {state.sourceCount > 1 && <button disabled={waiting} onClick={() => source((state.sourceIndex + 1) % state.sourceCount, 0)}>Next source ({state.sourceIndex + 1}/{state.sourceCount})</button>}
          </details>
        </> : <div className="quest-library-empty"><h2>Your motion library</h2><p>Choose an animation from the other page to preview it, organise it, or assign it.</p><p>Models and motions stay on this headset. Adding motions does not start playback.</p></div>}
      </div>
      <p className="quest-library-status" role="status">{waiting ? 'Working… ' : ''}{state.status}</p>
    </section>
  </div>;
}
