// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {createHash} from 'node:crypto';
import {mkdtemp,mkdir,readFile,writeFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join,dirname,basename,resolve} from 'node:path';
import {afterEach,expect,it,vi} from 'vitest';
import {awaitRoomPersistence} from './probe-room-persistence';
import {validateCapabilityArguments} from '../shared/capabilities';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {assertSupportCollisionShape,assertSupportSavedSource,assertSupportRefusal,probeStaticSupport,staticSupportFixture,type StaticSupportDriver,type SupportReceipt} from './probe-static-support';
import {supportSavedReader} from './probe-native-static-support';

// Synthetic client drivers check evidence assertions, never native support physics.
const owned:string[]=[],target='a'.repeat(32),stale='Object settings changed; inspect their current revision before editing',incompatible='Static support requires a fixed created object without scanned anchoring or recorded root motion';
const copy=<T>(value:T):T=>structuredClone(value);
async function directory(){const path=await mkdtemp(join(tmpdir(),'MaestroStaticSupportTest-'));owned.push(path);return path;}
afterEach(async()=>{vi.useRealTimers();for(const path of owned.splice(0)){expect(dirname(resolve(path))).toBe(resolve(tmpdir()));expect(basename(path)).toMatch(/^MaestroStaticSupportTest-/);await rm(path,{recursive:true,force:true});}});
function syntheticDriver(flaw=''){
 let revision=1,objects:Record<string,unknown>[]=[{id:'book',name:'Existing book',staticSupport:{version:1,enabled:false}}],position=0,serial=0;
 const history=[copy(objects)],calls:string[]=[];
 const item=()=>objects.find(item=>item.id===target);
 const publish=()=>{history.splice(position+1);history.push(copy(objects));position++;revision++;};
 const driver:StaticSupportDriver={
  saved:async()=>({version:47,sha256:createHash('sha256').update(JSON.stringify(objects)).digest('hex'),objects:copy(objects)}),
  fact:async(id,args)=>{
   if(id==='room.persistence')return {available:true,value:checkpointState()};
   if(id==='physics.simulation')return {available:true,value:{running:false}};
   if(flaw==='unavailable'&&id==='object.support')return {available:false,value:{enabled:false}};
   if(!item())return {available:false,value:null};
   if(id==='object.support')return {available:true,value:{target,revision:flaw==='wrongRevision'?revision+1:revision,enabled:(item()!.staticSupport as {enabled:boolean}).enabled,ready:(item()!.staticSupport as {enabled:boolean}).enabled,reason:(item()!.staticSupport as {enabled:boolean}).enabled?'':'Static support is disabled',temporary:false}};
   if(id==='object.definition')return {available:true,value:{target,revision,kind:'Assembly',position:{x:2,y:1,z:2}}};
   if(id==='object.collision')return {available:true,value:{target,revision,shapes:2,pieces:2,customActive:true}};
   expect(id).toBe('object.collision.shape');expect(args?.revision).toBe(revision);const shape=staticSupportFixture().collision.shapes[Number(args?.index)];
   return {available:true,value:{revision,index:args!.index,total:2,shape:{...shape,segments:0,innerRadius:0}}};
  },
  invoke:async(id,args)=>{
   calls.push(id);
   if(id==='object.create')objects.push({id:target,...copy(args),staticSupport:{version:1,enabled:flaw==='defaultOn'}});
   else {expect(id).toBe('object.support.set');expect(args.revision).toBe(revision);item()!.staticSupport={version:1,enabled:args.enabled};if(flaw==='sourceChanged')item()!.name='Unexpected name';}
   publish();return {id:(++serial).toString(16).padStart(32,'0'),capability:id,version:1,resources:[target],phase:'completed',status:'Synthetic saved action',call:{id,version:1,arguments:copy(args)},output:id==='object.create'?{objectId:target}:{target,revision,temporary:false}};
  },
  reject:async(id,args)=>{
   calls.push('reject:'+id);
   if(flaw==='rejectedWrites')item()!.name='Unexpected failed-save edit';
   if(flaw==='refusalUndo')publish();
   const issue=id==='object.support.set'?stale:incompatible;
   if(flaw==='wrongCause')return {ok:false,status:'The scene changed; inspect again'} as RoomAgentState;
   if(flaw==='acceptedRefusal')return {ok:true,status:'Accepted',execution:{selected:{phase:'completed'}}} as RoomAgentState;
   // Exercise direct stale rejection and failed asynchronous preparation separately.
   return id==='object.support.set'?{ok:false,status:issue} as RoomAgentState:{ok:true,status:'Action finished',execution:{selected:{id:'f'.repeat(32),capability:id,version:1,resources:[target],phase:'failed',status:issue,call:{id,version:1,arguments:copy(args)}}}} as RoomAgentState;
  },
  undo:async()=>{expect(position).toBeGreaterThan(0);objects=copy(history[--position]);revision++;},
  redo:async()=>{expect(position).toBeLessThan(history.length-1);objects=copy(history[++position]);revision++;},
  replay:async receipt=>{if(flaw==='replayReexecutes'){item()!.staticSupport={version:1,enabled:true};publish();}return copy(receipt);},
 };
 return {driver,calls};
}
it('authors matching compound boxes and keeps support absent rather than inferring it from a name',()=>{
 const value=staticSupportFixture();expect(validateCapabilityArguments('object.create',1,value)).toBeNull();expect(value).not.toHaveProperty('staticSupport');expect(value.physics).toEqual({mode:'fixed',shape:'automatic',mass:1});expect(value.recipe.playing).toBe(false);expect(value.collision.shapes).toHaveLength(2);
 for(let i=0;i<2;i++){const {parent,color,...part}=value.recipe.parts[i];expect(parent).toBeNull();expect(color.a).toBe(1);expect(part).toEqual(value.collision.shapes[i]);}
});
it('accepts native float32 collision projection while preserving collision-source checks',()=>{
 const expected=staticSupportFixture().collision.shapes[0],actual={...copy(expected),segments:0,innerRadius:0};
 for(const key of ['position','size','rotation'] as const)for(const axis of Object.keys(actual[key]))(actual[key] as Record<string,number>)[axis]=Math.fround((actual[key] as Record<string,number>)[axis]);
 expect(actual.position.x).not.toBe(expected.position.x);expect(()=>assertSupportCollisionShape(actual,expected)).not.toThrow();
});
it.each(['changed coordinate','missing axis','nonfinite','wrong identity','extra convex pieces'])('rejects %s in projected collision source',flaw=>{
 const expected=staticSupportFixture().collision.shapes[0],actual:Record<string,unknown>={...copy(expected),segments:0,innerRadius:0};
 if(flaw==='changed coordinate')(actual.position as Record<string,number>).x+=.0001;
 else if(flaw==='missing axis')delete (actual.size as Record<string,number>).z;
 else if(flaw==='nonfinite')(actual.rotation as Record<string,number>).w=Number.NaN;
 else if(flaw==='wrong identity')actual.id='Other';else actual.segments=8;
 expect(()=>assertSupportCollisionShape(actual,expected)).toThrow();
});
it('records completed persisted intent, exact revision refusals, immutable replay and ordinary Undo cleanup',async()=>{
 const path=await directory(),{driver,calls}=syntheticDriver();await probeStaticSupport(driver,path,'headless');const evidence=JSON.parse(await readFile(join(path,'headless-static-support.json'),'utf8'));
 expect(evidence).toMatchObject({complete:true,phase:'complete',initial:{enabled:false,ready:false},enabled:{enabled:true,ready:true},disabled:{enabled:false,ready:false},undo:{enabled:true,ready:true},redo:{enabled:false,ready:false}});
 expect(evidence.receipts).toHaveLength(3);expect(evidence.refusals).toHaveLength(2);expect(evidence.snapshots.length).toBeGreaterThan(8);expect(evidence.historical).toEqual(evidence.receipts[1]);expect(calls).toEqual(['object.create','object.support.set','reject:object.support.set','reject:object.physics.configure','object.support.set']);
 expect((await driver.saved()).objects).toHaveLength(1);expect(evidence.boundary).toContain('no physical collision');
});
it.each(['defaultOn','wrongRevision','unavailable','sourceChanged','rejectedWrites','refusalUndo','wrongCause','acceptedRefusal','replayReexecutes'])('refuses misleading evidence for %s and retains the failed phase',async flaw=>{
 const path=await directory(),{driver}=syntheticDriver(flaw);await expect(probeStaticSupport(driver,path,'headless')).rejects.toThrow();const evidence=JSON.parse(await readFile(join(path,'headless-static-support.json'),'utf8'));expect(evidence.complete).toBe(false);expect(evidence.phase).toBe('failed');expect(evidence.error).toBeTruthy();
});
it.each(['wrong call','wrong capability','wrong version','wrong reason','unrelated transport','preparing'])('does not count %s as a native incompatible-edit refusal',flaw=>{
 const args={target,revision:2,mode:'solid',shape:'automatic',mass:1};const receipt:SupportReceipt={id:'b'.repeat(32),capability:'object.physics.configure',version:1,resources:[],phase:'failed',status:incompatible,call:{id:'object.physics.configure',version:1,arguments:args}};
 const state={ok:true,status:'Action finished',execution:{selected:receipt}} as RoomAgentState;
 if(flaw==='wrong call')receipt.call={...receipt.call,arguments:{...args,revision:1}};
 else if(flaw==='wrong capability')receipt.capability='object.support.set';else if(flaw==='wrong version')receipt.version=2;else if(flaw==='wrong reason')receipt.status='Unrelated failure';else if(flaw==='preparing')receipt.phase='preparing';else {state.ok=false;state.status='The scene changed; inspect again';}
 expect(()=>assertSupportRefusal(state,'object.physics.configure',args,'incompatible')).toThrow();
});
async function savedFixture(){
 const path=await directory(),workspace=join(path,'workspace'),room=join(workspace,'room'),id='b'.repeat(32);await mkdir(room,{recursive:true});
 await writeFile(join(path,'owner.json'),JSON.stringify({version:1,id,providerUsed:false,providerScenario:'',resumedFrom:'',nativeRestart:false,actionInterruption:false,motionOnly:false,transportOnly:false,liveContinuity:false,firstLearner:false,identityOnly:false,steeringOnly:false,sparseObjectView:false}));
 await writeFile(join(path,'ready.json'),JSON.stringify({version:1,id,workspaceDirectory:workspace}));const bytes=JSON.stringify({version:47,objects:[]});await writeFile(join(room,'room.v47.json'),bytes);return {path,workspace,room,bytes};
}
it('reads only the owned fresh canonical primary and hashes its original bytes without rewriting',async()=>{
 const f=await savedFixture(),read=await supportSavedReader(f.path);expect(await read()).toEqual({version:47,objects:[],sha256:createHash('sha256').update(f.bytes).digest('hex')});expect(await readFile(join(f.room,'room.v47.json'),'utf8')).toBe(f.bytes);
});
it.each(['both primaries','newer primary','selected generation','malformed primary','provider','different ready owner'])('rejects %s as canonical saved support evidence',async mode=>{
 const f=await savedFixture();
 if(mode==='both primaries'||mode==='newer primary')await writeFile(join(f.room,mode==='both primaries'?'room.v46.json':'room.v48.json'),'{}');
 else if(mode==='selected generation'){await mkdir(join(f.workspace,'workspace-generations.v1'));await writeFile(join(f.workspace,'workspace-generations.v1','current.v1.json'),'{}');}
 else if(mode==='malformed primary')await writeFile(join(f.room,'room.v47.json'),'{}');
 else {const filename=join(f.path,mode==='provider'?'owner.json':'ready.json'),value=JSON.parse(await readFile(filename,'utf8'));if(mode==='provider')value.providerUsed=true;else value.id='c'.repeat(32);await writeFile(filename,JSON.stringify(value));}
 await expect((async()=>{const read=await supportSavedReader(f.path);return read();})()).rejects.toThrow();
});

