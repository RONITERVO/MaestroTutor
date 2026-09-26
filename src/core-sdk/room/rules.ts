// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {validVector,validRotation,type Vec3,type Rotation} from './recipe';
export {ruleActions,ruleGestures,ruleEvents,ruleConditions,rulePolicies,ruleMounts} from '../../../shared/prompts';
export interface RuleStep {id:string;action:number;targetId:string;gesture:number;seconds:number;loop:boolean;clipModelHash?:string|null;clipIndex?:number;motionId?:string|null;propId?:string|null;propAvatarHash?:string|null;propHand?:number;propRelease?:number;propOffset?:Vec3;propRotation?:Rotation;propReleaseAt?:number}
export interface RuleSequence {id:string;name:string;interruption:number;repeat:boolean;steps:RuleStep[]}
export interface RuleBinding {id:string;sequenceId:string;sourceId:string|null;trigger:number;condition:number;cooldown:number;enabled:boolean;stopOnExit:boolean}
export interface RuleButton {id:string;sequenceId:string;mount:number;position:Vec3;rotation:Rotation}
export interface RuleEdit {kind:'save'|'delete'|'bind'|'unbind'|'button'|'unbutton';reference?:string;target?:string;sequence?:RuleSequence;binding?:RuleBinding;mount?:number}
export interface RuleRequest {action:'inspect'|'edit'|'play'|'stop'|'undo'|'redo';revision?:number;target?:string;page?:number;edits?:RuleEdit[]}
export interface RuleView {revision:number;canUndo:boolean;canRedo:boolean;readOnly:boolean;status:string;sequences:{id:string;name:string;steps:number;repeat:boolean}[];selected:RuleSequence|null;bindings:RuleBinding[];buttons:RuleButton[];bindingPage:number;bindingCount:number;running:{id:string;sequenceId:string;stepId:string;preparing:boolean}[];queued:number}
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const int=(v:unknown,min:number,max:number):v is number=>typeof v==='number'&&Number.isInteger(v)&&v>=min&&v<=max;
const num=(v:unknown,min:number,max:number):v is number=>typeof v==='number'&&Number.isFinite(v)&&v>=min&&v<=max;
const guid=(v:unknown)=>typeof v==='string'&&/^[a-f0-9]{32}$/.test(v);
const ref=(v:unknown)=>typeof v==='string'&&/^[a-zA-Z0-9_]{1,32}$/.test(v);
const target=(v:unknown)=>v==='maestro'||v==='book'||guid(v);
const hash=(v:unknown)=>v==null||v===''||typeof v==='string'&&/^[a-f0-9]{64}$/.test(v);
const optionalId=(v:unknown)=>v==null||v===''||guid(v);
const title=(v:unknown)=>typeof v==='string'&&v.trim().length>0&&v.length<=32&&!/[\u0000-\u001f]/.test(v);
export const newRuleStep=(action=2):RuleStep=>({id:'',action,targetId:'maestro',gesture:0,seconds:action===2?1:2.5,loop:false});
export const copySequence=(value:RuleSequence):RuleSequence=>JSON.parse(JSON.stringify(value));
export function validSequence(v:unknown,draft=false):v is RuleSequence {
 if(!record(v)||!(guid(v.id)||draft&&v.id==='')||!title(v.name)||!int(v.interruption,0,2)||typeof v.repeat!=='boolean'||!Array.isArray(v.steps)||v.steps.length<1||v.steps.length>16)return false;
 const ids=new Set<string>();
 for(const s of v.steps) {
  if(!record(s)||!(guid(s.id)||draft&&s.id==='')||s.id!==''&&ids.has(s.id as string)||!int(s.action,0,8)||!int(s.gesture,0,5)||!num(s.seconds,0,30)||typeof s.loop!=='boolean'||s.action!==2&&!target(s.targetId))return false;
  if(s.id!=='')ids.add(s.id as string);
  if([1,2,4,5].includes(s.action)&&s.seconds<.1||[1,4,5].includes(s.action)&&s.targetId!=='maestro'||s.action===3&&(s.loop||s.seconds!==0))return false;
  if(!hash(s.clipModelHash)||!hash(s.propAvatarHash)||!optionalId(s.motionId)||s.clipIndex!==undefined&&!int(s.clipIndex,0,31))return false;
  if(s.propId!=null&&s.propId!=='') {
   if(!guid(s.propId)||s.targetId!=='maestro'||![0,1,6,7].includes(s.action)||!int(s.propHand,0,1)||!int(s.propRelease,0,2)||!num(s.propReleaseAt,.05,1)||!validVector(s.propOffset)||Math.hypot(s.propOffset.x,s.propOffset.y,s.propOffset.z)>1||!validRotation(s.propRotation))return false;
  }
 }
 return true;
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
 if(v.sequences.some(s=>!record(s)||!guid(s.id)||!title(s.name)||!int(s.steps,1,16)||typeof s.repeat!=='boolean')||v.selected!==null&&!validSequence(v.selected))return false;
 if(!Array.isArray(v.bindings)||v.bindings.length>8||v.bindings.some(b=>!validBinding(b))||!Array.isArray(v.buttons)||v.buttons.length>16||v.buttons.some(b=>!record(b)||!guid(b.id)||!guid(b.sequenceId)||!int(b.mount,0,2)||!validVector(b.position)||!validRotation(b.rotation)))return false;
 return Array.isArray(v.running)&&v.running.length<=8&&v.running.every(r=>record(r)&&guid(r.id)&&guid(r.sequenceId)&&guid(r.stepId)&&typeof r.preparing==='boolean');
}
