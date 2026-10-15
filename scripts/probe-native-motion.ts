// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import {startMotionObserver} from './native-probe-motion';
import {readNativeObjects} from './native-probe-read-set';
import {assertSameRoomObjects} from './agent-provider-contract';
import {assertPhysicsFlight} from './probe-agent-physics';
const pause=(ms:number)=>new Promise(resolve=>setTimeout(resolve,ms));
const distance=(a:{x:number;y:number;z:number},b:typeof a)=>Math.hypot(a.x-b.x,a.y-b.y,a.z-b.z);
/** Offline real Unity acceptance of the same passive observer used by provider
 * physics verification. Actions below are explicitly fixture-authored. */
export async function probeNativeMotion(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>,read:()=>RoomAgentState,directory:string){
 const initial=await readNativeObjects(execute),evidence:Record<string,unknown>={phase:'setup',boundary:'Real Unity, PhysX and shared user/program handlers; fixture-authored source and actions. Synthetic floor, no provider, headset or real-room alignment proof.',initial};
 let observer:Awaited<ReturnType<typeof startMotionObserver>>|undefined;const created:string[]=[];
 const save=()=>writeFile(join(directory,'native-motion-observer.json'),JSON.stringify({...evidence,samples:observer?.samples},null,2));
 try{
  assert.equal(read().physicsRunning,false);
  const ball=await execute([{action:'create',reference:'sample_ball',kind:'ball',name:'Observer physics ball',atPosition:true,position:{x:.2,y:1,z:2.5},scale:.5,color:{r:0,g:.3,b:1,a:1}}]);
  const target=ball.created[0];assert.ok(target);created.push(target);
  await execute([{action:'physicsSettings',target,physics:{mode:'solid',shape:'sphere',mass:.5}}]);
  const wall=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'recipe',name:'Observer fixed wall',x:2.2,y:.5,z:2.5,scale:1,recipe:{version:1,parts:[{id:'Wall',parent:null,shape:'box',position:{x:0,y:0,z:0},size:{x:.1,y:1,z:2},rotation:{x:0,y:0,z:0,w:1},color:{r:.5,g:.5,b:.5,a:1}}],tracks:[],duration:1,playing:false,loop:false}}}}}]);
  const wallId=wall.execution?.selected?.output?.objectId;assert.equal(typeof wallId,'string');created.push(wallId as string);
  const setup=await readNativeObjects(execute);evidence.setup=setup;
  observer=await startMotionObserver({target,execute,read});evidence.observer={source:observer.source,sequenceId:observer.sequenceId,runId:observer.runId};
  const settle=async()=>{
   const deadline=Date.now()+20000;let quietSince=Date.now(),previous=observer!.check().position;
   do{await pause(100);const sample=observer!.check();assert.ok(sample.running&&sample.simulating&&!sample.held&&!sample.animated);
    if(distance(sample.position,previous)>.001)quietSince=Date.now();previous=sample.position;
    if(Date.now()-quietSince>=1500&&sample.position.y<.07)return;
   }while(Date.now()<deadline);throw new Error('Motion fixture did not settle');
  };
  await execute([{action:'physicsRun',operation:'start'}]);
  const startDeadline=Date.now()+5000;while(!observer.check().running||!observer.check().simulating){if(Date.now()>startDeadline)throw new Error('Observer never saw running physics');await pause(50);}
  await settle();
  assert.ok(observer.samples.some(s=>s.running&&s.simulating&&s.position.y>.5)&&observer.samples.some(s=>s.running&&s.simulating&&s.position.y<.07),'Missing actual falling samples');
  const launchAt=observer.samples.length;
  const call={id:'object.physics.launch',version:2,arguments:{target,destination:{kind:'point',position:{x:1.2,y:.7,z:2.5}},seconds:.6,maxSpeed:5}};
  const trajectory=await execute([{action:'catalog',catalog:{operation:'check',call}}]);assert.equal(trajectory.catalog?.operation,'check');assert.ok(trajectory.catalog?.operation==='check'&&trajectory.catalog.available);
  const launched=await execute([{action:'execution',execution:{operation:'start',call}}]);
  const receipt=launched.execution?.selected;assert.equal(receipt?.phase,'completed');assert.equal(receipt?.output?.phase,'launched');evidence.receipt=receipt;
  await settle();
  const output=receipt!.output as {radius:number;origin:{x:number;y:number;z:number};destination:{x:number;y:number;z:number}};
  evidence.flight=assertPhysicsFlight(observer.samples.slice(launchAt),{...output,wallX:2.15});
  const settled=await readNativeObjects(execute),position=observer.check().position;
  assertSameRoomObjects({...setup,objects:setup.objects.map(o=>o.id===target?{...o,position}:o)},settled);evidence.settled=settled;
  const duplicateAt=observer.samples.length,replayed=await execute([{action:'execution',execution:{operation:'start',runId:receipt!.id,call}}]);
  assert.deepEqual(replayed.execution?.selected,receipt);await pause(1700);observer.check();
  assert.ok(observer.samples.length-duplicateAt>=5);assert.ok(observer.samples.slice(duplicateAt).every(s=>distance(s.position,position)<.006));
  await execute([{action:'physicsRun',operation:'pause'}]);await pause(200);const paused=await readNativeObjects(execute);await pause(1500);
  const last=observer.check();assert.equal(last.running,false);assert.equal(last.simulating,false);assertSameRoomObjects(paused,await readNativeObjects(execute));
  evidence.paused=paused;evidence.phase='passed';evidence.semantics={readOnlyProgramCoexistsWithPhysics:true,falling:true,ballisticFlight:true,floorAndWallCollision:true,settling:true,unchangedPhysicsAndOtherObjects:true,duplicateReceiptDoesNotRelaunch:true,pausedWithoutMotion:true};
 }catch(error){evidence.phase='failed';evidence.error=String(error);throw error;}
 finally{
  try{
   await observer?.stop();if(read().physicsRunning)await execute([{action:'physicsRun',operation:'pause'}]);
   // Let the native placement capture after pause finish before guarded deletion.
   await pause(1500);await readNativeObjects(execute);
   for(const target of created)await execute([{action:'delete',target}]);
   const final=await readNativeObjects(execute);assertSameRoomObjects(initial,final);evidence.cleaned=final;
  }catch(error){evidence.phase='failed';evidence.cleanupError=String(error);await save();throw error;}
  await save();
 }
}