it('accepts only finite submicrometre root-position rounding during native settings capture',()=>{
 const before={name:'Support',position:{x:2,y:1,z:2},scale:1,staticSupport:{version:1,enabled:false},collision:staticSupportFixture().collision};
 const after={...copy(before),position:{x:2,y:0.9999999403953552,z:2},staticSupport:{version:1,enabled:true}};
 expect(()=>assertSupportSavedSource(before,after)).not.toThrow();
});
it.each(['movement','missing axis','extra axis','nonfinite','missing position','changed scale','changed geometry'])('rejects %s despite root-position rounding allowance',flaw=>{
 const before={position:{x:2,y:1,z:2},scale:1,collision:staticSupportFixture().collision};
 const after:Record<string,unknown>=copy(before),position=after.position as Record<string,number>;
 if(flaw==='movement')position.y+=.0001;
 else if(flaw==='missing axis')delete position.z;
 else if(flaw==='extra axis')position.w=1;
 else if(flaw==='nonfinite')position.y=NaN;
 else if(flaw==='missing position')delete after.position;
 else if(flaw==='changed scale')after.scale=1.000001;
 else (after.collision as typeof before.collision).shapes[0].position.y+=.00000001;
 expect(()=>assertSupportSavedSource(before,after)).toThrow();
});

