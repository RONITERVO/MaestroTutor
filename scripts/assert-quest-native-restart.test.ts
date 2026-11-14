// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {it,expect} from 'vitest';
import {createHash} from 'node:crypto';
import {mkdtemp,mkdir,writeFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';
import {behaviourFact} from '../shared/behaviourCatalog';
import {assertNativeRestartResult,assertNativeRestartFiles} from './assert-quest-native-restart';
const root=resolve('synthetic-restart-report'),id='1'.repeat(32),child='2'.repeat(32),apple='3'.repeat(32),tree='4'.repeat(32),world='5'.repeat(32),clients=['6'.repeat(32),'7'.repeat(32)],sessions=['8'.repeat(32),'9'.repeat(32)];
const fact=(capability:string,value:any,args?:any)=>({operation:'inspect',category:'facts',capability,version:1,definition:behaviourFact(capability),status:'Current fact value',available:true,value,arguments:args??{}});
const clean=()=>({phase:'idle',pending:false,dirty:false,dispatchHeld:false,error:''});
function fixture(folder=root) {
 const checkpoint={room:clean(),behaviours:clean()},original=['0'.repeat(32),'d'.repeat(32),'e'.repeat(32),'book','maestro'].sort(),ids=[...original,apple].sort();
 const pages=(members:string[])=>[0,1].flatMap(()=>Array.from({length:Math.ceil(members.length/3)},(_,i)=>{const offset=i*3;return fact('world.objects',{worldId:world,offset,total:members.length,next:offset+3<members.length?members[offset+2]+'.'+'f'.repeat(32):'',entries:members.slice(offset,offset+3).map(id=>({id,name:id,kind:id==='book'?'Book':id==='maestro'?'Maestro':'Ball'}))},{query:'',kind:'all',cursor:offset?members[offset-1]+'.'+'f'.repeat(32):''});}));
 const capture={tracks:[{kind:'audio',readyState:'ended',enabled:true}],attempts:[{audio:true,video:false,state:'resolved'}]},restored={tracks:[],attempts:[]};
 const request={version:1,id,operation:'restart',clientId:clients[0],nativeSession:sessions[0],appleId:apple,treeId:tree,checkpoint},bytes=JSON.stringify(request);
 const receipts=[0,1,2].map(i=>({id:String.fromCharCode(97+i).repeat(32),capability:i<2?'object.create':'object.delete',version:1,resources:[],phase:'completed',status:'Action completed',call:{id:i<2?'object.create':'object.delete',version:1,arguments:i<2?{name:i?'apple':'tree'}:{target:tree}},...(i<2?{output:{objectId:i?apple:tree}}:{})}));
 const points=sessions.map((session,i)=>({identity:{version:1,source:'explicit-native-object-directory',session,worldId:world,sceneRevision:i?1:8,ids},
  identityPages:pages(ids),
  persistence:fact('room.persistence',structuredClone(checkpoint)),receipts:structuredClone(receipts),state:{session,workspaceReady:true,temporaryRoom:null},capture:structuredClone(i?restored:capture)}));
 const ready=(i:number)=>({version:1,id:i?child:id,processId:i?200:100,processStartedAt:i?3000:1000,workspaceDirectory:join(folder,'workspace'),restartOf:i?id:'',syntheticPhysics:true,syntheticScan:false});
 const process={version:1,id,method:'terminate-after-checkpoint',workspace:join(folder,'workspace'),requestSha256:createHash('sha256').update(bytes).digest('hex'),source:{id,processId:100,startedAt:1000,exitedAt:2000,exitCode:-1,terminalExisted:false},resumed:{id:child,directory:join(folder,'restart'),processId:200,startedAt:3000}};
 const appleDefinition=fact('object.definition',{target:apple,revision:10,kind:'Ball',name:'apple',position:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1},scale:1,content:{points:0,parts:0,frames:0,modelHash:'',recipePlaying:false}},{target:apple});
 const commands=[...receipts.map(r=>({action:'execution',execution:{operation:'start',call:r.call,runId:r.id}})),...receipts.map(r=>({action:'execution',execution:{operation:'inspect',runId:r.id}}))];
 const result:any={status:'passed',nativeProbeId:id,productionComposition:true,nativeExecution:true,realProvider:false,physicalDevice:false,native:{nativeProbeId:id,qualified:true,appleId:apple,treeId:tree,appleDefinition,receipts,clients,
  restored:{apple:structuredClone(appleDefinition),directory:fact('world.objects',{worldId:world,offset:0,total:0,next:'',entries:[]},{query:tree,kind:'all',cursor:''})},beforeReloadCapture:capture,restoredCapture:restored,
  requests:commands.map((command,i)=>({clientId:clients[i<3?0:1],request:{session:sessions[i<3?0:1],commands:[command]}})),
  observations:sessions.flatMap((session,i)=>receipts.map(r=>({clientId:clients[i],session,ok:true,execution:{id:r.id,phase:r.phase,capability:r.capability}}))),
  restart:{version:1,qualified:true,initialWorld:{identity:{...points[0].identity,ids:original},identityPages:pages(original)},sourceProbeId:id,childProbeId:child,request,process,sourceReady:ready(0),resumedReady:ready(1),before:points[0],after:points[1],requestsBeforeRestart:3}}};
 return {result,bytes};
}
it('qualifies saved identity and actual prior receipts across distinct process evidence',()=>{const f=fixture();expect(()=>assertNativeRestartResult(f.result,root,id,f.bytes)).not.toThrow();});
it('retains authored metadata while accepting a newly observed native-session revision',()=>{const f=fixture();f.result.native.restored.apple.value.revision=8;expect(()=>assertNativeRestartResult(f.result,root,id,f.bytes)).not.toThrow();});
const faults:Record<string,(n:any,r:any)=>void>={
 'same process':(_n,r)=>{r.process.resumed.processId=100;r.resumedReady.processId=100;},
 'same session':(_n,r)=>{r.after.identity.session=r.before.identity.session;r.after.state.session=r.before.state.session;},
 'changed world':(_n,r)=>{r.after.identity.worldId='f'.repeat(32);},
 'missing apple':(_n,r)=>{r.after.identity.ids=r.after.identity.ids.filter((x:string)=>x!==apple);},
 'missing starter':(_n,r)=>{r.after.identity.ids=r.after.identity.ids.filter((x:string)=>x!=='0'.repeat(32));},
 'changed starting world':(_n,r)=>{r.initialWorld.identity.worldId='0'.repeat(32);},
 'wrong cursor':(_n,r)=>{r.after.identityPages[1].arguments.cursor='';},
 'retained tree':(_n,r)=>{r.after.identity.ids.push(tree);},
 'inferred directory':(_n,r)=>{r.after.identity.source='periodic-objects';},
 'single directory pass':(_n,r)=>{r.after.identityPages.pop();},
 'partial directory page':(_n,r)=>{r.after.identityPages[0].value.total=4;},
 'different second directory pass':(_n,r)=>{r.after.identityPages[2].value.entries[0].id=tree;},
 'temporary world':(_n,r)=>{r.after.state.temporaryRoom={active:true,pending:false};},
 'pending world':(_n,r)=>{r.after.state.temporaryRoom={active:false,pending:true};},
 'unready world':(_n,r)=>{r.after.state.workspaceReady=false;},
 'unsaved room':(_n,r)=>{r.before.persistence.value.room.dirty=true;},
 'unsaved behaviours':(_n,r)=>{r.after.persistence.value.behaviours.pending=true;},
 'unknown checkpoint store':(_n,r)=>{r.request.checkpoint.other=clean();},
 'changed receipt output':(_n,r)=>{r.after.receipts[0].output.objectId=apple;},
 'changed receipt call':(_n,r)=>{r.after.receipts[1].call.arguments.name='other';},
 'missing receipt':(_n,r)=>{r.after.receipts.pop();},
 'invalid current revision':n=>{n.restored.apple.value.revision=0;},
 'string current revision':n=>{n.restored.apple.value.revision='8';},
 'changed apple pose':n=>{n.restored.apple.value.position.y=1;},
 'apple definition redirected':n=>{n.restored.apple.arguments.target=tree;},
 'tree query redirected':n=>{n.restored.directory.arguments.query=apple;},
 'tree restored':n=>{n.restored.directory.value.total=1;},
 'replayed action':n=>{n.requests.push(structuredClone(n.requests[0]));},
 'new action after restart':n=>{n.requests[3].request.commands[0].execution.operation='start';},
 'cancel after restart':n=>{n.requests[3].request.commands[0].execution.operation='stop';},
 'missing reinspection':n=>{n.requests.splice(3,1);},
 'unobserved receipt':n=>{n.observations.pop();},
 'different request session':n=>{n.requests[4].request.session=sessions[0];},
 'split before save':(_n,r)=>{r.requestsBeforeRestart=2;},
 'active microphone':(_n,r)=>{r.before.capture.tracks[0].readyState='live';},
 'new microphone':(_n,r)=>{r.after.capture.attempts.push({audio:true,video:false,state:'resolved'});},
 'fabricated source terminal':(_n,r)=>{r.process.source.terminalExisted=true;},
 'unqualified restart':(_n,r)=>{r.qualified=false;}
};
for(const [name,change] of Object.entries(faults))it('refuses '+name,()=>{const f=fixture();change(f.result.native,f.result.native.restart);expect(()=>assertNativeRestartResult(f.result,root,id,f.bytes)).toThrow();});
it('cross-checks the report against retained ownership, process, workspace and terminal files',async()=>{
 const temporary=await mkdtemp(join(tmpdir(),'maestro-restart-gate-'));
 try{
  const {result,bytes}=fixture(temporary),r=result.native.restart;
  await Promise.all(['restart','workspace','first-learner'].map(x=>mkdir(join(temporary,x))));
  const write=async(path:string,data:any)=>writeFile(join(temporary,path),JSON.stringify(data));
  const owner={version:1,id,firstLearner:true,liveContinuity:true,nativeRestart:true};
  await Promise.all([write('owner.json',owner),write('restart/owner.json',{...owner,id:child,restartOf:id}),write('ready.json',r.sourceReady),write('restart/ready.json',r.resumedReady),write('restart.json',r.process),write('first-learner/result.json',result),writeFile(join(temporary,'restart-request.json'),bytes),write('restart/terminal.json',{version:1,id:child,exitCode:0})]);
  expect((await assertNativeRestartFiles(temporary)).status).toBe('passed');
  await write('restart/ready.json',{...r.resumedReady,processId:201});await expect(assertNativeRestartFiles(temporary)).rejects.toThrow();await write('restart/ready.json',r.resumedReady);
  await write('terminal.json',{version:1,id,exitCode:0});await expect(assertNativeRestartFiles(temporary)).rejects.toThrow();await rm(join(temporary,'terminal.json'));
  await write('restart/terminal.json',{version:1,id:child,exitCode:1});await expect(assertNativeRestartFiles(temporary)).rejects.toThrow();await write('restart/terminal.json',{version:1,id:child,exitCode:0});
  await mkdir(join(temporary,'restart/workspace'));await expect(assertNativeRestartFiles(temporary)).rejects.toThrow();
 }finally{expect(temporary.startsWith(join(tmpdir(),'maestro-restart-gate-'))).toBe(true);await rm(temporary,{recursive:true,force:true});}
});
