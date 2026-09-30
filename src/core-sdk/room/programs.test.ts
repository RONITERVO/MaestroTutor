// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,it,expect} from 'vitest';
import {readFileSync} from 'node:fs';
import {parseProgram,sequenceProgram,simpleProgramSteps,withSimpleProgramSteps,type BehaviourProgram} from './programs';
import {validSequence,newRuleStep,validRuleView} from './rules';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {RoomAgentClient} from '../../platform/quest/roomAgentBridge';
import {parseRoomCommands,isRoomQuery} from './roomAgent';
const fixture=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-contract.json','utf8')) as {cases:{name:string;source:string;valid:boolean}[]};
describe('shared behaviour programs',()=>{
 it.each(fixture.cases)('$name',({source,valid})=>expect(parseProgram(source).program!==null).toBe(valid));
 it('preserves linear steps in a program and rejects mixed formats and unsupported clients',()=>{
  const steps=[{...newRuleStep(1),id:'b'.repeat(32)}],program=sequenceProgram(steps),source=JSON.stringify(program);
  expect(parseProgram(source).program).toEqual(program);expect((program.functions[0].body[0] as {id:string}).id).toBe(steps[0].id);
  const sequence={id:'a'.repeat(32),name:'Wave',interruption:0,repeat:false,program:source};expect(validSequence(sequence)).toBe(true);expect(validSequence({...sequence,steps})).toBe(false);
  const commands=parseRoomCommands({commands:[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence}]}}]});
  expect(()=>requireRoomCapabilities(commands,{})).toThrow('behaviour programs');expect(()=>requireRoomCapabilities(commands,{capabilities:['behaviourPrograms.v3']})).not.toThrow();
 });
 it('bounds source size, deep expressions, duplicate JSON keys and nested native fields',()=>{
  const source=fixture.cases[0].source;expect(parseProgram(source+' '.repeat(24000)).program).toBeNull();
  const program=JSON.parse(source) as BehaviourProgram;let test:unknown={value:true};for(let i=0;i<10;i++)test={op:'not',args:[test]};(program.functions[0].body[1] as unknown as {test:unknown}).test=test;expect(parseProgram(JSON.stringify(program)).program).toBeNull();
  expect(parseProgram(source.replace('"version":2','"version":2,"ver\\u0073ion":2')).program).toBeNull();
  expect(parseProgram(source.replace('"seconds":0.1','"seconds":null')).program).toBeNull();
 });
 it('accepts actual current Unity wire observations when provided',()=>{
  const path=process.env.MAESTRO_PROGRAM_EVIDENCE;if(!path)return;
  for(const phase of ['running','completed','cancelled']){const state=JSON.parse(readFileSync(`${path}/program-${phase}.json`,'utf8'));expect(validRuleView(state.rules),phase).toBe(true);}
 });
});

it('edits literal action views without losing identities, function names or extra resources',()=>{
 const program=sequenceProgram([{...newRuleStep(0),id:'move',targetId:'book'}]);program.entry='start';program.functions[0].name='start';program.resources.push('maestro');
 const source=JSON.stringify(program),steps=simpleProgramSteps(source)!;steps[0].targetId='e'.repeat(32);
 const edited=parseProgram(withSimpleProgramSteps(source,steps)).program!;expect(edited.entry).toBe('start');expect(edited.resources).toEqual(['maestro','e'.repeat(32)]);expect(edited.functions[0].body[0].id).toBe('move');expect(simpleProgramSteps(source)![0].targetId).toBe('book');
 const block=program.functions[0].body[0];if(block.op!=='invoke')throw new Error('Expected action');block.bindings.seconds={value:2};
 expect(simpleProgramSteps(JSON.stringify(program))).toBeNull();expect(()=>withSimpleProgramSteps(JSON.stringify(program),steps)).toThrow('function editor');
});

