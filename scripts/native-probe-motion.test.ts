// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it,vi} from 'vitest';
import {motionObserverProgram,motionSampleReader,startMotionObserver} from './native-probe-motion';
import {parseProgram} from '../src/core-sdk/room/programs';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
const target='a'.repeat(32),sequenceId='b'.repeat(32),runId='c'.repeat(32);
const observation=()=>({id:target,objectRevision:1,name:'Ball',kind:'Ball',position:{x:2,y:1,z:.6},scale:.5,color:{r:0,g:0,b:1,a:1},animated:false,held:false,simulating:true,runtimeState:'active',positionSource:'live'});
function state(serial=1):RoomAgentState{return {version:1,session:'d'.repeat(32),revision:serial+1,sceneRevision:1,ack:1,ok:true,status:'Ready',created:[],canUndo:false,canRedo:false,physicsRunning:true,objects:[],rules:{revision:1,canUndo:false,canRedo:false,readOnly:false,status:'Running',sequences:[],selected:null,bindings:[],buttons:[],bindingPage:0,bindingCount:0,queued:0,running:[{id:runId,sequenceId,preparing:false,state:[{name:'sample',type:'record',value:JSON.stringify({serial,running:true,observation:observation()})}]}]}};}
const patch=(s:RoomAgentState,change:Record<string,unknown>)=>{const entry=s.rules!.running[0].state![0];entry.value=JSON.stringify({...JSON.parse(entry.value),...change});return s;};
it('compiles an editable bounded v3 fact observer without object mutation actions',()=>{
 const program=motionObserverProgram(target);expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect(JSON.stringify(program)).not.toMatch(/"invoke"|"emitEvent"|"checkpoint"|"parallel"/);
 expect(program.version).toBe(3);expect(program.state?.[0].name).toBe('sample');
 expect(()=>motionObserverProgram('wrong')).toThrow();
});
it('consumes live exact-target samples from program state with an empty inventory',()=>{
 let now=0;const first=state(),read=motionSampleReader(first,sequenceId,runId,target,()=>now);
 expect(read(first)).toMatchObject({serial:1,running:true,simulating:true,position:{x:2,y:1,z:.6}});
 now=50;expect(read(first)).toBeNull();now=100;expect(read(state(3))?.serial).toBe(3);
 expect(first.objects).toEqual([]);
});
it('rejects stopped/replaced runs, wrong rooms, wrong targets and non-live evidence',()=>{
 const variants=[(s:RoomAgentState)=>{s.rules!.running=[];},(s:RoomAgentState)=>{s.rules!.running[0].id='e'.repeat(32);},(s:RoomAgentState)=>{s.session='e'.repeat(32);},(s:RoomAgentState)=>{s.workspaceReady=false;},
  (s:RoomAgentState)=>{patch(s,{observation:{...observation(),id:'e'.repeat(32)}});},(s:RoomAgentState)=>{patch(s,{observation:{...observation(),runtimeState:'unavailable',positionSource:'saved'}});},(s:RoomAgentState)=>{patch(s,{serial:NaN});}];
 for(const mutate of variants){const original=state(),next=state(2),read=motionSampleReader(original,sequenceId,runId,target,()=>100);mutate(next);expect(()=>read(next)).toThrow();}
});
it('cannot turn stale, reset, mutated or delayed samples into fresh motion',()=>{
 for(const kind of ['stale','backwards','mutated','gap','clock','revision']){
  let now=10;const read=motionSampleReader(state(),sequenceId,runId,target,()=>now);read(state(2));
  now=kind==='clock'?10:kind==='stale'||kind==='gap'?2500:60;
  const next=state(kind==='backwards'?1:kind==='gap'||kind==='clock'||kind==='revision'?3:2);
  if(kind==='mutated')patch(next,{running:false});if(kind==='revision')next.revision=3;
  expect(()=>read(next)).toThrow();
 }
});
it('refuses fixture edits in a room with active user programs',async()=>{
 const execute=vi.fn();await expect(startMotionObserver({target,execute,read:()=>state()})).rejects.toThrow(/isolated/);expect(execute).not.toHaveBeenCalled();
});
