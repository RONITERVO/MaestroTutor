// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityResources,literalCapabilityResources,capabilityParameterType,capabilityBindingFields,validateCapabilityArguments} from '../../../shared/capabilities';
import {validFactValue,factArgumentType,validateFactArguments} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseProgram,type BehaviourProgram} from './programs';
import {validRuleView} from './rules';
import {validRoomOwnership} from '../../../shared/roomOwnership';
const source=()=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-object-hold.json','utf8')) as BehaviourProgram;
const args=()=>{const n=source().functions[1].body[0];if(n.op!=='invoke')throw Error('fixture');return structuredClone(n.arguments);};
it('shares exact attachment variants, nested typed paths and both resource identities',()=>{
 const a=args(),before=structuredClone(a);expect(validateCapabilityArguments('object.hold',1,a)).toBeNull();
 expect(capabilityResources('object.hold',a)).toEqual(['0'.repeat(32),'1'.repeat(32)]);
 expect(literalCapabilityResources('object.hold',a,{'holder.objectId':{value:'2'.repeat(32)}},3)).toEqual(['0'.repeat(32)]);expect(a).toEqual(before);
 expect(capabilityParameterType('object.hold','holder.part',a)).toBe('text');expect(capabilityParameterType('object.hold','holder.revision',a)).toBe('number');expect(capabilityParameterType('object.hold','holder.kind',a)).toBeNull();
 expect(capabilityBindingFields('object.hold',a)['holder.part'].type).toBe('string');
 a.holder={kind:'avatarHand',objectId:'maestro',hand:'right',avatarHash:''};expect(validateCapabilityArguments('object.hold',1,a)).toBeNull();expect(capabilityParameterType('object.hold','holder.part',a)).toBeNull();expect(capabilityParameterType('object.hold','holder.hand',a)).toBe('text');
 expect(validateCapabilityArguments('object.hold',1,{...a,priority:99})).not.toBeNull();
});
it('keeps the same native program and blocks unsupported runtimes and bound selectors',()=>{
 const p=source();expect(parseProgram(JSON.stringify(p)).program).toEqual(p);
 const commands=[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{program:JSON.stringify(p)}}]}}];
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','parallelPrograms.v1','recipePartPlayback.v1','actionResults.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('objectAttachments.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'objectAttachments.v1']})).not.toThrow();
 const n=p.functions[1].body[0];if(n.op!=='invoke')throw Error('fixture');n.bindings['holder.kind']={value:'object'};expect(parseProgram(JSON.stringify(p)).program).toBeNull();
});
it('validates typed anchor queries without admitting another variant’s fields',()=>{
 const a={holder:{kind:'object',objectId:'book',revision:1}};
 expect(validateFactArguments('object.anchor',1,a)).toBeNull();expect(factArgumentType('object.anchor','holder.objectId',a)).toBe('text');expect(factArgumentType('object.anchor','holder.revision',a)).toBe('number');expect(factArgumentType('object.anchor','holder.kind',a)).toBeNull();expect(factArgumentType('object.anchor','holder.hand',a)).toBeNull();
 expect(validateFactArguments('object.anchor',1,{holder:{...a.holder,hand:'right'}})).not.toBeNull();
});

it('reads the actual native carry, physical release and joined result through the shared wire',()=>{
 const c=JSON.parse(readFileSync('test-fixtures/browser/objectAttachments.json','utf8'));
 expect(parseProgram(JSON.stringify(c.program)).program).toEqual(c.program);
 for(const phase of ['saved','holding','released','joined']){
  expect(validRuleView(c[phase].rules),phase).toBe(true);expect(validRoomOwnership(c[phase].ownership),phase).toBe(true);
  expect(validFactValue('object.attachment',c[phase+'Attachment']),phase).toBe(true);
 }
 expect(c.saved.rules.running).toHaveLength(0);expect(c.holding.rules.running).toHaveLength(3);expect(c.released.rules.running).toHaveLength(3);expect(c.joined.rules.running).toHaveLength(1);
 const claims=c.holding.ownership.owners.flatMap((o:{claims:{target:string;channel:string}[]})=>o.claims);
 expect(claims).toContainEqual({target:c.program.resources[0],channel:'wholeTarget'});expect(claims).toContainEqual({target:c.program.resources[1],channel:'recipePart:RightUpperArm'});
 expect(c.holdingAttachment.phase).toBe('holding');expect(c.releasedAttachment.phase).toBe('released');expect(c.joinedAttachment.phase).toBe('idle');
 expect(c.joined.rules.running[0].locals).toContainEqual({name:'didThrow',type:'boolean',value:'True'});
});
