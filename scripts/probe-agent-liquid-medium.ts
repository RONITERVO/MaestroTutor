// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {HeadlessClient} from '../src/headless/client';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {runHeadlessRoomTurn} from '../src/headless/roomJourney';
import {capabilityDefinition} from '../shared/capabilities';
import {factReply,placementReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
type Contents={revision:number;definition:Record<string,unknown>&{amountMl:number;capacityMl:number;liquid:string;fluid:{densityKgM3:number;linearDrag:number;angularDrag:number}}};
export async function prepareAgentLiquidMedium(execute:Execute){
 const invoke=async(id:string,args:Record<string,unknown>)=>{const s=await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]);assert.equal(s.execution?.selected?.phase,'completed',JSON.stringify(s.execution?.selected));return s;};
 const create=async(name:string,x:number)=>{const s=await invoke('object.create',{...capabilityDefinition('object.create')!.example!,shape:'block',name,x,y:1,z:0});const id=s.execution!.selected!.output!.objectId as string;
  const c=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.container',version:1,arguments:{target:id}}}])).value as Contents;
  await invoke('object.container.edit',{operation:'configure',target:id,revision:c.revision,definition:{...c.definition,frame:{position:{x:0,y:.2,z:0},rotation:{x:0,y:0,z:0,w:1}},radius:.35,height:.4,capacityMl:10000,amountMl:0}});return id;};
 const pool=await create('MediumPool',3),cup=await create('MediumCup',4);
 const baseline=await mediumEnvironment(execute,[pool,cup]);return {pool,cup,baseline};
}
async function mediumEnvironment(execute:Execute,targets:string[]){
 const placements=[];for(const target of targets)placements.push(placementReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.placement',version:1,arguments:{target}}}])));
 const simulation=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'physics.simulation',version:1}}])).value as {stateId:string;running:boolean};
 assert.equal(simulation.running,false,'The fluid authoring journey must leave physics paused');
 return {placements,simulation};
}
export async function runAgentLiquidMediumProof({client,execute,directory,pool,cup,baseline}:{client:HeadlessClient;execute:Execute;directory:string;pool:string;cup:string;baseline:Awaited<ReturnType<typeof mediumEnvironment>>}){
 const fact=async(capability:string,args:Record<string,unknown>)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1,arguments:args}}])).value;
 const read=async(target:string)=>await fact('object.container',{target}) as Contents;
 assert.deepEqual(await mediumEnvironment(execute,[pool,cup]),baseline,'Filling must preserve placement and simulation state');
 const before=await read(pool),empty=await read(cup);assert.equal(before.definition.amountMl,1000);assert.equal(before.definition.fluid.densityKgM3,850);assert.ok(before.definition.fluid.linearDrag>2);assert.equal(empty.definition.amountMl,0);
 const medium=await fact('world.medium',{position:{x:3,y:1.21,z:0},target:''}) as {found:boolean;active:boolean;densityKgM3:number};assert.equal(medium.found,true);assert.equal(medium.active,false);assert.equal(medium.densityKgM3,850);
 const transferJourney=await runHeadlessRoomTurn(client,{text:'Thank you! Please move exactly 200 millilitres of that oil from MediumPool into the empty MediumCup, keeping its physical properties. Keep both vessels where they are and leave physics paused. Please keep everything else unchanged.'});
 assert.deepEqual(await mediumEnvironment(execute,[pool,cup]),baseline,'Transfer must preserve placement and simulation state');
 const source=await read(pool),destination=await read(cup);assert.equal(source.definition.amountMl,800);assert.equal(destination.definition.amountMl,200);assert.equal(source.definition.liquid,destination.definition.liquid);assert.deepEqual(source.definition.fluid,before.definition.fluid);assert.deepEqual(destination.definition.fluid,before.definition.fluid);
 const result={scenario:'LiquidMedium',phase:'passed',boundary:'Fresh real English/Spanish provider to native liquid authoring, medium query and conserved transfer. Harness created two explicit empty vessels. No physical buoyancy, scanned room or headset acceptance.',semantics:{explicitDensityAndDrag:true,mediumReadback:true,transferConserved:true,propertiesFollowLiquid:true,placementsPreserved:true,physicsRemainedPaused:true},baseline,before,medium,transferJourney,source,destination};
 await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
