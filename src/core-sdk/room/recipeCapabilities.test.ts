// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import nativeProgram from '../../../test-fixtures/browser/recipeCreationProgram.json';
import nativeResult from '../../../test-fixtures/browser/recipeCreationResult.json';
import nativeRoom from '../../../test-fixtures/browser/programBookState.json';
import {validExecutionView} from '../../../shared/roomExecutions';
import {RoomAgentClient} from '../../platform/quest/roomAgentBridge';
import {readFileSync} from 'node:fs';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {boundedCapabilityCall,validCatalogView} from '../../../shared/roomCatalog';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {type RoomRecipe} from '../../../shared/roomRecipe';
import {parseProgram,simpleProgramSteps,sequenceProgram,type BehaviourProgram} from './programs';
import {parseRoomCommands} from './roomAgent';
import {newRuleStep} from './rules';
const capability='object.create';
const definition=()=>capabilityDefinition(capability)!;
const args=()=>definition().input.oneOf!.find(branch=>branch.properties?.kind.enum?.includes('recipe'))!.examples![0] as {kind:string;name:string;x:number;y:number;z:number;scale:number;recipe:RoomRecipe};

it('shares the native recipe example and domain validation with catalog checks and simple authoring',()=>{
 const argumentsValue=args(),call={id:capability,version:1,arguments:argumentsValue};
 expect(argumentsValue.recipe.parts).toHaveLength(19);expect(argumentsValue.recipe.tracks).toHaveLength(2);
 expect(argumentsValue.recipe.playing).toBe(false);
 expect(boundedCapabilityCall(call)).toBe(true);
 expect(validateCapabilityArguments(capability,1,argumentsValue)).toBeNull();
 expect(capabilityResources(capability,argumentsValue)).toEqual([]);
 expect(validCatalogView({operation:'inspect',capability,version:1,definition:definition(),status:'Ready'})).toBe(true);
 const program=sequenceProgram([newRuleStep(13)]),source=JSON.stringify(program);
 expect(parseProgram(source).error).toBeNull();expect(program.resources).toEqual([]);
 const step=simpleProgramSteps(source)![0];expect(step.creationRecipe).toEqual(argumentsValue.recipe);
 step.creationRecipe!.parts[0].color.r=.123;
 expect(simpleProgramSteps(source)![0].creationRecipe!.parts[0].color.r).not.toBe(.123);
 argumentsValue.recipe.parts[0].id='changed';
 expect(args().recipe.parts[0].id).not.toBe('changed');
});

it('rejects malformed collections, hierarchy, keyframes and extra fields before dispatch',()=>{
 const variants:((r:RoomRecipe)=>void)[]=[
  r=>{r.parts=[];},
  r=>{r.parts=Array.from({length:33},()=>r.parts[0]);},
  r=>{r.parts[1].id=r.parts[0].id;},
  r=>{r.parts[0].parent=r.parts[r.parts.length-1].id;},
  r=>{r.parts[0].parent='missing';},
  r=>{r.parts[0].position={x:2,y:2,z:2};},
  r=>{r.parts[0].size.x=.004;},
  r=>{r.parts[0].rotation.w=2;},
  r=>{r.parts[0].color.a=.5;},
  r=>{r.tracks[0].part='missing';},
  r=>{r.tracks.push(r.tracks[0]);},
  r=>{r.tracks[0].keys[0].time=.1;},
  r=>{r.tracks[0].keys[1].time=0;},
  r=>{r.tracks[0].keys[r.tracks[0].keys.length-1].time=r.duration-.1;},
  r=>{r.tracks[0].keys=Array.from({length:17},()=>r.tracks[0].keys[0]);},
  r=>{r.duration=31;},
  r=>{r.tracks=[];r.playing=true;},
  r=>{Object.assign(r.parts[0],{script:'anything'});},
 ];
 for(const mutate of variants){const value=args();mutate(value.recipe);expect(validateCapabilityArguments(capability,1,value)).not.toBeNull();}
 const value=args();value.recipe.parts[0].parent=null;
 expect(validateCapabilityArguments(capability,1,value)).toBeNull();
 value.recipe.parts[0].parent='';expect(validateCapabilityArguments(capability,1,value)).toBeNull();
 value.recipe.tracks=[];expect(validateCapabilityArguments(capability,1,value)).toBeNull();
});

it('keeps nested call inspection bounded even with recipe-size arguments',()=>{
 const call={id:capability,version:1,arguments:args()};expect(boundedCapabilityCall(call)).toBe(true);
 const check=(value:unknown)=>boundedCapabilityCall({...call,arguments:{value}});
 expect(check(Array(65).fill(0))).toBe(false);
 expect(check(Array.from({length:64},()=>Array(64).fill(0)))).toBe(false);
 expect(check(Array.from({length:64},()=>Array(4).fill('x'.repeat(100))))).toBe(false);
 let nested:unknown=0;for(let i=0;i<14;i++)nested={child:nested};expect(check(nested)).toBe(false);
});

it('retains recipe arrays in result-driven programs and requires recipe-capable Unity before saving',()=>{
 const program=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-create.json','utf8')) as BehaviourProgram;
 const create=program.functions[0].body[0],animate=program.functions[0].body[1];
 if(create.op!=='invoke'||animate.op!=='invoke')throw new Error('Expected invocations');
 create.capability=capability;create.arguments=args();animate.capability='animation.play';
 animate.arguments={target:'0'.repeat(32),seconds:.6,loop:true,source:{kind:'recipe'},channel:'wholeTarget'};
 const source=JSON.stringify(program);expect(parseProgram(source).error).toBeNull();
 expect(simpleProgramSteps(source)).toBeNull();
 const commands=parseRoomCommands({commands:[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',reference:'robot',sequence:{id:'',name:'Create robot',interruption:0,repeat:false,program:source}}]}}]});
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','actionResults.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('recipe objects');
 expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'recipeCreation.v1']})).not.toThrow();
 expect(parseProgram(source).program?.functions[0].body[0]).toEqual(create);
});

it('accepts the real native recipe program and persisted output without starting a new action',()=>{
 const source=JSON.stringify(nativeProgram),parsed=parseProgram(source);
 expect(parsed.error).toBeNull();expect(parsed.program).toEqual(nativeProgram);
 expect(validExecutionView(nativeResult)).toBe(true);
 expect(nativeResult.selected.phase).toBe('completed');
 expect(nativeResult.selected.call.arguments.recipe.parts).toHaveLength(19);
 const client=new RoomAgentClient();
 expect(client.receive({...nativeRoom,execution:nativeResult})).toBe(true);
 expect(client.snapshot().request).toBeNull();
 expect(client.getSnapshot().state?.execution?.selected?.output).toEqual(nativeResult.selected.output);
 client.cancel();
});
