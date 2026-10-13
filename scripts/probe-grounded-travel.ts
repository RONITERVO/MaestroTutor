// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
export async function probeGroundedTravel(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>,directory:string){
 const hash=createHash('sha256').update(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/robot.json')).digest('hex');
 const created=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'template',templateHash:hash,name:'Walking robot',x:-3,y:0,z:-3,scale:1}}}}]);
 const target=created.execution?.selected?.output?.objectId;assert.equal(typeof target,'string');if(typeof target!=='string')throw new Error('Missing walking robot');
 const obstruction=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'template',templateHash:hash,name:'Walking obstacle',x:-2,y:0,z:-3,scale:1}}}}]);
 const obstacle=obstruction.execution?.selected?.output?.objectId;if(typeof obstacle!=='string')throw new Error('Missing walking obstacle');
 const definition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'object.navigate',version:1}}]);
 const call={id:'object.navigate',version:1,arguments:{target,mode:'walk',destination:{x:-1,y:0,z:-3},speed:.65}};
 const check=await execute([{action:'catalog',catalog:{operation:'check',call}}]);assert.ok(check.catalog?.operation==='check');assert.equal(check.catalog.available,true,JSON.stringify(check.catalog));
 let state=await execute([{action:'execution',execution:{operation:'start',call}}]);const runId=state.execution?.selected?.id;assert.ok(runId);assert.equal(state.execution?.selected?.phase,'preparing');
 const samples=[state.objects.find(o=>o.id===target)!.position],deadline=Date.now()+30000;
 while(state.execution?.selected?.phase==='preparing'&&Date.now()<deadline){await new Promise(r=>setTimeout(r,80));state=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);samples.push(state.objects.find(o=>o.id===target)!.position);}
 const receipt=state.execution?.selected,position=state.objects.find(o=>o.id===target)!.position;
 assert.equal(receipt?.phase,'completed',JSON.stringify(receipt));assert.ok(Math.abs(position.x+1)<.02&&Math.abs(position.y)<.02&&Math.abs(position.z+3)<.02);
 assert.ok(samples.some(p=>p.x>-2.95&&p.x<-2.45),'No intermediate walking position');assert.ok(samples.some(p=>Math.abs(p.z+3)>.2),'No observed obstacle detour');assert.ok(Number(receipt.output?.distance)>2);
 const replay=await execute([{action:'execution',execution:{operation:'start',runId,call}}]);assert.deepEqual(replay.execution?.selected,receipt);assert.equal(replay.rules?.running?.some(r=>r.id===runId),false);
 await writeFile(join(directory,'native-grounded-travel.json'),JSON.stringify({boundary:'Shared headless catalog/readiness/action/receipt calls against actual Unity on an explicit synthetic floor. Gradual positions, lateral obstacle detour and arrival are observed; replay does not start again. No provider or headset proof.',created,obstruction,definition,check,samples,receipt,replay:replay.execution?.selected},null,2));
 return {target,obstacle};
}
