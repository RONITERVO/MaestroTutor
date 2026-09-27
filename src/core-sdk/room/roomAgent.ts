// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { roomControlFields, validRoomControl, requireRoomCapabilities, type ObjectPhysicsSettings, type AvatarMovementSettings, type PhysicsObservation, type AvatarMovementObservation } from '../../../shared/roomControls';
import {validMotionQuery,type MotionQuery,type MotionSearchView} from '../../../shared/roomMotions';
import type { RelatedRoomTask } from './taskSteering';
import {validRuleRequest,type RuleRequest,type RuleView} from './rules';
import { parseRecipe, type RoomRecipe } from './recipe';
import { generateGeminiResponse } from '../gemini/generative';
import { pickGeminiClientSource } from '../gemini/clientSource';
import { runTutorTextTurn, type TutorTextTurnInput, type TutorTextTurnOptions } from '../chat/tutorTextTurn';
import { buildRoomAgentPrompt, buildRoomResultInstruction, ROOM_AGENT_INSTRUCTION, ROOM_AGENT_SCHEMA } from '../../../shared/prompts';

export interface RoomCommand {
  action: 'create' | 'move' | 'resize' | 'paint' | 'recipe' | 'delete' | 'undo' | 'redo' | 'inspect' | 'workspace' | 'play' | 'stop' | 'rules' | 'motions' | keyof typeof roomControlFields;
  rule?:RuleRequest; motionQuery?:MotionQuery;
  operation?:'start'|'pause'|'look'|'follow'|'stop'; physics?:ObjectPhysicsSettings; movement?:AvatarMovementSettings;
  target?: string; partId?:string; reference?: string; name?: string;
  kind?: 'block' | 'ball' | 'cylinder' | 'recipe' | 'boxRobot';
  visible?: boolean; atPosition?: boolean; position?: { x: number; y: number; z: number };
  scale?: number; color?: { r: number; g: number; b: number; a: number }; recipe?: unknown;
}
export interface RoomAgentState {
  version: 1; session: string; revision: number; sceneRevision: number; ack: number;
  ok: boolean; status: string; created: string[]; canUndo: boolean; canRedo: boolean; physicsRunning: boolean;
  capabilities?:string[]; physics?:PhysicsObservation|null; avatar?:AvatarMovementObservation|null;
  workspaceView?:'objects'|'rules'; rules?:RuleView|null; motions?:MotionSearchView|null;
  visible?: boolean; inspection?: {id:string;partId?:string|null;objectRevision:number;recipe:RoomRecipe|null}|null;
  selectedId?: string | null;
  objects: { physics?:ObjectPhysicsSettings; movement?:AvatarMovementSettings|null; held?:boolean; simulating?:boolean; objectRevision?:number; id: string; name: string; kind: string; position: {x:number;y:number;z:number}; scale:number; color: {r:number;g:number;b:number;a:number}; animated:boolean }[];
}
export interface RoomAgentLease {
  state(): RoomAgentState;
  valid(): boolean;
  execute(commands: RoomCommand[], expectedRevision: number, expectedObjects?: RoomAgentState['objects'], signal?: AbortSignal): Promise<RoomAgentState>;
}
const record = (v: unknown): v is Record<string, unknown> => v !== null && typeof v === 'object' && !Array.isArray(v);
const validColor = (v: unknown) => record(v) && ['r','g','b'].every(k => typeof v[k] === 'number' && Number.isFinite(v[k]) && Number(v[k]) >= 0 && Number(v[k]) <= 1) && v.a === 1;
const vector = (v: unknown) => record(v) && ['x','y','z'].every(k => typeof v[k] === 'number' && Number.isFinite(v[k]) && Math.abs(v[k] as number) <= 25);
const fields: Record<RoomCommand['action'], readonly string[]> = {
  ...roomControlFields, motions:['target','motionQuery'],
  create:['reference','name','kind','atPosition','position','scale','color','recipe'], move:['target','position'], resize:['target','scale'],
  paint:['target','color'], recipe:['target','recipe'], delete:['target'], undo:[], redo:[], inspect:['target','partId'], workspace:['visible'], play:['target'], stop:['target'], rules:['rule'],
};
export function parseRoomCommands(input: unknown): RoomCommand[] {
  if (!record(input) || Object.keys(input).some(k => k !== 'commands') || !Array.isArray(input.commands) || input.commands.length > 8 || JSON.stringify(input).length > 28000) throw new Error('The room plan is invalid or too large.');
  for (const c of input.commands) {
    if (!record(c) || typeof c.action !== 'string' || !Object.prototype.hasOwnProperty.call(fields,c.action)) throw new Error('Unknown room action.');
    const action = c.action as RoomCommand['action'];
    if (Object.keys(c).some(k => k !== 'action' && !fields[action].includes(k))) throw new Error('Unknown room action field.');
    if (fields[action].includes('target') && (typeof c.target !== 'string' || !/^[a-zA-Z0-9_]{1,32}$/.test(c.target))) throw new Error('Invalid room target.');
    if (action === 'create' && (typeof c.reference !== 'string' || !/^[a-zA-Z0-9_]{1,32}$/.test(c.reference) || typeof c.name !== 'string' || c.name.length > 80 || /[\u0000-\u001f]/.test(c.name) || !['block','ball','cylinder','recipe','boxRobot'].includes(c.kind as string))) throw new Error('Invalid creation.');
    if ((action === 'move' || c.atPosition === true || c.position !== undefined) && !vector(c.position)) throw new Error('Invalid position.');
    if (c.partId !== undefined && (typeof c.partId !== 'string' || !/^[a-zA-Z0-9_]{1,32}$/.test(c.partId))) throw new Error('Invalid recipe part.');
    if (Object.prototype.hasOwnProperty.call(roomControlFields,action) && !validRoomControl(c)) throw new Error('Invalid room control.');
    if (action === 'motions' && !validMotionQuery(c.motionQuery)) throw new Error('Invalid motion search.');
    if (action === 'rules' && !validRuleRequest(c.rule)) throw new Error('Invalid behaviour request.');
    if (action === 'workspace' && typeof c.visible !== 'boolean') throw new Error('Invalid workspace state.');
    if (c.atPosition !== undefined && typeof c.atPosition !== 'boolean') throw new Error('Invalid placement.');
    if ((action === 'resize' || c.scale !== undefined) && (typeof c.scale !== 'number' || !Number.isFinite(c.scale) || c.scale < .1 || c.scale > 4)) throw new Error('Invalid scale.');
    if ((action === 'paint' || c.color !== undefined) && !validColor(c.color)) throw new Error('Invalid colour.');
    if ((action === 'recipe' || c.kind === 'recipe') && !parseRecipe(c.recipe)) throw new Error('Missing recipe.');
  }
  if (input.commands.some(c => ['undo','redo','inspect','workspace','play','stop','rules','motions','physicsRun','avatarMotion'].includes(c.action)) && input.commands.length !== 1) throw new Error('This action must be submitted on its own.');
  return input.commands as unknown as RoomCommand[];
}

