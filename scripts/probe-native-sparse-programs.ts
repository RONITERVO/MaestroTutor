// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import type {BehaviourProgram,ProgramModule} from '../shared/programSyntax';
import {readNativeObjects} from './native-probe-read-set';
import {readAvatarMovementSettings} from './native-probe-avatar-preferences';
import {assertSameRoomObjects} from './agent-provider-contract';
import {assertAvatarPreferences} from './probe-agent-animation';
import {assertParitySignalSource,assertProgramBall,isProgramDelay} from './probe-agent-program';
import {assertCompositeSource,assertParallelWaiting} from './probe-agent-composite';
import {paritySignalFixture,parityCompositeFixture} from './native-probe-program-fixtures';
/** Offline fixture authorship with actual native scheduling, facts and Undo.
 * Shared provider assertion helpers are exercised; no provider or chat claim. */
export async function probeNativeSparsePrograms(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>,directory:string){
 const startup=await execute([{action:'rules',rule:{action:'inspect'}}]);let initial=startup;
 // An imported avatar can still be loading when the protocol first connects.
 // Wait for its selected model's activity profile before recording preferences.
 const readyBy=Date.now()+30000;
 while(initial.walk?.modelHash&&initial.activityProfile?.modelHash!==initial.walk.modelHash){
  if(Date.now()>readyBy)throw new Error('The selected avatar did not finish loading its activity profile');
  await new Promise(r=>setTimeout(r,100));initial=await execute([{action:'rules',rule:{action:'inspect'}}]);
 }
 assert.ok(initial.rules&&!initial.rules.running.length&&!initial.rules.queued&&!initial.rules.bindingCount);
 assert.equal(initial.physicsRunning,false);
 const beforeObjects=await readNativeObjects(execute),beforeMovement=await readAvatarMovementSettings(execute);
 const evidence:Record<string,unknown>={phase:'setup',boundary:'Offline fixture-authored programs and shared assertion helpers against real Unity scheduling and explicit native reads. No provider, chat, headset or physical-controller proof.',startup,initial,beforeObjects,beforeMovement,samples:[]};
 const samples=evidence.samples as {label:string;state:RoomAgentState}[],owned=new Map<string,string>();let target='';
 const save=()=>writeFile(join(directory,'native-sparse-programs.json'),JSON.stringify(evidence,null,2));
 const capture=async(label:string,state:RoomAgentState)=>{samples.push({label,state:structuredClone(state)});await save();return state;};
 const inspect=(id?:string)=>execute([{action:'rules',rule:{action:'inspect',...(id?{target:id}:{})}}]);
 const wait=async(id:string,label:string,accepts:(s:RoomAgentState)=>boolean)=>{
  const deadline=Date.now()+15000;let state:RoomAgentState;
  do{state=await inspect(id);if(accepts(state))return capture(label,state);await new Promise(r=>setTimeout(r,100));}while(Date.now()<deadline);
  await capture('failed-'+label,state!);throw new Error('Native sparse program did not reach '+label);
 };
 const store=async(name:string,program:BehaviourProgram)=>{
  const current=await inspect(),source=JSON.stringify(program);
  const saved=await execute([{action:'rules',rule:{action:'edit',revision:current.rules!.revision,edits:[{kind:'save',reference:'sparse_program',sequence:{id:'',name,interruption:0,repeat:false,program:source}}]}}]);
  const added=saved.rules!.sequences.filter(s=>!current.rules!.sequences.some(v=>v.id===s.id));assert.equal(added.length,1);
  const id=added[0].id;owned.set(id,source);return (await inspect(id)).rules!.selected!;
 };
 const play=async(id:string)=>{const s=await inspect(id);return execute([{action:'rules',rule:{action:'play',target:id,revision:s.rules!.revision}}]);};
 const signal=async(id:string,eventName:string,value:string)=>{const s=await inspect(id);return execute([{action:'rules',rule:{action:'signal',revision:s.rules!.revision,eventName,value}}]);};
 try{
  const created=await execute([{action:'create',reference:'sparse_ball',kind:'ball',name:'ParityBall',atPosition:true,position:{x:.2,y:1,z:2.5},scale:.5,color:{r:0,g:0,b:1,a:1}}]);
  target=created.created[0];assert.ok(target);await execute([{action:'physicsSettings',target,physics:{mode:'bouncy',shape:'sphere',mass:2}}]);
  const baseline=await readNativeObjects(execute),sequence=await store('ParitySignal',paritySignalFixture(target));
  const program=assertParitySignalSource(sequence,target);assertSameRoomObjects(baseline,await readNativeObjects(execute));
  const running=(s:RoomAgentState)=>s.rules!.running.find(r=>r.sequenceId===sequence.id);
  const count=(s:RoomAgentState)=>Number(running(s)?.state?.find(v=>v.name==='handledCount')?.value);
  await play(sequence.id);
  const armed=await wait(sequence.id,'armed',s=>running(s)?.waitEvent==='user.parityColour'&&count(s)===0),runId=running(armed)!.id;
  assertSameRoomObjects(baseline,await readNativeObjects(execute));
  for(const [index,value] of ['red','other'].entries()){
   await signal(sequence.id,'user.parityColour',value);
   // Capture the one-second scheduler state BEFORE the slower inventory read.
   await wait(sequence.id,'delay-'+value,s=>count(s)===index+1&&isProgramDelay(running(s),program));
   assertProgramBall(baseline,await readNativeObjects(execute),target,index===0);
   if(index===0)await wait(sequence.id,'second-event',s=>running(s)?.waitEvent==='user.parityColour'&&count(s)===1);
  }
  await wait(sequence.id,'completed',s=>!running(s)&&s.rules!.outcomes?.some(o=>o.id===runId&&o.phase==='completed')===true);
  assertProgramBall(baseline,await readNativeObjects(execute),target,false);
  await play(sequence.id);const restarted=await wait(sequence.id,'restarted',s=>running(s)?.waitEvent==='user.parityColour'&&count(s)===0);assert.notEqual(running(restarted)!.id,runId);
  await execute([{action:'rules',rule:{action:'stop',target:sequence.id}}]);await signal(sequence.id,'user.parityColour','red');
  assertSameRoomObjects(baseline,await readNativeObjects(execute));assert.equal((await inspect(sequence.id)).rules!.selected!.program,sequence.program);
  const module=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Programs/Modules/Spinner.json','utf8')) as ProgramModule;
  const composite=await store('ParitySpinners',parityCompositeFixture(module));assertCompositeSource(composite,module);
  assertSameRoomObjects(baseline,await readNativeObjects(execute));await play(composite.id);
  const fork=await wait(composite.id,'two-branches',s=>s.rules!.running.filter(r=>r.sequenceId===composite.id&&r.parentRunId&&['user.spawnLeft','user.spawnRight'].includes(r.waitEvent||'')).length===2);
  const parent=assertParallelWaiting(fork,composite.id,['user.spawnLeft','user.spawnRight']);
  await signal(composite.id,'user.spawnLeft','go');
  const left=await wait(composite.id,'left-built',s=>s.rules!.running.filter(r=>r.parentRunId===parent).length===1&&s.rules!.running.some(r=>r.parentRunId===parent&&r.waitEvent==='user.spawnRight'));
  assertParallelWaiting(left,composite.id,['user.spawnRight']);
  const leftObjects=await readNativeObjects(execute);assert.equal(leftObjects.objects.length,baseline.objects.length+2);
  await signal(composite.id,'user.spawnRight','go');
  await wait(composite.id,'both-built',s=>!s.rules!.running.some(r=>r.sequenceId===composite.id)&&s.rules!.outcomes?.some(o=>o.id===parent&&o.phase==='completed')===true);
  const completed=await readNativeObjects(execute);assert.equal(completed.objects.length,baseline.objects.length+4);
  assertSameRoomObjects(baseline,{objects:completed.objects.filter(o=>baseline.objects.some(b=>b.id===o.id))});
  await execute([{action:'undo'}]);assertSameRoomObjects(leftObjects,await readNativeObjects(execute));
  await execute([{action:'undo'}]);assertSameRoomObjects(baseline,await readNativeObjects(execute));
  await execute([{action:'redo'}]);assertSameRoomObjects(leftObjects,await readNativeObjects(execute));
  await execute([{action:'redo'}]);assertSameRoomObjects(completed,await readNativeObjects(execute));
  await signal(composite.id,'user.spawnLeft','go');await signal(composite.id,'user.spawnRight','go');assertSameRoomObjects(completed,await readNativeObjects(execute));
  const final=await inspect(composite.id);assert.equal(final.rules!.running.length,0);assert.equal(final.rules!.selected!.program,composite.program);
  assert.equal(final.rules!.outcomes!.filter(o=>o.sequenceId===composite.id).length,1);
  await execute([{action:'undo'}]);await execute([{action:'undo'}]);assertSameRoomObjects(baseline,await readNativeObjects(execute));
  assertAvatarPreferences(initial,final,beforeMovement,await readAvatarMovementSettings(execute));
  evidence.phase='passed';evidence.semantics={explicitMovementPreferences:true,saveOnly:true,branchesAndCounter:true,oneSecondWaitObserved:true,freshRestart:true,noPostStopEffects:true,independentNativeBranches:true,completedParent:true,undoRedoIdentitiesAndPhysics:true,noPostCompletionEffects:true};
  evidence.programs=[sequence,composite];evidence.completedObjects=completed;
 }catch(error){evidence.phase='failed';evidence.error=String(error);await save();throw error;}
 finally{
  try{
  // Clean only known unchanged fixture sources in the same native session.
  const current=await inspect();assert.equal(current.session,initial.session);
  for(const [id,source] of owned){
   const state=await inspect(id);assert.equal(state.rules!.selected?.program,source);
   if(state.rules!.running.some(r=>r.sequenceId===id))await execute([{action:'rules',rule:{action:'stop',target:id}}]);
   const ready=await inspect(id);await execute([{action:'rules',rule:{action:'edit',revision:ready.rules!.revision,edits:[{kind:'delete',target:id}]}}]);
  }
  if(target)await execute([{action:'delete',target}]);
  const final=await inspect();assert.deepEqual(final.rules!.sequences,initial.rules!.sequences);assert.deepEqual(final.rules!.buttons,initial.rules!.buttons);assert.deepEqual(final.rules!.bindings,initial.rules!.bindings);
  assertSameRoomObjects(beforeObjects,await readNativeObjects(execute));
  }catch(error){evidence.phase='failed';evidence.cleanupError=String(error);throw error;}
  finally{await save();}
 }
}
