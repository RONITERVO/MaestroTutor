// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {ROOM_PLANNER_ARGUMENT_GUIDE,roomCaptureImages} from '../../../shared/prompts/room';
import {decodeRoomPlannerResponse} from './roomPlannerResponse';
import {ROOM_TASK_LIMITS,remainingRoomTaskBudget} from '../../../shared/roomTaskBudget';
import {validRoomCaptureImage,validRoomCaptureMetadata,sameRoomCapture,type RoomCaptureImage,type RoomCaptureMetadata} from '../../../shared/roomViewCapture';
import type {ConstructionSelection,ConstructionManipulation} from '../../../shared/roomSelection';
import type {RoomOwnershipView} from '../../../shared/roomOwnership';
import type {TemporaryRoomView} from '../../../shared/roomSession';
import {identifyExecution,validExecutionRequest,type ExecutionRequest,type ExecutionView} from '../../../shared/roomExecutions';
import {validCatalogRequest,type CatalogRequest,type CatalogView} from '../../../shared/roomCatalog';
import { roomCommandFields as fields } from '../../../shared/roomCommandFields';
import { roomControlFields, validRoomControl, requireRoomCapabilities, type ObjectPhysicsSettings, type AvatarMovementSettings, type PhysicsObservation, type AvatarMovementObservation, type AvatarWalkObservation } from '../../../shared/roomControls';
import {validAvatarActivityRequest,type AvatarActivityRequest,type ActivityProfile} from '../../../shared/avatarActivities';
import {validMotionQuery,type MotionQuery,type MotionSearchView} from '../../../shared/roomMotions';
import type { RelatedRoomTask } from './taskSteering';
import {validRuleRequest,type RuleRequest,type RuleView} from './rules';
import {parseProgram} from './programs';
import {resolveProgramImportReferences} from './programImportReferences';
import { parseRecipe, type RoomRecipe } from './recipe';
import { generateGeminiResponse } from '../gemini/generative';
import { pickGeminiClientSource } from '../gemini/clientSource';
import { runTutorTextTurn, type TutorTextTurnInput, type TutorTextTurnOptions } from '../chat/tutorTextTurn';
import { buildRoomAgentPrompt, buildRoomResultInstruction, ROOM_AGENT_INSTRUCTION, ROOM_AGENT_RESPONSE_SCHEMA } from '../../../shared/prompts';

export interface RoomCommand {
  action: 'create' | 'move' | 'resize' | 'paint' | 'recipe' | 'delete' | 'undo' | 'redo' | 'inspect' | 'workspace' | 'play' | 'stop' | 'rules' | 'motions' | 'catalog' | 'execution' | 'avatarActivities' | keyof typeof roomControlFields;
  execution?:ExecutionRequest; catalog?:CatalogRequest; activities?:AvatarActivityRequest; rule?:RuleRequest; motionId?:string; motionQuery?:MotionQuery;
  operation?:'start'|'pause'|'look'|'follow'|'stop'; physics?:ObjectPhysicsSettings; movement?:AvatarMovementSettings;
  target?: string; partId?:string; reference?: string; name?: string;
  kind?: 'block' | 'ball' | 'cylinder' | 'recipe' | 'boxRobot';
  visible?: boolean; atPosition?: boolean; position?: { x: number; y: number; z: number };
  scale?: number; color?: { r: number; g: number; b: number; a: number }; recipe?: unknown;
}
export interface RoomAgentState {
  capture?:RoomCaptureMetadata|null;
  version: 1; session: string; revision: number; sceneRevision: number; ack: number;
  ok: boolean; status: string; created: string[]; canUndo: boolean; canRedo: boolean; physicsRunning: boolean;
  ownership?:RoomOwnershipView|null; temporaryRoom?:TemporaryRoomView; capabilities?:string[]; physics?:PhysicsObservation|null; avatar?:AvatarMovementObservation|null; walk?:AvatarWalkObservation|null;
  execution?:ExecutionView|null; catalog?:CatalogView|null; activityProfile?:ActivityProfile|null; workspaceView?:'objects'|'rules'; rules?:RuleView|null; motions?:MotionSearchView|null;
  visible?: boolean; inspection?: {id:string;partId?:string|null;objectRevision:number;recipe:RoomRecipe|null}|null;
  selectedId?: string | null; constructionSelection?:ConstructionSelection|null; constructionManipulation?:ConstructionManipulation|null;
  objects: { physics?:ObjectPhysicsSettings; movement?:AvatarMovementSettings|null; held?:boolean; simulating?:boolean; objectRevision?:number; id: string; name: string; kind: string; position: {x:number;y:number;z:number}; scale:number; color: {r:number;g:number;b:number;a:number}; animated:boolean }[];
}
export interface RoomAgentLease {
  state(): RoomAgentState;
  valid(): boolean;
  capture?(id:string,signal?:AbortSignal):Promise<RoomCaptureImage>;
  execute(commands: RoomCommand[], expectedRevision: number, expectedObjects?: RoomAgentState['objects'], signal?: AbortSignal): Promise<RoomAgentState>;
}
const record = (v: unknown): v is Record<string, unknown> => v !== null && typeof v === 'object' && !Array.isArray(v);
/** Expand only explicit null imports from successful native inspections in this task.
 * parseRoomCommands and native validation still check the complete expanded draft. */
