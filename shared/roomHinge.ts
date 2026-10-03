// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Pure domain checks after the shared structural schema; native RoomHinge is authoritative.
export type HingeVector={x:number;y:number;z:number};
export type HingeRotation=HingeVector&{w:number};
export interface HingePose {position:HingeVector;rotation:HingeRotation;scale:number}
export interface HingeDefinition {enabled:boolean;ownerFrame:{position:HingeVector;rotation:HingeRotation};connectedFrame:{position:HingeVector;rotation:HingeRotation};limits:{enabled:boolean;minimum:number;maximum:number};drive:{mode:string;target:number}}
export const length2=(p:HingeVector)=>p.x*p.x+p.y*p.y+p.z*p.z;
const add=(a:HingeVector,b:HingeVector)=>({x:a.x+b.x,y:a.y+b.y,z:a.z+b.z});
const scale=(v:HingeVector,s:number)=>({x:v.x*s,y:v.y*s,z:v.z*s});
const dot=(a:HingeVector,b:HingeVector)=>a.x*b.x+a.y*b.y+a.z*b.z;
const cross=(a:HingeVector,b:HingeVector)=>({x:a.y*b.z-a.z*b.y,y:a.z*b.x-a.x*b.z,z:a.x*b.y-a.y*b.x});
export const normalized=(q:HingeRotation):HingeRotation=>{const n=Math.sqrt(length2(q)+q.w*q.w);return {...scale(q,1/n),w:q.w/n};};
export const rotate=(q:HingeRotation,v:HingeVector):HingeVector=>{const uv=cross(q,v),uuv=cross(q,uv);return add(v,scale(add(scale(uv,q.w),uuv),2));};
export const multiply=(a:HingeRotation,b:HingeRotation):HingeRotation=>({...add(add(scale(b,a.w),scale(a,b.w)),cross(a,b)),w:a.w*b.w-dot(a,b)});
const angle=(a:HingeVector,b:HingeVector)=>Math.acos(Math.max(-1,Math.min(1,dot(a,b)/Math.sqrt(length2(a)*length2(b)))))*180/Math.PI;
export function validHingeDefinition(d:HingeDefinition):boolean {
 return d.limits.minimum<d.limits.maximum&&(!d.limits.enabled||d.drive.mode!=='spring'||d.drive.target>=d.limits.minimum&&d.drive.target<=d.limits.maximum)&&[d.ownerFrame,d.connectedFrame].every(f=>length2(f.position)<=100);
}
export function alignedHinge(d:HingeDefinition,a:HingePose,b:HingePose):boolean {
 const ap=add(a.position,rotate(a.rotation,scale(d.ownerFrame.position,a.scale))),bp=add(b.position,rotate(b.rotation,scale(d.connectedFrame.position,b.scale)));
 const ar=multiply(a.rotation,d.ownerFrame.rotation),br=multiply(b.rotation,d.connectedFrame.rotation),axis=rotate(br,{x:1,y:0,z:0});
 if(length2(add(ap,scale(bp,-1)))>.03*.03||angle(rotate(ar,{x:1,y:0,z:0}),axis)>5)return false;
 const au=rotate(ar,{x:0,y:1,z:0}),bu=rotate(br,{x:0,y:1,z:0});const signed=angle(bu,au)*(dot(axis,cross(bu,au))>=0?1:-1);
 return !d.limits.enabled||signed>=d.limits.minimum-3&&signed<=d.limits.maximum+3;
}
