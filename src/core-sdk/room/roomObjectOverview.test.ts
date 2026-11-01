// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {RoomAgentClient} from './roomAgentClient';
import {inspectedRoomObject,roomRevisionBasis} from './roomObjectContext';
import type {RoomAgentState,RoomObjectObservation} from './roomAgent';
import {ROOM_OBJECT_OVERVIEW_FEATURE,ROOM_OBJECT_OVERVIEW_LIMIT} from '../../../shared/roomObjectOverview';
import {roomAgentInstruction,buildRoomResultInstruction,ROOM_OBJECT_OVERVIEW_GUIDE} from '../../../shared/prompts/room';
const object=(id:string):RoomObjectObservation=>({id,name:'Saved object',kind:'Block',position:{x:0,y:0,z:0},color:{r:1,g:1,b:1,a:1},scale:1,animated:false,objectRevision:2});
const state=():RoomAgentState=>({version:1,session:'a'.repeat(32),revision:1,sceneRevision:7,ack:0,ok:true,status:'Ready',created:[],canUndo:false,canRedo:false,physicsRunning:false,workspaceReady:true,
 capabilities:[ROOM_OBJECT_OVERVIEW_FEATURE,'objectSearch.v1'],objectOverview:{version:1,scope:'context',exhaustive:false,limit:4},objects:[object('book'),object('maestro')]});
it('accepts a bounded context including an empty view without treating it as an empty saved world',()=>{
 for(const objects of [[],state().objects,[...state().objects,object('b'.repeat(32)),object('c'.repeat(32))]]){
  const client=new RoomAgentClient(),input={...state(),objects};expect(client.receive(input)).toBe(true);
  expect(client.getSnapshot().state?.objectOverview?.exhaustive).toBe(false);expect(client.getSnapshot().state?.objects).toEqual(objects);client.cancel();
 }
});
it.each([undefined,null,{version:2,scope:'context',exhaustive:false,limit:4},{version:1,scope:'visible',exhaustive:false,limit:4},{version:1,scope:'context',exhaustive:true,limit:4},{version:1,scope:'context',exhaustive:false,limit:66},{version:1,scope:'context',exhaustive:false,limit:4,total:0}])('rejects missing, exhaustive or ambiguous overview metadata: %j',objectOverview=>{
 const client=new RoomAgentClient();expect(client.receive({...state(),objectOverview})).toBe(false);expect(client.getSnapshot().state).toBeNull();
});
it('requires matching discovery/readiness and refuses overflow or unadvertised metadata',()=>{
 const input=state();
 for(const bad of [{...input,capabilities:['objectSearch.v1']},{...input,capabilities:[ROOM_OBJECT_OVERVIEW_FEATURE]},{...input,workspaceReady:false},{...input,objects:Array.from({length:ROOM_OBJECT_OVERVIEW_LIMIT+1},(_,i)=>object(i.toString(16).padStart(32,'0')))}])expect(new RoomAgentClient().receive(bad)).toBe(false);
});
it('keeps exact off-overview inspection and earlier revision evidence when context focus changes',()=>{
 const item=object('b'.repeat(32)),first={...state(),objects:[item]};const client=new RoomAgentClient();expect(client.receive(first)).toBe(true);
 const next={...state(),revision:2,objects:[],inspection:{id:item.id,objectRevision:2,item,recipe:null}};
 expect(client.receive(next)).toBe(true);expect(inspectedRoomObject(next)).toEqual(item);expect(roomRevisionBasis(next,[first])).toContainEqual({id:item.id,objectRevision:2});
 const later={...state(),revision:3,objects:[]};expect(client.receive(later)).toBe(true);expect(roomRevisionBasis(later,[first])).toContainEqual({id:item.id,objectRevision:2});client.cancel();
});
it('retains legacy and unbound observations without inventing overview metadata',()=>{
 const input=state();delete input.objectOverview;input.capabilities=[];input.workspaceReady=false;input.objects=[];
 const client=new RoomAgentClient();expect(client.receive(input)).toBe(true);expect(client.getSnapshot().state).not.toHaveProperty('objectOverview');client.cancel();
});
it('teaches both planning and final replies that omitted objects still need exact evidence',()=>{
 expect(roomAgentInstruction([ROOM_OBJECT_OVERVIEW_FEATURE])).toContain(ROOM_OBJECT_OVERVIEW_GUIDE);
 expect(roomAgentInstruction(['catalogGuides.v1',ROOM_OBJECT_OVERVIEW_FEATURE])).toContain(ROOM_OBJECT_OVERVIEW_GUIDE);
 expect(roomAgentInstruction([])).not.toContain(ROOM_OBJECT_OVERVIEW_GUIDE);
 expect(buildRoomResultInstruction([],state())).toContain(ROOM_OBJECT_OVERVIEW_GUIDE);
 expect(buildRoomResultInstruction([state()])).toContain(ROOM_OBJECT_OVERVIEW_GUIDE);
 expect(buildRoomResultInstruction([])).not.toContain(ROOM_OBJECT_OVERVIEW_GUIDE);
});
