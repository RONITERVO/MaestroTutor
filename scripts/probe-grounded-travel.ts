// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {completeProbeAction} from './native-probe-contract';
import assert from 'node:assert/strict';
import {nativeProbeObjects} from './native-probe-objects';
import {createHash} from 'node:crypto';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
export async function probeGroundedTravel(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>,directory:string){
 const objects=nativeProbeObjects(execute);
 const hash=createHash('sha256').update(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/robot.json')).digest('hex');
 const created=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'template',templateHash:hash,name:'Walking robot',x:-3,y:0,z:-3,scale:1}}}}]));
 const target=created.execution?.selected?.output?.objectId;assert.equal(typeof target,'string');if(typeof target!=='string')throw new Error('Missing walking robot');
 const obstruction=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'template',templateHash:hash,name:'Walking obstacle',x:-2,y:0,z:-3,scale:1}}}}]));
 const obstacle=obstruction.execution?.selected?.output?.objectId;if(typeof obstacle!=='string')throw new Error('Missing walking obstacle');
 const definition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.navigate',version:1}}]);
 const call={id:'object.navigate',version:1,arguments:{target,mode:'walk',destination:{x:-1,y:0,z:-3},speed:.65}};
 const check=await execute([{action:'catalog',catalog:{operation:'check',call}}]);assert.ok(check.catalog?.operation==='check');assert.equal(check.catalog.available,true,JSON.stringify(check.catalog));
 let state=await execute([{action:'execution',execution:{operation:'start',call}}]);const runId=state.execution?.selected?.id;assert.ok(runId);assert.equal(state.execution?.selected?.phase,'preparing');
 const samples=[(await objects.observation(target)).position],deadline=Date.now()+30000;
 while(state.execution?.selected?.phase==='preparing'&&Date.now()<deadline){await new Promise(r=>setTimeout(r,80));state=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);samples.push((await objects.observation(target)).position);}
 const receipt=state.execution?.selected,position=(await objects.observation(target)).position;
 assert.equal(receipt?.phase,'completed',JSON.stringify(receipt));assert.ok(Math.abs(position.x+1)<.02&&Math.abs(position.y)<.02&&Math.abs(position.z+3)<.02);
 assert.ok(samples.some(p=>p.x>-2.95&&p.x<-2.45),'No intermediate walking position');assert.ok(samples.some(p=>Math.abs(p.z+3)>.2),'No observed obstacle detour');assert.ok(Number(receipt.output?.distance)>2);
 const replay=await execute([{action:'execution',execution:{operation:'start',runId,call}}]);assert.deepEqual(replay.execution?.selected,receipt);assert.equal(replay.rules?.running?.some(r=>r.id===runId),false);
 await writeFile(join(directory,'native-grounded-travel.json'),JSON.stringify({boundary:'Shared headless catalog/readiness/action/receipt calls against actual Unity on an explicit synthetic floor. Gradual positions, lateral obstacle detour and arrival are observed; replay does not start again. No provider or headset proof.',created,obstruction,definition,check,samples,receipt,replay:replay.execution?.selected},null,2));
 return {target,obstacle};
}

export async function probeGroundedFollowing(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>,directory:string,targets:{target:string;obstacle:string}){
 const objects=nativeProbeObjects(execute);
 const target=targets.target,leader=targets.obstacle;
 const call={id:'object.follow',version:1,arguments:{target,leader:{kind:'object',objectId:leader},distance:1,speed:.9,seconds:10}};
 const check=await execute([{action:'catalog',catalog:{operation:'check',call}}]);assert.ok(check.catalog?.operation==='check');assert.equal(check.catalog.available,true,JSON.stringify(check.catalog));
 const started=await execute([{action:'execution',execution:{operation:'start',call}}]);const runId=started.execution?.selected?.id;assert.ok(runId);assert.equal(started.execution?.selected?.phase,'running');
 const observed=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.navigation',version:1,arguments:{target}}}]);assert.ok(observed.catalog?.operation==='inspect'&&observed.catalog.category==='facts');assert.equal((observed.catalog.value as {active:boolean})?.active,true);
 const leaderCall={id:'object.navigate',version:1,arguments:{target:leader,mode:'walk',destination:{x:-4,y:0,z:-1},speed:.65}};
 const leaderStarted=await execute([{action:'execution',execution:{operation:'start',call:leaderCall}}]);const leaderRun=leaderStarted.execution?.selected?.id;assert.ok(leaderRun);
 let following=started,leading=leaderStarted;const samples:unknown[]=[],deadline=Date.now()+30000;
 while(Date.now()<deadline){
  following=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);leading=await execute([{action:'execution',execution:{operation:'inspect',runId:leaderRun}}]);
  samples.push({follower:(await objects.observation(target)).position,leader:(await objects.observation(leader)).position});
  if(following.execution?.selected?.phase==='completed'&&leading.execution?.selected?.phase==='completed')break;
  assert.ok(!['failed','cancelled'].includes(following.execution?.selected?.phase??''),JSON.stringify(following.execution?.selected));assert.ok(!['failed','cancelled'].includes(leading.execution?.selected?.phase??''),JSON.stringify(leading.execution?.selected));
  await new Promise(r=>setTimeout(r,100));
 }
 const receipt=following.execution?.selected,leaderReceipt=leading.execution?.selected;assert.equal(receipt?.phase,'completed',JSON.stringify(receipt));assert.equal(leaderReceipt?.phase,'completed',JSON.stringify(leaderReceipt));
 assert.ok(Number(receipt.output?.distance)>1);assert.equal(receipt.output?.state,'keepingDistance');
 const a=(await objects.observation(target)).position,b=(await objects.observation(leader)).position;const gap=Math.hypot(a.x-b.x,a.z-b.z);assert.ok(gap>=.95&&gap<=1.12,`Follow gap ${gap}`);
 const inactive=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.navigation',version:1,arguments:{target}}}]);assert.ok(inactive.catalog?.operation==='inspect'&&inactive.catalog.category==='facts');assert.equal((inactive.catalog.value as {active:boolean})?.active,false);
 const replay=await execute([{action:'execution',execution:{operation:'start',runId,call}}]);assert.deepEqual(replay.execution?.selected,receipt);
 await writeFile(join(directory,'native-grounded-following.json'),JSON.stringify({boundary:'Shared headless readiness, timed follow, independent native leader walking, passive status and terminal replay on synthetic ground. The moving leader is not owned by following. No provider or headset acceptance.',check,started:started.execution?.selected,observed:observed.catalog,samples,receipt,leaderReceipt,gap,inactive:inactive.catalog,replay:replay.execution?.selected},null,2));
}
