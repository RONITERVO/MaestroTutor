// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface SurfaceGeometry {shape?:string;curvatureRadius?:number;width:number;height:number}
export function validSurfaceGeometry(s:SurfaceGeometry):boolean {
 const shape=s.shape??'plane',r=Math.fround(s.curvatureRadius??0);
 if(shape==='plane')return r===0;
 const width=Math.fround(s.width),height=Math.fround(s.height),pi=Math.fround(Math.PI);
 return (shape==='cylinder'||shape==='sphere')&&Number.isFinite(r)&&r>=Math.fround(.01)&&r<=4&&
  width<=Math.fround(Math.fround(2*pi)*r)&&(shape!=='sphere'||height<=Math.fround(Math.fround(Math.fround(.9)*pi)*r));
}
export function surfaceRenderedPointCount(s:SurfaceGeometry,points:{x:number;y:number}[],radius:number):number {
 if(!points.length)return 0;if((s.shape??'plane')==='plane')return points.length;
 const f=Math.fround,r=f(s.curvatureRadius!),ink=f(radius),step=Math.min(f(5*f(Math.PI/180)),f(2*f(Math.acos(f(1-f(f(ink*f(.25))/f(r+ink)))))));
 let count=1;for(let i=1;i<points.length;i++){
  const travel=f(Math.abs(f(f(points[i].x)-f(points[i-1].x)))+(s.shape==='sphere'?Math.abs(f(f(points[i].y)-f(points[i-1].y))):0));
  count+=Math.max(1,Math.ceil(f(travel/f(r*step))));if(count>2048)return 2049;
 }return count;
}
