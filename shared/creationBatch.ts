// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Called after structural schema validation; the connected native room checks aggregate capacity.
type Vector={x:number;y:number;z:number};
type Rotation=Vector&{w:number};
interface Batch {position:Vector;rotation:Rotation;scale:number;blueprint:{pieces:{slot:string;name:string;position:Vector;scale:number;source:{kind:string;recipe?:{playing:boolean}}}[]}}
const length2=(p:Vector)=>p.x*p.x+p.y*p.y+p.z*p.z;
export function validCreationBatchGeometry(value:Record<string,unknown>):boolean {
 const b=value as unknown as Batch,pieces=b.blueprint.pieces;
 if(new Set(pieces.map(p=>p.slot)).size!==pieces.length||length2(b.position)>625)return false;
 const norm=Math.sqrt(length2(b.rotation)+b.rotation.w*b.rotation.w),q={x:b.rotation.x/norm,y:b.rotation.y/norm,z:b.rotation.z/norm,w:b.rotation.w/norm};
 return pieces.every(p=>{
  if(/\p{Cc}/u.test(p.name)||length2(p.position)>100||p.scale*b.scale<.1||p.scale*b.scale>4||p.source.kind==='recipe'&&p.source.recipe?.playing)return false;
  const v={x:p.position.x*b.scale,y:p.position.y*b.scale,z:p.position.z*b.scale};
  const uv={x:q.y*v.z-q.z*v.y,y:q.z*v.x-q.x*v.z,z:q.x*v.y-q.y*v.x};
  const uuv={x:q.y*uv.z-q.z*uv.y,y:q.z*uv.x-q.x*uv.z,z:q.x*uv.y-q.y*uv.x};
  return length2({x:b.position.x+v.x+2*(q.w*uv.x+uuv.x),y:b.position.y+v.y+2*(q.w*uv.y+uuv.y),z:b.position.z+v.z+2*(q.w*uv.z+uuv.z)})<=625;
 });
}
