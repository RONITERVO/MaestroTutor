// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import {validFactValue} from '../shared/behaviourFacts';
import {validAvatarMovement,type AvatarMovementSettings} from '../shared/roomControls';
import {checkedProbeReply,factReply} from './native-probe-contract';
export async function readAvatarMovementSettings(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>):Promise<AvatarMovementSettings>{
 const commands:RoomCommand[]=[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'avatar.movement.settings',version:1}}];
 const state=checkedProbeReply(commands,await execute(commands)),fact=factReply(state);
 if(!state.ok||!fact.available||!validFactValue('avatar.movement.settings',fact.value))throw new Error('Missing explicit avatar movement preferences');
 const value=fact.value as {distance:number;speed:number;temporary:boolean},settings={distance:value.distance,speed:value.speed};
 if(value.temporary!==(state.temporaryRoom?.active??false)||state.temporaryRoom?.pending||!validAvatarMovement(settings))throw new Error('Avatar movement preferences are invalid or belong to another room');
 return settings;
}
