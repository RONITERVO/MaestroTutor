// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {jpegDimensions} from './inlineImages';
import {sha256} from '@noble/hashes/sha2.js';
import {validRotation,validVector,type Vec3,type Rotation} from './roomRecipe';
export const ROOM_CAPTURE_BYTES=98304;
export interface RoomCaptureMetadata {captureId:string;sha256:string;mimeType:'image/jpeg';width:512;height:384;capturedAt:string;sceneRevision:number;verticalFov:60;position:Vec3;rotation:Rotation}
export interface RoomCaptureImage {capture:RoomCaptureMetadata;data:string}
const object=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
export function validRoomCaptureMetadata(v:unknown):v is RoomCaptureMetadata {
 return object(v)&&typeof v.captureId==='string'&&/^[a-f0-9]{32}$/.test(v.captureId)&&typeof v.sha256==='string'&&/^[a-f0-9]{64}$/.test(v.sha256)&&v.mimeType==='image/jpeg'&&v.width===512&&v.height===384&&v.verticalFov===60&&typeof v.capturedAt==='string'&&/^\d{4}-\d{2}-\d{2}T[0-9:.]+Z$/.test(v.capturedAt)&&v.capturedAt.length<=32&&Number.isFinite(Date.parse(v.capturedAt))&&typeof v.sceneRevision==='number'&&Number.isInteger(v.sceneRevision)&&v.sceneRevision>=1&&v.sceneRevision<=2147483647&&validVector(v.position)&&[v.position.x,v.position.y,v.position.z].every(n=>Math.abs(n)<=1000000)&&validRotation(v.rotation);
}
export function validRoomCaptureImage(v:unknown):v is RoomCaptureImage {
 if(!object(v)||!validRoomCaptureMetadata(v.capture)||typeof v.data!=='string'||!v.data.length||v.data.length>ROOM_CAPTURE_BYTES*4/3||v.data.length%4!==0||!/^[A-Za-z0-9+/]+={0,2}$/.test(v.data))return false;
 try{const bytes=Uint8Array.from(atob(v.data),c=>c.charCodeAt(0));if(bytes.length>ROOM_CAPTURE_BYTES)return false;const size=jpegDimensions(bytes);return size?.width===512&&size.height===384&&Array.from(sha256(bytes),b=>b.toString(16).padStart(2,'0')).join('')===v.capture.sha256;}catch{return false;}
}
export function sameRoomCapture(a:RoomCaptureMetadata,b:RoomCaptureMetadata):boolean {
 return ['captureId','sha256','mimeType','width','height','capturedAt','sceneRevision','verticalFov'].every(k=>a[k as keyof RoomCaptureMetadata]===b[k as keyof RoomCaptureMetadata])&&(['x','y','z'] as const).every(k=>a.position[k]===b.position[k])&&(['x','y','z','w'] as const).every(k=>a.rotation[k]===b.rotation[k]);
}
