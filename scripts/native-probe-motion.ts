// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {isDeepStrictEqual} from 'node:util';
import {performance} from 'node:perf_hooks';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import type {BehaviourProgram,Expression} from '../shared/programSyntax';
import {parseProgram} from '../src/core-sdk/room/programs';
import {roomObjectId,validRoomObjectObservation} from '../src/core-sdk/room/roomObjectContext';
import {validFactValue} from '../shared/behaviourFacts';
import {nativeProbeObjects} from './native-probe-objects';

const pause=(ms:number)=>new Promise(resolve=>setTimeout(resolve,ms));
const initialObservation=(target:string)=>({id:target,objectRevision:1,name:'',kind:'Ball',position:{x:0,y:0,z:0},scale:1,color:{r:1,g:1,b:1,a:1},animated:false,held:false,simulating:false,runtimeState:'unavailable',positionSource:'saved'});
/** Ordinary, editable read-only program. No native instrumentation, resource
 * acquisition, writes to objects, or concurrent catalog traffic while sampling. */
export function motionObserverProgram(target:string):BehaviourProgram {
 if(!roomObjectId(target))throw new Error('Invalid motion observer target');
 const initial=initialObservation(target),field=(record:Expression,key:string):Expression=>({op:'field',args:[record,{value:key}]}),sample:Expression={state:'sample'};
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[target],events:[],
  state:[{name:'sample',initial:{serial:0,running:false,observation:initial}}],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'observed',initial},{name:'running',initial:false}],body:[{id:'watch',op:'forever',body:[
   {id:'limit',op:'if',test:{op:'ge',args:[field(sample,'serial'),{value:12000}]},then:[{id:'finished',op:'return'}],else:[]},
   {id:'observe',op:'set',variable:'observed',value:{fact:'object.observation',version:1,arguments:{target},bindings:{}}},
   {id:'physics',op:'set',variable:'running',value:{fact:'physics.running'}},
   {id:'publish',op:'setState',variable:'sample',value:{op:'withField',args:[{op:'withField',args:[{op:'withField',args:[sample,{value:'observation'},{var:'observed'}]},{value:'running'},{var:'running'}]},{value:'serial'},{op:'add',args:[field(sample,'serial'),{value:1}]}]}},
   {id:'interval',op:'sleep',seconds:{value:.1}}
  ]}]}]};
 const parsed=parseProgram(JSON.stringify(program));if(!parsed.program)throw new Error('Motion observer source is invalid: '+parsed.error);
 return program;
}
export interface MotionSample {time:number;revision:number;serial:number;running:boolean;simulating:boolean;held:boolean;animated:boolean;position:{x:number;y:number;z:number}}
export function motionSampleReader(initial:RoomAgentState,sequenceId:string,runId:string,target:string,clock=()=>performance.now()){
 const scope={session:initial.session,temporary:initial.temporaryRoom?.id,active:initial.temporaryRoom?.active};
 let last:MotionSample|undefined,lastPayload='',lastRevision=-1,lastAdvance=clock();
 return (state:RoomAgentState):MotionSample|null=>{
  const now=clock();
  if(state.session!==scope.session||state.temporaryRoom?.id!==scope.temporary||state.temporaryRoom?.active!==scope.active||state.temporaryRoom?.pending||state.workspaceReady===false)throw new Error('Motion observer room scope changed');
  if(state.revision<lastRevision)throw new Error('Motion observations are out of order');lastRevision=state.revision;
  const runs=state.rules?.running.filter(r=>r.sequenceId===sequenceId);
  if(runs?.length!==1||runs[0].id!==runId||runs[0].preparing)throw new Error('Exact motion observer run is missing, stopped or replaced: '+(state.rules?.outcomes?.find(o=>o.id===runId)?.status??'no matching native outcome'));
  const entries=runs[0].state?.filter(v=>v.name==='sample');
  if(entries?.length!==1||entries[0].type!=='record')throw new Error('Missing native motion sample');
  const value:unknown=JSON.parse(entries[0].value);
  if(!value||typeof value!=='object'||Array.isArray(value))throw new Error('Malformed native motion sample');
  const sample=value as Record<string,unknown>,observation=sample.observation;
  if(Object.keys(sample).sort().join(',')!=='observation,running,serial'||typeof sample.serial!=='number'||!Number.isSafeInteger(sample.serial)||sample.serial<0||sample.serial>12000||typeof sample.running!=='boolean'||!validFactValue('object.observation',observation)||!validRoomObjectObservation(observation)||observation.id!==target)throw new Error('Invalid or mismatched native motion sample');
  if(sample.serial===0){if(last||now-lastAdvance>2000)throw new Error('Motion observer failed to start');return null;}
  if(observation.runtimeState!=='active'||observation.positionSource!=='live')throw new Error('Native motion sample is not a live transform');
  if(last&&sample.serial<last.serial)throw new Error('Motion sample serial went backwards');
  if(last&&sample.serial===last.serial){
   if(lastPayload!==entries[0].value)throw new Error('Motion sample changed without a new serial');
   if(now-lastAdvance>2000)throw new Error('Native motion sample is stale');return null;
  }
  if(last&&state.revision<=last.revision)throw new Error('New motion sample reused a native observation revision');
  if(last&&(now<=last.time||now-lastAdvance>2000))throw new Error('Native motion sampling has a time gap');
  lastAdvance=now;lastPayload=entries[0].value;
  return last={time:now,revision:state.revision,serial:sample.serial,running:sample.running,simulating:observation.simulating===true,held:observation.held===true,animated:observation.animated,position:{...observation.position}};
 };
}
export async function startMotionObserver(input:{target:string;execute:(commands:RoomCommand[])=>Promise<RoomAgentState>;read:()=>RoomAgentState}){
 const {target,execute,read}=input,baseline=structuredClone(read()),rules=baseline.rules;
 if(!rules||rules.running.length||rules.queued||rules.bindings.some(b=>b.enabled)||rules.bindingCount)throw new Error('Motion fixture requires an isolated room without running or bound programs');
 const source=motionObserverProgram(target);let sequenceId='',runId='',timer:ReturnType<typeof setInterval>|undefined,closed=false,error:unknown;
 const samples:MotionSample[]=[];
 const stop=async()=>{
  if(closed)return;closed=true;if(timer)clearInterval(timer);
  if(!sequenceId)return;
  const current=read();if(current.session!==baseline.session||current.temporaryRoom?.id!==baseline.temporaryRoom?.id)throw new Error('Cannot clean observer in another room');
  const running=current.rules?.running.filter(r=>r.sequenceId===sequenceId)??[];
  if(running.some(r=>r.id!==runId))throw new Error('Observer was replaced; refusing to stop a different run');
  if(running.length)await execute([{action:'rules',rule:{action:'stop',target:sequenceId}}]);
  const inspected=await execute([{action:'rules',rule:{action:'inspect',target:sequenceId}}]);
  if(inspected.rules?.running.length||inspected.rules?.queued||inspected.rules?.selected?.program!==JSON.stringify(source))throw new Error('Observer or other programs changed; refusing fixture cleanup');
  const cleaned=await execute([{action:'rules',rule:{action:'edit',revision:inspected.rules.revision,edits:[{kind:'delete',target:sequenceId}]}}]);
  if(!isDeepStrictEqual(cleaned.rules?.sequences,rules.sequences)||!isDeepStrictEqual(cleaned.rules?.buttons,rules.buttons)||!isDeepStrictEqual(cleaned.rules?.bindings,rules.bindings))throw new Error('Observer cleanup changed unrelated rules');
 };
 try{
  // Demand belongs to the normal viewer/physics system. Passive facts must not
  // make an off-screen object resident merely so a test can observe it.
  const objects=nativeProbeObjects(execute),readyBy=Date.now()+5000;
  while(true){const observed=await objects.observation(target);if(observed.runtimeState==='active'&&observed.positionSource==='live')break;
   if(Date.now()>readyBy)throw new Error('Motion fixture target is not in the native viewer demand');await pause(100);}
  const saved=await execute([{action:'rules',rule:{action:'edit',revision:rules.revision,edits:[{kind:'save',reference:'motion_observer',sequence:{id:'',name:'Probe motion observer',interruption:0,repeat:false,program:JSON.stringify(source)}}]}}]);
  const added=saved.rules?.sequences.filter(s=>!rules.sequences.some(b=>b.id===s.id));
  if(added?.length!==1)throw new Error('Motion observer was not saved exactly once');sequenceId=added[0].id;
  const played=await execute([{action:'rules',rule:{action:'play',target:sequenceId,revision:saved.rules!.revision}}]);
  const runs=played.rules?.running.filter(r=>r.sequenceId===sequenceId);if(runs?.length!==1)throw new Error('Motion observer did not start');runId=runs[0].id;
  const consume=motionSampleReader(played,sequenceId,runId,target),sample=()=>{
   if(error)return;try{const next=consume(read());if(next){if(samples.length>=12000)throw new Error('Motion evidence budget exceeded');samples.push(next);}}catch(cause){error=cause;}
  };
  timer=setInterval(sample,20);sample();const deadline=Date.now()+5000;
  while(!samples.length&&!error&&Date.now()<deadline)await pause(20);
  if(error)throw error;if(!samples.length)throw new Error('No live native motion sample');
  return {source,sequenceId,runId,samples,stop,check:()=>{sample();if(error)throw error;return samples.at(-1)!;}};
 }catch(cause){try{await stop();}catch(cleanup){throw new AggregateError([cause,cleanup],'Motion observer setup and cleanup failed');}throw cause;}
}
