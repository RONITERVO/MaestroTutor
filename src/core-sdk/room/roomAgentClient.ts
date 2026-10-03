// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {validConstructionSelection,validConstructionManipulation} from '../../../shared/roomSelection';
import {validRoomOwnership} from '../../../shared/roomOwnership';
import {validTemporaryRoom} from '../../../shared/roomSession';
import {capabilityResources} from '../../../shared/capabilities';
import {validExecutionView,identifyExecution} from '../../../shared/roomExecutions';
import {validCatalogView} from '../../../shared/roomCatalog';
import { MAX_ROOM_CAPABILITIES, requireRoomCapabilities, validObjectPhysics, validAvatarMovement, validPhysicsObservation, validAvatarObservation, validAvatarWalkObservation } from '../../../shared/roomControls';
import {validActivityProfile} from '../../../shared/avatarActivities';
import {validMotionSearchView} from '../../../shared/roomMotions';
import {validRuleView} from './rules';
import {parseRecipe,validPigment} from './recipe';
import { parseRoomCommands, type RoomAgentLease, type RoomAgentState, type RoomCommand } from './roomAgent';
const record=(v:unknown):v is Record<string,unknown> => v!==null && typeof v==='object' && !Array.isArray(v);
const vector=(v:unknown) => record(v) && ['x','y','z'].every(k=>typeof v[k]==='number' && Number.isFinite(v[k]));
const integer=(v:unknown,min=0) => typeof v==='number' && Number.isInteger(v) && v>=min && v<=2147483647;
const id=(v:unknown) => typeof v==='string' && /^[a-f0-9]{32}$/.test(v);
export class RoomAgentClient {
  private value:RoomAgentState|null=null;
  private clientId=crypto.randomUUID().replace(/-/g,'');
  private rejectedSessions=new Set<string>();
  private listeners=new Set<()=>void>();
  private view:{state:RoomAgentState|null;pending:boolean}={state:null,pending:false};
  subscribe=(listener:()=>void)=>{this.listeners.add(listener);return()=>{this.listeners.delete(listener);};};
  getSnapshot=()=>this.view;
  private publish(){this.view={state:this.value,pending:Boolean(this.pending)};for(const listener of this.listeners)listener();}
  private generation=0;
  private sequence=0;
  private pending?:{request:{version:1|2;session:string;sequence:number;sceneRevision:number;commands:RoomCommand[];conditions:{id:string;revision:number}[]};resolve:(value:RoomAgentState)=>void;reject:(error:Error)=>void;timer:ReturnType<typeof setTimeout>;detachAbort:()=>void};
  private lastSeen=0;
  receive=(input:unknown) => {
    if (!record(input) || input.version!==1 || !id(input.session) || !integer(input.revision,1) || !integer(input.sceneRevision,1) || !integer(input.ack) ||
      typeof input.status!=='string' || input.status.length>2048 || !['ok','canUndo','canRedo','physicsRunning'].every(k=>typeof input[k]==='boolean') ||
      !Array.isArray(input.created) || input.created.length>16 || !input.created.every(id) || !Array.isArray(input.objects) || input.objects.length>66 || JSON.stringify(input).length>327680) return false;
    if(input.objects.some(o=>!record(o) || typeof o.id!=='string' || !/^(book|maestro|[a-f0-9]{32})$/.test(o.id) || typeof o.name!=='string' || o.name.length>80 || typeof o.kind!=='string' || !vector(o.position) || typeof o.scale!=='number' || !Number.isFinite(o.scale) || !validPigment(o.color) || typeof o.animated!=='boolean' || o.objectRevision!==undefined&&!integer(o.objectRevision,1))) return false;
    if(input.capabilities!==undefined&&(!Array.isArray(input.capabilities)||input.capabilities.length>MAX_ROOM_CAPABILITIES||!input.capabilities.every(c=>typeof c==='string'&&/^[a-zA-Z][a-zA-Z0-9.]{0,63}$/.test(c))||new Set(input.capabilities).size!==input.capabilities.length))return false;
    if(input.constructionSelection!==undefined&&input.constructionSelection!==null&&!validConstructionSelection(input.constructionSelection,input.objects as RoomAgentState['objects'])||Array.isArray(input.capabilities)&&input.capabilities.includes('constructionSelection.v1')&&!validConstructionSelection(input.constructionSelection,input.objects as RoomAgentState['objects']))return false;
    if(input.constructionManipulation!==undefined&&input.constructionManipulation!==null&&!validConstructionManipulation(input.constructionManipulation,input.constructionSelection as RoomAgentState['constructionSelection'])||Array.isArray(input.capabilities)&&input.capabilities.includes('constructionManipulation.v1')&&(!validConstructionSelection(input.constructionSelection,input.objects as RoomAgentState['objects'])||!validConstructionManipulation(input.constructionManipulation,input.constructionSelection)))return false;
    if(input.temporaryRoom!==undefined&&!validTemporaryRoom(input.temporaryRoom)||Array.isArray(input.capabilities)&&input.capabilities.includes('temporaryRoom.v1')&&!validTemporaryRoom(input.temporaryRoom))return false;
    if(input.ownership!==undefined&&input.ownership!==null&&!validRoomOwnership(input.ownership)||Array.isArray(input.capabilities)&&input.capabilities.includes('roomOwnership.v1')&&!validRoomOwnership(input.ownership))return false;
    if(input.physics!==undefined&&input.physics!==null&&(!validPhysicsObservation(input.physics)||input.physics.running!==input.physicsRunning))return false;
    if(input.activityProfile!==undefined&&input.activityProfile!==null&&!validActivityProfile(input.activityProfile,true))return false;
    if(input.walk!==undefined&&input.walk!==null&&!validAvatarWalkObservation(input.walk))return false;
    if(input.avatar!==undefined&&input.avatar!==null&&!validAvatarObservation(input.avatar))return false;
    if(input.objects.some(o=>o.physics!==undefined&&!validObjectPhysics(o.physics)||o.movement!==undefined&&o.movement!==null&&!validAvatarMovement(o.movement)||['held','simulating'].some(key=>o[key]!==undefined&&typeof o[key]!=='boolean')))return false;
    if(input.visible!==undefined && typeof input.visible!=='boolean')return false;
    if(input.execution!==undefined&&input.execution!==null&&!validExecutionView(input.execution))return false;
    if(Array.isArray(input.capabilities)&&input.capabilities.includes('workspaceMaintenance.v1')&&(!record(input.execution)||!record(input.execution.workspace)))return false;
    if(input.catalog!==undefined&&input.catalog!==null&&!validCatalogView(input.catalog))return false;
    if(input.motions!==undefined&&input.motions!==null&&!validMotionSearchView(input.motions))return false;
    if(input.rules!==undefined&&input.rules!==null&&!validRuleView(input.rules))return false;
    if(input.workspaceView!==undefined&&!['objects','rules'].includes(input.workspaceView as string))return false;
    const inspection=input.inspection;
    if(inspection!==undefined && inspection!==null && (!record(inspection)||typeof inspection.id!=='string'||!input.objects.some(o=>o.id===inspection.id)||!integer(inspection.objectRevision,1)||inspection.recipe!==null&&!parseRecipe(inspection.recipe)))return false;
    if(record(inspection)&&inspection.partId!==undefined&&inspection.partId!==null&&inspection.partId!==''&&(typeof inspection.partId!=='string'||!/^[a-zA-Z0-9_]{1,32}$/.test(inspection.partId)))return false;
    const next=input as unknown as RoomAgentState;
    if(this.rejectedSessions.has(next.session))return false;
    if(this.value?.session===next.session && next.revision<=this.value.revision) return false;
    if(this.value?.session!==next.session) {this.reset(false);this.sequence=next.ack;}
    this.value=next;this.lastSeen=Date.now();this.sequence=Math.max(this.sequence,next.ack);
    if(this.pending && next.ack===this.pending.request.sequence) {const p=this.pending;this.pending=undefined;clearTimeout(p.timer);p.detachAbort();p.resolve(next);}
    this.publish();return true;
  };
  cancel() {this.reset(true);}
  private reset(rotate:boolean) {
    if(this.value){this.rejectedSessions.add(this.value.session);while(this.rejectedSessions.size>16)this.rejectedSessions.delete(this.rejectedSessions.values().next().value!);}
    if(rotate)this.clientId=crypto.randomUUID().replace(/-/g,'');
    this.generation++;
    if(this.pending) {clearTimeout(this.pending.timer);this.pending.detachAbort();this.pending.reject(new DOMException('Room request interrupted; inspect the room before retrying.','AbortError'));this.pending=undefined;}
    this.value=null;this.publish();
  }
  request(commands:RoomCommand[],expected?:RoomAgentState) {
    const lease=this.lease();if(!lease)return Promise.reject(new Error('The room is disconnected or another action is pending.'));
    const scene=expected??lease.state();if(scene.session!==lease.state().session)return Promise.reject(new Error('The room session changed. Reload the latest object before editing.'));return lease.execute(commands,scene.sceneRevision,scene.objects);
  }
  snapshot=() => ({clientId:this.clientId,session:this.value?.session??'',request:this.pending?.request??null});
  lease(allowPendingObservation=false):RoomAgentLease|null {
    if(!this.value || Date.now()-this.lastSeen>3000 || (this.pending && !allowPendingObservation)) return null;
    const generation=this.generation,session=this.value.session;
    const valid=() => generation===this.generation && this.value?.session===session && Date.now()-this.lastSeen<=3000;
    return {valid,state:()=>{if(!valid()) throw new Error('Room session unavailable');return this.value!;},execute:(commands,expectedRevision,expectedObjects,signal)=>{
      if(signal?.aborted)return Promise.reject(new DOMException('Room request cancelled before dispatch.','AbortError'));
      if(!valid() || this.pending || this.sequence>=2147483647) return Promise.reject(new Error('Room session unavailable or busy'));
      parseRoomCommands({commands});requireRoomCapabilities(commands,this.value!);
      if(this.value!.capabilities?.includes('executionReceipts.v1'))commands=commands.map(c=>c.action==='execution'&&c.execution?{...c,execution:identifyExecution(c.execution,this.value!.execution)}:c);
      const objects=expectedObjects??this.value!.objects;
      const modern=objects.every(object=>integer(object.objectRevision,1));
      const targets=new Set(commands.flatMap(command=>command.action==='execution'&&command.execution?.operation==='start'?capabilityResources(command.execution.call.id,command.execution.call.arguments):command.target?[command.target]:[]));
      const conditions=objects.filter(object=>targets.has(object.id)).map(object=>({id:object.id,revision:object.objectRevision!}));
      const request={version:(modern?2:1) as 1|2,session,sequence:++this.sequence,sceneRevision:expectedRevision,conditions,commands:commands.map(command => command.action === 'create' ? {scale:1,color:{r:1,g:1,b:1,a:1},...command} : command)};
      return new Promise<RoomAgentState>((resolve,reject)=>{
        const abort=()=>{if(this.pending?.request===request)this.cancel();};
        const timer=setTimeout(abort,15000);
        this.pending={request,resolve,reject,timer,detachAbort:()=>signal?.removeEventListener('abort',abort)};
        signal?.addEventListener('abort',abort,{once:true});
        this.publish();
      });
    }};
  }
}
