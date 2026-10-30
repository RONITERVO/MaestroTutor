// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export const ROOM_OBJECT_OVERVIEW_FEATURE='objectOverview.v1';
export const ROOM_OBJECT_OVERVIEW_LIMIT=4;
/** A few context objects, never a complete saved directory or a camera visibility result. */
export interface RoomObjectOverview {version:1;scope:'context';exhaustive:false;limit:4}
const record=(v:unknown):v is Record<string,unknown>=>!!v&&typeof v==='object'&&!Array.isArray(v);
export function validRoomObjectOverview(v:unknown):v is RoomObjectOverview {
 return record(v)&&Object.keys(v).sort().join(',')==='exhaustive,limit,scope,version'
  &&v.version===1&&v.scope==='context'&&v.exhaustive===false&&v.limit===ROOM_OBJECT_OVERVIEW_LIMIT;
}
export function hasRoomObjectOverview(scene:unknown):boolean {
 return record(scene)&&Array.isArray(scene.capabilities)&&scene.capabilities.includes(ROOM_OBJECT_OVERVIEW_FEATURE)&&validRoomObjectOverview(scene.objectOverview);
}