function resolveRoomProgramImports(value: unknown, observed: { hash: string; definition: unknown }[]): unknown {
  if (!record(value) || !Array.isArray(value.commands) || value.commands.length > 8 || JSON.stringify(value).length > 28000) return value;
  for (const command of value.commands) {
    if (!record(command) || command.action !== 'rules' || !record(command.rule) || !Array.isArray(command.rule.edits) || command.rule.edits.length > 16) continue;
    for (const edit of command.rule.edits) {
      if (!record(edit) || edit.kind !== 'save' || !record(edit.sequence) || typeof edit.sequence.program !== 'string') continue;
      try { edit.sequence.program = resolveProgramImportReferences(edit.sequence.program, observed); }
      catch (error) { throw new Error('Invalid behaviour request. '+(error instanceof Error?error.message:'Invalid module reference.')); }
    }
  }
  return value;
}
const validColor = (v: unknown) => record(v) && ['r','g','b'].every(k => typeof v[k] === 'number' && Number.isFinite(v[k]) && Number(v[k]) >= 0 && Number(v[k]) <= 1) && v.a === 1;
const vector = (v: unknown) => record(v) && ['x','y','z'].every(k => typeof v[k] === 'number' && Number.isFinite(v[k]) && Math.abs(v[k] as number) <= 25);
/** Fully validated commands with an invalid standalone batch shape; nothing was dispatched. */
class RoomBatchShapeError extends Error {}
class RoomExecutionShapeError extends Error {}
export function parseRoomCommands(input: unknown): RoomCommand[] {
  if (!record(input) || Object.keys(input).some(k => k !== 'commands') || !Array.isArray(input.commands) || input.commands.length > 8 || JSON.stringify(input).length > 28000) throw new Error('The room plan is invalid or too large.');
  for (const c of input.commands) {
    if (!record(c) || typeof c.action !== 'string' || !Object.prototype.hasOwnProperty.call(fields,c.action)) throw new Error('Unknown room action.');
    const action = c.action as RoomCommand['action'];
    if (Object.keys(c).some(k => k !== 'action' && !(fields[action] as readonly string[]).includes(k))) throw new Error('Unknown room action field.');
    if ((fields[action] as readonly string[]).includes('target') && (typeof c.target !== 'string' || !/^[a-zA-Z0-9_]{1,32}$/.test(c.target))) throw new Error('Invalid room target.');
    if (action === 'create' && (typeof c.reference !== 'string' || !/^[a-zA-Z0-9_]{1,32}$/.test(c.reference) || typeof c.name !== 'string' || c.name.length > 80 || /[\u0000-\u001f]/.test(c.name) || !['block','ball','cylinder','recipe','boxRobot'].includes(c.kind as string))) throw new Error('Invalid creation.');
    if ((action === 'move' || c.atPosition === true || c.position !== undefined) && !vector(c.position)) throw new Error('Invalid position.');
    if (c.partId !== undefined && (typeof c.partId !== 'string' || !/^[a-zA-Z0-9_]{1,32}$/.test(c.partId))) throw new Error('Invalid recipe part.');
    if (Object.prototype.hasOwnProperty.call(roomControlFields,action) && !validRoomControl(c)) throw new Error('Invalid room control.');
    if (action === 'avatarActivities' && !validAvatarActivityRequest(c.activities)) throw new Error('Invalid tutor-state assignment.');
    if (action === 'execution' && !validExecutionRequest(c.execution)) {
      if(record(c.execution)&&c.execution.operation==='start'&&!Object.prototype.hasOwnProperty.call(c.execution,'call')&&Object.keys(c.execution).every(k=>['operation','runId','recoveryId'].includes(k)))
        throw new RoomExecutionShapeError('Invalid action execution request: start requires call={id,version,arguments} for an inspected capability. Optional runId identifies that start; recoveryId belongs only to recover, never start. No part of this rejected plan was dispatched. Submit a complete new plan; no missing field will be guessed.');
      throw new Error('Invalid action execution request.');
    }
    if (action === 'catalog' && !validCatalogRequest(c.catalog)) throw new Error('Invalid capability query.');
    if (action === 'motions' && !validMotionQuery(c.motionQuery)) throw new Error('Invalid motion search.');
    if (action === 'rules' && !validRuleRequest(c.rule)) {
      const edits=record(c.rule)&&Array.isArray(c.rule.edits)?c.rule.edits.slice(0,16):[];
      const error=edits.flatMap(edit=>record(edit)&&record(edit.sequence)&&typeof edit.sequence.program==='string'?[parseProgram(edit.sequence.program).error]:[]).find(Boolean);
      throw new Error('Invalid behaviour request.'+(error?' '+error.slice(0,512):''));
    }
    if (action === 'workspace' && typeof c.visible !== 'boolean') throw new Error('Invalid workspace state.');
    if (c.atPosition !== undefined && typeof c.atPosition !== 'boolean') throw new Error('Invalid placement.');
    if ((action === 'resize' || c.scale !== undefined) && (typeof c.scale !== 'number' || !Number.isFinite(c.scale) || c.scale < .1 || c.scale > 4)) throw new Error('Invalid scale.');
    if ((action === 'paint' || c.color !== undefined) && !validColor(c.color)) throw new Error('Invalid colour.');
    if ((action === 'recipe' || c.kind === 'recipe') && !parseRecipe(c.recipe)) throw new Error('Missing recipe.');
  }
  if (input.commands.some(c => ['undo','redo','inspect','workspace','play','stop','rules','motions','catalog','execution','avatarActivities','physicsRun','avatarMotion'].includes(c.action)) && input.commands.length !== 1) throw new RoomBatchShapeError('This action must be submitted on its own. Submit exactly one command; no part of this rejected batch was dispatched.');
  return input.commands as unknown as RoomCommand[];
}

