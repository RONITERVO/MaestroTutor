// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface Vec3 {x:number;y:number;z:number}
export interface Rotation extends Vec3 {w:number}
export interface Pigment {r:number;g:number;b:number;a:number}
export interface RecipePart {id:string;parent:string|null;shape:'box'|'sphere'|'cylinder'|'lathe';profile?:{x:number;y:number}[];segments?:number;position:Vec3;rotation:Rotation;size:Vec3;color:Pigment}
export interface RecipeTrack {part:string;keys:{time:number;rotation:Rotation}[]}
export interface RoomRecipe {version:1;parts:RecipePart[];tracks:RecipeTrack[];duration:number;playing:boolean;loop:boolean}
const record=(v:unknown):v is Record<string,unknown>=>v!==null && typeof v==='object' && !Array.isArray(v);
const finite=(v:unknown):v is number=>typeof v==='number' && Number.isFinite(v);
export const validVector=(v:unknown):v is Vec3=>record(v)&&['x','y','z'].every(k=>finite(v[k]));
export const validRotation=(v:unknown):v is Rotation=>record(v)&&['x','y','z','w'].every(k=>finite(v[k]))&&Math.abs(['x','y','z','w'].reduce((sum,k)=>sum+Number(v[k])**2,0)-1)<.01;
export const validPigment=(v:unknown):v is Pigment=>record(v)&&['r','g','b'].every(k=>finite(v[k])&&Number(v[k])>=0&&Number(v[k])<=1)&&v.a===1;
const id=(v:unknown):v is string=>typeof v==='string'&&/^[a-zA-Z0-9_]{1,32}$/.test(v);
/** A simple closed cross section, radius x and height y, counter-clockwise. */
export function validLathePart(part:Record<string,unknown>):boolean {
 const p=part.profile;
 if(part.shape!=='lathe')return (p===undefined||p===null||Array.isArray(p)&&p.length===0)&&(part.segments===undefined||part.segments===0);
 if(!Array.isArray(p)||p.length<3||p.length>16||!Number.isInteger(part.segments)||Number(part.segments)<8||Number(part.segments)>48)return false;
 if(!p.every(a=>record(a)&&finite(a.x)&&finite(a.y)&&a.x>=0&&a.x<=.5&&a.y>=-.5&&a.y<=.5))return false;
 const points=p as {x:number;y:number}[],e=1e-8;
 const cross=(a:typeof points[0],b:typeof a,c:typeof a)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
 const on=(a:typeof points[0],b:typeof a,c:typeof a)=>c.x>=Math.min(a.x,b.x)-e&&c.x<=Math.max(a.x,b.x)+e&&c.y>=Math.min(a.y,b.y)-e&&c.y<=Math.max(a.y,b.y)+e;
 let area=0;
 for(let i=0;i<points.length;i++){
  const a=points[i],b=points[(i+1)%points.length];if((b.x-a.x)**2+(b.y-a.y)**2<.000001)return false;area+=a.x*b.y-b.x*a.y;
  for(let j=i+1;j<points.length;j++){
   if(j===i+1||i===0&&j===points.length-1)continue;
   const c=points[j],d=points[(j+1)%points.length],u=cross(a,b,c),v=cross(a,b,d),q=cross(c,d,a),r=cross(c,d,b);
   if((u>e&&v< -e||u< -e&&v>e)&&(q>e&&r< -e||q< -e&&r>e)||Math.abs(u)<=e&&on(a,b,c)||Math.abs(v)<=e&&on(a,b,d)||Math.abs(q)<=e&&on(c,d,a)||Math.abs(r)<=e&&on(c,d,b))return false;
  }
 }
 return area>=.0002;
}
export const defaultLatheProfile=()=>[{x:0,y:-.5},{x:.5,y:-.5},{x:.5,y:.5},{x:.4,y:.5},{x:.4,y:-.4},{x:0,y:-.4}];
export function parseRecipe(v:unknown):RoomRecipe|null {
 if(!record(v)||v.version!==1||!Array.isArray(v.parts)||v.parts.length<1||v.parts.length>32||!Array.isArray(v.tracks)||v.tracks.length>17||!finite(v.duration)||v.duration<.1||v.duration>30||typeof v.playing!=='boolean'||typeof v.loop!=='boolean')return null;
 const ids=new Map<string,number>();
 for(const p of v.parts) {
  if(!record(p)||!id(p.id)||ids.has(p.id)||!['box','sphere','cylinder','lathe'].includes(p.shape as string)||!validLathePart(p)||!validVector(p.position)||!validVector(p.size)||!validRotation(p.rotation)||!validPigment(p.color)||![p.size.x,p.size.y,p.size.z].every(n=>n>=.005&&n<=2))return null;
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
