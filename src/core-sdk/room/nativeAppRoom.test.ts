// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeAppRoom.json';
import {MAX_ROOM_CAPABILITIES} from '../../../shared/roomControls';
import {RoomAgentClient} from './roomAgentClient';
it('accepts the real complete Unity app feature inventory, not only smaller fixture rooms',()=>{
 const client=new RoomAgentClient();expect(native.state.capabilities.length).toBeGreaterThan(64);expect(client.receive(native.state)).toBe(true);
 expect(client.lease()?.state().objects.map(object=>object.id)).toContain('maestro');client.cancel();
});
it('bounds feature growth while still rejecting duplicates and malformed identifiers',()=>{
 const extras=Array.from({length:MAX_ROOM_CAPABILITIES-native.state.capabilities.length},(_,i)=>`extension${i}.v1`);
 const full={...native.state,capabilities:[...native.state.capabilities,...extras]};const client=new RoomAgentClient();
 expect(client.receive(full)).toBe(true);client.cancel();
 for(const capabilities of [[...full.capabilities,'overflow.v1'],[...native.state.capabilities,native.state.capabilities[0]],[...native.state.capabilities,'not a feature']]){
  const rejected=new RoomAgentClient();expect(rejected.receive({...native.state,capabilities})).toBe(false);expect(rejected.lease()).toBeNull();
 }
});
it('rejects an unscoped memory response rather than accepting malformed native state',()=>{
 const client=new RoomAgentClient();
 const memory={ready:false,pending:false,busy:false,temporary:false,error:'',revision:'',programId:'',sessionId:'',page:0,count:0,programs:[],cells:[]};
 expect(client.receive({...native.state,rules:{...native.state.rules,memory}})).toBe(false);
 expect(client.lease()).toBeNull();
});
