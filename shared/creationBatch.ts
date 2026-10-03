// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Called after structural schema validation; the connected native room checks aggregate capacity.
import {alignedHinge,validHingeDefinition,length2,normalized,rotate,multiply,type HingeDefinition,type HingePose} from './roomHinge';
interface Piece extends HingePose {slot:string;name:string;source:{kind:string;recipe?:{playing:boolean}}}
interface Batch extends HingePose {blueprint:{version:number;pieces:Piece[];hinges?:{owner:string;connected:string;definition:HingeDefinition}[]}}
export function validCreationBatchGeometry(value:Record<string,unknown>):boolean {
 const b=value as unknown as Batch,pieces=b.blueprint.pieces,links=b.blueprint.hinges??[];
 if(new Set(pieces.map(p=>p.slot)).size!==pieces.length||length2(b.position)>625)return false;
 if(b.blueprint.version===1&&links.length||b.blueprint.version===2&&(links.length<1||links.length>15))return false;
 const slots=new Map<string,HingePose>(),q=normalized(b.rotation);
 for(const p of pieces){
  if(/\p{Cc}/u.test(p.name)||/\p{Cc}/u.test(p.slot)||length2(p.position)>100||p.scale*b.scale<.1||p.scale*b.scale>4||p.source.kind==='recipe'&&p.source.recipe?.playing)return false;
  const v=rotate(q,{x:p.position.x*b.scale,y:p.position.y*b.scale,z:p.position.z*b.scale});
  const position={x:b.position.x+v.x,y:b.position.y+v.y,z:b.position.z+v.z};if(length2(position)>625)return false;
  slots.set(p.slot,{position,rotation:normalized(multiply(q,normalized(p.rotation))),scale:p.scale*b.scale});
 }
 const edges=new Map<string,string>();
 for(const link of links){
  if(!slots.has(link.owner)||!slots.has(link.connected)||link.owner===link.connected||edges.has(link.owner)||!validHingeDefinition(link.definition))return false;
  if(!alignedHinge(link.definition,slots.get(link.owner)!,slots.get(link.connected)!))return false;edges.set(link.owner,link.connected);
 }
 for(const start of edges.keys()){const seen=new Set<string>();let id=start;while(edges.has(id)){if(seen.has(id))return false;seen.add(id);id=edges.get(id)!;}}
 return true;
}
