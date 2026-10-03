// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, fireEvent, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { LibraryBookClient, parseLibraryState, type LibraryState } from './libraryBookBridge';
import { LibraryBookView } from './LibraryBookView';
import { QuestBookSurface } from './QuestBookSurface';
const session = 'a'.repeat(32), motionId = 'b'.repeat(32);
export const state = (): LibraryState => ({
  version: 1, session, revision: 1, ack: 0, visible: true, busy: false, readOnly: false,
  query: '', offset: 0, total: 1, pageSize: 12, compatibleOnly: true, favouritesOnly: false, includeShort: false,
  entries: [{ id: motionId, name: 'Stage walk', tags: ['Walking'], duration: 2, favourite: false, compatible: true, shortClip: false }],
  selected: null, canPreview: false, canWalk: false, canAssign: false, ruleId: null, ruleName: null, stepIndex: 0,
  sourceIndex: 0, sourceCount: 0, sourceName: null, attribution: '', termsPage: 0, termsPages: 1, status: 'Choose a motion',
});
afterEach(cleanup);
describe('book library transport and UI', () => {
  it('bounds native state and rejects invalid IDs, duplicate rows and nonfinite durations', () => {
    expect(parseLibraryState(state())).not.toBeNull();
    expect(parseLibraryState({ ...state(), entries: [...state().entries, ...state().entries] })).toBeNull();
    expect(parseLibraryState({ ...state(), session: '../save' })).toBeNull();
    expect(parseLibraryState({ ...state(), attribution: 'x'.repeat(1501) })).toBeNull();
    expect(parseLibraryState({ ...state(), entries: [{ ...state().entries[0], duration: NaN }] })).toBeNull();
  });
  it('retains a request until ack, prioritises Stop and drops old-session requests on resume', () => {
    const client = new LibraryBookClient(); client.receive(state());
    expect(client.request('preview', { motionId })).toBe(true);
    const request = client.snapshot().libraryRequest; expect(client.snapshot().libraryRequest).toBe(request);
    expect(client.request('walk', { motionId })).toBe(false);
    client.receive({ ...state(), revision: 2, busy: true }); expect(client.snapshot().libraryRequest).toBe(request);
    client.request('stop'); expect(client.snapshot().libraryRequest?.sequence).toBe(2);
    client.receive({ ...state(), revision: 3, ack: 1 }); expect(client.snapshot().libraryRequest?.action).toBe('stop');
    client.receive({ ...state(), revision: 4, ack: 2 }); expect(client.snapshot().libraryRequest).toBeNull();
    client.request('walk', { motionId }); client.suspend(); expect(client.snapshot().libraryRequest).toBeNull(); expect(client.request('walk', { motionId })).toBe(false);
    client.receive({ ...state(), session: 'c'.repeat(32) }); expect(client.snapshot().libraryRequest).toBeNull();
    expect(client.request('walk', { motionId })).toBe(true); expect(client.snapshot().libraryRequest?.sequence).toBe(1);
    expect(client.receive({ ...state(), session: 'c'.repeat(32), revision: 1 })).toBe(false);
  });
  it('searches, edits metadata without autoplay and explicitly assigns walking and rule actions', () => {
    const client = new LibraryBookClient(); const initial = state(); client.receive(initial);
    const ui = render(<LibraryBookView client={client} />);
    fireEvent.change(ui.getByLabelText('Search names and tags'), { target: { value: 'walk' } }); fireEvent.click(ui.getByText('Search'));
    expect(client.snapshot().libraryRequest).toMatchObject({ action: 'query', query: 'walk', offset: 0 });
    act(() => { client.receive({ ...initial, revision: 2, ack: 1, query: 'walk' }); });
    fireEvent.click(ui.getByRole('button', { name: /Stage walk/ })); expect(client.snapshot().libraryRequest).toMatchObject({ action: 'select', motionId });
    const selected = { ...initial, selected: initial.entries[0], canPreview: true, canWalk: true, canAssign: true, ruleId: 'd'.repeat(32), ruleName: 'Action 1' };
    act(() => { client.receive({ ...selected, revision: 3, ack: 2 }); });
    fireEvent.change(ui.getByLabelText('Name'), { target: { value: 'My walk' } }); fireEvent.click(ui.getByText('Save details'));
    expect(client.snapshot().libraryRequest).toMatchObject({ action: 'save', motionId, name: 'My walk', tags: ['Walking'] });
    act(() => { client.receive({ ...selected, revision: 4, ack: 3 }); });
    fireEvent.click(ui.getByText('Use for walking')); expect(client.snapshot().libraryRequest).toMatchObject({ action: 'walk', motionId });
    act(() => { client.receive({ ...selected, revision: 5, ack: 4 }); });
    fireEvent.click(ui.getByText('Use in selected action')); expect(client.snapshot().libraryRequest).toMatchObject({ action: 'rule', motionId, ruleId: 'd'.repeat(32), stepIndex: 0 });
  });
  it('escapes source text and keeps the existing chat composer mounted across library navigation', () => {
    const ui = render(<QuestBookSurface><input aria-label="Chat draft" defaultValue="Keep my draft" /></QuestBookSurface>);
    const original = ui.getByLabelText('Chat draft'); const initial = state();
    act(() => { window.maestroBook!.libraryState({ ...initial, selected: initial.entries[0], sourceName: 'Example', attribution: '<img src=x onerror=alert(1)>' }); });
    expect(ui.getByLabelText('Chat draft')).toBe(original); expect(original.closest('[hidden]')).not.toBeNull();
    expect(ui.container.querySelector('img[src=x]')).toBeNull();
    fireEvent.click(ui.getByText('Back to chat')); expect(window.maestroBook!.snapshot().libraryRequest?.action).toBe('close');
    act(() => { window.maestroBook!.libraryState({ ...initial, revision: 2, ack: 1, visible: false }); });
    expect(original.closest('[hidden]')).toBeNull(); expect((original as HTMLInputElement).value).toBe('Keep my draft');
    expect(ui.queryByLabelText('Animation library')).toBeNull();
  });
  it('validates bounded per-avatar profiles and sends explicit assignment settings without preview', () => {
    const modelHash = 'f'.repeat(64);
    const initial = { ...state(), selected: state().entries[0], activityProfile: { modelHash, revision: 1, status: 'Ready', canAssign: true, readOnly: false, canUndo: false, canRedo: false, roles: [0, 1, 2, 3].map(role => ({ role, choices: [] })) } };
    expect(parseLibraryState(initial)).not.toBeNull();
    expect(parseLibraryState({ ...initial, activityProfile: { ...initial.activityProfile, roles: [] } })).toBeNull();
    const client = new LibraryBookClient(); client.receive(initial); const ui = render(<LibraryBookView client={client} />);
    fireEvent.click(ui.getByText('Tutor-state motions'));
    fireEvent.change(ui.getByLabelText('Tutor state'), { target: { value: '3' } });
    fireEvent.change(ui.getByLabelText('Selection weight'), { target: { value: '5' } });
    fireEvent.change(ui.getByLabelText('Seconds before reusing'), { target: { value: '10' } });
    fireEvent.click(ui.getByText('Assign to tutor state'));
    expect(client.snapshot().libraryRequest).toMatchObject({ action: 'roleAssign', modelHash, profileRevision: 1, role: 3, motionId, speed: 1, weight: 5, cooldown: 10, loop: false });
    const profile = { ...initial.activityProfile, canUndo: true, roles: initial.activityProfile.roles.map(group => ({ ...group, choices: group.role === 3 ? [{ motionId, name: 'Stage walk', weight: 5, speed: 1, cooldown: 10, loop: false, available: true }] : [] })) };
    act(() => { client.receive({ ...initial, revision: 2, ack: 1, activityProfile: profile }); });
    fireEvent.click(ui.getByLabelText('Remove Stage walk from Speaking'));
    expect(client.snapshot().libraryRequest).toMatchObject({ action: 'roleRemove', modelHash, role: 3, motionId });
    act(() => { client.receive({ ...initial, revision: 3, ack: 2, activityProfile: profile }); });
    fireEvent.click(ui.getByText('Undo assignments'));
    expect(client.snapshot().libraryRequest).toMatchObject({ action: 'roleUndo', modelHash });
    const invalid = { ...profile, roles: [{ role: 0, choices: [{ ...profile.roles[3].choices[0], speed: Infinity }] }, ...profile.roles.slice(1)] };
    expect(parseLibraryState({ ...initial, activityProfile: invalid })).toBeNull();
  });
  it('reviews protected references and requires explicit confirmation before removing a download', () => {
    const initial = state();
    const selected = { ...initial.entries[0], archived: true, removed: false, downloaded: true, bytes: 1024 };
    const usage = { total: 0, page: 0, pages: 1, uses: [], history: false, saved: false, uncertain: false, playing: false, protection: null };
    const view = { ...initial, selected, entries: [selected], usage, archivedOnly: true, canRemoveDownload: true };
    expect(parseLibraryState(view)).not.toBeNull();
    expect(parseLibraryState({ ...view, usage: { ...usage, uses: Array(9).fill('A') } })).toBeNull();
    expect(parseLibraryState({ ...view, selected: { ...selected, bytes: Infinity } })).toBeNull();
    const client = new LibraryBookClient(); client.receive(view); const ui = render(<LibraryBookView client={client} />);
    fireEvent.click(ui.getByText('Usage and local storage')); fireEvent.click(ui.getByText('Remove local download'));
    expect(client.snapshot().libraryRequest).toBeNull(); expect(ui.getByLabelText('Confirm download removal')).toBeTruthy();
    fireEvent.click(ui.getByText('Keep download')); expect(ui.queryByLabelText('Confirm download removal')).toBeNull();
    fireEvent.click(ui.getByText('Remove local download')); fireEvent.click(ui.getByText('Confirm remove download'));
    expect(client.snapshot().libraryRequest).toMatchObject({ action: 'removeDownload', motionId });
    act(() => { client.receive({ ...view, revision: 2, ack: 1, canRemoveDownload: false, usage: { ...usage, history: true, protection: 'Undo needs this motion' } }); });
    expect((ui.getByText('Remove local download') as HTMLButtonElement).disabled).toBe(true);
    expect(ui.getByText('Undo needs this motion')).toBeTruthy();
  });
  it('drops removal confirmation when the selection or native session changes and queries archived motions', () => {
    const initial = state(); const selected = { ...initial.entries[0], archived: true, removed: false, downloaded: true, bytes: 1024 };
    const view = { ...initial, selected, entries: [selected], usage: { total: 0, page: 0, pages: 1, uses: [], history: false, saved: false, uncertain: false, playing: false, protection: null }, canRemoveDownload: true };
    const client = new LibraryBookClient(); client.receive(view); const ui = render(<LibraryBookView client={client} />);
    fireEvent.click(ui.getByText('Usage and local storage')); fireEvent.click(ui.getByText('Remove local download'));
    act(() => { client.receive({ ...view, revision: 2, selected: { ...selected, id: 'e'.repeat(32), name: 'Another motion' } }); });
    expect(ui.queryByLabelText('Confirm download removal')).toBeNull(); expect(client.snapshot().libraryRequest).toBeNull();
    fireEvent.click(ui.getByText('Remove local download')); act(() => { client.receive({ ...view, session: 'f'.repeat(32) }); });
    expect(ui.queryByLabelText('Confirm download removal')).toBeNull();
    fireEvent.click(ui.getByLabelText('Archived')); expect(client.snapshot().libraryRequest).toMatchObject({ action: 'query', archivedOnly: true, offset: 0 });
  });
  it('requires a separate confirmation to forget a removed motion identity', () => {
    const initial = state(); const selected = { ...initial.entries[0], archived: true, removed: true, downloaded: false, bytes: 1024 };
    const view = { ...initial, selected, entries: [selected], usage: { total: 0, page: 0, pages: 1, uses: [], history: false, saved: false, uncertain: false, playing: false, protection: null }, canRemoveDownload: false, canForgetMotion: true };
    expect(parseLibraryState({ ...view, canForgetMotion: 'yes' })).toBeNull();
    const client = new LibraryBookClient(); client.receive(view); const ui = render(<LibraryBookView client={client} />);
    fireEvent.click(ui.getByText('Usage and local storage')); fireEvent.click(ui.getByText('Forget motion details'));
    expect(client.snapshot().libraryRequest).toBeNull(); expect(ui.getByLabelText('Confirm forgetting motion')).toBeTruthy();
    fireEvent.click(ui.getByText('Keep details')); expect(ui.queryByLabelText('Confirm forgetting motion')).toBeNull();
    fireEvent.click(ui.getByText('Forget motion details')); fireEvent.click(ui.getByText('Confirm forget details'));
    expect(client.snapshot().libraryRequest).toMatchObject({ action: 'forgetMotion', motionId });
  });

});

