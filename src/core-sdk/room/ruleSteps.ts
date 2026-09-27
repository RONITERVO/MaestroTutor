// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {validVector,validRotation,type Vec3,type Rotation,type RoomRecipe,parseRecipe} from "./recipe";
export interface RuleStep {id:string;action:number;targetId:string;gesture:number;seconds:number;loop:boolean;impulse?:Vec3;creationRecipe?:RoomRecipe;creation?:{shape:string;name:string;x:number;y:number;z:number;scale:number;red:number;green:number;blue:number};clipModelHash?:string|null;clipIndex?:number;motionId?:string|null;propId?:string|null;propAvatarHash?:string|null;propHand?:number;propRelease?:number;propOffset?:Vec3;propRotation?:Rotation;propReleaseAt?:number}
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const int=(v:unknown,min:number,max:number):v is number=>typeof v==='number'&&Number.isInteger(v)&&v>=min&&v<=max;
const num=(v:unknown,min:number,max:number):v is number=>typeof v==='number'&&Number.isFinite(v)&&v>=min&&v<=max;
const guid=(v:unknown)=>typeof v==='string'&&/^[a-fA-F0-9]{32}$/.test(v);
const target=(v:unknown)=>v==='maestro'||v==='book'||guid(v);
const hash=(v:unknown)=>v==null||v===''||typeof v==='string'&&/^[a-f0-9]{64}$/.test(v);
const optionalId=(v:unknown)=>v==null||v===''||guid(v);
export function validRuleStep(s:unknown,draft=false):s is RuleStep {
 if(!record(s)||!(guid(s.id)||draft&&s.id==='')||!int(s.action,0,13)||!int(s.gesture,0,5)||!num(s.seconds,0,30)||typeof s.loop!=='boolean'||s.action!==2&&!target(s.targetId))return false;
 if([1,2,4,5,9].includes(s.action)&&s.seconds<.1||[1,4,5,9].includes(s.action)&&s.targetId!=='maestro'||s.action===3&&(s.loop||s.seconds!==0))return false;
 if(s.action===9&&s.gesture===5)return false;
 if([10,11].includes(s.action)&&(s.seconds!==0||s.loop||!guid(s.targetId)))return false;
 if(s.action===12&&(!record(s.creation)||!['ball','block','cylinder'].includes(s.creation.shape as string)||typeof s.creation.name!=='string'||s.creation.name.length>80||/[\u0000-\u001f\u007f-\u009f]/.test(s.creation.name)||s.seconds!==0||s.loop||!['x','y','z'].every(k=>num((s.creation as Record<string,unknown>)[k],-25,25))||Math.hypot(s.creation.x as number,s.creation.y as number,s.creation.z as number)>25||!num(s.creation.scale,.1,4)||!['red','green','blue'].every(k=>num((s.creation as Record<string,unknown>)[k],0,1))))return false;
 if(s.action===13&&(!record(s.creation)||!parseRecipe(s.creationRecipe)||typeof s.creation.name!=='string'||s.creation.name.length>80||/[\u0000-\u001f\u007f-\u009f]/.test(s.creation.name)||s.seconds!==0||s.loop||!['x','y','z'].every(k=>num((s.creation as Record<string,unknown>)[k],-25,25))||Math.hypot(s.creation.x as number,s.creation.y as number,s.creation.z as number)>25||!num(s.creation.scale,.1,4)))return false;
 if(s.action===10&&(!record(s.impulse)||!['x','y','z'].every(k=>num((s.impulse as Record<string,unknown>)[k],-20,20))))return false;
 if(!hash(s.clipModelHash)||!hash(s.propAvatarHash)||!optionalId(s.motionId)||s.clipIndex!==undefined&&!int(s.clipIndex,0,31))return false;
 if(s.propId!=null&&s.propId!=='') {
  if(!guid(s.propId)||s.targetId!=='maestro'||![0,1,6,7].includes(s.action)||!int(s.propHand,0,1)||!int(s.propRelease,0,2)||!num(s.propReleaseAt,.05,1)||!validVector(s.propOffset)||Math.hypot(s.propOffset.x,s.propOffset.y,s.propOffset.z)>1||!validRotation(s.propRotation))return false;
 }
 return true;
}
