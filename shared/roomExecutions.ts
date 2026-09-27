// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {validCapabilityInvocation,capabilityResources,validateCapabilityOutput,type CapabilityInvocation} from './capabilities';
import {boundedCapabilityCall} from './roomCatalog';
export type ExecutionRequest={operation:'start';call:CapabilityInvocation;runId?:string}|{operation:'inspect'|'cancel';runId:string}|{operation:'recover';recoveryId:string};
export interface ExecutionSummary {id:string;capability:string;version:number;resources:string[];phase:'preparing'|'running'|'completed'|'cancelled'|'failed'|'interrupted';status:string;output?:Record<string,unknown>}
export interface ExecutionDetail extends ExecutionSummary {call:CapabilityInvocation}
export interface ExecutionView {selected:ExecutionDetail|null;running:ExecutionSummary[];outcomes:ExecutionSummary[];nextRunId?:string|null;storageError?:string|null;recovery?:{id:string;status:string}|null}
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const exact=(v:Record<string,unknown>,keys:string[])=>Object.keys(v).length===keys.length&&keys.every(k=>Object.prototype.hasOwnProperty.call(v,k));
const id=(v:unknown):v is string=>typeof v==='string'&&/^[a-f0-9]{32}$/.test(v);
const call=(v:unknown):v is CapabilityInvocation=>boundedCapabilityCall(v)&&validCapabilityInvocation(v);
export function validExecutionRequest(v:unknown):v is ExecutionRequest {
 if(!record(v))return false;
 if(v.operation==='recover')return exact(v,['operation','recoveryId'])&&id(v.recoveryId);
 return v.operation==='start'?(exact(v,['operation','call'])||exact(v,['operation','call','runId'])&&id(v.runId))&&call(v.call):['inspect','cancel'].includes(v.operation as string)&&exact(v,['operation','runId'])&&id(v.runId);
}
const keys=['id','capability','version','resources','phase','status'];
function validStorage(v:Record<string,unknown>):boolean {
 if(exact(v,['selected','running','outcomes']))return true;
 if(!exact(v,['selected','running','outcomes','nextRunId','storageError',...(v.recovery!==undefined?['recovery']:[])]))return false;
 if(v.recovery!=null&&(!v.storageError||!record(v.recovery)||!exact(v.recovery,['id','status'])||!id(v.recovery.id)||typeof v.recovery.status!=='string'||v.recovery.status.length>2048))return false;
 return v.storageError===null?id(v.nextRunId):v.nextRunId===null&&typeof v.storageError==='string'&&v.storageError.length>0&&v.storageError.length<=2048;
}
function summary(v:unknown,detail=false):v is ExecutionSummary {
 return record(v)&&exact(v,[...keys,...(detail?['call']:[]),...(v.output!==undefined?['output']:[])])&&id(v.id)&&typeof v.capability==='string'&&v.capability.length<=96&&/^[a-z][a-zA-Z0-9]*(\.[a-z][a-zA-Z0-9]*)+$/.test(v.capability)&&
 typeof v.version==='number'&&Number.isInteger(v.version)&&v.version>=1&&v.version<=1000000&&
 Array.isArray(v.resources)&&v.resources.length<=16&&v.resources.every(x=>x==='maestro'||x==='book'||typeof x==='string'&&/^[a-fA-F0-9]{32}$/.test(x))&&new Set(v.resources).size===v.resources.length&&
 ['preparing','running','completed','cancelled','failed','interrupted'].includes(v.phase as string)&&typeof v.status==='string'&&v.status.length<=2048&&(v.output===undefined||v.phase==='completed'&&validateCapabilityOutput(v.capability,v.version,v.output)===null);
}
export function validExecutionView(v:unknown):v is ExecutionView {
 if(!record(v)||!validStorage(v)||!Array.isArray(v.running)||v.running.length>8||!Array.isArray(v.outcomes)||v.outcomes.length>16)return false;
 if(!v.running.every(x=>summary(x)&&['preparing','running'].includes(x.phase))||!v.outcomes.every(x=>summary(x)&&['completed','cancelled','failed','interrupted'].includes(x.phase)))return false;
 const entries=[...v.running,...v.outcomes];if(new Set(entries.map(x=>x.id)).size!==entries.length)return false;
 if(v.selected===null)return true;
 if(!summary(v.selected,true)||!record(v.selected)||!call(v.selected.call))return false;
 const selected=v.selected as unknown as ExecutionDetail,entry=entries.find(x=>x.id===selected.id);
 return !!entry&&JSON.stringify(entry.output)===JSON.stringify(selected.output)&&keys.every(key=>JSON.stringify(entry[key])===JSON.stringify(selected[key as keyof ExecutionDetail]))&&selected.capability===selected.call.id&&selected.version===selected.call.version&&
 JSON.stringify([...selected.resources].sort())===JSON.stringify(capabilityResources(selected.call.id,selected.call.arguments).sort());
}

/** Freeze the issued identity before the task journal is saved and before dispatch.
 * An explicit old ID is retained for reconciliation, never silently replaced. */
export function identifyExecution(request:ExecutionRequest,view:ExecutionView|null|undefined):ExecutionRequest {
 if(request.operation!=='start'||request.runId)return request;
 if(!view?.nextRunId)throw new Error(view?.storageError||'Action receipts are unavailable. Refresh the room before starting.');
 return {...request,runId:view.nextRunId};
}
