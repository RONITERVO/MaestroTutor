// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { projectRoomTaskSummaries, summarizeRoomTask } from './roomTaskProjection';
import type { RoomTaskRecord } from './roomTaskHandoff';
import type { ChatMessage } from '../../core/types';
const history: ChatMessage[] = [
  { id: 'user', role: 'user', text: 'Make a robot', timestamp: 1 },
  { id: 'assistant', role: 'assistant', text: 'I will ask the agent', timestamp: 2 },
  { id: 'later', role: 'user', text: 'Let us practise', timestamp: 4 },
];
const record: RoomTaskRecord = { version: 1, id: 'task', phase: 'completed', note: 'Finished.', startedAt: 3, updatedAt: 7,
  handoff: { version: 1, id: 'task', conversationId: 'pair', sourceUserId: 'user', sourceAssistantId: 'assistant', nativeSession: 'private-session', accessScope: 'private-account',
    input: { prompt: 'Make a robot', model: 'test', nativeLanguageCode: 'en', systemInstruction: 'Private system input', history: [] } },
  operations: [{ commands: [], sceneRevision: 123 }],
  reply: { rawResponse: 'private-raw', parsed: { visibleText: 'Ready.', translations: [{ target: 'Listo.', native: 'Ready.' }], hasSkippedNonLanguageContent: false } } };
describe('durable task chat projection', () => {
  it('restores a missing result next to its source, without copying private task data', () => {
    const summary = summarizeRoomTask(record);
    const messages = projectRoomTaskSummaries(history, [summary], 'pair');
    expect(messages.map(message => message.id)).toEqual(['user', 'assistant', 'task', 'later']);
    expect(messages[2]).toMatchObject({ role: 'assistant', translations: [{ target: 'Listo.', native: 'Ready.' }], agentTask: { phase: 'completed' } });
    const encoded = JSON.stringify(summary);
    for (const privateValue of ['private-', 'Private system', 'sceneRevision', 'operations', 'input', 'history']) expect(encoded).not.toContain(privateValue);
    expect(history).toHaveLength(3); expect(summary.message.timestamp).toBe(3);
  });
  it('repairs stale task status in place and retains message identity, position and audio cache', () => {
    const stale: ChatMessage = { id: 'task', role: 'status', timestamp: 5, text: 'Working',
      agentTask: { id: 'task', phase: 'working', note: 'Working' }, ttsAudioCache: [] };
    const messages = projectRoomTaskSummaries([...history, stale], [summarizeRoomTask(record)], 'pair');
    expect(messages[3]).toMatchObject({ id: 'task', timestamp: 5, role: 'assistant', ttsAudioCache: [], agentTask: { phase: 'completed' } });
    expect(projectRoomTaskSummaries(messages, [summarizeRoomTask(record)], 'pair')).toEqual(messages);
  });
  it.each(['user', 'assistant'])('does not restore tasks after the %s source is removed', source => {
    const summary = summarizeRoomTask(record);
    const messages = [...history.filter(message => message.id !== source), summary.message];
    expect(projectRoomTaskSummaries(messages, [summary], 'pair').some(message => message.agentTask)).toBe(false);
  });
  it('keeps explicitly hidden results hidden while preserving the claim in the journal', () => {
    const summary = { ...summarizeRoomTask(record), hidden: true };
    expect(projectRoomTaskSummaries([...history, summary.message], [summary], 'pair')).toEqual(history);
    expect(projectRoomTaskSummaries(history, [summary], 'pair')).toEqual(history);
  });
  it('does not confuse an unrelated conversation or colliding ordinary message with a task result', () => {
    const summary = summarizeRoomTask(record);
    expect(projectRoomTaskSummaries(history, [summary], 'other')).toEqual(history);
    const messages = [...history, { id: 'task', role: 'user' as const, timestamp: 10, text: 'Keep me' }];
    expect(projectRoomTaskSummaries(messages, [summary], 'pair')).toEqual(messages);
  });
  it('preserves an unfinished journal as uncertain history, without inventing completion or changing the record', () => {
    const working = { ...record, phase: 'working' as const, note: 'Applying an action.', reply: undefined };
    const messages = projectRoomTaskSummaries(history, [summarizeRoomTask(working)], 'pair');
    expect(messages[2]).toMatchObject({ role: 'status', text: 'Applying an action.', agentTask: { phase: 'working' } });
    expect(working.operations[0].receipt).toBeUndefined(); expect(working.reply).toBeUndefined();
  });
});
