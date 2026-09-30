// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourCatalog,type BehaviourValueType} from './behaviourCatalog';
import type {CapabilitySchema} from './capabilities';
export interface BehaviourEventDefinition {id:string;label:string;activity:string|null;objectEvent:boolean;description?:string;fields?:CapabilitySchema;features?:string[]}
const definitions=new Map((structuredClone(behaviourCatalog.events) as BehaviourEventDefinition[]).map(event=>[event.id,event]));
export function behaviourEvent(id:string):BehaviourEventDefinition|null {const event=definitions.get(id);return event?structuredClone(event):null;}
export function eventFieldType(id:string,field:string):BehaviourValueType|null {
 const fields=definitions.get(id)?.fields?.properties;
 const type=fields&&Object.prototype.hasOwnProperty.call(fields,field)?fields[field].type:null;
 return type==='string'?'text':type==='integer'?'number':type==='number'||type==='boolean'?type:null;
}
export const behaviourEventFieldsGuide=[...definitions.values()].filter(e=>e.fields).map(e=>
 `${e.id} requires ${(e.features??[]).join(', ')}: ${e.description} Bindable fields: ${Object.keys(e.fields!.properties??{}).map(key=>key+' ('+eventFieldType(e.id,key)+')').join(', ')}.`).join('\n');
