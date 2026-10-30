// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,it,expect} from 'vitest';
import {assertPhysicsFlight,type PhysicsSample} from './probe-agent-physics';
const expected={radius:.0325,origin:{x:2,y:.0325,z:.6},destination:{x:3,y:.7,z:.6},wallX:3.95};
function flight():PhysicsSample[]{
 const points=[[2,.0325],[2.3,.6],[2.7,.9],[3,.7],[3.3,.3],[3.9175,.0325],...Array.from({length:12},()=>[3.9175,.0325])];
 return points.map(([x,y],i)=>({time:i*100,revision:i+1,running:true,simulating:true,position:{x,y,z:.6}}));
}
describe('native provider physics evidence',()=>{
 it('requires actual rise, destination approach, floor support and settled motion',()=>{
  expect(assertPhysicsFlight(flight(),expected)).toMatchObject({ballisticRiseAndFall:true,floorCollisionAndSettling:true});
  expect(()=>assertPhysicsFlight(flight().map(s=>({...s,position:{...s.position,y:.0325}})),expected)).toThrow(/rise/);
  expect(()=>assertPhysicsFlight(flight().map(s=>({...s,position:{...s.position,x:s.position.x+2}})),expected)).toThrow();
  const penetrated=flight();penetrated[5].position.y=-.1;expect(()=>assertPhysicsFlight(penetrated,expected)).toThrow(/penetrated/);
  const throughWall=flight();throughWall[5].position.x=4.2;expect(()=>assertPhysicsFlight(throughWall,expected)).toThrow(/wall/);
  const moving=flight();moving.at(-1)!.position.x-=.2;expect(()=>assertPhysicsFlight(moving,expected)).toThrow(/settle/);
 });
 it('rejects paused, missing, invalid and reordered observations',()=>{
  expect(()=>assertPhysicsFlight(flight().slice(0,3),expected)).toThrow(/few/);
  expect(()=>assertPhysicsFlight(flight().map(s=>({...s,simulating:false})),expected)).toThrow(/running/);
  expect(()=>assertPhysicsFlight(flight().map(s=>({...s,running:false})),expected)).toThrow(/running/);
  const duplicate=flight();duplicate[4].time=duplicate[3].time;expect(()=>assertPhysicsFlight(duplicate,expected)).toThrow(/monotonic/);
  const invalid=flight();invalid[3].position.y=NaN;expect(()=>assertPhysicsFlight(invalid,expected)).toThrow();
 });
});