export interface RoomTaskControl {
  relatedTask?: RelatedRoomTask;
  isCurrent?:()=>boolean;
  beforePlan?:()=>Promise<void>;
  beforeDispatch?:(commands:RoomCommand[],scene:RoomAgentState)=>Promise<void>;
  signal?:AbortSignal;
  /** Called with each actual native acknowledgement, before the next model call. */
  onReceipt?:(receipt:RoomAgentState)=>void|Promise<void>;
}
export interface RoomTaskResult {receipts:RoomAgentState[];scene:RoomAgentState;budgetExhausted:boolean;relatedTask?:RelatedRoomTask;needsReview?:boolean}
const copy=<T>(value:T):T=>JSON.parse(JSON.stringify(value));

/** A bounded tool task owned by the original Maestro app. The caller supplies
 * its existing Gemini access route and conversation lifetime; Unity never owns
 * a provider client. The result can be presented even if narration later fails. */
export async function runRoomActionTask(input: Pick<TutorTextTurnInput,'model'|'prompt'|'history'|'timeoutMs'> & Partial<Pick<TutorTextTurnInput,'systemInstruction'|'currentFileParts'|'nativeLanguageCode'|'liveInputMedia'>>,
  options:TutorTextTurnOptions,lease:RoomAgentLease,
  onUsage:(response:Awaited<ReturnType<typeof generateGeminiResponse>>)=>void,control:RoomTaskControl={}
):Promise<RoomTaskResult> {
  const receipts:RoomAgentState[]=[];
  const active=()=>{if(control.signal?.aborted||control.isCurrent?.()===false||!lease.valid())throw new DOMException('The room request was interrupted. No further actions will run.','AbortError');};
  for(let step=0;step<3;step++) {
    active();await control.beforePlan?.();active();
    const scene=copy(lease.state());
    const response=await generateGeminiResponse(input.model,buildRoomAgentPrompt(input.prompt,scene,receipts,{systemInstruction:input.systemInstruction,nativeLanguageCode:input.nativeLanguageCode,relatedTask:control.relatedTask}),input.history,{
      ...pickGeminiClientSource(options),systemInstruction:ROOM_AGENT_INSTRUCTION,currentFileParts:input.currentFileParts,
      ...(input.liveInputMedia ? {liveInputMedia:input.liveInputMedia} : {}),
      configOverrides:{responseMimeType:'application/json',responseJsonSchema:ROOM_AGENT_SCHEMA},
      timeoutMs:input.timeoutMs,signal:control.signal,lifecycleHooks:{onProgress:options.lifecycleHooks?.onProgress},
    });
    onUsage(response);active();
    const commands=parseRoomCommands(JSON.parse(response.text||'{}'));
    if(!commands.length)return {receipts,scene:copy(lease.state()),budgetExhausted:false,relatedTask:control.relatedTask,needsReview:!!control.relatedTask?.unconfirmed};
    // An unconfirmed earlier action is evidence of uncertainty, never permission to retry it.
    if (control.relatedTask?.unconfirmed && commands.some(command => command.action !== 'inspect' && command.action !== 'motions'))
      return { receipts, scene: copy(lease.state()), budgetExhausted: false, relatedTask: control.relatedTask, needsReview: true };
    requireRoomCapabilities(commands,scene);
    await control.beforeDispatch?.(commands,scene);active();
    const receipt=await (control.signal
      ? lease.execute(commands,scene.sceneRevision,scene.objects,control.signal)
      : lease.execute(commands,scene.sceneRevision,scene.objects));
    receipts.push(copy(receipt));
    // Cancellation may race an acknowledgement. Preserve that evidence before
    // checking the turn fence; never relabel a completed edit as rolled back.
    await control.onReceipt?.(copy(receipt));
    active();
  }
  active();return {receipts,scene:copy(lease.state()),budgetExhausted:true,relatedTask:control.relatedTask};
}

/** Compatibility wrapper until room tasks enter the common tool dispatcher. */
export async function runRoomTutorTurn(input:TutorTextTurnInput,options:TutorTextTurnOptions,lease:RoomAgentLease,
  onUsage:(response:Awaited<ReturnType<typeof generateGeminiResponse>>)=>void,isCurrent:()=>boolean=()=>true) {
  const task=await runRoomActionTask(input,options,lease,onUsage,{isCurrent});
  if(!isCurrent()||!lease.valid())throw new DOMException('The room request was interrupted. No further actions will run.','AbortError');
  return runTutorTextTurn({...input,systemInstruction:input.systemInstruction+'\n\n'+buildRoomResultInstruction(task.receipts,task.scene)},options);
}
