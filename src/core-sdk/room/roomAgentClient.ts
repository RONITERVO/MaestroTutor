// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { ChatImageSource } from './chatImageSource';
import { ChatAudioSource } from './chatAudioSource';
import { validChatAudioRequest } from '../../../shared/chatAudio';
import { validChatImageRequest } from '../../../shared/chatImages';
import {validRoomCaptureMetadata,validRoomCaptureImage,sameRoomCapture,type RoomCaptureImage} from '../../../shared/roomViewCapture';
import {validConstructionSelection,validConstructionManipulation} from '../../../shared/roomSelection';
import {validRoomOwnership} from '../../../shared/roomOwnership';
import {validTemporaryRoom} from '../../../shared/roomSession';
import {roomCommandConditions,roomCommandTargets,roomRevisionBasis,roomObjectId,validRoomObjectObservation} from './roomObjectContext';
import {validExecutionView,identifyExecution,executionForCapability} from '../../../shared/roomExecutions';
import {validCatalogView} from '../../../shared/roomCatalog';
import { MAX_ROOM_CAPABILITIES, requireRoomCapabilities, validPhysicsObservation, validAvatarObservation, validAvatarWalkObservation } from '../../../shared/roomControls';
import {validActivityProfile} from '../../../shared/avatarActivities';
import {validMotionSearchView} from '../../../shared/roomMotions';
import {validRuleView} from './rules';
import {parseRecipe} from './recipe';
import { parseRoomCommands, type RoomAgentLease, type RoomAgentState, type RoomCommand, type RoomObjectRevision } from './roomAgent';
import {objectReferenceRequest,readObjectReference} from '../../../shared/objectReference';
export interface RoomCommandContext {state:RoomAgentState;objects:RoomObjectRevision[]}
const record=(v:unknown):v is Record<string,unknown> => v!==null && typeof v==='object' && !Array.isArray(v);
const integer=(v:unknown,min=0) => typeof v==='number' && Number.isInteger(v) && v>=min && v<=2147483647;
const id=(v:unknown) => typeof v==='string' && /^[a-f0-9]{32}$/.test(v);
export class RoomAgentClient {
  readonly chatImages = new ChatImageSource();
  readonly chatAudio = new ChatAudioSource();
  private mediaFirst = false;
  private value:RoomAgentState|null=null;
  private clientId=crypto.randomUUID().replace(/-/g,'');
  private rejectedSessions=new Set<string>();
  private listeners=new Set<()=>void>();
  private view:{state:RoomAgentState|null;pending:boolean}={state:null,pending:false};
  subscribe=(listener:()=>void)=>{this.listeners.add(listener);return()=>{this.listeners.delete(listener);};};
  getSnapshot=()=>this.view;
  private publish(){this.view={state:this.value,pending:Boolean(this.pending)};for(const listener of this.listeners)listener();}
  private captures=new Map<string,RoomCaptureImage>();
  private captureAck:string|undefined;
  receiveCapture=(input:unknown):boolean=>{
    if(!record(input)||input.version!==1||input.revision!==1||input.session!==this.value?.session||!this.value?.capture||!record(input.capture)||input.capture.captureId!==this.value.capture.captureId||input.capture.sha256!==this.value.capture.sha256||JSON.stringify(input).length>140000||!validRoomCaptureImage(input)||!sameRoomCapture(input.capture,this.value.capture))return false;
    const frame=structuredClone({capture:input.capture,data:input.data});this.captures.set(frame.capture.captureId,frame);while(this.captures.size>6)this.captures.delete(this.captures.keys().next().value!);this.captureAck=frame.capture.captureId;this.publish();return true;
  };
  captureImage=(id:string)=>{const image=this.captures.get(id);return image?structuredClone(image):null;};
  private generation=0;
  private sequence=0;
  private pending?:{request:{version:1|2;session:string;sequence:number;sceneRevision:number;commands:RoomCommand[];conditions:{id:string;revision:number}[]};resolve:(value:RoomAgentState)=>void;reject:(error:Error)=>void;timer:ReturnType<typeof setTimeout>;detachAbort:()=>void};
  private lastSeen=0;
  receive=(input:unknown) => {
    if (!record(input) || input.version!==1 || !id(input.session) || !integer(input.revision,1) || !integer(input.sceneRevision,1) || !integer(input.ack) ||
      typeof input.status!=='string' || input.status.length>2048 || !['ok','canUndo','canRedo','physicsRunning'].every(k=>typeof input[k]==='boolean') ||
      !Array.isArray(input.created) || input.created.length>16 || !input.created.every(id) || !Array.isArray(input.objects) || input.objects.length>66 || JSON.stringify(input).length>327680) return false;
    if(!input.objects.every(validRoomObjectObservation)||new Set(input.objects.map(o=>o.id)).size!==input.objects.length)return false;
    if(input.capabilities!==undefined&&(!Array.isArray(input.capabilities)||input.capabilities.length>MAX_ROOM_CAPABILITIES||!input.capabilities.every(c=>typeof c==='string'&&/^[a-zA-Z][a-zA-Z0-9.]{0,63}$/.test(c))||new Set(input.capabilities).size!==input.capabilities.length))return false;
    if(input.constructionSelection!==undefined&&input.constructionSelection!==null&&!validConstructionSelection(input.constructionSelection)||Array.isArray(input.capabilities)&&input.capabilities.includes('constructionSelection.v1')&&!validConstructionSelection(input.constructionSelection))return false;
    if(input.constructionManipulation!==undefined&&input.constructionManipulation!==null&&!validConstructionManipulation(input.constructionManipulation,input.constructionSelection as RoomAgentState['constructionSelection'])||Array.isArray(input.capabilities)&&input.capabilities.includes('constructionManipulation.v1')&&(!validConstructionSelection(input.constructionSelection)||!validConstructionManipulation(input.constructionManipulation,input.constructionSelection)))return false;
    if(input.temporaryRoom!==undefined&&!validTemporaryRoom(input.temporaryRoom)||Array.isArray(input.capabilities)&&input.capabilities.includes('temporaryRoom.v1')&&!validTemporaryRoom(input.temporaryRoom))return false;
    if(input.ownership!==undefined&&input.ownership!==null&&!validRoomOwnership(input.ownership)||Array.isArray(input.capabilities)&&input.capabilities.includes('roomOwnership.v1')&&!validRoomOwnership(input.ownership))return false;
    if(input.physics!==undefined&&input.physics!==null&&(!validPhysicsObservation(input.physics)||input.physics.running!==input.physicsRunning))return false;
    if(input.activityProfile!==undefined&&input.activityProfile!==null&&!validActivityProfile(input.activityProfile,true))return false;
    if(input.walk!==undefined&&input.walk!==null&&!validAvatarWalkObservation(input.walk))return false;
    if(input.avatar!==undefined&&input.avatar!==null&&!validAvatarObservation(input.avatar))return false;
    if(input.visible!==undefined && typeof input.visible!=='boolean')return false;
    if(input.execution!==undefined&&input.execution!==null&&!validExecutionView(input.execution))return false;
    if(Array.isArray(input.capabilities)&&input.capabilities.includes('workspaceMaintenance.v1')&&(!record(input.execution)||!record(input.execution.workspace)))return false;
    if(input.catalog!==undefined&&input.catalog!==null&&!validCatalogView(input.catalog))return false;
    if(input.motions!==undefined&&input.motions!==null&&!validMotionSearchView(input.motions))return false;
    if(input.rules!==undefined&&input.rules!==null&&!validRuleView(input.rules))return false;
    if(input.workspaceView!==undefined&&!['objects','rules'].includes(input.workspaceView as string))return false;
    const inspection=input.inspection;
    if(inspection!==undefined && inspection!==null && (!record(inspection)||!roomObjectId(inspection.id)||!integer(inspection.objectRevision,1)||inspection.recipe!==null&&!parseRecipe(inspection.recipe)))return false;
    if(record(inspection)){
      const listed=input.objects.find(o=>o.id===inspection.id),item=inspection.item;
      if(item!==undefined&&(!validRoomObjectObservation(item)||item.id!==inspection.id||item.objectRevision!==inspection.objectRevision))return false;
      if(item===undefined&&!listed||listed?.objectRevision!==undefined&&listed.objectRevision!==inspection.objectRevision)return false;
    }
    if(record(inspection)&&inspection.partId!==undefined&&inspection.partId!==null&&inspection.partId!==''&&(typeof inspection.partId!=='string'||!/^[a-zA-Z0-9_]{1,32}$/.test(inspection.partId)))return false;
    if(input.capture!==undefined&&input.capture!==null&&!validRoomCaptureMetadata(input.capture))return false;
    if(input.chatImage!==undefined&&input.chatImage!==null&&!validChatImageRequest(input.chatImage))return false;
    if(input.chatAudio!==undefined&&input.chatAudio!==null&&!validChatAudioRequest(input.chatAudio))return false;
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
    this.captures.clear();this.captureAck=undefined;
    this.generation++;
    if(this.pending) {clearTimeout(this.pending.timer);this.pending.detachAbort();this.pending.reject(new DOMException('Room request interrupted; inspect the room before retrying.','AbortError'));this.pending=undefined;}
    this.value=null;this.publish();
  }
  request(commands:RoomCommand[],expected?:RoomAgentState,expectedObjects?:readonly RoomObjectRevision[]) {
    const lease=this.lease();if(!lease)return Promise.reject(new Error('The room is disconnected or another action is pending.'));
    const scene=expected??lease.state();if(scene.session!==lease.state().session||scene.temporaryRoom?.id!==lease.state().temporaryRoom?.id)return Promise.reject(new Error('The room session changed. Reload the latest object before editing.'));return lease.execute(commands,scene.sceneRevision,expectedObjects??roomRevisionBasis(scene));
  }
  /** Read only missing exact targets. Never refresh evidence the author already reviewed. */
  prepare(commands:RoomCommand[],expected?:RoomAgentState,expectedObjects?:readonly RoomObjectRevision[]):RoomCommandContext|Promise<RoomCommandContext> {
    const lease=this.lease();if(!lease)throw new Error('The room is disconnected or another action is pending.');
    const state=expected??lease.state(),live=lease.state();
    if(state.session!==live.session||state.temporaryRoom?.id!==live.temporaryRoom?.id)throw new Error('The room session changed. Reload the latest object before editing.');
    parseRoomCommands({commands});requireRoomCapabilities(commands,live);
    const objects=(expectedObjects??roomRevisionBasis(state)).map(o=>({...o}));
    if(this.reconciling(commands)||objects.some(o=>o.objectRevision===undefined))return {state,objects};
    const missing=[...roomCommandTargets(commands).required].filter(id=>!objects.some(o=>o.id===id));
    if(!missing.length){roomCommandConditions(commands,objects);return {state,objects};}
    return (async()=>{
    for(const target of missing){
      if(!roomObjectId(target))throw new Error('Choose an exact saved object before editing: '+target);
      const receipt=await lease.execute([{action:'catalog',catalog:objectReferenceRequest(target)}],state.sceneRevision,[]);
      if(!lease.valid())throw new Error('The room session changed while reading action objects.');
      const read=receipt.ok?readObjectReference(receipt.catalog,target):null;
      if(!read)throw new Error(receipt.ok?'The saved object cannot be read now: '+target:receipt.status);
      objects.push({id:target,objectRevision:read.revision});
    }
    roomCommandConditions(commands,objects);
    return {state,objects};
    })();
  }
  /** Explicit user-action preparation, shared with headless clients. */
  requestPrepared(commands:RoomCommand[],expected?:RoomAgentState,expectedObjects?:readonly RoomObjectRevision[]):Promise<RoomAgentState> {
    const prepared=this.prepare(commands,expected,expectedObjects);
    const dispatch=(context:RoomCommandContext)=>this.request(commands,context.state,context.objects);
    return prepared instanceof Promise?prepared.then(dispatch):dispatch(prepared);
  }
  private reconciling(commands:readonly RoomCommand[]):boolean {
    const start=commands.length===1&&commands[0].action==='execution'&&commands[0].execution?.operation==='start'?commands[0].execution:null;
    return Boolean(start?.runId&&this.value?.capabilities?.includes('executionReceipts.v1')&&start.runId!==executionForCapability(start.call.id,this.value.execution)?.nextRunId);
  }
  snapshot=() => {
    const snapshot = {clientId:this.clientId,session:this.value?.session??'',...(this.captureAck?{captureAck:this.captureAck}:{}),request:this.pending?.request??null};
    const media: {images?: ReturnType<ChatImageSource['snapshot']>; sounds?: ReturnType<ChatAudioSource['snapshot']>} = {};
    const images=this.value?.capabilities?.includes('chatImages.v1')?this.chatImages.snapshot(this.value.chatImage):undefined;
    const sounds=this.value?.capabilities?.includes('chatAudio.v1')?this.chatAudio.snapshot(this.value.chatAudio):undefined;
    // One bounded wire serves actions and both media types. Rotate priority so
    // concurrent image and audio selections cannot starve one another.
    this.mediaFirst=!this.mediaFirst;
    const addImage=()=>{if(images&&JSON.stringify({...snapshot,...media,images}).length<=32768)media.images=images;};
    const addSound=()=>{if(sounds&&JSON.stringify({...snapshot,...media,sounds}).length<=32768)media.sounds=sounds;};
    if(this.mediaFirst){addImage();addSound();}else{addSound();addImage();}
    return {...snapshot,...media};
  };
  lease(allowPendingObservation=false):RoomAgentLease|null {
    if(!this.value || Date.now()-this.lastSeen>3000 || (this.pending && !allowPendingObservation)) return null;
    const generation=this.generation,session=this.value.session,temporary=this.value.temporaryRoom?.id;
    const valid=() => generation===this.generation && this.value?.session===session && this.value?.temporaryRoom?.id===temporary && Date.now()-this.lastSeen<=3000;
    return {valid,capture:(id,signal)=>new Promise<RoomCaptureImage>((resolve,reject)=>{
      let timer:ReturnType<typeof setTimeout>|undefined;let unsubscribe=()=>{};
      const finish=(image:RoomCaptureImage|null,error?:Error)=>{if(timer)clearTimeout(timer);unsubscribe();signal?.removeEventListener('abort',aborted);if(image)resolve(image);else reject(error??new Error('The virtual room snapshot is unavailable. Capture a fresh view.'));};
      const aborted=()=>finish(null,new DOMException('Room snapshot interrupted.','AbortError'));
      const check=()=>{if(signal?.aborted||!valid()){aborted();return;}const image=this.captureImage(id);if(image)finish(image);};
      unsubscribe=this.subscribe(check);timer=setTimeout(()=>finish(null),5000);signal?.addEventListener('abort',aborted,{once:true});check();
    }),state:()=>{if(!valid()) throw new Error('Room session unavailable');return this.value!;},execute:(commands,expectedRevision,expectedObjects,signal)=>{
      if(signal?.aborted)return Promise.reject(new DOMException('Room request cancelled before dispatch.','AbortError'));
      if(!valid() || this.pending || this.sequence>=2147483647) return Promise.reject(new Error('Room session unavailable or busy'));
      parseRoomCommands({commands});requireRoomCapabilities(commands,this.value!);
      if(this.value!.capabilities?.includes('executionReceipts.v1'))commands=commands.map(c=>c.action==='execution'&&c.execution?{...c,execution:identifyExecution(c.execution,this.value!.execution)}:c);
      const context=roomCommandConditions(commands,expectedObjects??roomRevisionBasis(this.value!),this.reconciling(commands));
      const request={...context,session,sequence:++this.sequence,sceneRevision:expectedRevision,commands:commands.map(command => command.action === 'create' ? {scale:1,color:{r:1,g:1,b:1,a:1},...command} : command)};
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