it('keeps a dirty assignment draft when an agent changes the shared profile and requires reload', () => {
 const client=new LibraryBookClient();
 const profile={modelHash:'f'.repeat(64),revision:1,status:'Ready',canAssign:true,readOnly:false,canUndo:true,canRedo:false,roles:[0,1,2,3].map(role=>({role,choices:[]}))};
 const initial={...state(),selected:state().entries[0],activityProfile:profile};client.receive(initial);
 const ui=render(<LibraryBookView client={client}/>);fireEvent.click(ui.getByText('Tutor-state motions'));
 fireEvent.change(ui.getByLabelText('Selection weight'),{target:{value:'5'}});
 act(()=>{client.receive({...initial,revision:2,activityProfile:{...profile,revision:2,roles:profile.roles.map(r=>({...r,choices:r.role===0?[{motionId,name:'Stage walk',weight:2,speed:.5,cooldown:10,loop:true,available:true}]:[]}))}});});
 expect((ui.getByLabelText('Selection weight') as HTMLSelectElement).value).toBe('5');
 expect((ui.getByLabelText('Selection weight') as HTMLSelectElement).closest('fieldset')?.disabled).toBe(true);
 expect(client.snapshot().libraryRequest).toBeNull();
 fireEvent.click(ui.getByText('Reload current assignments'));
 expect((ui.getByLabelText('Selection weight') as HTMLSelectElement).value).toBe('2');
 fireEvent.change(ui.getByLabelText('Playback speed'),{target:{value:'1.5'}});fireEvent.click(ui.getByText('Update state motion'));
 expect(client.snapshot().libraryRequest).toMatchObject({action:'roleAssign',profileRevision:2,weight:2,speed:1.5,cooldown:10,loop:true});
});
