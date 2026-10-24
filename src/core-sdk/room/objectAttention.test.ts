// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {parseProgram} from './programs';
const id='object.look',target='a'.repeat(32);
const args={target,joint:{kind:'part',part:'Head'},focus:{kind:'object',objectId:'book'},seconds:2};
it('shares humanoid and recipe attention inputs between users, agents and programs',()=>{
 expect(capabilityDefinition(id)).toMatchObject({duration:'timed',channels:['gaze','recipePart']});
 expect(validateCapabilityArguments(id,1,args)).toBeNull();
 expect(validateCapabilityArguments(id,1,{...args,joint:{kind:'head'}})).toBeNull();
 expect(capabilityResources(id,args)).toEqual([target,'book']);
 const commands=[{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1']})).toThrow('objectAttention.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1','objectAttention.v1']})).not.toThrow();
 const program={version:3,resources:[target,'book'],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'look',op:'invoke',capability:id,version:1,arguments:args,bindings:{}}]}],entry:'main'};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
});
it('keeps explicit joint and focus choices strict and exposes the same sampled result as Maestro',()=>{
 for(const patch of [{joint:{kind:'part'}},{joint:{kind:'head',part:'Head'}},{joint:{kind:'magic'}},{seconds:31},{focus:{kind:'point',point:{x:Infinity,y:0,z:0}}}])
  expect(validateCapabilityArguments(id,1,{...args,...patch})).not.toBeNull();
 expect(behaviourFact('object.attention')).toBeDefined();
 expect(capabilityDefinition(id)?.output).toEqual(capabilityDefinition('avatar.look')?.output);
});