export const isRoomQuery=(command:RoomCommand)=>['inspect','motions','catalog'].includes(command.action)||command.action==='execution'&&(command.execution?.operation==='inspect'||command.execution?.operation==='start'&&command.execution.call.id==='room.view.capture')||command.action==='rules'&&['inspect','memory'].includes(command.rule?.action??'');
export interface RoomTaskControl {
  relatedTask?: RelatedRoomTask;
  isCurrent?:()=>boolean;
  beforePlan?:()=>Promise<void>;
  beforeDispatch?:(commands:RoomCommand[],scene:RoomAgentState)=>Promise<void>;
  signal?:AbortSignal;
  /** Called with each actual native acknowledgement, before the next model call. */
  onReceipt?:(receipt:RoomAgentState)=>void|Promise<void>;
  onSnapshot?:(image:RoomCaptureImage)=>void|Promise<void>;
}
export interface RoomTaskOperation {commands:RoomCommand[];receiptIndex:number}
export interface RoomTaskResult {operations?:RoomTaskOperation[];snapshots?:RoomCaptureImage[];receipts:RoomAgentState[];scene:RoomAgentState;budgetExhausted:boolean;relatedTask?:RelatedRoomTask;needsReview?:boolean}
const copy=<T>(value:T):T=>JSON.parse(JSON.stringify(value));

/** A bounded tool task owned by the original Maestro app. The caller supplies
 * its existing Gemini access route and conversation lifetime; Unity never owns
 * a provider client. The result can be presented even if narration later fails. */
