// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Called after structural schema validation; the connected native room checks aggregate capacity.
import {alignedConnection,validConnectionDefinition,length2,normalized,rotate,multiply,type ConnectionDefinition,type ConnectionPose} from './roomConnection';
import {validPrototypePlacement,type CreationPrototype} from './creationPrototype';
interface Piece extends ConnectionPose {slot:string;name:string;source:{kind:string;recipe?:{playing:boolean};prototype?:CreationPrototype}}
interface Batch extends ConnectionPose {blueprint:{version:number;pieces:Piece[];connections?:{owner:string;connected:string;definition:ConnectionDefinition}[]}}
export function validCreationBatchGeometry(value:Record<string,unknown>):boolean {
 const b=value as unknown as Batch,pieces=b.blueprint.pieces,links=b.blueprint.connections??[];
 if(new Set(pieces.map(p=>p.slot)).size!==pieces.length||length2(b.position)>625)return false;
 if(![1,3].includes(b.blueprint.version)||b.blueprint.version===1&&links.length||b.blueprint.version===3&&(links.length<1||links.length>15))return false;
 const slots=new Map<string,ConnectionPose>(),q=normalized(b.rotation);
 for(const p of pieces){
  if(/\p{Cc}/u.test(p.name)||/\p{Cc}/u.test(p.slot)||length2(p.position)>100||p.scale*b.scale<.1||p.scale*b.scale>4||p.source.kind==='recipe'&&p.source.recipe?.playing)return false;
  const v=rotate(q,{x:p.position.x*b.scale,y:p.position.y*b.scale,z:p.position.z*b.scale});
  const position={x:b.position.x+v.x,y:b.position.y+v.y,z:b.position.z+v.z};if(length2(position)>625)return false;
  const pose={position,rotation:normalized(multiply(q,normalized(p.rotation))),scale:p.scale*b.scale};
  if(p.source.prototype&&!validPrototypePlacement(p.source.prototype,pose))return false;
  slots.set(p.slot,pose);
 }
 const edges=new Map<string,string>();
 for(const link of links){
  if(!slots.has(link.owner)||!slots.has(link.connected)||link.owner===link.connected||edges.has(link.owner)||!validConnectionDefinition(link.definition))return false;
  if(!alignedConnection(link.definition,slots.get(link.owner)!,slots.get(link.connected)!))return false;edges.set(link.owner,link.connected);
 }
 for(const start of edges.keys()){const seen=new Set<string>();let id=start;while(edges.has(id)){if(seen.has(id))return false;seen.add(id);id=edges.get(id)!;}}
 return true;
}
