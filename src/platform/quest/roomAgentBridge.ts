// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { parseRoomCommands, type RoomAgentLease, type RoomAgentState, type RoomCommand } from '../../core-sdk/room/roomAgent';
const record=(v:unknown):v is Record<string,unknown> => v!==null && typeof v==='object' && !Array.isArray(v);
const vector=(v:unknown) => record(v) && ['x','y','z'].every(k=>typeof v[k]==='number' && Number.isFinite(v[k]));
const integer=(v:unknown,min=0) => typeof v==='number' && Number.isInteger(v) && v>=min && v<=2147483647;
const id=(v:unknown) => typeof v==='string' && /^[a-f0-9]{32}$/.test(v);
export class RoomAgentClient {
  private value:RoomAgentState|null=null;
  private generation=0;
  private sequence=0;
  private pending?:{request:{version:1;session:string;sequence:number;sceneRevision:number;commands:RoomCommand[]};resolve:(value:RoomAgentState)=>void;reject:(error:Error)=>void;timer:ReturnType<typeof setTimeout>};
  private lastSeen=0;
  receive=(input:unknown) => {
    if (!record(input) || input.version!==1 || !id(input.session) || !integer(input.revision,1) || !integer(input.sceneRevision,1) || !integer(input.ack) ||
      typeof input.status!=='string' || input.status.length>2048 || !['ok','canUndo','canRedo','physicsRunning'].every(k=>typeof input[k]==='boolean') ||
      !Array.isArray(input.created) || input.created.length>8 || !input.created.every(id) || !Array.isArray(input.objects) || input.objects.length>66 || JSON.stringify(input).length>32768) return false;
    if(input.objects.some(o=>!record(o) || typeof o.id!=='string' || !/^(book|maestro|[a-f0-9]{32})$/.test(o.id) || typeof o.name!=='string' || o.name.length>80 || typeof o.kind!=='string' || !vector(o.position) || typeof o.scale!=='number' || !Number.isFinite(o.scale) || !record(o.color) || typeof o.animated!=='boolean')) return false;
    const next=input as unknown as RoomAgentState;
    if(this.value?.session===next.session && next.revision<=this.value.revision) return false;
    if(this.value?.session!==next.session) {this.cancel();this.sequence=next.ack;}
    this.value=next;this.lastSeen=Date.now();this.sequence=Math.max(this.sequence,next.ack);
    if(this.pending && next.ack===this.pending.request.sequence) {const p=this.pending;this.pending=undefined;clearTimeout(p.timer);p.resolve(next);}
    return true;
  };
  cancel() {
    this.generation++;
    if(this.pending) {clearTimeout(this.pending.timer);this.pending.reject(new DOMException('Room request interrupted; inspect the room before retrying.','AbortError'));this.pending=undefined;}
    this.value=null;
  }
  snapshot=() => ({session:this.value?.session??'',request:this.pending?.request??null});
  lease():RoomAgentLease|null {
    if(!this.value || Date.now()-this.lastSeen>3000 || this.pending) return null;
    const generation=this.generation,session=this.value.session;
    const valid=() => generation===this.generation && this.value?.session===session && Date.now()-this.lastSeen<=3000;
    return {valid,state:()=>{if(!valid()) throw new Error('Room session unavailable');return this.value!;},execute:(commands,expectedRevision)=>{
      if(!valid() || this.pending || this.sequence>=2147483647) return Promise.reject(new Error('Room session unavailable or busy'));
      parseRoomCommands({commands});
      const request={version:1 as const,session,sequence:++this.sequence,sceneRevision:expectedRevision,commands:commands.map(command => command.action === 'create' ? {scale:1,color:{r:1,g:1,b:1,a:1},...command} : command)};
      return new Promise<RoomAgentState>((resolve,reject)=>{
        const timer=setTimeout(()=>{this.cancel();},15000);this.pending={request,resolve,reject,timer};
      });
    }};
  }
}
let installed:RoomAgentClient|undefined;
export const currentRoomAgentLease=() => installed?.lease()??null;
export function registerRoomAgent(client:RoomAgentClient) {installed=client;return()=>{client.cancel();if(installed===client) installed=undefined;};}
