// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {validVector,validRotation,type Vec3,type Rotation} from './recipe';
export {ruleActions,ruleGestures,ruleEvents,ruleConditions,rulePolicies,ruleMounts} from '../../../shared/prompts';
import type {RuleStep} from "./ruleSteps";
export type {RuleStep} from "./ruleSteps";
import {parseProgram} from "./programs";
export interface RuleSequence {id:string;name:string;interruption:number;repeat:boolean;program:string}
export interface RuleBinding {id:string;sequenceId:string;sourceId:string|null;trigger:number;condition:number;cooldown:number;enabled:boolean;stopOnExit:boolean}
export interface RuleButton {id:string;sequenceId:string;mount:number;position:Vec3;rotation:Rotation}
export interface RuleEdit {kind:'save'|'delete'|'bind'|'unbind'|'button'|'unbutton';reference?:string;target?:string;sequence?:RuleSequence;binding?:RuleBinding;mount?:number}
export interface RuleRequest {action:'inspect'|'edit'|'play'|'stop'|'undo'|'redo'|'signal';eventName?:string;value?:number|boolean|string;revision?:number;target?:string;page?:number;edits?:RuleEdit[]}
export interface RuleView {revision:number;canUndo:boolean;canRedo:boolean;readOnly:boolean;status:string;sequences:{id:string;name:string;steps:number;repeat:boolean;program?:boolean}[];selected:RuleSequence|null;bindings:RuleBinding[];buttons:RuleButton[];bindingPage:number;bindingCount:number;running:RuleRun[];outcomes?:RuleOutcome[];queued:number;eventQueue?:number;eventsDropped?:number}
export interface RuleRun {id:string;sequenceId:string;preparing:boolean;waiting?:boolean;waitEvent?:string|null;waitSeconds?:number;state?:{name:string;type:string;value:string}[];nodeId?:string|null;functionName?:string|null;status?:string;locals?:{name:string;type:string;value:string}[]}
export interface RuleOutcome {id:string;sequenceId:string;phase:'completed'|'cancelled'|'failed';nodeId?:string|null;status:string}
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const int=(v:unknown,min:number,max:number):v is number=>typeof v==='number'&&Number.isInteger(v)&&v>=min&&v<=max;
const num=(v:unknown,min:number,max:number):v is number=>typeof v==='number'&&Number.isFinite(v)&&v>=min&&v<=max;
const guid=(v:unknown)=>typeof v==='string'&&/^[a-f0-9]{32}$/.test(v);
const ref=(v:unknown)=>typeof v==='string'&&/^[a-zA-Z0-9_]{1,32}$/.test(v);
const target=(v:unknown)=>v==='maestro'||v==='book'||guid(v);
const title=(v:unknown)=>typeof v==='string'&&v.trim().length>0&&v.length<=32&&!/[\u0000-\u001f]/.test(v);
import {capabilityDefinition} from '../../../shared/capabilities';
import {invocationStep} from './capabilitySteps';
export const newRuleStep=(action=2):RuleStep=>action===13?invocationStep({id:'object.create.recipe',version:1,arguments:capabilityDefinition('object.create.recipe')!.example!},crypto.randomUUID().replace(/-/g,'')):({id:crypto.randomUUID().replace(/-/g,''),action,targetId:'maestro',gesture:0,seconds:[10,11,12,14,15,16,17].includes(action)?0:action===2?1:2.5,loop:false,...(action===14?{editPosition:{x:0,y:0,z:0}}:{}),...(action===15?{editScale:1}:{}),...(action===16?{editColor:{red:1,green:1,blue:1}}:{}),...(action===12?{creation:{shape:'ball',name:'Ball',x:.3,y:1.3,z:.65,scale:1,red:.2,green:.6,blue:.9}}:{}),...(action===10?{impulse:{x:0,y:.6,z:0}}:{})});
export const copySequence=(value:RuleSequence):RuleSequence=>JSON.parse(JSON.stringify(value));
export function validSequence(v:unknown,draft=false):v is RuleSequence {
 return record(v)&&(guid(v.id)||draft&&v.id==='')&&title(v.name)&&int(v.interruption,0,2)&&typeof v.repeat==='boolean'&&
 Object.keys(v).every(k=>['id','name','interruption','repeat','program'].includes(k))&&parseProgram(v.program).program!==null&&!(v.repeat&&parseProgram(v.program).program?.version===3);
}
const validBinding=(v:unknown,draft=false):v is RuleBinding=>record(v)&&(guid(v.id)||draft&&v.id==='')&&(draft?ref(v.sequenceId):guid(v.sequenceId))&&int(v.trigger,0,6)&&int(v.condition,0,4)&&num(v.cooldown,.25,30)&&typeof v.enabled==='boolean'&&typeof v.stopOnExit==='boolean'&&(v.trigger<4||target(v.sourceId));
export function validRuleRequest(v:unknown):v is RuleRequest {
 if(!record(v)||!['inspect','edit','play','stop','undo','redo','signal'].includes(v.action as string)||Object.keys(v).some(k=>!['action','revision','target','page','edits','eventName','value'].includes(k)))return false;
 if(!['inspect','stop'].includes(v.action as string)&&!int(v.revision,1,2147483647))return false;
 if(v.action==='signal')return Object.keys(v).length===4&&typeof v.eventName==='string'&&/^user\.[a-zA-Z0-9_]{1,32}$/.test(v.eventName)&&(typeof v.value==='boolean'||typeof v.value==='number'&&Number.isFinite(v.value)&&Math.abs(v.value)<=1000000||typeof v.value==='string'&&v.value.length<=128&&!/[\u0000-\u001f\u007f-\u009f]/.test(v.value));
 if(v.eventName!==undefined||v.value!==undefined)return false;
 if(v.action==='play'&&!guid(v.target)||v.target!==undefined&&!guid(v.target)||v.page!==undefined&&!int(v.page,0,15))return false;
 if(v.action!=='edit')return v.edits===undefined;
 if(!Array.isArray(v.edits)||v.edits.length<1||v.edits.length>16)return false;
 return v.edits.every(e=>{
  if(!record(e))return false;
  const fields:Record<string,string[]>={save:['sequence','reference'],delete:['target'],bind:['binding'],unbind:['target'],button:['target','mount'],unbutton:['target']};
  if(typeof e.kind!=='string'||!Object.prototype.hasOwnProperty.call(fields,e.kind))return false;const kind=e.kind;
  if(Object.keys(e).some(k=>k!=='kind'&&!fields[kind].includes(k)))return false;
  if(e.kind==='save')return validSequence(e.sequence,true)&&(e.sequence.id!==''||ref(e.reference));
  if(e.kind==='bind')return validBinding(e.binding,true);
  return ref(e.target)&&(e.kind!=='button'||int(e.mount,0,2));
 });
}
export function validRuleView(v:unknown):v is RuleView {
 if(!record(v)||!int(v.revision,1,2147483647)||!['canUndo','canRedo','readOnly'].every(k=>typeof v[k]==='boolean')||typeof v.status!=='string'||v.status.length>2048||!Array.isArray(v.sequences)||v.sequences.length>32||!int(v.bindingPage,0,15)||!int(v.bindingCount,0,128)||!int(v.queued,0,8))return false;
 if(v.sequences.some(s=>!record(s)||!guid(s.id)||!title(s.name)||!int(s.steps,s.program?0:1,s.program?128:16)||s.program!==undefined&&typeof s.program!=='boolean'||typeof s.repeat!=='boolean')||v.selected!==null&&!validSequence(v.selected))return false;
 if(!Array.isArray(v.bindings)||v.bindings.length>8||v.bindings.some(b=>!validBinding(b))||!Array.isArray(v.buttons)||v.buttons.length>16||v.buttons.some(b=>!record(b)||!guid(b.id)||!guid(b.sequenceId)||!int(b.mount,0,2)||!validVector(b.position)||!validRotation(b.rotation)))return false;
 if(v.eventQueue!==undefined&&!int(v.eventQueue,0,64)||v.eventsDropped!==undefined&&!int(v.eventsDropped,0,2147483647))return false;
 const text=(x:unknown,max=2048)=>typeof x==='string'&&x.length<=max;
 const name=(x:unknown)=>x==null||x===''||ref(x);
 if(v.outcomes!==undefined&&(!Array.isArray(v.outcomes)||v.outcomes.length>16||!v.outcomes.every(o=>record(o)&&guid(o.id)&&guid(o.sequenceId)&&['completed','cancelled','failed'].includes(o.phase as string)&&name(o.nodeId)&&text(o.status))))return false;
 const values=(x:unknown,max:number)=>Array.isArray(x)&&x.length<=max&&x.every(l=>record(l)&&ref(l.name)&&['number','boolean','text'].includes(l.type as string)&&text(l.value,128));
 return Array.isArray(v.running)&&v.running.length<=8&&v.running.every(r=>record(r)&&guid(r.id)&&guid(r.sequenceId)&&typeof r.preparing==='boolean'&&(r.waiting===undefined||typeof r.waiting==='boolean')&&(r.waitEvent==null||text(r.waitEvent,96))&&(r.waitSeconds===undefined||num(r.waitSeconds,0,3601))&&(r.state===undefined||values(r.state,16))&&name(r.nodeId)&&name(r.functionName)&&(r.status===undefined||text(r.status))&&(r.locals===undefined||Array.isArray(r.locals)&&r.locals.length<=24&&r.locals.every(l=>record(l)&&ref(l.name)&&['number','boolean','text'].includes(l.type as string)&&text(l.value,128))));
}
