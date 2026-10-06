// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourCatalog} from '../behaviourCatalog';
/** Generated labels retain the current native wire identities. */
export const ruleActions=behaviourCatalog.adapters.ruleStep.actionLabels;
export const ruleGestures=['Greeting','Pointing','Listening','Speaking','Idle','Walk'] as const;
export const ruleEvents=behaviourCatalog.adapters.ruleStep.eventIds.map(id=>behaviourCatalog.events.find(event=>event.id===id)!.label);
export const ruleConditions=['Any','Speaking','Listening','Thinking','Idle'] as const;
export const rulePolicies=['Restart','Ignore','Queue latest'] as const;
export const ruleMounts=['Room','Left controller','Right controller'] as const;
const text={type:'string'},number={type:'number'},boolean={type:'boolean'};
const enumeration=(count:number)=>({type:'integer',enum:Array.from({length:count},(_,i)=>i)});
const nullableText={type:['string','null']};
export const ruleSequenceSchema={type:'object',properties:{id:text,name:text,interruption:enumeration(3),repeat:boolean,program:{type:'string',maxLength:24000}},required:['id','name','interruption','repeat','program'],additionalProperties:false};
export const ruleBindingSchema={type:'object',properties:{id:text,sequenceId:text,sourceId:nullableText,trigger:enumeration(7),condition:enumeration(5),cooldown:number,enabled:boolean,stopOnExit:boolean},required:['id','sequenceId','sourceId','trigger','condition','cooldown','enabled','stopOnExit'],additionalProperties:false};
export const ruleRequestSchema={type:'object',properties:{action:{type:'string',enum:['inspect','memory','edit','play','stop','undo','redo','signal']},eventName:{type:'string',pattern:'^user\\.[a-zA-Z0-9_]{1,32}$'},value:{type:['string','number','boolean']},target:text,revision:{type:'integer'},page:{type:'integer'},
 edits:{type:'array',minItems:1,maxItems:16,items:{type:'object',properties:{kind:{type:'string',enum:['save','delete','bind','unbind','button','unbutton']},reference:text,target:text,sequence:ruleSequenceSchema,binding:ruleBindingSchema,mount:enumeration(3)},required:['kind'],additionalProperties:false}}},required:['action'],additionalProperties:false};

// The native request schema remains stable. Structured provider decoding needs
// operation-specific requirements, including an alias for a newly saved source.
const ruleId = { type: 'string', pattern: '^[a-f0-9]{32}$' };
const ruleReference = { type: 'string', pattern: '^[a-zA-Z0-9_]{1,32}$' };
const editProperties = ruleRequestSchema.properties.edits.items.properties;
const savedSequence = (id: object) => ({ ...ruleSequenceSchema, properties: { ...ruleSequenceSchema.properties, id } });
const editVariant = (kind: string, fields: Record<string, unknown>, required = Object.keys(fields)) => ({
 type: 'object', properties: { kind: { type: 'string', enum: [kind] }, ...fields }, required: ['kind', ...required], additionalProperties: false,
});
const ruleEdits = { ...ruleRequestSchema.properties.edits, items: { anyOf: [
 editVariant('save', { reference: ruleReference, sequence: savedSequence({ type: 'string', enum: [''] }) }),
 editVariant('save', { sequence: savedSequence(ruleId) }),
 editVariant('bind', { binding: editProperties.binding }),
 ...['delete', 'unbind', 'unbutton'].map(kind => editVariant(kind, { target: ruleReference })),
 editVariant('button', { target: ruleReference, mount: editProperties.mount }),
] } };
const revision = { type: 'integer', minimum: 1, maximum: 2147483647 };
const ruleVariant = (action: string, fields: Record<string, unknown>, required: string[] = []) => ({
 type: 'object', properties: { action: { type: 'string', enum: [action] }, ...fields }, required: ['action', ...required], additionalProperties: false,
});
export const ruleResponseSchema = { anyOf: [
 ruleVariant('inspect', { target: ruleId, page: { type: 'integer', minimum: 0, maximum: 15 } }),
 ruleVariant('memory', { target: ruleId }),
 ruleVariant('edit', { revision, edits: ruleEdits }, ['revision', 'edits']),
 ruleVariant('play', { revision, target: ruleId }, ['revision', 'target']),
 ruleVariant('stop', { target: ruleId }),
 ...['undo', 'redo'].map(action => ruleVariant(action, { revision }, ['revision'])),
 ruleVariant('signal', { revision, eventName: ruleRequestSchema.properties.eventName, value: ruleRequestSchema.properties.value }, ['revision', 'eventName', 'value']),
] };
