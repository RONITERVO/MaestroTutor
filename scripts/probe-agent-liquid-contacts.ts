// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {HeadlessClient} from '../src/headless/client';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {parseProgram,type ProgramNode} from '../src/core-sdk/room/programs';
import {runHeadlessRoomTurn} from '../src/headless/roomJourney';
import {capabilityDefinition} from '../shared/capabilities';
import {factReply,type NativeProbeState} from './native-probe-contract';
import {assertSameRoomObjects} from './agent-provider-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function prepareAgentLiquidContacts(execute:Execute){
 const s=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...capabilityDefinition('object.create')!.example!,shape:'block',name:'ContactPool',x:3,y:1,z:0}}}}]);assert.equal(s.execution?.selected?.phase,'completed');const pool=s.execution!.selected!.output!.objectId as string;
 const c=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.container',version:1,arguments:{target:pool}}}])).value as {revision:number;definition:Record<string,unknown>};
 const configured=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.container.edit',version:1,arguments:{operation:'configure',target:pool,revision:c.revision,definition:{...c.definition,rectangle:{width:1,depth:1},height:.6,capacityMl:10000,amountMl:5000}}}}}]);
 assert.equal(configured.execution?.selected?.phase,'completed');
 return {pool,baseline:await execute([{action:'rules',rule:{action:'inspect'}}]),mediumBaseline:await contactEnvironment(execute,pool)};
}
async function contactEnvironment(execute:Execute,pool:string){
 const simulation=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'physics.simulation',version:1}}])).value as {running:boolean};assert.equal(simulation.running,false);
 const container=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.container',version:1,arguments:{target:pool}}}])).value as {definition:unknown};return {simulation,definition:container.definition};
}
function flatten(body:ProgramNode[]):ProgramNode[]{return body.flatMap(n=>[n,...('body' in n?flatten(n.body):[]),...(n.op==='if'?[...flatten(n.then),...flatten(n.else)]:[]),...(n.op==='switch'?[...n.cases.flatMap(c=>flatten(c.body)),...flatten(n.default)]:[])]);}
export async function runAgentLiquidContactsProof({client,execute,directory,pool,baseline,mediumBaseline}:{client:HeadlessClient;execute:Execute;directory:string;pool:string;baseline:NativeProbeState<RoomCommand[]>;mediumBaseline:Awaited<ReturnType<typeof contactEnvironment>>}){
 const saved=await execute([{action:'rules',rule:{action:'inspect'}}]);assertSameRoomObjects(baseline,saved);assert.deepEqual(await contactEnvironment(execute,pool),mediumBaseline);
 const added=saved.rules!.sequences.filter(s=>!baseline.rules!.sequences.some(old=>old.id===s.id));assert.equal(added.length,1);assert.equal(added[0].name,'WaterTouch');assert.equal(saved.rules!.running.length,0);assert.equal(saved.rules!.buttons.length,baseline.rules!.buttons.length);assert.equal(saved.rules!.bindingCount,baseline.rules!.bindingCount);
 const id=added[0].id;const inspect=()=>execute([{action:'rules',rule:{action:'inspect',target:id}}]);const source=(await inspect()).rules!.selected!;assert.equal(source.repeat,false);const parsed=parseProgram(source.program);assert.equal(parsed.error,null);assert.ok(parsed.program);
 const nodes=parsed.program.functions.flatMap(f=>flatten(f.body));const wait=nodes.find(n=>n.op==='awaitEvent'&&n.event==='object.medium.contact'&&n.source===pool);assert.ok(wait&&wait.op==='awaitEvent','Program must use the native water-contact event for the requested vessel');assert.ok(wait.fields?.participantId);assert.ok(parsed.program.state?.some(s=>s.name==='participant'&&s.initial===''));
 assert.ok(nodes.some(n=>n.op==='setState'&&n.variable==='participant'&&'var' in n.value&&n.value.var===wait.fields!.participantId),'Program must remember the actual contact participant');
 assert.ok(nodes.some(n=>n.op==='sleep'&&'value' in n.seconds&&n.seconds.value===20||n.op==='invoke'&&n.capability==='time.wait'&&(n.arguments.seconds===20&&!n.bindings?.seconds||n.bindings?.seconds&&'value' in n.bindings.seconds&&n.bindings.seconds.value===20)),'Program must retain the requested twenty-second observation delay');
 assert.ok(nodes.every(n=>n.op!=='invoke'||n.capability==='time.wait'),'Observation-only program must not change objects after the event');
 const start=await runHeadlessRoomTurn(client,{text:'Please start my saved WaterTouch program now. Do not touch the water, change the program, or start physics.'});
 let armed=await inspect();for(let i=0;i<50&&!armed.rules?.running.some(r=>r.sequenceId===id&&r.waitEvent==='object.medium.contact');i++){await new Promise(resolve=>setTimeout(resolve,100));armed=await inspect();}
 assert.ok(armed.rules!.running.some(r=>r.sequenceId===id&&r.waitEvent==='object.medium.contact'));assertSameRoomObjects(baseline,armed);assert.deepEqual(await contactEnvironment(execute,pool),mediumBaseline);
 const contact=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'input.medium.contactState',version:1,arguments:{side:'left'}}}])).value as {known:boolean};assert.equal(contact.known,false);
 const stop=await runHeadlessRoomTurn(client,{text:'Please stop WaterTouch, keeping it saved for next time. Leave the water, objects and physics as they are.'});const stopped=await inspect();assert.equal(stopped.rules!.running.length,0);assert.equal(stopped.rules!.selected!.program,source.program);assertSameRoomObjects(baseline,stopped);assert.deepEqual(await contactEnvironment(execute,pool),mediumBaseline);
 const result={scenario:'LiquidContacts',phase:'passed',boundary:'Fresh real-provider English/Spanish chat authors, starts and stops an editable native water-contact listener. Harness creates a named finite vessel; physics stays paused and no tracked input is fabricated. Physical contacts and field delivery are separate Unity PlayMode evidence.',pool,mediumBaseline,source,armed,contact,start,stop,stopped};await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
