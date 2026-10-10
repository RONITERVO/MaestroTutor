// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {HeadlessClient} from '../src/headless/client';
import {isRoomQuery,type RoomCommand} from '../src/core-sdk/room/roomAgent';
import {runHeadlessRoomTurn} from '../src/headless/roomJourney';
import {factReply,type NativeProbeState} from './native-probe-contract';
import {assertSameRoomObjects} from './agent-provider-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
type Fixture={hash:string;name:string;width:number;height:number;source?:'generated-chat'};
export async function prepareAgentImportedImage(execute:Execute,directory:string){
 const fixture=JSON.parse(await readFile(join(directory,'image-fixture.json'),'utf8')) as Fixture;
 assert.match(fixture.hash,/^[a-f0-9]{64}$/);assert.equal(fixture.name,'Blue tiles.png');assert.equal(fixture.width,64);assert.equal(fixture.height,64);
 const baseline=await execute([{action:'rules',rule:{action:'inspect'}}]);return {baseline,fixture};
}
export async function runAgentImportedImageProof({client,execute,directory,baseline,fixture}:{client:HeadlessClient;execute:Execute;directory:string;baseline:NativeProbeState<RoomCommand[]>;fixture:Fixture}){
 const read=async(capability:string,args?:Record<string,unknown>,version=1)=>{const fact=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version,arguments:args}}]));assert.equal(fact.available,true,`Expected available ${capability}`);assert.notEqual(fact.value,null);return fact.value;};
 type Bindings={bindings:{appearanceId:string;kind:string;state:string}[]};type Definition={source:{imageHash:string;patternMode:string};surface:{renderMode:string;opacity:number}};
 const bindings=await read('object.appearances',{target:'book',offset:0}) as Bindings;assert.equal(bindings.bindings.length,1);const binding=bindings.bindings[0];assert.equal(binding.kind,'root');const id=binding.appearanceId;
 const initial=await read('appearance.definition',{id},2) as Definition;assert.equal(initial.source.imageHash,fixture.hash);assert.equal(initial.source.patternMode,'image');
 let render=await read('image.render',{imageHash:fixture.hash}) as {state:string;error:string};const until=Date.now()+10000;while(render.state==='loading'&&Date.now()<until){await new Promise(r=>setTimeout(r,100));render=await read('image.render',{imageHash:fixture.hash}) as typeof render;}assert.equal(render.state,'ready',render.error);
 const previousTask=(await client.roomAgent!.store.list()).sort((a,b)=>b.startedAt-a.startedAt)[0];assert.ok(previousTask,'The initial image task must be recorded');
 const translucent=await runHeadlessRoomTurn(client,{text:"Lovely! Please continue the picture task you just finished: make that picture on the book cover half see-through. Keep the same picture and leave the pages, everything's position, and how things move alone."});
 const continued=await client.roomAgent!.store.get(translucent.task!.id);assert.equal(continued?.directive?.action,'continue');assert.equal(continued?.relatedTask?.id,previousTask.id);assert.equal(continued?.relatedTask?.unconfirmed,false);
 const followup={action:continued!.directive!.action,previousTaskId:previousTask.id,requests:continued!.relatedTask!.requests,priorOperationCount:continued!.relatedTask!.operations.length,unconfirmed:continued!.relatedTask!.unconfirmed};
 const changed=await read('appearance.definition',{id},2) as Definition;assert.equal(changed.source.imageHash,fixture.hash);assert.equal(changed.surface.renderMode,'blend');assert.ok(Math.abs(changed.surface.opacity-.5)<.0001);assert.deepEqual(changed.source,initial.source);assert.deepEqual({...changed.surface,renderMode:initial.surface.renderMode,opacity:initial.surface.opacity},initial.surface);
 type ImageBudget={entries:number;owners:number;textureBytes:number};
 type Reservation={reservationId:string;imageHash:string;state:string;owners:number;textureBytes:number};
 type Owner={reservationId:string;leaseId:string;imageHash:string;worldId:string;regionId:string;target:string;role:string};
 const usageBefore=await read('runtime.imageBudget') as ImageBudget;
 assert.equal(usageBefore.entries,1);assert.equal(usageBefore.owners,1);assert.ok(usageBefore.textureBytes>0);
 const expectedReservation=await read('runtime.imageReservation',{index:0}) as Reservation;
 const expectedOwner=await read('runtime.imageOwner',{reservationId:expectedReservation.reservationId,index:0}) as Owner;
 assert.equal(expectedReservation.imageHash,fixture.hash);assert.equal(expectedOwner.target,'book');
 const inspectPrompt="Could you check which objects are using this picture right now, who they belong to, and how much texture space the picture uses? Just inspect and explain; don't change or remove anything.";
 const inspection=await runHeadlessRoomTurn(client,{text:inspectPrompt,requireActions:false});
 const task=(await client.roomAgent!.store.list()).find(value=>value.handoff.input.prompt===inspectPrompt);assert.ok(task,'Image inspection must retain its actual agent task');
 await writeFile(join(directory,'provider-image-reservation-inspection.json'),JSON.stringify({inspection,usageBefore,expectedReservation,expectedOwner,operations:task.operations},null,2));
 const reservations:Reservation[]=[],owners:Owner[]=[];
 for(const operation of task.operations){
  assert.ok(operation.commands.every(isRoomQuery),'Read-only image request changed the world');
  const request=operation.commands[0];if(request.action!=='catalog'||request.catalog?.operation!=='inspect'||!['runtime.imageReservation','runtime.imageOwner'].includes(request.catalog.capability??''))continue;
  assert.ok(operation.receipt?.ok);const catalog=operation.receipt.catalog;assert.ok(catalog?.operation==='inspect'&&catalog.category==='facts');
  if(!request.catalog.arguments){assert.ok(catalog.definition);continue;}
  assert.ok(catalog.available);if(catalog.capability==='runtime.imageReservation')reservations.push(catalog.value as Reservation);else owners.push(catalog.value as Owner);
 }
 assert.deepEqual(reservations.find(value=>value.reservationId===expectedReservation.reservationId),expectedReservation,'Agent did not inspect the actual shared image reservation');
 assert.deepEqual(owners.find(value=>value.leaseId===expectedOwner.leaseId),expectedOwner,'Agent did not inspect the actual book image owner');
 assert.deepEqual(await read('runtime.imageBudget'),usageBefore);assert.deepEqual(await read('appearance.definition',{id},2),changed);
 const removed=await runHeadlessRoomTurn(client,{text:"Please take the picture off the book again. Keep its saved appearance and imported picture so I can use them later. Don't change anything else."});
 const remaining=await read('object.appearances',{target:'book',offset:0}) as Bindings;assert.equal(remaining.bindings.length,0);const kept=await read('appearance.definition',{id},2);assert.deepEqual(kept,changed);
 const usageAfter=await read('runtime.imageBudget') as ImageBudget;assert.deepEqual(usageAfter,{...usageBefore,entries:0,owners:0,textureBytes:0});
 const retainedBytes=await readFile(join(directory,'workspace','room','images',fixture.hash+'.image'));
 const retainedFile={hash:createHash('sha256').update(retainedBytes).digest('hex'),bytes:retainedBytes.length};
 assert.equal(retainedFile.hash,fixture.hash,'Unbinding must retain the exact reusable private image.');
 const after=await execute([{action:'rules',rule:{action:'inspect'}}]);
 // Root appearance tint may become ordinary source paint when unbound; placement,
 // identity and physics remain exact, and unrelated objects must be untouched.
 const expected=structuredClone(baseline);const book=expected.objects.find(o=>o.id==='book')!;book.color=after.objects.find(o=>o.id==='book')!.color;assertSameRoomObjects(expected,after);assert.deepEqual(after.rules?.sequences,baseline.rules?.sequences);
 const result={scenario:fixture.source==='generated-chat'?'GeneratedImage':'ImportedImage',phase:'passed',boundary:fixture.source==='generated-chat'?'Real original-app generated image from chat, exact byte transfer to native preview/acceptance, real-provider binding/opacity, read-only residency/owner inspection, and final lease release on unbinding. No seeded native image or physical headset claim.':'Fresh real-provider chat, synthetic PNG seeded through the private ImageLibrary, native material binding and texture decode. Pixel correctness has separate GPU readback tests. Android picker, headset visual comfort and real device texture performance remain separate checks.',fixture,id,initial,render,followup,translucent,changed,inspection,reservations,owners,usageBefore,usageAfter,removed,kept,retainedFile,after};
 await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
