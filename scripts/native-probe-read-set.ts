// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {isDeepStrictEqual} from 'node:util';
import type {RoomAgentState,RoomCommand,RoomObjectObservation} from '../src/core-sdk/room/roomAgent';
import {validRoomObjectObservation} from '../src/core-sdk/room/roomObjectContext';
import {validObjectPhysics,type ObjectPhysicsSettings} from '../shared/roomControls';
import {validFactValue} from '../shared/behaviourFacts';
import {checkedProbeReply,factReply} from './native-probe-contract';
import {nativeProbeObjects} from './native-probe-objects';

export interface NativeObjectReadSet {
 source:'explicit-native-reads';session:string;worldId:string;sceneRevision:number;temporaryId:string|null;temporary:boolean;
 objects:(RoomObjectObservation&{physics:ObjectPhysicsSettings;objectRevision:number;physicsEvidence:'component-fact'|'native-inspection'})[];
}
/** Separate evidence, never synthetic RoomAgentState.objects. Saved revisions and
 * scope must stay stable throughout the read. Live positions are sequential
 * readings: callers must settle moving actors before asserting room parity. */
export async function readNativeObjects(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>):Promise<NativeObjectReadSet>{
 let scope:Pick<NativeObjectReadSet,'session'|'sceneRevision'|'temporaryId'|'temporary'>|undefined,worldId='';
 const checked=async(commands:RoomCommand[])=>{
  const state=checkedProbeReply(commands,await execute(commands));
  if(!state.ok||state.workspaceReady===false||state.temporaryRoom?.pending)throw new Error('Explicit native read is unavailable');
  const current={session:state.session,sceneRevision:state.sceneRevision,temporaryId:state.temporaryRoom?.id??null,temporary:state.temporaryRoom?.active??false};
  if(scope&&!isDeepStrictEqual(scope,current))throw new Error('Room scope or saved revision changed during explicit reads');scope??=current;
  const view=state.catalog;if(commands[0]?.action==='catalog'&&view?.operation==='inspect'&&view.category==='facts'&&view.capability==='world.objects'&&view.available){
   const id=(view.value as {worldId?:unknown})?.worldId;
   if(typeof id!=='string'||worldId&&worldId!==id)throw new Error('World changed during explicit reads');worldId=id;
  }
  return state;
 };
 const native=nativeProbeObjects(checked),ids=await native.ids(),objects:NativeObjectReadSet['objects']=[];
 for(const target of ids){
  const observed=await native.observation(target);let physics:ObjectPhysicsSettings,physicsEvidence:NativeObjectReadSet['objects'][number]['physicsEvidence'];
  if(target==='book'||target==='maestro'){
   // The editable-physics component deliberately excludes built-ins. Read their
   // actual native inspection, never invent fixed/default settings or omit them.
   const state=await checked([{action:'inspect',target}]),item=state.inspection?.item;
   if(state.inspection?.id!==target||state.inspection.objectRevision!==observed.objectRevision||!validRoomObjectObservation(item)||item.id!==target||item.objectRevision!==observed.objectRevision||!validObjectPhysics(item.physics))throw new Error('Missing exact built-in physics inspection');
   physics=item.physics;physicsEvidence='native-inspection';
  }else{
   const state=await checked([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.physics.settings',version:1,arguments:{target}}}]),view=factReply(state);
   if(!view.available||!validFactValue('object.physics.settings',view.value))throw new Error('Missing explicit object physics settings');
   const settings=view.value as {target:string;revision:number;temporary:boolean;mode:ObjectPhysicsSettings['mode'];shape:ObjectPhysicsSettings['shape'];mass:number};
   physics={mode:settings.mode,shape:settings.shape,mass:settings.mass};physicsEvidence='component-fact';
   if(settings.target!==target||settings.revision!==observed.objectRevision||settings.temporary!==scope!.temporary||!validObjectPhysics(physics))throw new Error('Object physics read changed target, revision or room');
  }
  objects.push({...observed,physics:structuredClone(physics),physicsEvidence});
 }
 if(!isDeepStrictEqual(ids,await native.ids()))throw new Error('Saved-object membership changed during explicit reads');
 if(!scope||!worldId)throw new Error('Missing explicit native read scope');
 return {source:'explicit-native-reads',...scope,worldId,objects};
}
