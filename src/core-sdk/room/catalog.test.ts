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

import {behaviourEvent} from '../../../shared/behaviourEvents';
import {behaviourFact} from '../../../shared/behaviourCatalog';
it('scopes discovery without allowing categories on execution checks or older runtimes',()=>{
 for(const category of ['actions','events','facts']) {
  const query={operation:'inspect',category,capability:'physics.ready',version:1};
  expect(validCatalogRequest(query)).toBe(true);
  const command={action:'catalog',catalog:query};
  expect(()=>requireRoomCapabilities([command],{capabilities:['catalog.v1']})).toThrow('catalogVocabulary.v1');
  expect(()=>requireRoomCapabilities([command],{capabilities:['catalog.v1','catalogVocabulary.v1']})).not.toThrow();
 }
 for(const bad of [{...check,category:'actions'},{operation:'search',category:null,query:'',offset:0},{operation:'search',category:'unknown',query:'',offset:0}])expect(validCatalogRequest(bad)).toBe(false);
});
it('checks exact event and fact definitions and distinguishes false, unavailable and wrong scalar types',()=>{
 const event={operation:'inspect',category:'events',capability:'object.collided',version:2,definition:behaviourEvent('object.collided'),status:'Event definition'};
 expect(validCatalogView(event)).toBe(true);expect(validCatalogView({...event,category:'actions'})).toBe(false);
 expect(validCatalogView({...event,definition:{...event.definition,features:[]}})).toBe(false);
 const fact={operation:'inspect',category:'facts',capability:'physics.ready',version:1,definition:behaviourFact('physics.ready'),available:true,value:false,status:'Current reading'};
 expect(validCatalogView(fact)).toBe(true);expect(validCatalogView({...fact,value:true})).toBe(true);
 expect(validCatalogView({...fact,available:false,value:null})).toBe(true);
 for(const value of [null,0,'false',{},NaN])expect(validCatalogView({...fact,value})).toBe(false);
 expect(validCatalogView({...fact,available:false})).toBe(false);
 expect(validCatalogView({...fact,definition:null})).toBe(false);
 expect(validCatalogView({...fact,capability:'future.fact',definition:null,available:false,value:null})).toBe(true);
 const text={...fact,capability:'room.sessionId',definition:behaviourFact('room.sessionId'),value:''};expect(validCatalogView(text)).toBe(true);
 expect(validCatalogView({...text,value:'x'.repeat(129)})).toBe(false);
 fact.definition!.description='Changed';expect(behaviourFact('physics.ready')!.description).not.toBe('Changed');expect(validCatalogView(fact)).toBe(false);
});

import nativeModules from '../../../test-fixtures/browser/moduleLibraryCatalog.json';
it('accepts native module pins and requires library support for human and agent requests',()=>{
 for(const view of Object.values(nativeModules))expect(validCatalogView(view)).toBe(true);
 const queries=[{operation:'search',category:'modules',query:'remember',offset:0},{operation:'inspect',category:'modules',capability:nativeModules.inspected.capability,version:1}];
 for(const query of queries){const commands=parseRoomCommands({commands:[{action:'catalog',catalog:query}]});
  expect(()=>requireRoomCapabilities(commands,{capabilities:['catalog.v1']})).toThrow('moduleLibrary.v1');
  expect(()=>requireRoomCapabilities(commands,{capabilities:['catalog.v1','moduleLibrary.v1']})).not.toThrow();
 }
 const call={id:'program.module.publish',version:1,arguments:{sequenceId:'b'.repeat(32),rulesRevision:1,name:'Remember amounts',exports:['remember']}};
 const commands=parseRoomCommands({commands:[{action:'execution',execution:{operation:'start',call}}]});
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('moduleLibrary.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','moduleLibrary.v1']})).not.toThrow();
 expect(nativeModules.removed.definition).toBeNull();
});

it('correlates bounded fact arguments and validates structured available values',()=>{
 const query={operation:'inspect',category:'facts',capability:'object.position',version:2,arguments:{target:'book'}};
 expect(validCatalogRequest(query)).toBe(true);expect(parseRoomCommands({commands:[{action:'catalog',catalog:query}]})).toHaveLength(1);
 for(const category of ['events','actions','modules'])expect(validCatalogRequest({...query,category})).toBe(false);
 expect(validCatalogRequest({...query,arguments:{target:'x'.repeat(129)}})).toBe(false);
 const view={...query,definition:behaviourFact('object.position'),available:true,value:{x:0,y:1,z:2},status:'Current position'};
 expect(validCatalogView(view)).toBe(true);for(const value of [{x:0,y:1},{x:0,y:1,z:Infinity},{x:0,y:1,z:2,extra:0},'wrong',null])expect(validCatalogView({...view,value})).toBe(false);
 expect(validCatalogView({...view,arguments:{target:'bad'}})).toBe(false);expect(validCatalogView({...view,available:false,value:null})).toBe(true);
 const command=parseRoomCommands({commands:[{action:'catalog',catalog:query}]});expect(()=>requireRoomCapabilities(command,{capabilities:['catalog.v1','catalogVocabulary.v1']})).toThrow('factQueries.v1');
 expect(()=>requireRoomCapabilities(command,{capabilities:['catalog.v1','catalogVocabulary.v1','factQueries.v1']})).not.toThrow();
});

it('accepts included module provenance only for a readable definition and bounds the combined library',()=>{
 expect(validCatalogView({...nativeModules.inspected,included:true})).toBe(true);
 expect(validCatalogView({...nativeModules.inspected,included:'yes'})).toBe(false);
 expect(validCatalogView({...nativeModules.removed,included:true})).toBe(false);
 const empty={operation:'search',category:'modules',query:'',offset:0,pageSize:6,total:272,entries:[],revision:1,ready:true,pending:false,status:'Ready'};
 expect(validCatalogView(empty)).toBe(true);expect(validCatalogView({...empty,total:273})).toBe(false);
});
