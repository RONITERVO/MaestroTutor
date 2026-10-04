// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface Vec3 {x:number;y:number;z:number}
export interface Rotation extends Vec3 {w:number}
export interface Pigment {r:number;g:number;b:number;a:number}
export interface RecipePattern {kind:'solid'|'checker'|'stripes';plane:'uv'|'xy'|'xz'|'yz';columns:number;rows:number;secondary:string}
export const defaultRecipePattern=():RecipePattern=>({kind:'solid',plane:'uv',columns:1,rows:1,secondary:'#FFFFFF'});
export interface RecipePart {pattern?:RecipePattern;id:string;parent:string|null;shape:'box'|'sphere'|'cylinder'|'lathe'|'extrude'|'sweep';path?:Vec3[];profile?:{x:number;y:number}[];segments?:number;position:Vec3;rotation:Rotation;size:Vec3;color:Pigment}
export interface RecipeTrack {part:string;keys:{time:number;rotation:Rotation}[]}
export interface RoomRecipe {version:1;parts:RecipePart[];tracks:RecipeTrack[];duration:number;playing:boolean;loop:boolean}
const record=(v:unknown):v is Record<string,unknown>=>v!==null && typeof v==='object' && !Array.isArray(v);
const finite=(v:unknown):v is number=>typeof v==='number' && Number.isFinite(v);
export const validVector=(v:unknown):v is Vec3=>record(v)&&['x','y','z'].every(k=>finite(v[k]));
export const validRotation=(v:unknown):v is Rotation=>record(v)&&['x','y','z','w'].every(k=>finite(v[k]))&&Math.abs(['x','y','z','w'].reduce((sum,k)=>sum+Number(v[k])**2,0)-1)<.01;
export const validPigment=(v:unknown):v is Pigment=>record(v)&&['r','g','b'].every(k=>finite(v[k])&&Number(v[k])>=0&&Number(v[k])<=1)&&v.a===1;
export const validRecipePattern=(v:unknown):boolean=>v===undefined||v===null||record(v)&&typeof v.kind==='string'&&['solid','checker','stripes'].includes(v.kind)&&typeof v.plane==='string'&&['uv','xy','xz','yz'].includes(v.plane)&&['columns','rows'].every(k=>Number.isInteger(v[k])&&Number(v[k])>=1&&Number(v[k])<=32)&&typeof v.secondary==='string'&&v.secondary.length===7&&/^#[a-fA-F0-9]{6}$/.test(v.secondary);
const id=(v:unknown):v is string=>typeof v==='string'&&/^[a-zA-Z0-9_]{1,32}$/.test(v);
/** A simple closed cross section, radius x and height y, counter-clockwise. */
export function validLathePart(part:Record<string,unknown>):boolean {
 const p=part.profile;
 if(part.shape!=='lathe')return (p===undefined||p===null||Array.isArray(p)&&p.length===0)&&(part.segments===undefined||part.segments===0);
 if(!Array.isArray(p)||p.length<3||p.length>16||!Number.isInteger(part.segments)||Number(part.segments)<8||Number(part.segments)>48)return false;
 return validOutline(p,0);
}
function validOutline(p:unknown[],minimumX:number):boolean {
 if(!p.every(a=>record(a)&&finite(a.x)&&finite(a.y)&&a.x>=minimumX&&a.x<=.5&&a.y>=-.5&&a.y<=.5))return false;
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
/** Same bounded ear clipping as the native evaluator, used only to validate source. */
export function validExtrudedPart(part:Record<string,unknown>):boolean {
 if(part.shape!=='extrude'||part.segments!==undefined&&part.segments!==0||!Array.isArray(part.profile)||part.profile.length<3||part.profile.length>32)return false;
 const p=part.profile.map(v=>record(v)&&finite(v.x)&&finite(v.y)?{x:Math.fround(v.x),y:Math.fround(v.y)}:v);
 if(!validOutline(p,-.5))return false;
 const points=p as {x:number;y:number}[],polygon=points.map((_,i)=>i),epsilon=1e-8;
 const cross=(a:number,b:number,c:number)=>Math.fround(points[b].x-points[a].x)*Math.fround(points[c].y-points[a].y)-Math.fround(points[b].y-points[a].y)*Math.fround(points[c].x-points[a].x);
 for(let i=polygon.length-1;i>=0&&polygon.length>3;i--)if(Math.abs(cross(polygon[(i+polygon.length-1)%polygon.length],polygon[i],polygon[(i+1)%polygon.length]))<=epsilon)polygon.splice(i,1);
 while(polygon.length>3){let found=false;
  for(let i=0;i<polygon.length;i++){
   const a=polygon[(i+polygon.length-1)%polygon.length],b=polygon[i],c=polygon[(i+1)%polygon.length];if(cross(a,b,c)<=epsilon)continue;
   if(polygon.some(p=>p!==a&&p!==b&&p!==c&&cross(a,b,p)>=-epsilon&&cross(b,c,p)>=-epsilon&&cross(c,a,p)>=-epsilon))continue;
   polygon.splice(i,1);found=true;break;
  }
  if(!found)return false;
 }
 return cross(polygon[0],polygon[1],polygon[2])>epsilon;
}
/** Mirrors native source admission; Unity owns the generated mesh and live world. */
function validSweepPath(profile:{x:number;y:number}[],path:Vec3[]):boolean {
const add=(a:Vec3,b:Vec3):Vec3=>({x:a.x+b.x,y:a.y+b.y,z:a.z+b.z});
const sub=(a:Vec3,b:Vec3):Vec3=>({x:a.x-b.x,y:a.y-b.y,z:a.z-b.z});
const scale=(a:Vec3,n:number):Vec3=>({x:a.x*n,y:a.y*n,z:a.z*n});
const dot=(a:Vec3,b:Vec3)=>a.x*b.x+a.y*b.y+a.z*b.z;
const cross=(a:Vec3,b:Vec3):Vec3=>({x:a.y*b.z-a.z*b.y,y:a.z*b.x-a.x*b.z,z:a.x*b.y-a.y*b.x});
const norm=(a:Vec3)=>scale(a,1/Math.hypot(a.x,a.y,a.z));

 if(path.length<2||path.length>16||path.some(p=>[p.x,p.y,p.z].some(n=>!Number.isFinite(n)||Math.abs(n)>.5)))return false;
 if(dot(sub(path[0],path[path.length-1]),sub(path[0],path[path.length-1]))<.000001)return false;
 const directions:Vec3[]=[];
 for(let i=1;i<path.length;i++){
  const delta=sub(path[i],path[i-1]);if(dot(delta,delta)<.000001)return false;directions.push(norm(delta));
  if(i>1&&dot(directions[i-2],directions[i-1])<-.95)return false;
 }
 const tangents:Vec3[]=[],right:Vec3[]=[],up:Vec3[]=[],rings:Vec3[][]=[];
 for(let i=0;i<path.length;i++){
  const tangent=i===0?directions[0]:i===path.length-1?directions[directions.length-1]:norm(add(directions[i-1],directions[i]));tangents.push(tangent);
  if(i===0)right.push(norm(cross(Math.abs(tangent.y)>.99?{x:0,y:0,z:1}:{x:0,y:1,z:0},tangent)));
  else {
   const axis=cross(tangents[i-1],tangent),prior=right[i-1];
   const transported=add(add(prior,cross(axis,prior)),scale(cross(axis,cross(axis,prior)),1/(1+dot(tangents[i-1],tangent))));
   right.push(norm(sub(transported,scale(tangent,dot(transported,tangent)))));
  }
  up.push(cross(tangent,right[i]));const ring=profile.map(p=>add(path[i],add(scale(right[i],p.x),scale(up[i],p.y))));
  if(ring.some(p=>[p.x,p.y,p.z].some(n=>!Number.isFinite(n)||Math.abs(n)>.50001)))return false;rings.push(ring);
 }
 for(let i=0;i<path.length-1;i++)for(let j=0;j<profile.length;j++){
  const next=(j+1)%profile.length,edge={x:profile[next].x-profile[j].x,y:profile[next].y-profile[j].y};
  const outside=sub(scale(add(right[i],right[i+1]),edge.y),scale(add(up[i],up[i+1]),edge.x));
  const a=rings[i][j],b=rings[i][next],c=rings[i+1][next],d=rings[i+1][j];
  if(dot(cross(sub(b,a),sub(c,a)),outside)<=1e-10||dot(cross(sub(c,a),sub(d,a)),outside)<=1e-10)return false;
 }
 return true;
}

export function validSweptPart(part:Record<string,unknown>):boolean {
 if(part.shape!=='sweep'||!validExtrudedPart({...part,shape:'extrude'})||!Array.isArray(part.path)||!part.path.every(validVector))return false;
 const profile=(part.profile as {x:number;y:number}[]).map(p=>({x:Math.fround(p.x),y:Math.fround(p.y)}));
 const path=part.path.map(p=>({x:Math.fround(p.x),y:Math.fround(p.y),z:Math.fround(p.z)}));return validSweepPath(profile,path);
}
export const defaultSweepProfile=()=>Array.from({length:8},(_,i)=>({x:Math.round(Math.cos(i*Math.PI/4)*.04*1e8)/1e8,y:Math.round(Math.sin(i*Math.PI/4)*.04*1e8)/1e8}));
export const defaultSweepPath=()=>[{x:-.3,y:-.3,z:0},{x:-.3,y:.15,z:0},{x:-.2,y:.3,z:0},{x:.2,y:.3,z:0},{x:.3,y:.15,z:0},{x:.3,y:-.3,z:0}];
export const defaultExtrusionProfile=()=>[{x:-.5,y:-.5},{x:.5,y:-.5},{x:.5,y:-.1},{x:-.1,y:-.1},{x:-.1,y:.5},{x:-.5,y:.5}];
export const defaultLatheProfile=()=>[{x:0,y:-.5},{x:.5,y:-.5},{x:.5,y:.5},{x:.4,y:.5},{x:.4,y:-.4},{x:0,y:-.4}];
export function parseRecipe(v:unknown):RoomRecipe|null {
 if(!record(v)||v.version!==1||!Array.isArray(v.parts)||v.parts.length<1||v.parts.length>32||!Array.isArray(v.tracks)||v.tracks.length>17||!finite(v.duration)||v.duration<.1||v.duration>30||typeof v.playing!=='boolean'||typeof v.loop!=='boolean')return null;
 const ids=new Map<string,number>();
 for(const p of v.parts) {
  if(!record(p)||!id(p.id)||ids.has(p.id)||!['box','sphere','cylinder','lathe','extrude','sweep'].includes(p.shape as string)||!(p.shape==='sweep'?validSweptPart(p):(p.path===undefined||p.path===null||Array.isArray(p.path)&&p.path.length===0)&&(p.shape==='extrude'?validExtrudedPart(p):validLathePart(p)))||!validVector(p.position)||!validVector(p.size)||!validRotation(p.rotation)||!validPigment(p.color)||!validRecipePattern(p.pattern)||![p.size.x,p.size.y,p.size.z].every(n=>n>=.005&&n<=2))return null;
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