describe('event programs share the native contract',()=>{
 const source=readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-events.json','utf8');
 it('accepts the actual native event fixture, preserves state and requires native support',()=>{
  const p=parseProgram(source);expect(p.error).toBeNull();expect(p.program?.state).toEqual([{name:'count',initial:0}]);expect(simpleProgramSteps(source)).toBeNull();
  const sequence={id:'a'.repeat(32),name:'Reactive',interruption:0,repeat:false,program:source};
  expect(validSequence(sequence)).toBe(true);expect(validSequence({...sequence,repeat:true})).toBe(false);
  const commands=parseRoomCommands({commands:[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence}]}}]});
  expect(()=>requireRoomCapabilities(commands,{capabilities:['behaviourPrograms.v3']})).toThrow('event programs');
  expect(()=>requireRoomCapabilities(commands,{capabilities:['behaviourPrograms.v3','eventPrograms.v1']})).not.toThrow();
 });
 it('rejects unknown events, wrong payload locals, undeclared state and forged built-in emission',()=>{
  const variants=[
   (p:ReturnType<typeof JSON.parse>)=>{p.version=2;},
   (p:ReturnType<typeof JSON.parse>)=>{p.functions[0].body[0].body[0].event='unknown';},
   (p:ReturnType<typeof JSON.parse>)=>{p.functions[0].body[0].body[0].received='payload';},
   (p:ReturnType<typeof JSON.parse>)=>{p.functions[0].body[0].body[0].value='received';},
   (p:ReturnType<typeof JSON.parse>)=>{p.functions[0].body[0].body[1].then[0].value={state:'missing'};},
   (p:ReturnType<typeof JSON.parse>)=>{p.functions[0].body=[{id:'fake',op:'emitEvent',event:'maestro.speaking.enter',value:{value:'speaking'}}];},
   (p:ReturnType<typeof JSON.parse>)=>{p.events[0].type='void';},
  ];
  for(const mutate of variants){const p=JSON.parse(source);mutate(p);expect(parseProgram(JSON.stringify(p)).program).toBeNull();}
 });
 it('validates custom signals as actions and refuses unknown fields and oversized values',()=>{
  const command={action:'rules',rule:{action:'signal',revision:1,eventName:'user.wave',value:3}};
  expect(parseRoomCommands({commands:[command]})).toEqual([command]);
  expect(isRoomQuery(parseRoomCommands({commands:[command]})[0])).toBe(false);
  expect(isRoomQuery({action:'rules',rule:{action:'inspect',target:'a'.repeat(32)}})).toBe(true);
  expect(()=>requireRoomCapabilities([{action:'rules',rule:{action:'stop',target:'a'.repeat(32)}}],{capabilities:['behaviourPrograms.v3']})).toThrow('event programs');
  expect(()=>requireRoomCapabilities(parseRoomCommands({commands:[command]}),{capabilities:['behaviourPrograms.v3']})).toThrow('event programs');
  for(const rule of [{...command.rule,value:Infinity},{...command.rule,eventName:'maestro.speaking.enter'},{...command.rule,value:'x'.repeat(129)},{...command.rule,extra:true}])
   expect(()=>parseRoomCommands({commands:[{...command,rule}]})).toThrow();
 });
});

it('validates actual native event runs and their retained state through the room bridge',()=>{
 const states=JSON.parse(readFileSync('test-fixtures/browser/eventProgramStates.json','utf8'));
 for(const state of Object.values(states))expect(new RoomAgentClient().receive(state)).toBe(true);
 expect(states.waiting.rules.running[0]).toMatchObject({waiting:true,waitEvent:'user.wave',state:[{name:'count',type:'number',value:'0'}]});
 expect(states.second.rules.running[0].state[0].value).toBe('2');
 expect(states.stopped.rules.running).toEqual([]);expect(states.paused.rules.running).toEqual([]);
});

it('shares native creation-result programs and gates result authoring on runtime support',()=>{
 const program=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-create.json','utf8')) as BehaviourProgram;
 const source=JSON.stringify(program);expect(parseProgram(source).error).toBeNull();expect(simpleProgramSteps(source)).toBeNull();
 const commands=parseRoomCommands({commands:[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',reference:'create',sequence:{id:'',name:'Create and push',interruption:0,repeat:false,program:source}}]}}]});
 expect(()=>requireRoomCapabilities(commands,{capabilities:['behaviourPrograms.v3','eventPrograms.v1']})).toThrow('action results');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['behaviourPrograms.v3','eventPrograms.v1','actionResults.v1']})).not.toThrow();
 const create=program.functions[0].body[0];if(create.op!=='invoke')throw new Error('Expected create');
 create.results={unknown:'ball'};expect(parseProgram(JSON.stringify(program)).program).toBeNull();
 create.results={objectId:'ball'};program.functions[0].locals[0].initial=0;expect(parseProgram(JSON.stringify(program)).program).toBeNull();
 program.functions[0].locals[0].initial='';program.version=2;delete program.state;delete program.events;expect(parseProgram(JSON.stringify(program)).program).toBeNull();
});

