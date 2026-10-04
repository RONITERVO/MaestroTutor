// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Geometry checks after the catalog's strict structural validation.
import {validSurfaceGeometry,surfaceRenderedPointCount,type SurfaceGeometry} from './surfaceGeometry';
import {length2,normalized,rotate,multiply,type ConnectionPose} from './roomConnection';
type V={x:number;y:number;z:number};
type Q=V&{w:number};
interface Frame {time:number;position:V;rotation:Q;scale:number}
interface Surface extends SurfaceGeometry {version:number;id:string;part:string;position:V;width:number;height:number;strokes:{id:string;radius:number;points:V[]}[]}
export interface CreationPrototype {
 geometry:{kind:string;recipe?:{playing:boolean;parts:{id:string}[]};points?:V[];radius?:number};
 snapPoints?:{id:string;frame:{position:V}}[];surfaces:Surface[];drawingTips:{part:string;position:V}[];motion?:{loop:boolean;frames:Frame[]};
}
const hasLength=(p:V[])=>p.some(v=>(v.x-p[0].x)**2+(v.y-p[0].y)**2+(v.z-p[0].z)**2>.000001);
export function validCreationPrototypeGeometry(value:Record<string,unknown>):boolean {
 const p=value as unknown as CreationPrototype,g=p.geometry;
 if(g.recipe?.playing)return false;
 if(g.kind==='drawing'&&(!hasLength(g.points!)||g.points!.some(v=>length2(v)>100)))return false;
 const part=(name:string)=>name===''||!!g.recipe?.parts.some(x=>x.id===name);
 if(new Set(p.surfaces.map(s=>s.id)).size!==p.surfaces.length)return false;
 let points=g.points?.length??0;
 for(const s of p.surfaces){
  if(!validSurfaceGeometry(s)||s.version!==((s.shape??'plane')==='plane'?1:2)||!part(s.part)||length2(s.position)>100||new Set(s.strokes.map(x=>x.id)).size!==s.strokes.length)return false;
  for(const ink of s.strokes){const rendered=surfaceRenderedPointCount(s,ink.points,ink.radius);if(rendered>2048)return false;points+=rendered;
   if(!hasLength(ink.points)||ink.points.some(v=>length2(v)>100||Math.abs(v.z)>.000001||Math.abs(v.x)+ink.radius>s.width*.5+.000001||Math.abs(v.y)+ink.radius>s.height*.5+.000001))return false;
  }
 }
 if(p.snapPoints&&(new Set(p.snapPoints.map(s=>s.id)).size!==p.snapPoints.length||p.snapPoints.some(s=>length2(s.frame.position)>100)))return false;
 if(points>32768||p.drawingTips.some(t=>!part(t.part)||length2(t.position)>100))return false;
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
