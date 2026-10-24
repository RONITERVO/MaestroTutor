// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {validVector,validRotation} from './roomRecipe';
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
export function validCollisionRecipe(v:unknown):boolean {
 if(!record(v)||v.version!==1||!Array.isArray(v.shapes)||v.shapes.length>16)return false;
 const ids=new Set<string>();let pieces=0;
 for(const s of v.shapes){
  if(!record(s)||typeof s.id!=='string'||!/^[a-zA-Z0-9_]{1,32}$/.test(s.id)||ids.has(s.id)||!['box','sphere','cylinder','ring'].includes(s.shape as string)||!validVector(s.position)||!validVector(s.size)||!validRotation(s.rotation))return false;
  ids.add(s.id);const {x,y,z}=s.size,p=s.position,reach=Math.hypot(p.x,p.y,p.z);
  if([x,y,z].some(n=>n<.005||n>2)||reach>2||reach+Math.hypot(x,y,z)/2>3)return false;
  const segments=s.segments??0,inner=s.innerRadius??0;
  if(typeof inner!=='number'||!Number.isFinite(inner)||typeof segments!=='number'||!Number.isInteger(segments))return false;
  if(s.shape==='box'||s.shape==='sphere') {if(segments!==0||inner!==0||s.shape==='sphere'&&(x!==y||x!==z))return false;pieces++;}
  else {if(segments<8||segments>24||s.shape==='cylinder'&&inner!==0||s.shape==='ring'&&(inner<.05||inner>.45||Math.min(x,z)*(.5-inner)<.005-1e-7))return false;pieces+=s.shape==='ring'?segments:1;}
 }
 return pieces<=64;
}
