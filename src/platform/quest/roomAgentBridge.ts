// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {RoomAgentClient} from '../../core-sdk/room/roomAgentClient';
export {RoomAgentClient} from '../../core-sdk/room/roomAgentClient';
let installed:RoomAgentClient|undefined;
export const currentRoomAgentLease=() => installed?.lease(true)??null;
export function registerRoomAgent(client:RoomAgentClient) {installed=client;return()=>{client.cancel();if(installed===client) installed=undefined;};}
