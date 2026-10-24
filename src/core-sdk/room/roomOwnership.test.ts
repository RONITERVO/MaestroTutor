// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {validRoomOwnership,type RoomOwnershipView} from '../../../shared/roomOwnership';
import {RoomAgentClient} from '../../platform/quest/roomAgentBridge';
const view=():RoomOwnershipView=>({suspended:false,error:'',owners:[{id:'hand:maestro',label:'Your grip',role:'grab',allowsGrab:false,claims:[{target:'maestro',channel:'wholeTarget'}]}]});
const state=(ownership:unknown)=>({version:1,session:'b'.repeat(32),revision:1,sceneRevision:1,ack:0,ok:true,status:'Ready',created:[],objects:[],canUndo:false,canRedo:false,physicsRunning:false,capabilities:['roomOwnership.v1'],ownership});
it('accepts compatible owners and rejects malformed evidence before publishing it',()=>{
 const v=view();v.owners.push({id:'recording',label:'Record motion',role:'control',allowsGrab:true,claims:[{target:'maestro',channel:'wholeTarget'}]});
 expect(validRoomOwnership(v)).toBe(true);expect(new RoomAgentClient().receive(state(v))).toBe(true);
 for(const ownership of [undefined,null,{...v,suspended:true},{...v,owners:[...v.owners,v.owners[0]]},{...v,owners:Array(65).fill(v.owners[0])},{...v,error:4},{...v,extra:true}])expect(new RoomAgentClient().receive(state(ownership))).toBe(false);
 for(const owner of [{...v.owners[0],allowsGrab:true},{...v.owners[0],role:'superuser'},{...v.owners[0],claims:[{target:'maestro',channel:''}]},{...v.owners[0],claims:[...v.owners[0].claims,...v.owners[0].claims]}])expect(validRoomOwnership({...v,owners:[owner]})).toBe(false);
 expect(validRoomOwnership({suspended:true,error:'',owners:[]})).toBe(true);
});
it('allows earlier native clients without advertising evidence they do not supply',()=>{
 const old={...state(undefined),capabilities:[]};expect(new RoomAgentClient().receive(old)).toBe(true);
 expect(new RoomAgentClient().receive({...old,ownership:null})).toBe(true);
 expect(new RoomAgentClient().receive({...old,ownership:{unknown:1}})).toBe(false);
});
