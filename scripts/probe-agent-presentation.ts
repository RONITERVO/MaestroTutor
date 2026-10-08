// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {HeadlessClient} from '../src/headless/client';
import type {RoomCommand,RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {runHeadlessRoomTurn} from '../src/headless/roomJourney';
import {assertSameRoomObjects} from './agent-provider-contract';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function runAgentPresentationProof({client,initial,execute,directory,read}:{client:HeadlessClient;initial:RoomAgentState;execute:Execute;directory:string;read:()=>RoomAgentState}){
 const fact=async(capability:string,args?:Record<string,unknown>)=>{
  const reply=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1,...(args?{arguments:args}:{})}}]));
  assert.ok(reply.available&&reply.value!==null,`Native fact ${capability} unavailable: ${reply.status}`);
  return reply.value as Record<string,unknown>;
 };
 const changed=await fact('world.presentation'),physics=await fact('physics.environment');
 assert.equal(changed.backdropOpacity,.5,'The agent did not apply half backdrop opacity');
 assert.equal(changed.realDepth,false,'The agent left real occlusion enabled');
 assert.equal(changed.depthEligible,false);assertSameRoomObjects(initial,read());
 assert.equal(read().sceneRevision,initial.sceneRevision,'The visual request edited the saved scene');
 assert.equal(physics.realCollisions,true,'The visual request changed the independent default collisions');
 const restoredJourney=await runHeadlessRoomTurn(client,{text:'Please put the normal room view back, with real objects hiding virtual ones again. Leave the book, objects, and collisions unchanged.'});
 const restored=await fact('world.presentation'),restoredPhysics=await fact('physics.environment');
 assert.equal(restored.backdropOpacity,0);assert.equal(restored.realDepth,true);assert.equal(restored.depthEligible,true);
 assert.equal(restoredPhysics.realCollisions,true);assertSameRoomObjects(initial,read());assert.equal(read().sceneRevision,initial.sceneRevision);
 const beforeMovement=await fact('controller.mode');
 const movementJourney=await runHeadlessRoomTurn(client,{text:"I want to use the thumbstick to walk around the virtual world while still seeing my room. Please turn on my movement, but keep this mixed view and leave Maestro's controls and objects alone."});
 const enabled=await fact('controller.mode'),movingView=await fact('world.presentation');
 assert.equal(enabled.userEnabled,true);assert.equal(enabled.virtualView,false);assert.equal(enabled.avatarEnabled,beforeMovement.avatarEnabled);
 assert.equal(movingView.backdropOpacity,0);assert.equal(movingView.realDepth,true);assertSameRoomObjects(initial,read());assert.equal(read().sceneRevision,initial.sceneRevision);
 const stopJourney=await runHeadlessRoomTurn(client,{text:'Thanks. Please switch my thumbstick movement off again. Keep the room view and everything else as it is.'});
 const stopped=await fact('controller.mode');assert.equal(stopped.userEnabled,false);assert.equal(stopped.virtualView,false);assert.equal(stopped.avatarEnabled,beforeMovement.avatarEnabled);
 assertSameRoomObjects(initial,read());assert.equal(read().sceneRevision,initial.sceneRevision);assert.equal((await fact('physics.environment')).realCollisions,true);
 const entityBefore=await fact('object.environment',{target:'maestro'});
 const layerJourney=await runHeadlessRoomTurn(client,{text:'Could you make just Maestro half see-through and keep him visible even behind real things? Please save that for him. Keep the room view and his movement, sounds and collisions unchanged.'});
 const layerBinding=await fact('object.visibility',{target:'maestro'});
 assert.ok(layerBinding.layerId);assert.ok(Math.abs(Number(layerBinding.opacity)-.5)<1e-6);assert.equal(layerBinding.realDepth,false);
 const entityAfter=await fact('object.environment',{target:'maestro'});delete entityBefore.revision;delete entityAfter.revision;assert.deepEqual(entityAfter,entityBefore);
 const unchangedView=await fact('world.presentation');assert.equal(unchangedView.backdropOpacity,0);assert.equal(unchangedView.realDepth,true);
 const clearLayerJourney=await runHeadlessRoomTurn(client,{text:'Thanks. Please remove that visual-layer assignment from Maestro, so he is fully visible with the normal real-world hiding again. Leave everything else alone.'});
 const clearLayer=await fact('object.visibility',{target:'maestro'});assert.equal(clearLayer.layerId,'');assert.equal(clearLayer.opacity,1);assert.equal(clearLayer.realDepth,true);
 const result={scenario:'WorldPresentation',phase:'passed',boundary:'Real chat handoff and provider planning/execution against desktop Unity; no real passthrough pixels, device depth or headset acceptance.',changed,physics,restoredJourney,restored,restoredPhysics,movementJourney,enabled,movingView,stopJourney,stopped,layerJourney,layerBinding,entityAfter,clearLayerJourney,clearLayer};
 await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
