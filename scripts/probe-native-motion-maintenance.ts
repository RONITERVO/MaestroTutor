// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {capabilityDefinition} from '../shared/capabilities';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import {completeProbeAction,factReply,type NativeProbeState} from './native-probe-contract';
import {probeMotionMaintenance} from './probe-motion-maintenance';

export async function probeNativeMotionMaintenance(
 execute:<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>,
 reject:(commands:RoomCommand[])=>Promise<RoomAgentState>,directory:string,
){
 await probeMotionMaintenance({
  fact:async(id,args)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:id,version:1,arguments:args}}])),
  invoke:async(id,args)=>(await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id,version:capabilityDefinition(id)!.version,arguments:args}}}]),120000)).execution!.selected!,
  reject:async(id,args)=>reject([{action:'execution',execution:{operation:'start',call:{id,version:capabilityDefinition(id)!.version,arguments:args}}}]),
  replay:async receipt=>(await execute([{action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}}])).execution!.selected!,
 },directory,'headless');
}
