// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityInput,capabilityParameterType,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseProgram} from './programs';
import {validRuleView} from './rules';
import {validRoomOwnership} from '../../../shared/roomOwnership';
import {validFactValue} from '../../../shared/behaviourFacts';
const target='0'.repeat(32);
const args={target,source:{kind:'recipe',part:'RightUpperArm'},channel:'recipePart',seconds:1,loop:false};
it('uses one native animation verb with a stable named part and feature-gated source variant',()=>{
 expect(validateCapabilityArguments('animation.play',1,args)).toBeNull();
 expect(capabilityDefinition('animation.recipe.part')).toBeNull();
 expect(capabilityInput('animation.play',args)).toMatchObject({'x-channels':['recipePart'],'x-features':['recipePartPlayback.v1']});
 expect(capabilityParameterType('animation.play','source.part',args)).toBe('text');
 expect(capabilityResources('animation.play',args)).toEqual([target]);
 for(const part of ['', 'arm/child', 'arm:child','a'.repeat(33)])expect(validateCapabilityArguments('animation.play',1,{...args,source:{kind:'recipe',part}})).not.toBeNull();
 for(const extra of [{priority:100},{resume:true},{prop:{objectId:target}}])expect(validateCapabilityArguments('animation.play',1,{...args,...extra})).not.toBeNull();
});
it('loads the exact native parallel program and refuses a runtime missing named-part playback',()=>{
 const source=readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-recipe-parts.json','utf8'),program=JSON.parse(source);
 expect(parseProgram(source).program).toEqual(program);
 const commands=[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{program:source}}]}}];
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','parallelPrograms.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('recipePartPlayback.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'recipePartPlayback.v1']})).not.toThrow();
});

it('validates actual native part traces, pose readback, successful join and grip interruption',()=>{
 const capture=JSON.parse(readFileSync('test-fixtures/browser/recipePartPlayback.json','utf8'));
 for(const phase of ['saved','running','completed','held']){
  expect(validRuleView(capture[phase].rules),phase).toBe(true);expect(validRoomOwnership(capture[phase].ownership),phase).toBe(true);
  expect(validFactValue('object.recipe.pose',capture[phase+'Pose']),phase).toBe(true);
 }
 expect(parseProgram(JSON.stringify(capture.program)).program).toEqual(capture.program);
 expect(capture.running.rules.running).toHaveLength(3);expect(capture.completed.rules.running).toHaveLength(0);expect(capture.held.rules.running).toHaveLength(0);
 expect(capture.running.ownership.owners.flatMap((o:{claims:{channel:string}[]})=>o.claims.map(c=>c.channel)).sort()).toEqual(['recipePart:RightLowerArm','recipePart:RightUpperArm']);
 expect(capture.runningPose.playing).toBe(true);expect(capture.completedPose.playing).toBe(false);expect(capture.heldPose.playing).toBe(false);
 expect(capture.running.rules.outcomes).toHaveLength(0);expect(capture.completed.rules.outcomes[0].phase).toBe('completed');
});
