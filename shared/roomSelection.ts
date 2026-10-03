// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {CapabilityInvocation} from './capabilities';
export interface ConstructionSelection {stateId:string;collecting:boolean;members:string[]}
type SceneObject={id:string;name?:string;objectRevision?:number};
const id=(value:unknown):value is string=>typeof value==='string'&&/^[a-f0-9]{32}$/.test(value);
export function validConstructionSelection(value:unknown,objects?:readonly SceneObject[]):value is ConstructionSelection {
 if(!value||typeof value!=='object'||Array.isArray(value))return false;const s=value as Record<string,unknown>;
 return Object.keys(s).sort().join(',')==='collecting,members,stateId'&&id(s.stateId)&&typeof s.collecting==='boolean'&&Array.isArray(s.members)&&s.members.length<=16&&s.members.every(id)&&new Set(s.members).size===s.members.length&&(!objects||s.members.every(member=>objects.some(o=>o.id===member)));
}
/** Labels distinguish duplicates; identity always remains the exact object ID. */
export function roomObjectLabel(object:SceneObject,objects:readonly SceneObject[]):string {
 const name=object.name||object.id,duplicates=objects.filter(o=>(o.name||o.id)===name);if(duplicates.length<2)return name;
 let length=8;while(length<object.id.length&&duplicates.some(o=>o.id!==object.id&&o.id.slice(0,length)===object.id.slice(0,length)))length+=2;return `${name} · ${object.id.slice(0,length)}`;
}
export function constructionSelectionCall(selection:ConstructionSelection,members:string[],collecting:boolean):CapabilityInvocation {
 const next={...selection,members,collecting};if(!validConstructionSelection(next))throw new Error('Choose at most 16 distinct creations.');
 return {id:'room.selection.set',version:1,arguments:{stateId:selection.stateId,members:[...members],collecting}};
}
/** Prepare an ordinary editable capture draft; selection never authorizes execution. */
export function constructionCaptureCall(selection:ConstructionSelection,objects:readonly SceneObject[]):CapabilityInvocation {
 if(!validConstructionSelection(selection,objects)||!selection.members.length)throw new Error('Choose existing construction pieces first.');
 const members=selection.members.map((target,i)=>{const revision=objects.find(o=>o.id===target)?.objectRevision;if(!Number.isInteger(revision)||revision!<1||revision!>2147483647)throw new Error('Read the current object revisions before capturing.');return {target,revision,slot:'piece_'+(i+1)};});
 return {id:'program.module.captureConstruction',version:1,arguments:{name:'My construction',members}};
}
