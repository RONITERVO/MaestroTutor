// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Pure domain checks after the shared structural schema; native RoomConnection is authoritative.
export type ConnectionVector={x:number;y:number;z:number};
export type ConnectionRotation=ConnectionVector&{w:number};
export interface ConnectionPose {position:ConnectionVector;rotation:ConnectionRotation;scale:number}
export type ConnectionDefinition={breakForce:number;breakTorque:number;enabled:boolean;ownerFrame:{position:ConnectionVector;rotation:ConnectionRotation};connectedFrame:{position:ConnectionVector;rotation:ConnectionRotation}} & (
 {kind:'fixed'} | {kind:'hinge';limits:{enabled:boolean;minimum:number;maximum:number};drive:{mode:string;target:number}}
);
export const length2=(p:ConnectionVector)=>p.x*p.x+p.y*p.y+p.z*p.z;
const add=(a:ConnectionVector,b:ConnectionVector)=>({x:a.x+b.x,y:a.y+b.y,z:a.z+b.z});
const scale=(v:ConnectionVector,s:number)=>({x:v.x*s,y:v.y*s,z:v.z*s});
const dot=(a:ConnectionVector,b:ConnectionVector)=>a.x*b.x+a.y*b.y+a.z*b.z;
const cross=(a:ConnectionVector,b:ConnectionVector)=>({x:a.y*b.z-a.z*b.y,y:a.z*b.x-a.x*b.z,z:a.x*b.y-a.y*b.x});
export const normalized=(q:ConnectionRotation):ConnectionRotation=>{const n=Math.sqrt(length2(q)+q.w*q.w);return {...scale(q,1/n),w:q.w/n};};
export const rotate=(q:ConnectionRotation,v:ConnectionVector):ConnectionVector=>{const uv=cross(q,v),uuv=cross(q,uv);return add(v,scale(add(scale(uv,q.w),uuv),2));};
export const multiply=(a:ConnectionRotation,b:ConnectionRotation):ConnectionRotation=>({...add(add(scale(b,a.w),scale(a,b.w)),cross(a,b)),w:a.w*b.w-dot(a,b)});
const angle=(a:ConnectionVector,b:ConnectionVector)=>Math.acos(Math.max(-1,Math.min(1,dot(a,b)/Math.sqrt(length2(a)*length2(b)))))*180/Math.PI;
export function validConnectionDefinition(d:ConnectionDefinition):boolean {
 return ['hinge','fixed'].includes(d.kind)&&[d.breakForce,d.breakTorque].every(n=>Number.isFinite(n)&&n>=0&&n<=10000)&&(d.kind==='fixed'||d.limits.minimum<d.limits.maximum&&(!d.limits.enabled||d.drive.mode!=='spring'||d.drive.target>=d.limits.minimum&&d.drive.target<=d.limits.maximum))&&[d.ownerFrame,d.connectedFrame].every(f=>length2(f.position)<=100);
}
export function alignedConnection(d:ConnectionDefinition,a:ConnectionPose,b:ConnectionPose):boolean {
 const ap=add(a.position,rotate(a.rotation,scale(d.ownerFrame.position,a.scale))),bp=add(b.position,rotate(b.rotation,scale(d.connectedFrame.position,b.scale)));
 const ar=multiply(a.rotation,d.ownerFrame.rotation),br=multiply(b.rotation,d.connectedFrame.rotation),axis=rotate(br,{x:1,y:0,z:0});
 if(length2(add(ap,scale(bp,-1)))>.03*.03||angle(rotate(ar,{x:1,y:0,z:0}),axis)>5)return false;
 if(d.kind==='fixed')return Math.abs(ar.x*br.x+ar.y*br.y+ar.z*br.z+ar.w*br.w)>=Math.cos(2.5*Math.PI/180);
 const au=rotate(ar,{x:0,y:1,z:0}),bu=rotate(br,{x:0,y:1,z:0});const signed=angle(bu,au)*(dot(axis,cross(bu,au))>=0?1:-1);
 return !d.limits.enabled||signed>=d.limits.minimum-3&&signed<=d.limits.maximum+3;
}