it('shares typed contact fields and rejects ambiguous bindings without granting object authority',()=>{
 const program=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-contact.json','utf8'));
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 const commands=()=>parseRoomCommands({commands:[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',reference:'watch',sequence:{id:'',name:'Contact watcher',repeat:false,interruption:0,program:JSON.stringify(program)}}]}}]});
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1'];
 expect(()=>requireRoomCapabilities(commands(),{capabilities})).toThrow('eventFields.v1');
 expect(()=>requireRoomCapabilities(commands(),{capabilities:[...capabilities,'eventFields.v1']})).not.toThrow();
 const wait=program.functions[0].body[0].body[0];delete wait.fields;
 expect(()=>requireRoomCapabilities(commands(),{capabilities})).toThrow('eventFields.v1');
 for(const fields of [{speed:'kind'},{otherId:'source'},{speed:'speed',x:'speed'},{future:'speed'},{speed:'missing'},null,[]]){
  wait.fields=fields;expect(parseProgram(JSON.stringify(program)).program).toBeNull();
 }
 wait.fields={speed:'speed'};wait.event='user.example';program.events=[{name:'user.example',type:'text'}];expect(parseProgram(JSON.stringify(program)).program).toBeNull();
});

it('shares strict versioned subscription inputs and gates the native producer',()=>{
 const source=readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-proximity.json','utf8');expect(parseProgram(source).error).toBeNull();
 const sequence={id:'a'.repeat(32),name:'Near',interruption:0,repeat:false,program:source};
 const commands=parseRoomCommands({commands:[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence}]}}]});
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','eventFields.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('eventSubscriptions.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'eventSubscriptions.v1']})).not.toThrow();
 for(const mutate of [
  (n:any)=>{delete n.version;},(n:any)=>{n.version=2;},(n:any)=>{delete n.bindings;},(n:any)=>{delete n.arguments;},
  (n:any)=>{n.source='book';},(n:any)=>{n.arguments.radius=0;},(n:any)=>{n.arguments.hysteresis=0;},(n:any)=>{n.arguments.extra=true;},
  (n:any)=>{n.bindings.target={value:1};},(n:any)=>{n.bindings.unknown={value:1};},(n:any)=>{n.fields.inside='distance';},
 ]){const p=JSON.parse(source);mutate(p.functions[0].body[0].body[0]);expect(parseProgram(JSON.stringify(p)).program).toBeNull();}
 const p=JSON.parse(source);p.functions[0].body[0].body[0].bindings={radius:{value:20},target:{var:'other'}};
 expect(parseProgram(JSON.stringify(p)).error).toBeNull(); // Native revalidates computed values when the wait starts.
 p.functions[0].body[0].body[0].event='object.collided';expect(parseProgram(JSON.stringify(p)).program).toBeNull();
});

it('uses catalog validation for motion thresholds, initial-state policy and measured fields',()=>{
 const source=readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-physics-motion.json','utf8');expect(parseProgram(source).error).toBeNull();
 for(const edit of [(w:any)=>{w.arguments.speedThreshold=0;},(w:any)=>{w.arguments.angularThreshold=6;},(w:any)=>{w.arguments.quietSeconds=11;},(w:any)=>{w.arguments.initial='automatic';},(w:any)=>{w.arguments.transition='idle';},(w:any)=>{w.source='book';},(w:any)=>{w.fields.speed='settled';},(w:any)=>{w.fields.madeUp='quiet';}]){
  const p=JSON.parse(source);edit(p.functions[0].body[0]);expect(parseProgram(JSON.stringify(p)).program).toBeNull();
 }
 const p=JSON.parse(source);p.functions[0].body[0].bindings={speedThreshold:{var:'speed'}};expect(parseProgram(JSON.stringify(p)).error).toBeNull();
});
