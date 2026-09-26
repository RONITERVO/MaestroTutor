// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface Vec3 {x:number;y:number;z:number}
export interface Rotation extends Vec3 {w:number}
export interface Pigment {r:number;g:number;b:number;a:number}
export interface RecipePart {id:string;parent:string|null;shape:'box'|'sphere'|'cylinder';position:Vec3;rotation:Rotation;size:Vec3;color:Pigment}
export interface RecipeTrack {part:string;keys:{time:number;rotation:Rotation}[]}
export interface RoomRecipe {version:1;parts:RecipePart[];tracks:RecipeTrack[];duration:number;playing:boolean;loop:boolean}
const record=(v:unknown):v is Record<string,unknown>=>v!==null && typeof v==='object' && !Array.isArray(v);
const finite=(v:unknown):v is number=>typeof v==='number' && Number.isFinite(v);
export const validVector=(v:unknown):v is Vec3=>record(v)&&['x','y','z'].every(k=>finite(v[k]));
export const validRotation=(v:unknown):v is Rotation=>record(v)&&['x','y','z','w'].every(k=>finite(v[k]))&&Math.abs(['x','y','z','w'].reduce((sum,k)=>sum+Number(v[k])**2,0)-1)<.01;
export const validPigment=(v:unknown):v is Pigment=>record(v)&&['r','g','b'].every(k=>finite(v[k])&&Number(v[k])>=0&&Number(v[k])<=1)&&v.a===1;
const id=(v:unknown):v is string=>typeof v==='string'&&/^[a-zA-Z0-9_]{1,32}$/.test(v);
export function parseRecipe(v:unknown):RoomRecipe|null {
 if(!record(v)||v.version!==1||!Array.isArray(v.parts)||v.parts.length<1||v.parts.length>32||!Array.isArray(v.tracks)||v.tracks.length>17||!finite(v.duration)||v.duration<.1||v.duration>30||typeof v.playing!=='boolean'||typeof v.loop!=='boolean')return null;
 const ids=new Map<string,number>();
 for(const p of v.parts) {
  if(!record(p)||!id(p.id)||ids.has(p.id)||!['box','sphere','cylinder'].includes(p.shape as string)||!validVector(p.position)||!validVector(p.size)||!validRotation(p.rotation)||!validPigment(p.color)||![p.size.x,p.size.y,p.size.z].every(n=>n>=.005&&n<=2))return null;
  if(p.parent!==null&&p.parent!==''&&(typeof p.parent!=='string'||!ids.has(p.parent)))return null;
  const length=Math.hypot(p.position.x,p.position.y,p.position.z),reach=(ids.get(p.parent as string)??0)+length;
  if(length>2||reach+Math.hypot(p.size.x,p.size.y,p.size.z)/2>3)return null;ids.set(p.id,reach);
 }
 const tracks=new Set<string>();
 for(const t of v.tracks) {
  if(!record(t)||typeof t.part!=='string'||!ids.has(t.part)||tracks.has(t.part)||!Array.isArray(t.keys)||t.keys.length<2||t.keys.length>16)return null;tracks.add(t.part);
  let previous=-1;
  for(const key of t.keys) {if(!record(key)||!finite(key.time)||key.time<=previous||key.time>v.duration||previous<0&&key.time!==0||!validRotation(key.rotation))return null;previous=key.time;}
  if(Math.abs(previous-v.duration)>.001)return null;
 }
 if(v.playing&&tracks.size===0)return null;
 return v as unknown as RoomRecipe;
}
export const copyRecipe=(value:RoomRecipe):RoomRecipe=>JSON.parse(JSON.stringify(value));
/** Local-axis increments avoid an ambiguous Euler convention at the Unity boundary. */
export function rotateBy(q:Rotation,axis:keyof Vec3,degrees:number):Rotation {
 const s=Math.sin(degrees*Math.PI/360),c=Math.cos(degrees*Math.PI/360),a={x:0,y:0,z:0,w:c};a[axis]=s;
 const next={x:q.w*a.x+q.x*a.w+q.y*a.z-q.z*a.y,y:q.w*a.y-q.x*a.z+q.y*a.w+q.z*a.x,z:q.w*a.z+q.x*a.y-q.y*a.x+q.z*a.w,w:q.w*a.w-q.x*a.x-q.y*a.y-q.z*a.z};
 const norm=Math.hypot(next.x,next.y,next.z,next.w);return{x:next.x/norm,y:next.y/norm,z:next.z/norm,w:next.w/norm};
}
