// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Geometry checks after the catalog's strict structural validation.
import {validSurfaceGeometry,surfaceRenderedPointCount,type SurfaceGeometry} from './surfaceGeometry';
import {validPrototypeResources,type PrototypeResources} from './creationResources';
import {length2,normalized,rotate,multiply,type ConnectionPose} from './roomConnection';
type V={x:number;y:number;z:number};
type Q=V&{w:number};
interface Frame {time:number;position:V;rotation:Q;scale:number}
interface Surface extends SurfaceGeometry {version:number;id:string;part:string;position:V;width:number;height:number;strokes:{id:string;radius:number;points:V[]}[]}
export interface CreationPrototype extends PrototypeResources {
 physics:{mode:string;shape?:string};collision?:{shapes:unknown[]};modelGeometry?:{version:number;scaleMode:string;metresPerUnit:number;pivot:string;meshCollision:boolean;walkable:boolean};heightFields?:unknown[];sculptTips?:{part:string;enabled:boolean}[];
 geometry:{kind:string;recipe?:{playing:boolean;parts:{id:string;color?:{r:number;g:number;b:number;a:number}}[]};points?:V[];radius?:number};
 windows?:{version:number;id:string;surface:string;shape:string;reveal:number}[];
 snapPoints?:{id:string;frame:{position:V}}[];surfaces:Surface[];drawingTips:{enabled:boolean;version:number;mode?:string;part:string;position:V}[];motion?:{loop:boolean;frames:Frame[]};
}
const hasLength=(p:V[])=>p.some(v=>(v.x-p[0].x)**2+(v.y-p[0].y)**2+(v.z-p[0].z)**2>.000001);
export function validCreationPrototypeGeometry(value:Record<string,unknown>):boolean {
 const p=value as unknown as CreationPrototype,g=p.geometry;
 if(!validPrototypeResources(p)||g.recipe?.playing||(p.heightFields?.length??0)>0&&p.physics.mode!=='fixed')return false;
 if(g.kind==='drawing'&&(!hasLength(g.points!)||g.points!.some(v=>length2(v)>100)))return false;
 const model=p.modelGeometry;
 if(model){
  const defaults=model.version===1&&model.scaleMode==='fitted'&&model.metresPerUnit===1&&model.pivot==='center'&&!model.meshCollision&&!model.walkable;
  if(model.version!==1||!['fitted','source'].includes(model.scaleMode)||!['center','base','source'].includes(model.pivot)||!Number.isFinite(model.metresPerUnit)||model.metresPerUnit<.001||model.metresPerUnit>100||model.scaleMode==='fitted'&&model.metresPerUnit!==1||model.walkable&&!model.meshCollision)return false;
  if(!defaults&&(p.version!==5||g.kind!=='model'))return false;
  if(model.meshCollision&&(p.physics.mode!=='fixed'||p.physics.shape!=='automatic'||(p.collision?.shapes.length??0)>0||p.motion))return false;
 }
 const part=(name:string)=>name===''||!!g.recipe?.parts.some(x=>x.id===name);
 if(new Set(p.surfaces.map(s=>s.id)).size!==p.surfaces.length)return false;
 let points=g.points?.length??0;
 for(const s of p.surfaces){
  if(!validSurfaceGeometry(s)||s.version!==((s.shape??'plane')==='plane'?1:2)||!part(s.part)||length2(s.position)>100||new Set(s.strokes.map(x=>x.id)).size!==s.strokes.length)return false;
  for(const ink of s.strokes){const rendered=surfaceRenderedPointCount(s,ink.points,ink.radius);if(rendered>2048)return false;points+=rendered;
   if(!hasLength(ink.points)||ink.points.some(v=>length2(v)>100||Math.abs(v.z)>.000001||Math.abs(v.x)+ink.radius>s.width*.5+.000001||Math.abs(v.y)+ink.radius>s.height*.5+.000001))return false;
  }
 }
 const windows=p.windows??[];
 if(windows.length>4||windows.length>0&&p.version!==4&&p.version!==5||new Set(windows.map(w=>w.id)).size!==windows.length||new Set(windows.map(w=>w.surface)).size!==windows.length||windows.some(w=>w.version!==1||! /^[a-zA-Z][a-zA-Z0-9_]{0,31}$/.test(w.id)||!['rectangle','ellipse'].includes(w.shape)||!Number.isFinite(w.reveal)||w.reveal<0||w.reveal>1||!p.surfaces.some(s=>s.id===w.surface&&(s.shape??'plane')==='plane')))return false;
 if(p.snapPoints&&(new Set(p.snapPoints.map(s=>s.id)).size!==p.snapPoints.length||p.snapPoints.some(s=>length2(s.frame.position)>100)))return false;
 if(points>32768||p.drawingTips.some(t=>!part(t.part)||length2(t.position)>100||!['draw','erase'].includes(t.mode??'draw')||t.version!==((t.mode??'draw')==='erase'?2:1)))return false;
 if(p.sculptTips?.some(t=>!part(t.part))||p.sculptTips?.some(t=>t.enabled)&&p.drawingTips.some(t=>t.enabled))return false;
 if(p.motion?.frames.some((f,i)=>i===0?f.time!==0||length2(f.position)>250001:f.time<=p.motion!.frames[i-1].time||length2(f.position)>250001))return false;
 return true;
}
export function validPrototypePlacement(p:CreationPrototype,pose:ConnectionPose):boolean {
 const q=normalized(pose.rotation);
 return !p.motion||p.motion.frames.every(f=>{
  const local=rotate(q,{x:f.position.x*pose.scale,y:f.position.y*pose.scale,z:f.position.z*pose.scale});
  const position={x:pose.position.x+local.x,y:pose.position.y+local.y,z:pose.position.z+local.z};
  const scale=pose.scale*f.scale,rotation=multiply(q,f.rotation);
  return length2(position)<=625&&scale>=.1-.0001&&scale<=4+.0001&&Math.abs(rotation.x**2+rotation.y**2+rotation.z**2+rotation.w**2-1)<.02;
 });
}
