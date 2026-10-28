// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,it,expect} from 'vitest';
import {createHash} from 'node:crypto';
import {resolve,join} from 'node:path';
import {restartCheckpoint,validateRestartProcess} from './first-learner-native-restart';
const root=resolve('synthetic-restart-evidence'),id='1'.repeat(32),child='2'.repeat(32),bytes='{"synthetic":"restart request"}';
const ready=()=>({id,processId:100,processStartedAt:1000,workspaceDirectory:join(root,'workspace')});
const resumed=()=>({id:child,processId:200,processStartedAt:3000,workspaceDirectory:join(root,'workspace'),restartOf:id,syntheticPhysics:true,syntheticScan:false});
const process=()=>({version:1,id,method:'terminate-after-checkpoint',workspace:join(root,'workspace'),requestSha256:createHash('sha256').update(bytes).digest('hex'),source:{id,processId:100,startedAt:1000,exitedAt:2000,exitCode:-1,terminalExisted:false},resumed:{id:child,directory:join(root,'restart'),processId:200,startedAt:3000}});
const clean=()=>({room:{phase:'idle',pending:false,dirty:false,dispatchHeld:false,error:''},behaviours:{phase:'idle',pending:false,dirty:false,dispatchHeld:false,error:''}});
describe('native interruption precondition',()=>{
 it('preserves a real-shaped clean persistence value without mutating it',()=>{const v=clean(),r=restartCheckpoint(v);expect(r).toEqual(v);expect(r).not.toBe(v);expect(r.room).not.toBe(v.room);});
 for(const [field,value] of [['phase','saving'],['phase','temporary'],['phase','unconfirmed'],['pending',true],['dirty',true],['dispatchHeld',true],['error','disk failed'],['pending',0]] as const) {
  for(const store of ['room','behaviours'] as const)it(`refuses ${store}.${field}=${value}`,()=>{const v:any=clean();v[store][field]=value;expect(()=>restartCheckpoint(v)).toThrow();});
 }
 it('does not silently ignore a renamed or additional store',()=>{const v:any=clean();v.behaviour=v.behaviours;delete v.behaviours;expect(()=>restartCheckpoint(v)).toThrow();v.behaviours=v.behaviour;expect(()=>restartCheckpoint(v)).toThrow();});
});
describe('parent-observed native process restart',()=>{
 it('binds both actual process identities, exit ordering, workspace and request bytes',()=>{const p=process();expect(validateRestartProcess(p,ready(),resumed(),root,id,bytes)).toBe(p);});
 const changes:Record<string,(p:any,a:any,b:any)=>void>={
  'wrong root':p=>{p.id=child;},'wrong method':p=>{p.method='browser-reload';},'copied workspace':p=>{p.workspace=join(root,'workspace-copy');},
  'different request bytes':p=>{p.requestSha256='0'.repeat(64);},'fabricated successful first terminal':p=>{p.source.terminalExisted=true;},
  'wrong source process':p=>{p.source.processId=101;},'wrong source creation time':p=>{p.source.startedAt=1001;},
  'exit before source creation':p=>{p.source.exitedAt=999;},'source not known exited':p=>{delete p.source.exitedAt;},
  'same process':(p,a,b)=>{p.resumed.processId=a.processId;b.processId=a.processId;},'same wire owner':(p,_a,b)=>{p.resumed.id=id;b.id=id;},
  'second process started before first exit':(p,_a,b)=>{p.resumed.startedAt=1999;b.processStartedAt=1999;},
  'wrong phase-two directory':p=>{p.resumed.directory=join(root,'foreign');},'wrong resumed ready PID':(_p,_a,b)=>{b.processId=300;},
  'wrong resumed ready start':(_p,_a,b)=>{b.processStartedAt=3001;},'wrong resumed ready owner':(_p,_a,b)=>{b.restartOf=child;},
  'ready claims different workspace':(_p,_a,b)=>{b.workspaceDirectory=join(root,'other');},'ready claims a physical scan':(_p,_a,b)=>{b.syntheticScan=true;},
  'noninteger process time':p=>{p.source.exitedAt=2000.5;},'string exit code':p=>{p.source.exitCode='-1';},
 };
 for(const [label,change] of Object.entries(changes))it('rejects '+label,()=>{const p=process(),a=ready(),b=resumed();change(p,a,b);expect(()=>validateRestartProcess(p,a,b,root,id,bytes)).toThrow();});
});
