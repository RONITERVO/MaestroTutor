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
 const fact=async(capability:string)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1}}])).value as Record<string,unknown>;
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
 const result={scenario:'WorldPresentation',phase:'passed',boundary:'Real chat handoff and provider planning/execution against desktop Unity; no real passthrough pixels, device depth or headset acceptance.',changed,physics,restoredJourney,restored,restoredPhysics,movementJourney,enabled,movingView,stopJourney,stopped};
 await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
