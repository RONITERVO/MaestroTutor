// Development-only simulated native/provider ports; real task journal and UI.
import { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { RoomTaskHandoff, type RoomTaskRecord, type RoomHandoff } from '../../src/core-sdk/room/roomTaskHandoff';
import { runRoomActionTask, type RoomAgentState } from '../../src/core-sdk/room/roomAgent';
import { roomTaskStore } from '../../src/features/chat/services/roomTaskStore';
import { saveChatHistoryDB } from '../../src/features/chat/services/chatHistory';
import { AgentTaskStatus } from '../../src/features/chat/components/AgentTaskStatus';
import CollapsedMaestroStatus from '../../src/features/session/components/CollapsedMaestroStatus';
import { useMaestroStore } from '../../src/store';
import { selectIsSending, selectIsAgentWorking } from '../../src/store/slices/uiSlice';
import { enTranslations as en } from '../../src/core/i18n/en';
import '../../src/app/index.css';
if (!import.meta.env.DEV) throw new Error('Development-only fixture.');
const scene: RoomAgentState = { version: 1, session: 'fixture-room', revision: 1, sceneRevision: 1, ack: 0, ok: true, status: 'Ready',
  objects: [], created: [], canUndo: false, canRedo: false, physicsRunning: false };
const source: RoomHandoff = { version: 1, id: 'fixture-agent-task', sourceUserId: 'fixture-user', sourceAssistantId: 'fixture-assistant',
  conversationId: 'fixture-pair', nativeSession: scene.session, accessScope: 'fixture',
  input: { model: 'fixture', prompt: 'Make a little blue robot.', history: [], systemInstruction: 'Fixture', nativeLanguageCode: 'en' } };
let update: (record: RoomTaskRecord) => void = () => {};
let finish: (() => void) | undefined;
let executions = 0;
let acknowledged = false;
let activityToken = '';
const ai = { models: { generateContent: async () => ({}), generateContentStream: async () => (async function* () {
  yield { text: acknowledged ? '{"commands":[]}' : '{"commands":[{"action":"create","reference":"robot","name":"Robot","kind":"boxRobot"}]}' };
})() } } as any;
const manager = new RoomTaskHandoff({
  store: roomTaskStore,
  lease: () => ({ state: () => scene, valid: () => true, execute: (_commands, _revision, _objects, signal) => {
    executions++;
    return new Promise((resolve, reject) => {
      const abort = () => { finish = undefined; reject(new DOMException('Stopped', 'AbortError')); };
      signal?.addEventListener('abort', abort, { once: true });
      finish = () => { signal?.removeEventListener('abort', abort); acknowledged = true; resolve({ ...scene, ack: 1, status: 'Created robot', created: ['robot'] }); };
    });
  } }),
  run: (input, lease, control) => runRoomActionTask(input, { aiClient: ai }, lease, () => {}, control),
  reply: async () => ({ rawResponse: 'Your robot is ready.', parsed: { visibleText: 'Your robot is ready.', translations: [], hasSkippedNonLanguageContent: false } }),
  changed: record => update(record),
  activity: active => {
    if (active) activityToken = useMaestroStore.getState().addActivityToken('agent', 'task');
    else useMaestroStore.getState().removeActivityToken(activityToken);
  }, now: Date.now,
});
manager.capture(source, async () => true);
export function evidence() {
  const state = useMaestroStore.getState();
  return { executions, inputBlocked: selectIsSending(state), agentWorking: selectIsAgentWorking(state) };
}
export async function pruneAndCheck() {
  const record = (await roomTaskStore.get(source.id))!;
  const claims = await Promise.all([roomTaskStore.claim({ ...record, id: 'fixture-atomic' }), roomTaskStore.claim({ ...record, id: 'fixture-atomic' })]);
  await saveChatHistoryDB('fixture-pair', []);
  let lateSaveRejected = false;
  try { await roomTaskStore.save(record); } catch { lateSaveRejected = true; }
  return { removed: !(await roomTaskStore.get(source.id)), lateSaveRejected, atomicClaims: claims.filter(claim => claim.claimed).length };
}
(window as any).agentTaskFixture = { evidence, pruneAndCheck };
function Fixture() {
  const [record, setRecord] = useState<RoomTaskRecord>();
  update = setRecord;
  useEffect(() => { void roomTaskStore.get(source.id).then(value => { if (value) setRecord(value); }); }, []);
  return <main style={{ maxWidth: 680, margin: '32px auto', padding: 28, background: '#f9f0da', color: '#332f34', borderRadius: 12, fontFamily: 'Georgia, serif' }}>
    <header className="mb-6 border-b border-current/20 pb-4"><CollapsedMaestroStatus stage="idle" t={key => en[key as keyof typeof en] || key} isExpanded /></header>
    <p className="mb-3">Make a little blue robot.</p>
    <p className="mb-5">I’ll hand that to the agent and report back here.</p>
    {record && <AgentTaskStatus task={{ id: record.id, phase: record.phase, note: record.note }} controls={manager} />}
    {record?.reply && <p className="my-4">{record.reply.parsed.visibleText}</p>}
    <label className="block mt-6">Continue the conversation<input aria-label="Chat message" className="block w-full rounded border border-current/30 bg-white/60 p-3 mt-2" placeholder="You can keep chatting…" /></label>
    <div className="flex gap-3 mt-6 text-sm">
      <button className="rounded border border-current/30 p-2" onClick={() => { void manager.start('fixture-assistant'); }}>Start simulated handoff</button>
      <button className="rounded border border-current/30 p-2" onClick={() => finish?.()}>Acknowledge simulated action</button>
    </div>
    <p className="mt-5 text-xs opacity-70">Development fixture · simulated provider and room · real IndexedDB task storage</p>
  </main>;
}
createRoot(document.getElementById('root')!).render(<Fixture />);
