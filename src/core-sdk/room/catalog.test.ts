// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {validCatalogRequest,validCatalogView} from '../../../shared/roomCatalog';
import {capabilityDefinition,capabilityResources} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseRoomCommands} from './roomAgent';
const check={operation:'check',call:{id:'time.wait',version:1,arguments:{seconds:1}}};
it('allows inspection of invalid or unsupported calls while keeping queries bounded and standalone',()=>{
 expect(validCatalogRequest(check)).toBe(true);
 expect(validCatalogRequest({...check,call:{id:'future.action',version:1,arguments:{seconds:'wrong'}}})).toBe(true);
 for(const bad of [{...check,extra:true},{operation:'run',call:check.call},{...check,call:{...check.call,arguments:{value:'x'.repeat(129)}}},
  {...check,call:{...check.call,version:'1'}},{operation:'search',query:'x',offset:-1},{operation:'search',query:'x\n',offset:0}])
  expect(validCatalogRequest(bad)).toBe(false);
 const command={action:'catalog' as const,catalog:check as {operation:'check';call:typeof check.call}};
 expect(parseRoomCommands({commands:[command]})).toEqual([command]);
 expect(()=>parseRoomCommands({commands:[command,{action:'stop',target:'book'}]})).toThrow('on its own');
 expect(()=>requireRoomCapabilities([command],{capabilities:[]})).toThrow('does not support');
 expect(()=>requireRoomCapabilities([command],{capabilities:['catalog.v1']})).not.toThrow();
});
it('verifies returned definitions semantically and rejects altered contracts or impossible readiness',()=>{
 const definition=capabilityDefinition('time.wait')!,view={operation:'inspect',capability:definition.id,version:1,definition,status:'Definition'};
 expect(validCatalogView(view)).toBe(true);
 expect(validCatalogView({...view,definition:Object.fromEntries(Object.entries(definition).reverse())})).toBe(true);
 expect(validCatalogView({...view,definition:{...definition,version:2}})).toBe(false);
 const result={...check,valid:true,available:true,occupied:false,resources:[],status:'Ready'};
 expect(validCatalogView(result)).toBe(true);
 expect(validCatalogView({...result,valid:false})).toBe(false);
 expect(validCatalogView({...result,occupied:true})).toBe(false);
 expect(validCatalogView({...result,resources:['maestro','maestro']})).toBe(false);
});
it('finds resources through schema annotations, including nested props',()=>{
 expect(capabilityResources('time.wait',{seconds:1})).toEqual([]);
 expect(capabilityResources('animation.play',{source:{kind:'recording'},channel:'wholeTarget',target:'maestro',prop:{objectId:'a'.repeat(32)}})).toEqual(['maestro','a'.repeat(32)]);
 expect(capabilityResources('animation.play',{source:{kind:'recording'},channel:'wholeTarget',target:'maestro',unregistered:{objectId:'book'}})).toEqual(['maestro']);
});

import native from '../../../test-fixtures/browser/catalogStates.json';
import {RoomAgentClient} from '../../platform/quest/roomAgentBridge';
it('accepts actual native catalog wire observations including invalid and occupied actions',()=>{
 const client=new RoomAgentClient();
 for(const state of Object.values(native).sort((a,b)=>a.revision-b.revision)){
  expect(validCatalogView(state.catalog)).toBe(true);
  expect(client.receive(state),state.catalog.status).toBe(true);
 }
 expect(native.ready.catalog.available).toBe(true);
 expect(native.occupied.catalog.occupied).toBe(true);
 expect(native.invalid.catalog.valid).toBe(false);
 expect(native.missing.catalog.valid).toBe(true);
 expect(native.missing.catalog.available).toBe(false);
 expect(native.unknown.catalog.definition).toBeNull();client.cancel();
});
it('sends a read-only nested call without reserving target revisions',async()=>{
 const client=new RoomAgentClient();client.receive(native.ready);
 const command={action:'catalog' as const,catalog:{operation:'check' as const,call:native.ready.catalog.call}};
 const pending=client.request([command]);expect(client.snapshot().request?.conditions).toEqual([]);
 expect(client.snapshot().request?.commands).toEqual([command]);
 client.receive({...native.ready,ack:1,revision:native.ready.revision+1});await pending;client.cancel();
});
