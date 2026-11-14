// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {capabilityDefinition} from '../shared/capabilities';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import type {HeadlessRoomTransport} from '../src/headless/roomTransport';
import {completeProbeAction,factReply,type NativeProbeState} from './native-probe-contract';
import {probeAudioMaintenance} from './probe-audio-maintenance';
import {maintenanceFactRequest} from './probe-maintenance-facts';

export async function probeNativeAudioMaintenance(transport:HeadlessRoomTransport,
 execute:<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>,
 reject:(commands:RoomCommand[])=>Promise<RoomAgentState>,directory:string){
 try {await probeAudioMaintenance({
  fact:async(id,args,version=1)=>factReply(await execute([{action:'catalog',catalog:maintenanceFactRequest(id,args,version)}])),
  invoke:async(id,args)=>(await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id,version:capabilityDefinition(id)!.version,arguments:args}}}]),120000)).execution!.selected!,
  reject:async(id,args)=>{
   let state=await reject([{action:'execution',execution:{operation:'start',call:{id,version:capabilityDefinition(id)!.version,arguments:args}}}]);
   const runId=state.execution?.selected?.id,deadline=Date.now()+120000;
   while(state.ok&&runId&&['preparing','running'].includes(state.execution!.selected!.phase)&&Date.now()<deadline){await new Promise(resolve=>setTimeout(resolve,100));state=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);}
   return state;
  },
  offer:async audio=>{transport.setChatSource(()=>({scope:'audio-maintenance-'+audio.hash,messages:[{id:'maintenance-original',role:'assistant',timestamp:1,text:'Explicit synthetic original audio for reimport.',imageUrl:audio.dataUrl,imageMimeType:'audio/wav',maestroToolKind:'audio-note',attachmentName:audio.name}]}));},
  replay:async receipt=>(await execute([{action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}}])).execution!.selected!,
 },directory,'headless');}finally{transport.setChatSource(()=>({scope:'audio-maintenance-ended',messages:[]}));}
}
