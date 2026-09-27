// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourCatalog} from '../behaviourCatalog';
/** Generated labels retain the current native wire identities. */
export const ruleActions=behaviourCatalog.adapters.ruleStep.actionIds.map(id=>behaviourCatalog.actions.find(action=>action.id===id)!.label);
export const ruleGestures=['Greeting','Pointing','Listening','Speaking','Idle','Walk'] as const;
export const ruleEvents=behaviourCatalog.adapters.ruleStep.eventIds.map(id=>behaviourCatalog.events.find(event=>event.id===id)!.label);
export const ruleConditions=['Any','Speaking','Listening','Thinking','Idle'] as const;
export const rulePolicies=['Restart','Ignore','Queue latest'] as const;
export const ruleMounts=['Room','Left controller','Right controller'] as const;
const text={type:'string'},number={type:'number'},boolean={type:'boolean'};
const enumeration=(count:number)=>({type:'integer',enum:Array.from({length:count},(_,i)=>i)});
const nullableText={type:['string','null']};
const vector={type:'object',properties:{x:number,y:number,z:number},required:['x','y','z'],additionalProperties:false};
const rotation={type:'object',properties:{x:number,y:number,z:number,w:number},required:['x','y','z','w'],additionalProperties:false};
export const ruleStepSchema={type:'object',properties:{id:text,action:enumeration(ruleActions.length),targetId:text,gesture:enumeration(ruleGestures.length),seconds:number,loop:boolean,
 clipModelHash:nullableText,clipIndex:{type:'integer'},motionId:nullableText,propId:nullableText,propAvatarHash:nullableText,propHand:enumeration(2),propRelease:enumeration(3),propOffset:vector,propRotation:rotation,propReleaseAt:number},
 required:['id','action','targetId','gesture','seconds','loop'],additionalProperties:false};
export const ruleSequenceSchema={type:'object',properties:{id:text,name:text,interruption:enumeration(3),repeat:boolean,program:{type:'string',maxLength:24000}},required:['id','name','interruption','repeat','program'],additionalProperties:false};
export const ruleBindingSchema={type:'object',properties:{id:text,sequenceId:text,sourceId:nullableText,trigger:enumeration(7),condition:enumeration(5),cooldown:number,enabled:boolean,stopOnExit:boolean},required:['id','sequenceId','sourceId','trigger','condition','cooldown','enabled','stopOnExit'],additionalProperties:false};
export const ruleRequestSchema={type:'object',properties:{action:{type:'string',enum:['inspect','edit','play','stop','undo','redo']},target:text,revision:{type:'integer'},page:{type:'integer'},
 edits:{type:'array',minItems:1,maxItems:16,items:{type:'object',properties:{kind:{type:'string',enum:['save','delete','bind','unbind','button','unbutton']},reference:text,target:text,sequence:ruleSequenceSchema,binding:ruleBindingSchema,mount:enumeration(3)},required:['kind'],additionalProperties:false}}},required:['action'],additionalProperties:false};
