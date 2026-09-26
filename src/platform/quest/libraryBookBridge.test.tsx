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
    const initial = { ...state(), selected: state().entries[0], activityProfile: { modelHash, status: 'Ready', canAssign: true, readOnly: false, canUndo: false, canRedo: false, roles: [0, 1, 2, 3].map(role => ({ role, choices: [] })) } };
    expect(parseLibraryState(initial)).not.toBeNull();
    expect(parseLibraryState({ ...initial, activityProfile: { ...initial.activityProfile, roles: [] } })).toBeNull();
    const client = new LibraryBookClient(); client.receive(initial); const ui = render(<LibraryBookView client={client} />);
    fireEvent.click(ui.getByText('Tutor-state motions'));
    fireEvent.change(ui.getByLabelText('Tutor state'), { target: { value: '3' } });
    fireEvent.change(ui.getByLabelText('Selection weight'), { target: { value: '5' } });
    fireEvent.change(ui.getByLabelText('Seconds before reusing'), { target: { value: '10' } });
    fireEvent.click(ui.getByText('Assign to tutor state'));
    expect(client.snapshot().libraryRequest).toMatchObject({ action: 'roleAssign', modelHash, role: 3, motionId, speed: 1, weight: 5, cooldown: 10, loop: false });
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

});
