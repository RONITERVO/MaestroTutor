// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Cross-component validation after the shared catalog validates every field.
type Frame={position:{x:number;y:number;z:number};rotation:{x:number;y:number;z:number;w:number}};
export interface ReservoirBed {frame:Frame;width:number;depth:number;cells:number;heights:number[]}
export interface ReservoirContainer {version:number;frame:Frame;rectangle?:{width:number;depth:number}|null;height:number;capacityMl:number;amountMl:number}
export function validTerrainReservoirs(physics:{mode:string},fields:ReservoirBed[],containers:ReservoirContainer[]):boolean {
 const f=Math.fround;
 for(const c of containers){
  if(c.version!==3)continue;
  const bed=fields[0];
  if(physics.mode!=='fixed'||fields.length!==1||!bed||!c.rectangle||f(c.rectangle.width)!==f(bed.width)||f(c.rectangle.depth)!==f(bed.depth))return false;
  for(const key of ['x','y','z'] as const)if(f(c.frame.position[key])!==f(bed.frame.position[key])||f(c.frame.rotation[key])!==f(bed.frame.rotation[key]))return false;
  if(f(c.frame.rotation.w)!==f(bed.frame.rotation.w)||bed.heights.some(h=>f(h)>f(c.height)))return false;
  // Match the float vertices used by native terrain collision, then integrate
  // each triangular column in double precision; no duplicate bed or inventory.
  const vertex=(x:number,z:number)=>({x:f(f(f(x/bed.cells)*f(bed.width))-f(f(bed.width)*.5)),y:f(bed.heights[z*(bed.cells+1)+x]),z:f(f(f(z/bed.cells)*f(bed.depth))-f(f(bed.depth)*.5))});
  type V=ReturnType<typeof vertex>;
  const part=(a:V,b:V,d:V)=>Math.abs((b.x-a.x)*(d.z-a.z)-(b.z-a.z)*(d.x-a.x))*(3*f(c.height)-a.y-b.y-d.y)/6;
  let volume=0;
  for(let z=0;z<bed.cells;z++)for(let x=0;x<bed.cells;x++){
   const a=vertex(x,z),b=vertex(x+1,z),d=vertex(x,z+1),e=vertex(x+1,z+1);volume+=part(a,d,b)+part(b,d,e);
  }
  const capacity=volume*1000000;
  if(capacity!==c.capacityMl||capacity<1||capacity>32000000||c.amountMl>capacity)return false;
 }
 return true;
}
