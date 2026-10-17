// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {RoomAgentClient} from './roomAgentClient';
import type {RoomAgentState} from './roomAgent';
import {capabilityDefinition,capabilityFeatures,capabilityResources,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {validFactValue} from '../../../shared/behaviourFacts';
import {insertProgramCapability} from './programCapabilityEditing';
import {parseProgram} from './programs';
import {behaviourEvent,eventFieldType} from '../../../shared/behaviourEvents';
import {requireRoomCapabilities} from '../../../shared/roomControls';
it('uses exact model/anchor resources and bounded shared catch arguments',()=>{
 const args=structuredClone(capabilityDefinition('object.physics.catch')!.example!);
 expect(validateCapabilityArguments('object.physics.catch',1,args)).toBeNull();
 expect(capabilityResources('object.physics.catch',args)).toEqual(['0'.repeat(32),'maestro']);
 for(const patch of [{timeout:16},{holdSeconds:0},{maxSpeed:9},{gripRadius:1},{offset:{x:1,y:1,z:1}},{priority:40}])expect(validateCapabilityArguments('object.physics.catch',1,{...args,...patch})).not.toBeNull();
 args.holder={kind:'recipePart',objectId:'1'.repeat(32),part:'RightHand',revision:1};expect(validateCapabilityArguments('object.physics.catch',1,args)).toBeNull();expect(capabilityResources('object.physics.catch',args)).toEqual(['0'.repeat(32),'1'.repeat(32)]);
});
it('distinguishes an observed capture/drop from an ordinary timeout and accepts an empty live attempt list',()=>{
 const result={target:'0'.repeat(32),holder:'maestro',phase:'missed',caught:false,dropped:false,reason:'No physical contact'};
 expect(validateCapabilityOutput('object.physics.catch',1,result)).toBeNull();expect(validateCapabilityOutput('object.physics.catch',1,{...result,phase:'dropped',caught:true,dropped:true})).toBeNull();expect(validateCapabilityOutput('object.physics.catch',1,{...result,phase:'launched'})).not.toBeNull();
 expect(validFactValue('object.catch',{target:result.target,attempts:[]})).toBe(true);expect(validFactValue('object.catch',{target:result.target,attempts:[{holder:'maestro',part:'right',phase:'waiting',caught:false,reason:'Waiting for contact'}]})).toBe(true);
});
it('refuses dispatch to an older runtime without physical catching',()=>{
 const args=structuredClone(capabilityDefinition('object.physics.catch')!.example!);const commands=[{action:'execution',execution:{operation:'start',call:{id:'object.physics.catch',version:1,arguments:args}}}];
 expect(capabilityFeatures('object.physics.catch',args)).toEqual(expect.arrayContaining(['physicalCatching.v1','objectAttachments.v1','actionResults.v1']));
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','objectAttachments.v1','actionResults.v1']})).toThrow('physicalCatching.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','objectAttachments.v1','actionResults.v1','physicalCatching.v1']})).not.toThrow();
});

it('upgrades book-authored legacy drafts through the declared program minimum and exposes a typed caught event',()=>{
 const source=JSON.stringify({version:2,entry:'main',resources:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[]}]});const call={id:'object.physics.catch',version:1,arguments:structuredClone(capabilityDefinition('object.physics.catch')!.example!)};
 const program=insertProgramCapability(source,call);expect(program.version).toBe(3);expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 const legacy={...program,version:2};delete legacy.state;delete legacy.events;expect(parseProgram(JSON.stringify(legacy)).error).toContain('version 3');
 expect(behaviourEvent('object.caught')?.features).toContain('physicalCatching.v1');expect(eventFieldType('object.caught','holder')).toBe('text');expect(eventFieldType('object.caught','speed')).toBe('number');
});

it('accepts the full-app native catch journey with exact receipts and both named resources',()=>{
 const native=JSON.parse(readFileSync('test-fixtures/browser/physicalCatching.json','utf8')) as Record<string,unknown>&{call:Parameters<typeof insertProgramCapability>[1];after:RoomAgentState;replay:RoomAgentState;waiting:RoomAgentState;fact:RoomAgentState;blocked:RoomAgentState;readyCheck:RoomAgentState};
 const states=Object.values(native).filter((value):value is RoomAgentState=>Boolean(value&&typeof value==='object'&&'session' in value));
 expect(states).toHaveLength(10);for(const state of states)expect(new RoomAgentClient().receive(state),state.status).toBe(true);
 const receipt=native.after.execution!.selected!;
 expect(receipt.call).toEqual(native.call);expect(receipt.resources).toEqual(capabilityResources(native.call.id,native.call.arguments));expect(receipt.resources).toHaveLength(2);
 expect(receipt.phase).toBe('completed');expect(receipt.output).toMatchObject({caught:true,dropped:true,phase:'dropped'});
 expect(native.replay.execution!.selected).toEqual(receipt);expect(native.waiting.execution!.selected!.phase).toBe('preparing');
 expect(native.fact.catalog).toMatchObject({operation:'inspect',value:{attempts:[{phase:'waiting',caught:false}]}});expect(native.blocked.catalog).toMatchObject({operation:'check',available:false});expect(native.readyCheck.catalog).toMatchObject({operation:'check',available:true});
});
