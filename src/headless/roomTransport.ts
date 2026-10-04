// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFile,writeFile,rename,stat} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {RoomAgentClient} from '../core-sdk/room/roomAgentClient';
import type {RoomAgentLease} from '../core-sdk/room/roomAgent';

const sleep=(ms:number)=>new Promise<void>(resolve=>setTimeout(resolve,ms));
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
/** Replace an observation/request atomically; Windows readers or scanners may briefly hold the destination. */
export async function publishRoomProbeFile(path:string,bytes:string){
 await writeFile(path+'.pending',bytes,{encoding:'utf8',mode:0o600});
 for(let attempt=0;;attempt++){
  try{await rename(path+'.pending',path);return;}
  catch(error){if(attempt>=7||!['EPERM','EACCES','EBUSY'].includes((error as NodeJS.ErrnoException).code??''))throw error;await sleep(20*(attempt+1));}
 }
}
/** The explicit, bounded file wire shared by CLI and real-book browser probes.
 * It never synthesizes an acknowledgement or owns a second scene/client. */
export class RoomProbeChannel {
 private pendingWrite:Promise<void>=Promise.resolve();
 private exchanging=false;
 private stopped=false;
 private constructor(readonly directory:string,readonly id:string){}
 private async read(name:string,limit:number):Promise<unknown>{
  const path=join(this.directory,name);
  for(let attempt=0;;attempt++){
   try{
    if((await stat(path)).size>limit)throw new Error('Room probe message exceeds its byte limit.');
    const bytes=await readFile(path);if(bytes.length>limit)throw new Error('Room probe message exceeds its byte limit.');return JSON.parse(bytes.toString('utf8'));
   }catch(error){if(attempt>=7||!['EPERM','EACCES','EBUSY'].includes((error as NodeJS.ErrnoException).code??''))throw error;await sleep(20*(attempt+1));}
  }
 }
 private send(value:unknown){
  const bytes=JSON.stringify(value);if(Buffer.byteLength(bytes)>40000)throw new Error('Room probe request exceeds its byte limit.');
  const write=this.pendingWrite.then(()=>publishRoomProbeFile(join(this.directory,'request.json'),bytes));
  this.pendingWrite=write.catch(()=>undefined);return write;
 }
 static async connect(directory:string,timeoutMs=30000){
  directory=resolve(directory);const owner=JSON.parse(await readFile(join(directory,'owner.json'),'utf8')) as unknown;
  if(!record(owner)||owner.version!==1||typeof owner.id!=='string'||!/^[a-f0-9]{32}$/.test(owner.id))throw new Error('Invalid room probe owner receipt.');
  if(!Number.isFinite(timeoutMs)||timeoutMs<100||timeoutMs>120000)throw new Error('Invalid room probe connection timeout.');
  const channel=new RoomProbeChannel(directory,owner.id),deadline=Date.now()+timeoutMs;
  while(Date.now()<deadline){
   try{const ready=await channel.read('ready.json',4096);if(!record(ready)||ready.version!==1||ready.id!==owner.id)throw new Error('Wrong native probe identity.');return channel;}
   catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;await sleep(100);}
  }
  throw new Error('The native room probe did not become ready.');
 }
 async exchange(snapshot:ReturnType<RoomAgentClient['snapshot']>):Promise<{state?:unknown;capture?:unknown}>{
  if(this.stopped)throw new Error('Room probe channel is closed.');
  if(this.exchanging)throw new Error('Room probe exchange is already pending.');
  if(!snapshot||typeof snapshot.clientId!=='string'||!/^[a-f0-9]{32}$/.test(snapshot.clientId))throw new Error('Invalid room probe client identity.');
  this.exchanging=true;
  try{
   await this.send({version:1,id:this.id,operation:'exchange',snapshot});
   const result:{state?:unknown;capture?:unknown}={};
   try{
    const envelope=await this.read('state.json',1024*1024);
    if(!record(envelope)||envelope.version!==1||envelope.id!==this.id||!record(envelope.state))throw new Error('Wrong native room state envelope.');
    // A file from the previous browser document cannot acknowledge this client.
    if(envelope.clientId===snapshot.clientId)result.state=envelope.state;
   }catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
   try{result.capture=await this.read('capture.json',140000);}catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
   try{const terminal=await this.read('terminal.json',4096);throw new Error('Native room probe stopped: '+JSON.stringify(terminal));}catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
   return result;
  }finally{this.exchanging=false;}
 }
 async stop(){this.stopped=true;await this.send({version:1,id:this.id,operation:'stop'});}
}
/** Explicit local adapter for QuestRoomProbe. No room actions, receipts or provider replies are simulated. */
export class HeadlessRoomTransport {
 readonly client=new RoomAgentClient();
 private stopped=false;
 private pump?:Promise<void>;
 private failure:Error|null=null;
 private constructor(private channel:RoomProbeChannel){}
 get directory(){return this.channel.directory;}
 get id(){return this.channel.id;}
 static async connect(directory:string,timeoutMs=30000){
  const deadline=Date.now()+timeoutMs,channel=await RoomProbeChannel.connect(directory,timeoutMs),transport=new HeadlessRoomTransport(channel);
  try{
   transport.pump=transport.run();
   while(!transport.client.lease()&&!transport.failure&&Date.now()<deadline)await sleep(50);
   if(transport.failure)throw transport.failure;if(!transport.client.lease())throw new Error('The native room handshake timed out.');return transport;
  }catch(error){await transport.close(false);throw error;}
 }
 private async run(){
  try{while(!this.stopped){
   const snapshot=this.client.snapshot(),envelope=await this.channel.exchange(snapshot);
   if(envelope.state){
    const previous=this.client.getSnapshot().state,state=envelope.state;
    if(!this.client.receive(state)&&!(record(state)&&previous!==null&&previous.session===state.session&&typeof state.revision==='number'&&state.revision<=previous.revision)){
     await publishRoomProbeFile(join(this.directory,'rejected-state.json'),JSON.stringify({version:1,id:this.id,clientId:snapshot.clientId,state}));
     throw new Error('Native room state failed the shared client contract. See rejected-state.json.');
    }
   }
   if(envelope.capture)this.client.receiveCapture(envelope.capture);
   await sleep(100);
  }}catch(error){this.failure=error instanceof Error?error:new Error(String(error));this.client.cancel();this.stopped=true;}
 }
 checkHealth(){if(this.failure)throw this.failure;}
 lease():RoomAgentLease{this.checkHealth();const lease=this.client.lease();if(!lease)throw new Error('The native room is unavailable or busy.');return lease;}
 async close(stopNative=true){this.stopped=true;this.client.cancel();await this.pump;if(stopNative)await this.channel.stop();}
}
