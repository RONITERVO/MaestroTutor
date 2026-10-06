// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {performance} from 'node:perf_hooks';
import {isDeepStrictEqual} from 'node:util';
import type {HeadlessClient} from '../src/headless/client';
import {runHeadlessRoomTurn} from '../src/headless/roomJourney';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import type {CapabilityInvocation} from '../shared/capabilities';
import {assertSameRoomObjects} from './agent-provider-contract';

export interface PhysicsSample {time:number;revision:number;running:boolean;simulating:boolean;position:{x:number;y:number;z:number}}
const pause=(ms:number)=>new Promise(resolve=>setTimeout(resolve,ms));
const distance=(a:PhysicsSample['position'],b:PhysicsSample['position'])=>Math.hypot(a.x-b.x,a.y-b.y,a.z-b.z);
export function assertPhysicsFlight(samples:PhysicsSample[],expected:{radius:number;origin:PhysicsSample['position'];destination:PhysicsSample['position'];wallX:number}) {
 assert.ok(samples.length>=8,'Too few native motion observations');
 assert.ok(expected.radius>0&&expected.radius<.1,'Unexpected probe ball radius');
 for(const [index,sample] of samples.entries()){
  assert.ok(Number.isFinite(sample.time)&&(!index||sample.time>samples[index-1].time),'Non-monotonic native observations');
  assert.ok(Object.values(sample.position).every(Number.isFinite)&&sample.running&&sample.simulating,'Physics was not continuously running');
  assert.ok(sample.position.y>=expected.radius-.02,'The ball penetrated the synthetic floor');
  assert.ok(sample.position.x<=expected.wallX-expected.radius+.025,'The ball penetrated the fixed wall');
 }
 assert.ok(samples.some(s=>Math.abs(s.position.x-(expected.wallX-expected.radius))<.06),'No observed wall contact');
 const ys=samples.map(s=>s.position.y),peak=Math.max(...ys),apex=ys.indexOf(peak),tail=samples.at(-1)!;
 assert.ok(apex>0&&apex<samples.length-2&&peak>expected.origin.y+.3,'No observed ballistic rise and fall');
 assert.ok(samples.some(s=>distance(s.position,expected.destination)<.2),'The actual ball never approached the requested destination');
 assert.ok(tail.position.y<expected.radius+.03&&peak-tail.position.y>.3,'The ball did not return to the floor');
 const quiet=samples.filter(s=>tail.time-s.time<=1000);
 assert.ok(quiet.length>=5&&quiet.every(s=>distance(s.position,tail.position)<.006),'The ball did not settle on the floor');
 return {samples:samples.length,peakHeight:peak,minimumHeight:Math.min(...ys),finalHeight:tail.position.y,ballisticRiseAndFall:true,destinationApproached:true,floorCollisionAndSettling:true,wallCollision:true};
}
function preserve(before:RoomAgentState,after:RoomAgentState,target:string){
 const original=before.objects.find(o=>o.id===target),current=after.objects.find(o=>o.id===target);assert.ok(original&&current);
 assertSameRoomObjects({...before,objects:before.objects.map(o=>o.id===target?{...o,position:current.position}:o)},after);
 assert.equal(current.held,false);assert.equal(current.animated,false);
}
export async function runAgentPhysicsProof(input:{client:HeadlessClient;before:RoomAgentState;target:string;directory:string;
 execute:(commands:RoomCommand[])=>Promise<RoomAgentState>;read:()=>RoomAgentState}){
 const {client,before,target,directory,execute,read}=input;
 const ready=JSON.parse(await readFile(join(directory,'ready.json'),'utf8'));assert.equal(ready.syntheticPhysics,true,'This proof requires the explicitly synthetic native floor');
 const evidence:Record<string,unknown>={phase:'setup',boundary:'Real provider, shared agent/native handlers and PhysX with a probe-only synthetic floor and a fixed wall authored by the fixture. Positions are native live transforms. No real scan, headset input, wall alignment or physical comfort proof.'};
 const samples:PhysicsSample[]=[];let samplingError:string|undefined,lastRevision=-1;
 const save=()=>writeFile(join(directory,'provider-physics.json'),JSON.stringify({...evidence,samples,samplingError},null,2));
 // Explicit fixture setup through ordinary user-edit handlers. The provider still
 // performs simulation start, trajectory discovery/launch and subsequent pause.
 await execute([{action:'move',target,position:{x:2,y:1,z:.6}}]);
 await execute([{action:'physicsSettings',target,physics:{mode:'solid',shape:'sphere',mass:.5}}]);
 const setup=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{
  kind:'recipe',name:'Probe-only fixed wall',x:4,y:.5,z:.6,scale:1,recipe:{version:1,parts:[{id:'Wall',parent:null,shape:'box',
   position:{x:0,y:0,z:0},size:{x:.1,y:1,z:2},rotation:{x:0,y:0,z:0,w:1},color:{r:.5,g:.5,b:.5,a:1}}],
   tracks:[],duration:1,playing:false,loop:false}}}}}]);
 assert.equal(setup.objects.find(o=>o.id===setup.execution?.selected?.output?.objectId)?.physics?.mode,'fixed');
 evidence.setup=setup;assert.equal(setup.physicsRunning,false);
 const sample=()=>{try{const state=read(),object=state.objects.find(o=>o.id===target);assert.ok(object);if(state.revision===lastRevision)return;lastRevision=state.revision;
  if(samples.length>=12000)throw new Error('Native physics observation budget exceeded');
  samples.push({time:performance.now(),revision:state.revision,running:state.physicsRunning,simulating:object.simulating===true,position:{...object.position}});
 }catch(error){samplingError=String(error);}};
 sample();const timer=setInterval(sample,50);
 const waitSettled=async()=>{
  const deadline=Date.now()+20000;let quietSince=Date.now(),previous=read().objects.find(o=>o.id===target)!.position;
  do{await pause(100);const current=read().objects.find(o=>o.id===target)!;assert.ok(current);
   if(distance(previous,current.position)>.001)quietSince=Date.now();previous=current.position;
   if(Date.now()-quietSince>=1500&&current.position.y<.07)return structuredClone(read());
  }while(Date.now()<deadline);throw new Error('Native physical ball did not settle within 20 seconds');
 };
 const requireRecord=async(id:string|undefined)=>{assert.ok(id);const record=await client.roomAgent!.store.get(id);assert.ok(record);return record;};
 try{
  evidence.phase='starting';await save();
  const start=await runHeadlessRoomTurn(client,{text:'Ask the room agent to inspect the current room physics readiness and start gravity and room physics if ready. The existing ParityBall is ready for this test. Do not change objects, create anything, throw anything or add programs. Leave physics running.'});
  evidence.startJourney=start;await save();
  assert.equal(read().physicsRunning,true);
  const settled=await waitSettled();preserve(setup,settled,target);evidence.afterDrop=settled;
  const falling=samples.filter(s=>s.running&&s.simulating);
  assert.ok(falling.length>=4&&falling.some(s=>s.position.y>.5)&&falling.some(s=>s.position.y<.07),'The native ball was not observed falling to the floor');
  const launchStart=samples.length;
  evidence.phase='launching';await save();
  const launch=await runHeadlessRoomTurn(client,{text:'Ask the room agent to throw the existing ParityBall once toward the world point x=3, y=0.7, z=0.6, using a flight time of 0.6 seconds and maximum speed 5 metres per second. Inspect the current trajectory readiness before launching. This is a physical throw, not a teleport, animation or saved position edit. Keep its identity, colour, scale and physics settings. Do not add objects, programs or buttons. Leave physics running. Report what the native result actually confirms; a launch receipt alone does not prove arrival.'});
  evidence.launchJourney=launch;await save();
  const record=await requireRecord(launch.task?.id),commands=record.operations.flatMap(op=>op.commands);
  const launches=commands.filter(c=>c.action==='execution'&&c.execution?.operation==='start'&&c.execution.call?.id==='object.physics.launch');
  assert.equal(launches.length,1,'Expected exactly one native launch, without retries');
  assert.ok(commands.some(c=>c.action==='catalog'&&(c.catalog?.operation==='inspect'&&c.catalog.capability==='object.physics.trajectory'||c.catalog?.operation==='check'&&c.catalog.call?.id==='object.physics.launch')),'Provider skipped trajectory discovery');
  assert.ok(commands.every(c=>c.action==='catalog'||c.action==='execution'&&c.execution?.operation==='inspect'||launches.includes(c)),'Throw task made an unrequested change');
  const command=launches[0];assert.equal(command.execution?.operation,'start');if(command.execution?.operation!=='start')throw new Error('Missing launch command');
  const call=command.execution.call as CapabilityInvocation,op=record.operations.find(op=>op.commands.includes(command))!;
  const receipt=op.receipt?.execution?.selected;assert.ok(receipt?.phase==='completed'&&receipt.output?.phase==='launched');
  const output=receipt.output as {radius:number;origin:PhysicsSample['position'];destination:PhysicsSample['position'];seconds:number;speed:number};
  assert.equal(call.arguments.target,target);assert.deepEqual(call.arguments.destination,{kind:'point',position:{x:3,y:.7,z:.6}});
  assert.equal(call.arguments.seconds,.6);assert.equal(call.arguments.maxSpeed,5);assert.ok(output.seconds>=.6&&output.seconds<=.65&&output.speed<=5);
  const afterLaunch=await waitSettled();preserve(setup,afterLaunch,target);evidence.afterLaunch=afterLaunch;evidence.launchReceipt=receipt;
  // State delivery can lag the instant launch by one sample. Include the native
  // floor baseline and all actual movement, not an inferred trajectory.
  const movement=samples.slice(launchStart).filter(s=>s.running&&s.simulating);
  evidence.flight=assertPhysicsFlight(movement,{...output,wallX:3.95});
  const replayAt=samples.length;
  const replay=await execute([{action:'execution',execution:{operation:'start',runId:receipt.id,call}}]);await pause(1700);
  assert.ok(isDeepStrictEqual(replay.execution?.selected,receipt),'Duplicate launch did not return the original receipt');
  assert.ok(samples.slice(replayAt).every(s=>distance(s.position,afterLaunch.objects.find(o=>o.id===target)!.position)<.006),'Duplicate receipt relaunched the ball');
  evidence.replayedReceipt=replay.execution?.selected;
  evidence.phase='pausing';await save();
  evidence.pauseJourney=await runHeadlessRoomTurn(client,{text:'Ask the room agent to pause room physics now, using its current simulation state. Keep all objects and their saved settings. Do not move, undo, delete, create or restart anything.'});
  assert.equal(read().physicsRunning,false);const paused=structuredClone(read());assert.equal(paused.objects.find(o=>o.id===target)?.simulating,false);
  await pause(1500);const afterPause=structuredClone(read());preserve(setup,afterPause,target);assertSameRoomObjects(paused,afterPause);
  evidence.paused=paused;evidence.afterPause=afterPause;
  assert.equal(read().rules?.sequences.length,before.rules?.sequences.length);assert.equal(read().rules?.buttons.length,before.rules?.buttons.length);
  assert.equal(samplingError,undefined);
  evidence.phase='passed';evidence.semantics={agentStartsPhysics:true,actualGravity:true,agentInspectsTrajectory:true,exactSingleNativeLaunch:true,actualBallisticFlight:true,floorCollisionAndSettling:true,wallCollision:true,unchangedIdentityAndSettings:true,unrelatedObjectsPreserved:true,duplicateReceiptDoesNotRelaunch:true,agentPausesPhysics:true,noMotionAfterPause:true};
 }catch(error){evidence.phase='failed';evidence.error=String(error);throw error;}
 finally{clearInterval(timer);sample();await save();}
 return evidence;
}