export async function runRoomActionTask(input: Pick<TutorTextTurnInput,'model'|'prompt'|'history'|'timeoutMs'> & Partial<Pick<TutorTextTurnInput,'systemInstruction'|'currentFileParts'|'nativeLanguageCode'|'liveInputMedia'|'currentImages'>>,
  options:TutorTextTurnOptions,lease:RoomAgentLease,
  onUsage:(response:Awaited<ReturnType<typeof generateGeminiResponse>>)=>void,control:RoomTaskControl={}
):Promise<RoomTaskResult> {
  const receipts:RoomAgentState[]=[],snapshots:RoomCaptureImage[]=[],operations:RoomTaskOperation[]=[];
  // One task starts a saved program revision once. Accepted preparation/queueing
  // is already an effect; a repeated model proposal must observe it, not replay it.
  const acceptedProgramStarts=new Map<string,{target:string;revision:number;receiptIndex:number;runIds:string[]}>();
  const active=()=>{if(control.signal?.aborted||control.isCurrent?.()===false||!lease.valid())throw new DOMException('The room request was interrupted. No further actions will run.','AbortError');};
  const inspectedModules=new Map<string,unknown>();
  let queries=0,actions=0;
  let planRejection:{message:string;response:string;truncated:boolean}|undefined;
  for(let step=0;step<ROOM_TASK_LIMITS.planningCalls;step++) {
    active();await control.beforePlan?.();active();
    const scene=copy(lease.state()),budget=remainingRoomTaskBudget(step,queries,actions);
    const response=await generateGeminiResponse(input.model,buildRoomAgentPrompt(input.prompt,scene,receipts,{systemInstruction:input.systemInstruction,nativeLanguageCode:input.nativeLanguageCode,relatedTask:control.relatedTask,operations,...(planRejection?{planRejection}:{}),...(acceptedProgramStarts.size?{acceptedProgramStarts:[...acceptedProgramStarts.values()]}:{})},budget),input.history,{
      ...pickGeminiClientSource(options),systemInstruction:ROOM_AGENT_INSTRUCTION+'\n'+ROOM_PLANNER_ARGUMENT_GUIDE,currentFileParts:input.currentFileParts,
      currentImages:[...(input.currentImages??[]),...roomCaptureImages(snapshots)],
      ...(input.liveInputMedia ? {liveInputMedia:input.liveInputMedia} : {}),
      configOverrides:{responseMimeType:'application/json',responseJsonSchema:ROOM_AGENT_RESPONSE_SCHEMA},
      timeoutMs:input.timeoutMs,signal:control.signal,lifecycleHooks:{onProgress:options.lifecycleHooks?.onProgress},
    });
    onUsage(response);active();
    let commands:RoomCommand[];
    try { commands=parseRoomCommands(resolveRoomProgramImports(decodeRoomPlannerResponse(response.text||'{}'),[...inspectedModules].map(([hash,definition])=>({hash,definition})))); }
    catch(error) {
      // This is before durable intent and native dispatch. A new bounded planning
      // call may correct syntax/validation; transport/receipt errors are not caught.
      // Unsupported actions, oversized envelopes and other command-contract errors
      // remain hard failures. Syntax, rejected behaviour source and the batch shape
      // of otherwise valid commands, or a start missing its call, can be corrected
      // within the existing budget. Invalid capability calls still fail closed.
      if(!(error instanceof SyntaxError)&&!(error instanceof RoomBatchShapeError)&&!(error instanceof RoomExecutionShapeError)&&!(error instanceof Error&&error.message.startsWith('Invalid behaviour request.')))throw error;
      const raw=response.text||'';
      planRejection={message:(error instanceof Error?error.message:'Invalid room plan.').slice(0,1024),response:raw.slice(0,28000),truncated:raw.length>28000};
      continue;
    }
    if(!commands.length&&planRejection)throw new Error('The room planner stopped after an invalid plan: '+planRejection.message);
    planRejection=undefined;
    if(!commands.length)return {...(snapshots.length?{snapshots}:{}),operations:copy(operations),receipts,scene:copy(lease.state()),budgetExhausted:false,relatedTask:control.relatedTask,needsReview:!!control.relatedTask?.unconfirmed};
    // An unconfirmed earlier action is evidence of uncertainty, never permission to retry it.
    if (control.relatedTask?.unconfirmed && commands.some(command => !isRoomQuery(command)))
      return { ...(snapshots.length?{snapshots}:{}), operations:copy(operations),receipts, scene: copy(lease.state()), budgetExhausted: false, relatedTask: control.relatedTask, needsReview: true };
    const proposedStart=commands.length===1&&commands[0].action==='rules'&&commands[0].rule?.action==='play'?commands[0].rule:null;
    const startKey=proposedStart?JSON.stringify([proposedStart.target,proposedStart.revision]):null;
    if(startKey&&acceptedProgramStarts.has(startKey))commands=[{action:'rules',rule:{action:'inspect',target:proposedStart!.target}}];
    // Reconciliation above is an ordinary native read with its own real receipt,
    // recorded as a read in the task journal and charged to the query allowance.
    // A used discovery allowance must not discard the remaining action allowance,
    // and used actions must still permit observing their actual outcomes. Refuse
    // an over-budget proposal before the durable intent or any native dispatch.
    const query=commands.every(isRoomQuery);
    if((query?budget.queryBatches:budget.actionBatches)===0)break;
    requireRoomCapabilities(commands,scene);
    if(scene.capabilities?.includes('executionReceipts.v1'))commands=commands.map(c=>c.action==='execution'&&c.execution?{...c,execution:identifyExecution(c.execution,scene.execution)}:c);
    await control.beforeDispatch?.(commands,scene);active();
    // Preserve the actual dispatch, including reconciled reads and issued run IDs.
    // A post-action scene alone cannot tell the next planner what was removed.
    const dispatched=copy(commands);
    const receipt=await (control.signal
      ? lease.execute(commands,scene.sceneRevision,scene.objects,control.signal)
      : lease.execute(commands,scene.sceneRevision,scene.objects));
    operations.push({commands:dispatched,receiptIndex:receipts.length});
    receipts.push(copy(receipt));
    const moduleQuery=commands.length===1&&commands[0].action==='catalog'&&commands[0].catalog?.operation==='inspect'&&commands[0].catalog.category==='modules'?commands[0].catalog:null;
    const moduleReply=receipt.catalog;
    if(moduleQuery&&receipt.ok&&moduleReply?.operation==='inspect'&&moduleReply.category==='modules'&&moduleReply.capability===moduleQuery.capability&&moduleReply.definition)
      inspectedModules.set(moduleReply.capability,copy(moduleReply.definition));
    if(startKey&&proposedStart&&!query&&receipt.ok)acceptedProgramStarts.set(startKey,{target:proposedStart.target!,revision:proposedStart.revision!,receiptIndex:receipts.length-1,
      runIds:[...new Set([...(receipt.rules?.running??[]),...(receipt.rules?.outcomes??[])].filter(run=>run.sequenceId===proposedStart.target).map(run=>run.id))]});
    if(query)queries++;else actions++;
    // Cancellation may race an acknowledgement. Preserve that evidence before
    // checking the turn fence; never relabel a completed edit as rolled back.
    await control.onReceipt?.(copy(receipt));
    active();
    const captureCall=commands.length===1&&commands[0].action==='execution'&&commands[0].execution?.operation==='start'&&commands[0].execution.call.id==='room.view.capture'?commands[0].execution:null;
    const completed=receipt.execution?.selected;
    if(captureCall&&receipt.ok&&completed&&completed.id===captureCall.runId&&completed.phase==='completed'&&typeof completed.output?.captureId==='string'){
      if(!lease.capture)throw new Error('The room snapshot image channel is unavailable.');
      const image=await lease.capture(completed.output.captureId,control.signal);active();
      if(!validRoomCaptureMetadata(completed.output)||!validRoomCaptureImage(image)||!sameRoomCapture(image.capture,completed.output))throw new Error('The room snapshot does not match its completed capture receipt.');
      snapshots.push(image);await control.onSnapshot?.(copy(image));active();
    }
  }
  active();return {...(snapshots.length?{snapshots}:{}),operations:copy(operations),receipts,scene:copy(lease.state()),budgetExhausted:true,relatedTask:control.relatedTask};
}

/** Compatibility wrapper until room tasks enter the common tool dispatcher. */
export async function runRoomTutorTurn(input:TutorTextTurnInput,options:TutorTextTurnOptions,lease:RoomAgentLease,
  onUsage:(response:Awaited<ReturnType<typeof generateGeminiResponse>>)=>void,isCurrent:()=>boolean=()=>true) {
  const task=await runRoomActionTask(input,options,lease,onUsage,{isCurrent});
  if(!isCurrent()||!lease.valid())throw new DOMException('The room request was interrupted. No further actions will run.','AbortError');
  return runTutorTextTurn({...input,currentImages:[...(input.currentImages??[]),...roomCaptureImages(task.snapshots)],systemInstruction:input.systemInstruction+'\n\n'+buildRoomResultInstruction(task.receipts,task.scene,task.operations)},options);
}
