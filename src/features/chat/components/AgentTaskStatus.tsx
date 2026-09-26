// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useEffect, useState } from 'react';
import type { ChatMessage } from '../../../core/types';
import type { RoomTaskRecord } from '../../../core-sdk/room/roomTaskHandoff';
import { loadRoomAgentTask, roomAgentTasks } from '../services/roomAgentTasks';

/** Detailed evidence is fetched on demand and never copied into tutor history. */
export function AgentTaskStatus({ task, controls = roomAgentTasks }: {
  task: NonNullable<ChatMessage['agentTask']>;
  controls?: Pick<typeof roomAgentTasks, 'running' | 'stop'>;
}) {
  const [record, setRecord] = useState<RoomTaskRecord>();
  const [stopping, setStopping] = useState(false);
  const [loadError, setLoadError] = useState('');
  const [detailsOpen, setDetailsOpen] = useState(false);
  useEffect(() => {
    if (!detailsOpen) return;
    let current = true;
    void loadRoomAgentTask(task.id).then(value => {
      if (current) { setRecord(value); setLoadError(value ? '' : 'No saved action record is available.'); }
    }, () => { if (current) setLoadError('Task details could not be loaded.'); });
    return () => { current = false; };
  }, [detailsOpen, task.id, task.phase, task.note]);
  const activePhase = task.phase === 'working' || task.phase === 'replying';
  const active = activePhase && controls.running(task.id);
  const note = activePhase && !active ? 'This task is no longer running here. Inspect the room before starting it again.' : task.note;
  return <section className="my-2 rounded border border-current/20 p-2 text-sm" aria-label="Agent task">
    <p aria-live="polite">{note}</p>
    {active && <button type="button" className="my-2 rounded border border-current/40 px-3 py-1" disabled={stopping} onClick={() => { setStopping(true); controls.stop(task.id); }}>{stopping ? 'Stopping…' : 'Stop task'}</button>}
    <details onToggle={event => setDetailsOpen(event.currentTarget.open)}>
      <summary className="cursor-pointer py-1">Task details</summary>
      {loadError && <p>{loadError}</p>}
      {record && <>
        {record.note !== task.note && <p>{record.note}</p>}
        <p>Recorded action batches: {record.operations.length}.</p>
        <ol className="list-decimal pl-5">{record.operations.map((operation, index) => <li key={index} className="my-1">
          {operation.commands.map(command => command.action).join(', ')}: {operation.receipt
            ? operation.receipt.status : 'Outcome unconfirmed; do not automatically repeat.'}
        </li>)}</ol>
      </>}
    </details>
  </section>;
}
