// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,it,expect} from 'vitest';
import {assertWaterActorPose} from './probe-water-traversal';
const pose=()=>({target:'maestro',saved:{position:{x:-.78,y:0,z:1.4},rotation:{x:0,y:.9848078,z:0,w:.173648238},scale:1},live:{x:-.78,y:0,z:1.4}});
describe('water policy placement preservation',()=>{
 it('accepts observed native quaternion roundoff and equivalent signs',()=>{
  const before=pose(),after=pose();after.saved.rotation.w=.173648208;after.live.x+=.000001;
  expect(()=>assertWaterActorPose(before,after)).not.toThrow();
  for(const axis of ['x','y','z','w'] as const)after.saved.rotation[axis]*=-1;
  expect(()=>assertWaterActorPose(before,after)).not.toThrow();
 });
 it('rejects changed identity, saved/live movement, rotation, scale and invalid evidence',()=>{
  const before=pose();
  for(const change of [
   (p:ReturnType<typeof pose>)=>{p.target='robot';},
   (p:ReturnType<typeof pose>)=>{p.saved.position.x+=.001;},
   (p:ReturnType<typeof pose>)=>{p.live.y+=.001;},
   (p:ReturnType<typeof pose>)=>{p.saved.rotation={x:0,y:0,z:0,w:1};},
   (p:ReturnType<typeof pose>)=>{p.saved.scale+=.000001;},
   (p:ReturnType<typeof pose>)=>{p.live.x=NaN;},
   (p:ReturnType<typeof pose>)=>{p.saved.rotation.w=2;},
  ]){
   const after=pose();change(after);expect(()=>assertWaterActorPose(before,after)).toThrow();
  }
 });
});
