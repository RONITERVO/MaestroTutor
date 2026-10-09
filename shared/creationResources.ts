// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Cross-reference checks after the shared catalog's strict structural validation.
import {length2} from './roomConnection';
interface Pigment {r:number;g:number;b:number;a:number}
interface Binding {appearanceId:string;kind:string;partId:string;modelHash:string;materialIndex:number}
interface Emitter {id:string;source:string;part:string;joint:string;position:{x:number;y:number;z:number};minDistance:number;maxDistance:number}
export interface PrototypeResources {
 version?:number;color?:Pigment;appearanceBindings?:Binding[];audioEmitters?:Emitter[];environmentProfile?:string;visibilityLayer?:string;
 geometry:{kind:string;recipe?:{parts:{id:string;color?:Pigment}[]}};
}
interface Definition {id:string;name:string}
export interface ConstructionResources {
 version:number;appearances:Definition[];audioSources:(Definition&{kind:string;assetHash?:string;wave:string;frequency:number;endFrequency:number;attack:number;release:number;seconds:number;seed:number})[];environmentProfiles:Definition[];visibilityLayers?:Definition[];
}
const white=(c:Pigment|undefined)=>!!c&&c.r===1&&c.g===1&&c.b===1&&c.a===1;
export function validPrototypeResources(p:PrototypeResources):boolean {
 const styles=p.appearanceBindings??[],sounds=p.audioEmitters??[],parts=p.geometry.recipe?.parts??[];
 if((styles.length||sounds.length||p.environmentProfile)&&p.version!==2&&p.version!==3||p.visibilityLayer&&p.version!==3||p.version===3&&!p.visibilityLayer)return false;
 const keys=styles.map(b=>b.kind==='root'?'root':b.kind==='part'?'part:'+b.partId:'material:'+b.modelHash+':'+b.materialIndex);
 if(new Set(keys).size!==keys.length||new Set(sounds.map(e=>e.id)).size!==sounds.length)return false;
 for(const b of styles){
  if(b.kind==='root'&&!white(p.color))return false;
  if(b.kind==='part'&&(p.geometry.kind!=='recipe'||! /^[a-zA-Z][a-zA-Z0-9_]{0,31}$/.test(b.partId)))return false;
  const part=b.kind==='part'?parts.find(part=>part.id===b.partId):undefined;
  if(part&&!white(part.color)||b.kind==='material'&&p.geometry.kind!=='model')return false;
 }
 return sounds.every(e=>e.joint===''&&(!e.part||parts.some(p=>p.id===e.part))&&length2(e.position)<=100&&e.minDistance<=e.maxDistance);
}
export function validConstructionResources(version:number,r:ConstructionResources|undefined,objects:PrototypeResources[]):boolean {
 const styles=objects.flatMap(p=>p.appearanceBindings??[]),sounds=objects.flatMap(p=>p.audioEmitters??[]),profiles=objects.map(p=>p.environmentProfile??'').filter(Boolean),layers=objects.map(p=>p.visibilityLayer??'').filter(Boolean);
 if(version!==4)return !r&&!styles.length&&!sounds.length&&!profiles.length&&!layers.length;
 if(!r||r.version!==1&&r.version!==2||r.version===1&&(r.visibilityLayers!==undefined||layers.length>0)||r.version===2&&!Array.isArray(r.visibilityLayers))return false;
 if(r.appearances.length+r.audioSources.length+r.environmentProfiles.length+(r.visibilityLayers?.length??0)===0||sounds.length>32)return false;
 const closed=(definitions:Definition[],references:string[],named:boolean)=>{
  const ids=new Set(definitions.map(d=>d.id)),refs=new Set(references);
  return ids.size===definitions.length&&ids.size===refs.size&&[...refs].every(id=>ids.has(id))&&definitions.every(d=>! /\p{Cc}/u.test(d.name)&&(!named||d.name.trim().length>0));
 };
 return closed(r.appearances,styles.map(b=>b.appearanceId),true)&&closed(r.audioSources,sounds.map(e=>e.source),false)&&closed(r.environmentProfiles,profiles,true)&&closed(r.visibilityLayers??[],layers,true)&&
  r.audioSources.every(s=>s.kind==='clip'?/^[a-f0-9]{64}$/.test(s.assetHash??'')&&s.wave==='sine'&&s.frequency===440&&s.endFrequency===440&&Math.fround(s.attack)===Math.fround(.01)&&Math.fround(s.release)===Math.fround(.04)&&s.seed===1:!s.assetHash&&s.attack+s.release<=s.seconds+1e-7)&&r.environmentProfiles.every(p=>profiles.filter(id=>id===p.id).length<=16);
}
