// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {capabilityDefinition} from '../shared/capabilities';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {assertSamePlacementValues} from './native-probe-contract';

export type RecoveryReceipt=NonNullable<NonNullable<RoomAgentState['execution']>['selected']>;
export interface RecoveryDriver {
 fact(id:string,args?:Record<string,unknown>):Promise<{available:boolean;value?:unknown}>;
 invoke(id:string,args:Record<string,unknown>):Promise<RecoveryReceipt>;
 reject(id:string,args:Record<string,unknown>):Promise<RoomAgentState>;
 count():Promise<number>;
 capture?(phase:string):Promise<void>;
 replay?(receipt:RecoveryReceipt):Promise<RecoveryReceipt>;
}

/** The same ordinary-client scenario runs in the headless transport and the
 * rendered book. A known synthetic floor is explicit evidence, never a claim
 * about the user's scanned room. No test-only native mutation creates a hold. */
export async function probeEnvironmentRecovery(driver:RecoveryDriver,directory:string,client:'headless'|'book'){
 const ready=JSON.parse(await readFile(join(directory,'ready.json'),'utf8'));
 assert.equal(ready.syntheticPhysics,true,'Recovery scenario requires the declared synthetic floor');
 const evidence:Record<string,unknown>={client,boundary:'Ordinary client actions against actual desktop Unity restoration and collision checks. Synthetic floor only; no real scan, headset or provider proof.'};
 const checkpoint=async(phase:string)=>{evidence.phase=phase;await writeFile(join(directory,client+'-environment-recovery.json'),JSON.stringify(evidence,null,2));await driver.capture?.(phase);};
 const fact=async(id:string,args?:Record<string,unknown>)=>{const result=await driver.fact(id,args);assert.equal(result.available,true,id);assert.ok(result.value&&typeof result.value==='object'&&!Array.isArray(result.value));return result.value as Record<string,unknown>;};
 const invoke=driver.invoke,initialCount=await driver.count();
 const environment=await fact('physics.environment'),simulation=await fact('physics.simulation');
 assert.equal(environment.realCollisions,true);assert.equal(environment.scanReady,true);assert.equal(simulation.running,false);
 const viewer=await fact('world.viewpoint');assert.equal(viewer.located,true);
 const position=viewer.position as {x:number;y:number;z:number},yaw=Number(viewer.yaw)*Math.PI/180;
 const obstructed={x:position.x-Math.sin(yaw)*6,y:-.08,z:position.z-Math.cos(yaw)*6};
 const clear={x:position.x+Math.sin(yaw)*2,y:1.2,z:position.z+Math.cos(yaw)*2};
 for(const point of [obstructed,clear])assert.ok(Math.abs(point.x)<9&&Math.abs(point.z)<9,'Placement must stay inside the declared 20 m synthetic floor');
 const created=await invoke('object.create',{...capabilityDefinition('object.create')!.example!,name:'Restoration recovery proof',...obstructed,scale:1});
 const target=String(created.output!.objectId);
 const area=await invoke('world.region.save',{id:'',revision:0,name:'Recovery proof area',members:[],loading:'independent'}),areaId=String(area.output!.id);
 await invoke('object.region.assign',{target,revision:(await fact('object.region',{target})).revision,regionId:areaId,regionRevision:area.output!.revision});
 const definition=await fact('object.definition',{target});
 let dormant=await fact('world.region.retention',{id:areaId});const deadline=Date.now()+25000;
 while(dormant.retirementState!=='dormant'&&Date.now()<deadline){await new Promise(r=>setTimeout(r,200));dormant=await fact('world.region.retention',{id:areaId});}
 assert.equal(dormant.retirementState,'dormant',JSON.stringify(dormant));assert.equal(dormant.residentCount,0);
 const unavailable=await driver.fact('object.environment.recovery',{target});assert.equal(unavailable.available,false);
 assert.equal((await fact('object.presence',{target})).nativeInstance,false,'Passive recovery inspection loaded an object');
 assert.deepEqual(await fact('object.definition',{target}),definition,'Retirement or passive reads changed saved content');
 Object.assign(evidence,{target,areaId,initialCount,environment,simulation,obstructed,clear,dormant,unavailable});await checkpoint('dormant');

 // Saved placement can intentionally stay unloaded. A visible member of a
 // together area is ordinary native demand for the same obstructed saved peer.
 const anchorCreated=await invoke('object.create',{...capabilityDefinition('object.create')!.example!,name:'Recovery area companion',...clear,x:clear.x+.7,scale:.2});
 const anchor=String(anchorCreated.output!.objectId);
 await invoke('object.region.assign',{target:anchor,revision:(await fact('object.region',{target:anchor})).revision,regionId:areaId,regionRevision:(await fact('world.region',{id:areaId})).revision});
 const coupled=await invoke('world.region.save',{id:areaId,revision:(await fact('world.region',{id:areaId})).revision,name:'Recovery proof area',members:[target,anchor].sort(),loading:'together'});
 let presence=await fact('object.presence',{target});const restoreDeadline=Date.now()+18000;
 while(!presence.active&&Date.now()<restoreDeadline){await new Promise(r=>setTimeout(r,200));presence=await fact('object.presence',{target});}
 assert.equal(presence.active,true,JSON.stringify(presence));Object.assign(evidence,{anchor,coupled,presence});
 const held=await fact('object.environment.recovery',{target});assert.equal(held.held,true);assert.ok(held.reason);
 const placement=await fact('object.placement',{target});
 assert.deepEqual(await fact('object.environment.recovery',{target}),held,'Passive inspection retried the hold');
 const args=(value:Record<string,unknown>)=>({target,revision:value.revision,stateId:value.stateId});
 const blocked=await invoke('object.environment.recheck',args(held));assert.equal(blocked.output!.held,true,'A completed check must still report a blocked object');
 const blockedFact=await fact('object.environment.recovery',{target});assert.deepEqual(blocked.output,blockedFact);assert.notEqual(blockedFact.stateId,held.stateId);
 assertSamePlacementValues(placement,await fact('object.placement',{target}),'Blocked retry moved the object');
 const staleState=await driver.reject('object.environment.recheck',args(held));assert.equal(staleState.ok,false);assert.match(staleState.status,/changed|again/i);
 assert.deepEqual(await fact('object.environment.recovery',{target}),blockedFact,'Stale runtime state changed recovery');
 Object.assign(evidence,{held,blocked,blockedFact,staleState:{ok:staleState.ok,status:staleState.status}});await checkpoint('blocked');

 await invoke('object.position.set',{target,...clear});
 const moved=await fact('object.environment.recovery',{target});assert.equal(moved.held,true,'Moving or readiness must not silently retry a failed hold');assert.notEqual(moved.revision,blockedFact.revision);
 const staleRevision=await driver.reject('object.environment.recheck',{...args(moved),revision:blockedFact.revision});assert.equal(staleRevision.ok,false);assert.match(staleRevision.status,/changed|again/i);
 assert.deepEqual(await fact('object.environment.recovery',{target}),moved,'Stale saved revision changed recovery');
 const before=await fact('object.placement',{target}),recovered=await invoke('object.environment.recheck',args(moved));
 assert.equal(recovered.output!.held,false,JSON.stringify(recovered));assert.equal(recovered.output!.reason,'');assert.equal(recovered.output!.revision,moved.revision);
 const after=await fact('object.placement',{target});assertSamePlacementValues(before,after,'Recovery moved the saved pose');
 assert.deepEqual(await fact('object.environment.recovery',{target}),recovered.output);
 const alreadyClear=await invoke('object.environment.recheck',args(recovered.output!));assert.deepEqual(alreadyClear.output,recovered.output,'Already-clear check changed recovery state');
 let replay:RecoveryReceipt|undefined;
 if(driver.replay){replay=await driver.replay(recovered);assert.equal(replay.id,recovered.id);assert.deepEqual(replay.output,recovered.output);assert.deepEqual(await fact('object.environment.recovery',{target}),recovered.output,'Duplicate receipt performed another recovery');}
 const afterSimulation=await fact('physics.simulation');assert.equal(afterSimulation.running,false,'Recovery enabled room physics');
 assert.deepEqual(await fact('object.environment.recovery',{target}),recovered.output);
 Object.assign(evidence,{moved,staleRevision:{ok:staleRevision.ok,status:staleRevision.status},before,recovered,after,alreadyClear,replay:replay??null,afterSimulation});await checkpoint('recovered');
 await invoke('object.delete',{target});
 await invoke('object.delete',{target:anchor});
 const emptyArea=await fact('world.region',{id:areaId});assert.equal(emptyArea.memberCount,0);
 await invoke('world.region.remove',{id:areaId,revision:emptyArea.revision});
 assert.equal(await driver.count(),initialCount,'Recovery proof left a saved object behind');
 evidence.cleanup=true;await checkpoint('passed');
}
