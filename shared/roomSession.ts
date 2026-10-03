// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {CapabilityInvocation} from './capabilities';
export interface TemporaryRoomView {
 active:boolean;pending:boolean;id:string;saveId:string;phase:'idle'|'starting'|'ready'|'pending'|'saved'|'failed';error:string;savedRevision:number;
}
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const id=(v:unknown):v is string=>typeof v==='string'&&/^[a-f0-9]{32}$/.test(v);
export function validTemporaryRoom(v:unknown):v is TemporaryRoomView {
 if(!record(v)||Object.keys(v).length!==7||!['active','pending','id','saveId','phase','error','savedRevision'].every(k=>Object.prototype.hasOwnProperty.call(v,k)))return false;
 return typeof v.active==='boolean'&&typeof v.pending==='boolean'&&id(v.id)&&(v.saveId===''||id(v.saveId))&&
  typeof v.error==='string'&&v.error.length<=2048&&typeof v.savedRevision==='number'&&Number.isInteger(v.savedRevision)&&v.savedRevision>=0&&v.savedRevision<=1000000&&
  typeof v.phase==='string'&&['idle','starting','ready','pending','saved','failed'].includes(v.phase)&&v.pending===(v.phase==='pending'||v.phase==='starting')&&(!v.pending||v.active===true&&id(v.saveId))&&
  (!['starting','ready'].includes(v.phase)||id(v.saveId)&&v.savedRevision===0)&&(v.phase!=='saved'||id(v.saveId)&&v.savedRevision>0)&&(v.phase!=='failed'||id(v.saveId)&&v.error.length>0)&&
  (v.phase==='failed'||v.error==='')&&(v.phase!=='idle'||v.savedRevision===0&&v.saveId==='')&&(v.active||v.phase==='idle');
}
export const roomSessionCall=(operation:'begin'|'keep'|'discard',view:TemporaryRoomView):CapabilityInvocation=>({id:'room.session',version:1,arguments:{operation,sessionId:view.id}});
