// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useEffect, useState } from 'react';
import { LibraryBookClient, type ActivityProfile, type LibraryEntry } from './libraryBookBridge';
const sortedOptions = (values: number[], current: number) => [...new Set([...values, current])].sort((a, b) => a - b);
const names = ['Idle', 'Listening', 'Thinking', 'Speaking'];
export function ActivityMotionEditor({ client, profile, selected, waiting }: { client: LibraryBookClient; profile: ActivityProfile; selected: LibraryEntry | null; waiting: boolean }) {
  const [role, setRole] = useState(0);
  const choices = profile.roles[role].choices, choice = choices.find(item => item.motionId === selected?.id);
  const key = `${profile.modelHash}:${role}:${selected?.id ?? ''}`;
  const initial = { key, revision: profile.revision, dirty: false, speed: choice?.speed ?? 1, weight: choice?.weight ?? 1, cooldown: choice?.cooldown ?? 0, loop: choice?.loop ?? false };
  const [draft, setDraft] = useState(initial);
  useEffect(() => { setDraft(previous => previous.key !== key || !previous.dirty ? { key, revision: profile.revision, dirty: false, speed: choice?.speed ?? 1, weight: choice?.weight ?? 1, cooldown: choice?.cooldown ?? 0, loop: choice?.loop ?? false } : previous); }, [key, profile.revision, choice?.speed, choice?.weight, choice?.cooldown, choice?.loop]);
  const { speed, weight, cooldown, loop } = draft;
  const stale = draft.key !== key || draft.revision !== profile.revision;
  const disabled = waiting || profile.readOnly || !profile.modelHash || !profile.revision || stale;
  const version = { modelHash: profile.modelHash, profileRevision: profile.revision };
  return <details className="quest-library-source quest-activity-editor"><summary>Tutor-state motions</summary>
    {stale && <p role="status">Assignments changed while you were editing. Your draft is kept. <button disabled={waiting} onClick={() => setDraft(initial)}>Reload current assignments</button></p>}
    <p>Saved for this avatar. Walking, poses, previews and explicit actions take priority.</p>
    <label>Tutor state<select value={role} onChange={event => setRole(Number(event.target.value))}>{names.map((name, index) => <option key={name} value={index}>{name}</option>)}</select></label>
    <ul aria-label="Assigned state motions">{choices.map(item => <li key={item.motionId}><button disabled={waiting || !item.available} onClick={() => client.request('select', { motionId: item.motionId })}>{item.name}</button><span>{item.available ? `${item.speed}× · weight ${item.weight} · ${item.cooldown}s gap${item.loop ? ' · loops' : ''}` : 'Unavailable for this avatar'}</span><button disabled={disabled} aria-label={`Remove ${item.name} from ${names[role]}`} onClick={() => client.request('roleRemove', { ...version, role, motionId: item.motionId })}>Remove</button></li>)}</ul>
    {!choices.length && <p>Uses the included {names[role].toLowerCase()} animation.</p>}
    <fieldset disabled={disabled}><legend>{choice ? 'Edit selected motion' : 'Add selected motion'}</legend>
      <label>Playback speed<select value={speed} onChange={event => setDraft({ ...draft, dirty: true, speed: Number(event.target.value) })}>{sortedOptions([.25, .5, .75, 1, 1.25, 1.5, 2], speed).map(value => <option key={value} value={value}>{value}×</option>)}</select></label>
      <label>Selection weight<select value={weight} onChange={event => setDraft({ ...draft, dirty: true, weight: Number(event.target.value) })}>{Array.from({ length: 10 }, (_, i) => i + 1).map(value => <option key={value} value={value}>{value}</option>)}</select></label>
      <label>Seconds before reusing<select value={cooldown} onChange={event => setDraft({ ...draft, dirty: true, cooldown: Number(event.target.value) })}>{sortedOptions([0, 2, 5, 10, 30, 60], cooldown).map(value => <option key={value} value={value}>{value}</option>)}</select></label>
      <label className="quest-library-check"><input type="checkbox" checked={loop} onChange={event => setDraft({ ...draft, dirty: true, loop: event.target.checked })} />Loop until the tutor state changes</label>
      <div className="quest-library-actions"><button disabled={!selected || !profile.canAssign || !choice && choices.length >= 4} onClick={() => { if (selected && client.request('roleAssign', { ...version, profileRevision: draft.revision, role, motionId: selected.id, speed, weight, cooldown, loop })) setDraft({ ...draft, dirty: false }); }}>{choice ? 'Update state motion' : 'Assign to tutor state'}</button>
      <button disabled={!choices.length} onClick={() => client.request('roleClear', { ...version, role })}>Use included animation</button></div>
    </fieldset>
    <p>Up to four choices per state. Completed motions can be chosen again after their gap; weights favour some choices, and immediate repeats are avoided when another is ready. Looping keeps one choice until the state changes.</p>
    <div className="quest-library-actions"><button disabled={disabled || !profile.canUndo} onClick={() => client.request('roleUndo', version)}>Undo assignments</button><button disabled={disabled || !profile.canRedo} onClick={() => client.request('roleRedo', version)}>Redo assignments</button></div>
    <p>{profile.modelHash ? profile.status : 'Load a compatible custom Maestro to assign saved state motions.'}</p><p>Return to chat to use automatic state motions.</p>
  </details>;
}
