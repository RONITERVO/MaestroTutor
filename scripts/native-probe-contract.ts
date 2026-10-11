// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {isDeepStrictEqual} from 'node:util';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import type {CatalogRequest,CatalogView} from '../shared/roomCatalog';

type CatalogReply<Q extends CatalogRequest> = Q extends {operation:'inspect'}
 ? Q extends {category:'actions'} ? Extract<CatalogView,{operation:'inspect';category?:'actions'}>
 : Q extends {category:infer Category} ? Extract<CatalogView,{operation:'inspect';category:Category}>
 : Extract<CatalogView,{operation:'inspect';category?:'actions'}>
 : Extract<CatalogView,{operation:Q['operation']}>;
export type NativeProbeState<C extends readonly RoomCommand[]> = C extends readonly [{action:'catalog';catalog:infer Q extends CatalogRequest}]
 ? RoomAgentState & {catalog:CatalogReply<Q>} : RoomAgentState;

/** Narrow only after the native acknowledgement matches the exact catalog request.
 * Search, inspection and availability-check replies are different wire contracts. */
export function checkedProbeReply<const C extends readonly RoomCommand[]>(commands:C,state:RoomAgentState):NativeProbeState<C>{
 const request=commands.length===1&&commands[0].action==='catalog'?commands[0].catalog:undefined;
 if(request){
  const view=state.catalog;
  if(!view||view.operation!==request.operation)throw new Error('Native catalog reply does not match the requested operation');
  if(request.operation==='search'){
   if(view.operation!=='search'||view.query!==request.query||view.offset!==request.offset||(view.category??'actions')!==(request.category??'actions'))throw new Error('Native catalog search reply does not match the requested page');
  }else if(request.operation==='check'){
   if(view.operation!=='check'||!isDeepStrictEqual(view.call,request.call))throw new Error('Native catalog check replied to a different action');
  }else{
   if(view.operation!=='inspect'||view.capability!==request.capability||view.version!==request.version||(view.category??'actions')!==(request.category??'actions'))throw new Error('Native catalog inspection replied to a different definition');
   if(view.category==='facts'&&!isDeepStrictEqual(view.arguments??{},request.arguments??{}))throw new Error('Native fact reply does not match the requested arguments');
  }
 }
 // The category and identity checks above establish this conditional refinement.
 return state as NativeProbeState<C>;
}

export function factReply(state:RoomAgentState){
 const view=state.catalog;
 if(view?.operation!=='inspect'||view.category!=='facts')throw new Error('Expected a native fact inspection');
 return view;
}

const record=(value:unknown):value is Record<string,unknown>=>value!==null&&typeof value==='object'&&!Array.isArray(value);
const finite=(value:unknown):value is number=>typeof value==='number'&&Number.isFinite(value);
function vector(value:unknown){
 if(!record(value)||!finite(value.x)||!finite(value.y)||!finite(value.z))throw new Error('Expected a finite native placement vector');
 return {x:value.x,y:value.y,z:value.z};
}
export function placementReply(state:RoomAgentState){
 const fact=factReply(state),value=fact.value;
 if(fact.capability!=='object.placement'||!fact.available||!record(value)||typeof value.target!=='string'||value.target!==fact.arguments?.target||!finite(value.scale)||value.scale<=0||!record(value.rotation)||!finite(value.rotation.w))throw new Error('Expected an available placement for the requested target');
 const rotation={...vector(value.rotation),w:value.rotation.w};
 if(Math.abs(Math.hypot(rotation.x,rotation.y,rotation.z,rotation.w)-1)>.001)throw new Error('Native placement rotation is not a unit quaternion');
 return {target:value.target,position:vector(value.position),rotation,scale:value.scale};
}
export function assertSamePlacement(before:RoomAgentState,after:RoomAgentState,label:string){
 const a=placementReply(before),b=placementReply(after),axes=['x','y','z'] as const,rotationAxes=['x','y','z','w'] as const;
 const tolerance=.00001;
 const sameRotation=rotationAxes.every(k=>Math.abs(a.rotation[k]-b.rotation[k])<=tolerance)||rotationAxes.every(k=>Math.abs(a.rotation[k]+b.rotation[k])<=tolerance);
 if(a.target!==b.target||Math.abs(a.scale-b.scale)>tolerance||axes.some(k=>Math.abs(a.position[k]-b.position[k])>tolerance)||!sameRotation)throw new Error(label+': native position, rotation or scale changed');
}