function checkpointState(){const store={phase:'idle',pending:false,dirty:false,dispatchHeld:false,error:''};return {room:{...store},behaviours:{...store}};}
it('waits for accepted history saves and held dispatch through the public persistence fact',async()=>{
 vi.useFakeTimers();const unsaved=checkpointState(),saving=checkpointState(),held=checkpointState(),idle=checkpointState();
 unsaved.room.phase='unsaved';unsaved.room.dirty=true;saving.room.phase='saving';saving.room.pending=true;held.room.dispatchHeld=true;
 const states=[unsaved,saving,held,idle],read=vi.fn(async()=>states.shift());
 const result=awaitRoomPersistence(read,1000);await vi.advanceTimersByTimeAsync(300);
 expect(await result).toEqual([unsaved,saving,held,idle]);expect(read).toHaveBeenCalledTimes(4);
});
it.each(['unconfirmed','unavailable','temporary','error while saving','invalid fact'])('refuses %s instead of treating a saved checkpoint as complete',async flaw=>{
 const state=checkpointState();state.room.phase=flaw==='error while saving'?'saving':flaw;
 if(flaw==='error while saving'){state.room.pending=true;state.room.error='Disk publication failed';}
 await expect(awaitRoomPersistence(async()=>state)).rejects.toThrow();
});
it('bounds a never-completing save without repeating its accepted edit',async()=>{
 vi.useFakeTimers();const state=checkpointState();state.room.phase='saving';state.room.pending=true;
 const read=vi.fn(async()=>state),rejection=expect(awaitRoomPersistence(read,200)).rejects.toThrow('confirmed idle');
 await vi.advanceTimersByTimeAsync(200);await rejection;expect(read).toHaveBeenCalledTimes(2);
});
