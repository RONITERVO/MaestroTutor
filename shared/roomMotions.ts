// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface MotionQuery {query:string;offset:number;includeShort:boolean;favouritesOnly:boolean;archivedOnly:boolean}
export interface MotionSearchEntry {id:string;name:string;tags:string[];duration:number;shortClip:boolean;favourite:boolean;archived:boolean;downloaded:boolean}
export interface MotionSearchView {targetId:string;modelHash:string;status:string;ready:boolean;query:MotionQuery;offset:number;total:number;pageSize:12;entries:MotionSearchEntry[]}
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const exact=(v:Record<string,unknown>,keys:string[])=>Object.keys(v).length===keys.length&&keys.every(k=>Object.prototype.hasOwnProperty.call(v,k));
const integer=(v:unknown,min:number,max:number)=>typeof v==='number'&&Number.isInteger(v)&&v>=min&&v<=max;
const text=(v:unknown,max:number)=>typeof v==='string'&&v.length<=max&&!/[\u0000-\u001f\u007f-\u009f]/.test(v);
export const validMotionQuery=(v:unknown):v is MotionQuery=>record(v)&&exact(v,['query','offset','includeShort','favouritesOnly','archivedOnly'])&&text(v.query,80)&&integer(v.offset,0,1024)&&['includeShort','favouritesOnly','archivedOnly'].every(k=>typeof v[k]==='boolean');
export function validMotionSearchView(v:unknown):v is MotionSearchView {
 if(!record(v)||typeof v.targetId!=='string'||!/^[a-zA-Z0-9_]{1,32}$/.test(v.targetId)||typeof v.modelHash!=='string'||v.modelHash!==''&&!/^[a-f0-9]{64}$/.test(v.modelHash)||!text(v.status,2048)||typeof v.ready!=='boolean'||!validMotionQuery(v.query)||!integer(v.total,0,1024)||!integer(v.offset,0,1024)||v.pageSize!==12||!Array.isArray(v.entries)||v.entries.length>12)return false;
 if(v.offset!==Math.floor((v.offset as number)/12)*12||v.offset!==(v.total===0?0:Math.min(Math.floor(v.query.offset/12),Math.floor(((v.total as number)-1)/12))*12)||v.entries.length!==Math.min(12,(v.total as number)-(v.offset as number))||!v.ready&&(v.total!==0||v.modelHash!==''))return false;
 const query=v.query;
 return new Set(v.entries.map(e=>record(e)?e.id:null)).size===v.entries.length&&v.entries.every(e=>record(e)&&typeof e.id==='string'&&/^[a-f0-9]{32}$/.test(e.id)&&text(e.name,100)&&Array.isArray(e.tags)&&e.tags.length<=16&&e.tags.every(t=>text(t,32))&&typeof e.duration==='number'&&Number.isFinite(e.duration)&&e.duration>0&&e.duration<=3600&&['shortClip','favourite','archived','downloaded'].every(k=>typeof e[k]==='boolean')&&e.shortClip===(e.duration<.1)&&e.archived===query.archivedOnly&&(!query.favouritesOnly||e.favourite)&& (query.includeShort||!e.shortClip));
}
