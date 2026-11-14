// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {capabilityDefinition} from '../shared/capabilities';
import {behaviourFact} from '../shared/behaviourCatalog';
import {validateFactArguments} from '../shared/behaviourFacts';
import type {CatalogRequest} from '../shared/roomCatalog';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import type {HeadlessRoomTransport} from '../src/headless/roomTransport';
import {completeProbeAction,factReply,type NativeProbeState} from './native-probe-contract';
import {probeImageMaintenance} from './probe-image-maintenance';

/** A no-input fact must omit the wire field entirely; `{arguments:{}}` is a
 * different request and is correctly refused by the native catalog. */
export function imageMaintenanceFactRequest(id:string,args:Record<string,unknown>,version=1):Extract<CatalogRequest,{operation:'inspect'}>{
 const definition=behaviourFact(id);assert.ok(definition,'Unknown image maintenance fact');assert.equal(definition.version,version,'Unexpected fact version');
 assert.ok(definition.input||Object.keys(args).length===0,'A no-input fact cannot receive argument values');
 const input=definition.input?args:undefined;assert.equal(validateFactArguments(id,version,input),null);
 return {operation:'inspect',category:'facts',capability:id,version,...(input===undefined?{}:{arguments:input})};
}

export async function probeNativeImageMaintenance(transport:HeadlessRoomTransport,
 execute:<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>,
 reject:(commands:RoomCommand[])=>Promise<RoomAgentState>,directory:string){
 try {await probeImageMaintenance({
  fact:async(id,args,version=1)=>factReply(await execute([{action:'catalog',catalog:imageMaintenanceFactRequest(id,args,version)}])),
  invoke:async(id,args)=>(await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id,version:capabilityDefinition(id)!.version,arguments:args}}}]),120000)).execution!.selected!,
  reject:async(id,args)=>{
   let state=await reject([{action:'execution',execution:{operation:'start',call:{id,version:capabilityDefinition(id)!.version,arguments:args}}}]);
   const runId=state.execution?.selected?.id,deadline=Date.now()+120000;
   while(state.ok&&runId&&['preparing','running'].includes(state.execution!.selected!.phase)&&Date.now()<deadline){await new Promise(resolve=>setTimeout(resolve,100));state=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);}
   return state;
  },
  offer:async image=>{transport.setChatSource(()=>({scope:'image-maintenance-'+image.hash,messages:[{id:'maintenance-original',role:'assistant',timestamp:1,text:'Explicit synthetic original image for reimport.',imageUrl:image.dataUrl,imageMimeType:'image/png',imageOrigin:'generated',attachmentName:image.name}]}));},
  replay:async receipt=>(await execute([{action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}}])).execution!.selected!,
 },directory,'headless');}finally{transport.setChatSource(()=>({scope:'image-maintenance-ended',messages:[]}));}
}
