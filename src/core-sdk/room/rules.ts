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
export interface RuleRequest {action:'inspect'|'edit'|'play'|'stop'|'undo'|'redo';revision?:number;target?:string;page?:number;edits?:RuleEdit[]}
export interface RuleView {revision:number;canUndo:boolean;canRedo:boolean;readOnly:boolean;status:string;sequences:{id:string;name:string;steps:number;repeat:boolean;program?:boolean}[];selected:RuleSequence|null;bindings:RuleBinding[];buttons:RuleButton[];bindingPage:number;bindingCount:number;running:RuleRun[];outcomes?:RuleOutcome[];queued:number}
export interface RuleRun {id:string;sequenceId:string;preparing:boolean;nodeId?:string|null;functionName?:string|null;status?:string;locals?:{name:string;type:string;value:string}[]}
export interface RuleOutcome {id:string;sequenceId:string;phase:'completed'|'cancelled'|'failed';nodeId?:string|null;status:string}
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const int=(v:unknown,min:number,max:number):v is number=>typeof v==='number'&&Number.isInteger(v)&&v>=min&&v<=max;
const num=(v:unknown,min:number,max:number):v is number=>typeof v==='number'&&Number.isFinite(v)&&v>=min&&v<=max;
const guid=(v:unknown)=>typeof v==='string'&&/^[a-f0-9]{32}$/.test(v);
const ref=(v:unknown)=>typeof v==='string'&&/^[a-zA-Z0-9_]{1,32}$/.test(v);
const target=(v:unknown)=>v==='maestro'||v==='book'||guid(v);
const title=(v:unknown)=>typeof v==='string'&&v.trim().length>0&&v.length<=32&&!/[\u0000-\u001f]/.test(v);
export const newRuleStep=(action=2):RuleStep=>({id:crypto.randomUUID().replace(/-/g,''),action,targetId:'maestro',gesture:0,seconds:action===2?1:2.5,loop:false});
export const copySequence=(value:RuleSequence):RuleSequence=>JSON.parse(JSON.stringify(value));
export function validSequence(v:unknown,draft=false):v is RuleSequence {
 return record(v)&&(guid(v.id)||draft&&v.id==='')&&title(v.name)&&int(v.interruption,0,2)&&typeof v.repeat==='boolean'&&
 Object.keys(v).every(k=>['id','name','interruption','repeat','program'].includes(k))&&parseProgram(v.program).program!==null;
}
const validBinding=(v:unknown,draft=false):v is RuleBinding=>record(v)&&(guid(v.id)||draft&&v.id==='')&&(draft?ref(v.sequenceId):guid(v.sequenceId))&&int(v.trigger,0,6)&&int(v.condition,0,4)&&num(v.cooldown,.25,30)&&typeof v.enabled==='boolean'&&typeof v.stopOnExit==='boolean'&&(v.trigger<4||target(v.sourceId));
export function validRuleRequest(v:unknown):v is RuleRequest {
 if(!record(v)||!['inspect','edit','play','stop','undo','redo'].includes(v.action as string)||Object.keys(v).some(k=>!['action','revision','target','page','edits'].includes(k)))return false;
 if(!['inspect','stop'].includes(v.action as string)&&!int(v.revision,1,2147483647))return false;
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
 const text=(x:unknown,max=2048)=>typeof x==='string'&&x.length<=max;
 const name=(x:unknown)=>x==null||x===''||ref(x);
 if(v.outcomes!==undefined&&(!Array.isArray(v.outcomes)||v.outcomes.length>16||!v.outcomes.every(o=>record(o)&&guid(o.id)&&guid(o.sequenceId)&&['completed','cancelled','failed'].includes(o.phase as string)&&name(o.nodeId)&&text(o.status))))return false;
 return Array.isArray(v.running)&&v.running.length<=8&&v.running.every(r=>record(r)&&guid(r.id)&&guid(r.sequenceId)&&typeof r.preparing==='boolean'&&name(r.nodeId)&&name(r.functionName)&&(r.status===undefined||text(r.status))&&(r.locals===undefined||Array.isArray(r.locals)&&r.locals.length<=24&&r.locals.every(l=>record(l)&&ref(l.name)&&['number','boolean','text'].includes(l.type as string)&&text(l.value,128))));
}
