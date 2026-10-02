// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {afterEach,expect,it,vi} from 'vitest';
import {mkdtemp,readFile,writeFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {randomUUID} from 'node:crypto';
import {HeadlessRoomTransport,publishRoomProbeFile} from './roomTransport';
const sleep=(ms:number)=>new Promise<void>(resolve=>setTimeout(resolve,ms));
const cleanup:(()=>Promise<void>)[]=[];
afterEach(async()=>{vi.restoreAllMocks();const errors:unknown[]=[];for(const close of cleanup.splice(0).reverse())try{await close();}catch(error){errors.push(error);}if(errors.length)throw errors[0];});
async function boundary(wrongIdentity=false){
 const directory=await mkdtemp(join(tmpdir(),'maestro-room-transport-')),id=randomUUID().replace(/-/g,'');
 await writeFile(join(directory,'owner.json'),JSON.stringify({version:1,id}));await writeFile(join(directory,'ready.json'),JSON.stringify({version:1,id}));
 let running=true,freeze=false,invalid=false,revision=0,ack=0,received=0,stopSeen=false;
 const pump=(async()=>{while(running){
  try{
   const request=JSON.parse(await readFile(join(directory,'request.json'),'utf8'));
   if(request.operation==='stop'){stopSeen=true;break;}
   const candidate=request.snapshot.request;if(candidate&&candidate.sequence>ack){ack=candidate.sequence;received++;}
   if(!freeze){const state={version:1,session:'a'.repeat(32),revision:++revision,sceneRevision:1,ack,ok:true,status:invalid?null:'Transport fixture acknowledgement',objects:[],created:[],canUndo:false,canRedo:false,physicsRunning:false,capabilities:[]};
    const path=join(directory,'state.json');await publishRoomProbeFile(path,JSON.stringify({version:1,id:wrongIdentity?'b'.repeat(32):id,clientId:request.snapshot.clientId,state}));
   }
  }catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
  await sleep(20);
 }})();
 cleanup.push(async()=>{running=false;await pump;await rm(directory,{recursive:true,force:true});});
 return {directory,invalidate:()=>{invalid=true;},freeze:()=>{freeze=true;},received:()=>received,stopped:()=>stopSeen};
}
it('exchanges the same shared-client requests and waits for native acknowledgements',async()=>{
 const native=await boundary(),transport=await HeadlessRoomTransport.connect(native.directory,3000);cleanup.push(()=>transport.close(false));
 const lease=transport.lease();const initial=lease.state();const pending=lease.execute([{action:'create',reference:'shape',name:'Fixture request',kind:'block'}],initial.sceneRevision,initial.objects);
 expect(transport.client.snapshot().request?.version).toBe(2);
 const receipt=await pending;expect(receipt.ack).toBe(1);expect(native.received()).toBe(1);
 await transport.close();for(let i=0;i<30&&!native.stopped();i++)await sleep(20);expect(native.stopped()).toBe(true);
});
it('does not treat stale native files as fresh room observations',async()=>{
 const native=await boundary(),transport=await HeadlessRoomTransport.connect(native.directory,3000);cleanup.push(()=>transport.close(false));native.freeze();
 await sleep(150);const now=Date.now();vi.spyOn(Date,'now').mockReturnValue(now+4000);
 expect(()=>transport.lease()).toThrow('unavailable');
});
it('rejects a mismatched native session before exposing a lease',async()=>{
 const native=await boundary(true);await expect(HeadlessRoomTransport.connect(native.directory,3000)).rejects.toThrow('envelope');expect(native.received()).toBe(0);
});
it('refuses invalid owner receipts instead of opening a room connection',async()=>{
 const directory=await mkdtemp(join(tmpdir(),'maestro-room-owner-'));cleanup.push(()=>rm(directory,{recursive:true,force:true}));
 await writeFile(join(directory,'owner.json'),JSON.stringify({version:1,id:'invalid'}));await expect(HeadlessRoomTransport.connect(directory,100)).rejects.toThrow('owner');
});

it('preserves the rejected native observation and its diagnostic when a pending request is interrupted',async()=>{
 const native=await boundary(),transport=await HeadlessRoomTransport.connect(native.directory,3000);cleanup.push(()=>transport.close(false));
 const lease=transport.lease(),initial=lease.state();native.invalidate();
 await expect(lease.execute([{action:'undo'}],initial.sceneRevision,initial.objects)).rejects.toThrow('interrupted');
 expect(()=>transport.checkHealth()).toThrow('shared client contract');
 const rejected=JSON.parse(await readFile(join(native.directory,'rejected-state.json'),'utf8'));expect(rejected.state.status).toBeNull();
});
