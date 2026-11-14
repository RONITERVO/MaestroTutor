// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,expect,it} from 'vitest';
import {RoomAgentClient} from './roomAgentClient';
import {type RoomAgentState} from './roomAgent';
const state=():RoomAgentState=>({version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',
 objects:[{id:'b'.repeat(32),name:'Saved robot',kind:'Block',objectRevision:3,position:{x:1,y:2,z:3},scale:1,color:{r:1,g:1,b:1,a:1},animated:false,runtimeState:'unavailable',positionSource:'saved'}],
 created:[],canUndo:false,canRedo:false,physicsRunning:false});
describe('native saved and runtime presence',()=>{
 it.each(['active','inactive','unavailable'] as const)('preserves %s entities and the source of their coordinates',runtimeState=>{
  const input=state();input.objects[0].runtimeState=runtimeState;input.objects[0].positionSource=runtimeState==='active'?'live':'saved';
  const client=new RoomAgentClient();expect(client.receive(input)).toBe(true);expect(client.getSnapshot().state?.objects).toEqual(input.objects);
 });
 it.each([{runtimeState:'dormant'},{runtimeState:'missing'},{positionSource:'guess'},{runtimeState:'inactive',positionSource:'live'},{runtimeState:'unavailable',positionSource:'live'}])('rejects unknown or contradictory metadata %j',patch=>{
  const input=state();Object.assign(input.objects[0],patch);const client=new RoomAgentClient();expect(client.receive(input)).toBe(false);expect(client.getSnapshot().state).toBeNull();
 });
 it('accepts older object observations without inventing a presence or coordinate source',()=>{
  const input=state();delete input.objects[0].runtimeState;delete input.objects[0].positionSource;
  const client=new RoomAgentClient();expect(client.receive(input)).toBe(true);expect(client.getSnapshot().state?.objects[0]).not.toHaveProperty('runtimeState');expect(client.getSnapshot().state?.objects[0]).not.toHaveProperty('positionSource');
 });
 it('allows an active instance with saved coordinates when a live authored-frame read is unavailable',()=>{
  const input=state();input.objects[0].runtimeState='active';const client=new RoomAgentClient();expect(client.receive(input)).toBe(true);expect(client.getSnapshot().state?.objects[0].positionSource).toBe('saved');
 });
});
