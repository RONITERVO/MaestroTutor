// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {HeadlessClient} from '../src/headless/client';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {runHeadlessRoomTurn} from '../src/headless/roomJourney';
import {capabilityDefinition} from '../shared/capabilities';
import {factReply,type NativeProbeState} from './native-probe-contract';
import {readWaterActorPose} from './probe-water-traversal';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
type Settings={target:string;revision:number;mode:string;effectiveMode:string;maxDepthMetres:number;temporary:boolean};
async function preservedState(execute:Execute,robot:string){
 const poses=[];for(const target of ['maestro',robot]){
  const pose=await readWaterActorPose(execute,target);
  const environment=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.environment',version:1,arguments:{target}}}])).value as Record<string,unknown>;
  const settings=Object.fromEntries(Object.entries(environment).filter(([key])=>key!=='revision'));
  poses.push({...pose,environment:settings});
 }
 const simulation=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'physics.simulation',version:1}}])).value;
 return {poses,simulation};
}
export async function prepareAgentWaterTraversal(execute:Execute){
 const created=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...capabilityDefinition('object.create')!.example!,shape:'block',name:'WaterRobot',x:3,y:1,z:0}}}}]);
 assert.equal(created.execution?.selected?.phase,'completed');const robot=created.execution!.selected!.output!.objectId as string;
 return {robot,baseline:await preservedState(execute,robot)};
}
export async function runAgentWaterTraversalProof({client,execute,directory,robot,baseline}:{client:HeadlessClient;execute:Execute;directory:string;robot:string;baseline:Awaited<ReturnType<typeof preservedState>>}){
 const read=async(target:string)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.water.traversal.settings',version:1,arguments:{target}}}])).value as Settings;
 const maestro=await read('maestro'),other=await read(robot);
 assert.equal(maestro.mode,'wade');assert.equal(maestro.maxDepthMetres,.15);assert.equal(other.effectiveMode,'avoid');
 assert.deepEqual(await preservedState(execute,robot),baseline,'Water policies must preserve placement, collision environments and simulation state');
 const second=await runHeadlessRoomTurn(client,{text:'Actually, please have Maestro avoid water entirely for now. Keep WaterRobot out of water too. Remember the 15 centimetre limit I chose for Maestro in case I switch back to wading. Leave everything in place and do not change any other settings.'});
 const after=await read('maestro'),robotAfter=await read(robot);
 assert.equal(after.effectiveMode,'avoid');assert.equal(after.maxDepthMetres,.15);assert.equal(robotAfter.mode,other.mode);assert.equal(robotAfter.maxDepthMetres,other.maxDepthMetres);
 assert.deepEqual(await preservedState(execute,robot),baseline);
 const result={scenario:'WaterTraversal',phase:'passed',boundary:'Fresh English/Spanish real-provider turns through shared native settings. Harness creates a named box stand-in; no walking, swimming, camera, scanned room or headset acceptance is claimed.',semantics:{independentActorPolicy:true,preserveUnrequestedDepth:true,placementsAndCollisionPoliciesPreserved:true},maestro,other,second,after,robotAfter};
 await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
