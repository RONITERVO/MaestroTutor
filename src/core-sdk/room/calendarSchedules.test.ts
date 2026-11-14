// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {behaviourEvent,eventArgumentType,validateEventArguments} from '../../../shared/behaviourEvents';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseProgram,type BehaviourProgram} from './programs';
import {validRuleView} from './rules';
import {validFactValue} from '../../../shared/behaviourFacts';
const source=()=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-calendar.json','utf8')) as BehaviourProgram;
it('shares calendar variants with static selectors and typed variant-specific inputs',()=>{
 const definition=behaviourEvent('clock.scheduled')!,weekly=definition.example!;
 expect(validateEventArguments(definition.id,1,weekly)).toBeNull();expect(eventArgumentType(definition.id,'hour',weekly)).toBe('number');expect(eventArgumentType(definition.id,'kind',weekly)).toBeNull();
 const once={kind:'once',at:'2026-10-02T18:00:00+03:00',missed:'fail',graceSeconds:60};
 expect(validateEventArguments(definition.id,1,once)).toBeNull();expect(eventArgumentType(definition.id,'hour',once)).toBeNull();expect(eventArgumentType(definition.id,'at',once)).toBe('text');
 for(const change of [{at:'2026-10-02T18:00:00'},{resume:true},{graceSeconds:-1},{kind:'cron'}])expect(validateEventArguments(definition.id,1,{...once,...change})).not.toBeNull();
 expect(validateEventArguments(definition.id,1,{...weekly,weekdays:[]})).not.toBeNull();expect(validateEventArguments(definition.id,1,{...weekly,hour:24})).not.toBeNull();
});
it('uses the native weekly program and requires advertised calendar support',()=>{
 const p=source();expect(parseProgram(JSON.stringify(p)).program).toEqual(p);
 const commands=[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{program:JSON.stringify(p)}}]}}];
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','eventFields.v1','eventSubscriptions.v1','actionResults.v1','objectCreation.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('calendarSchedules.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'calendarSchedules.v1']})).not.toThrow();
});
it('rejects bindings made invalid by a schedule change and cannot compute selectors',()=>{
 const p=source(),loop=p.functions[0].body[0];if(loop.op!=='forever')throw Error('fixture');const wait=loop.body[0];if(wait.op!=='awaitEvent')throw Error('fixture');
 wait.bindings={kind:{value:'once'}};expect(parseProgram(JSON.stringify(p)).program).toBeNull();wait.bindings={hour:{value:12}};wait.arguments={kind:'once',at:'2026-10-02T18:00:00Z',missed:'fail',graceSeconds:60};expect(parseProgram(JSON.stringify(p)).program).toBeNull();wait.bindings={at:{value:'2026-10-02T19:00:00Z'}};expect(parseProgram(JSON.stringify(p)).program).not.toBeNull();
});

it('validates native scheduled creation, late branching and stopped observations',()=>{
 const c=JSON.parse(readFileSync('test-fixtures/browser/calendarSchedules.json','utf8'));
 for(const phase of ['saved','waiting','fired','missed','stopped']){
  expect(validRuleView(c[phase].rules),phase).toBe(true);expect(validFactValue('clock.now',c[phase+'Clock']),phase).toBe(true);
 }
 expect(parseProgram(JSON.stringify(c.program)).program).toEqual(c.program);
 expect(c.fired.objects).toHaveLength(c.saved.objects.length+1);expect(c.missed.objects).toHaveLength(c.fired.objects.length);
 expect(c.fired.rules.running[0].state).toContainEqual({name:'created',type:'number',value:'1'});expect(c.missed.rules.running[0].state).toContainEqual({name:'missed',type:'number',value:'1'});
 expect(c.waiting.rules.running[0].waitEvent).toBe('clock.scheduled');expect(c.stopped.rules.running).toHaveLength(0);
});
