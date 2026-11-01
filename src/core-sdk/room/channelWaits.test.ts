// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {parseProgram,simpleProgramSteps,type BehaviourProgram} from './programs';
import {moduleHash} from './programModules';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {capabilityFeatures} from '../../../shared/capabilities';
import {validRuleView} from './rules';
import {validRoomOwnership} from '../../../shared/roomOwnership';
const source=()=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-channel-wait.json','utf8')) as BehaviourProgram;
it('shares the native wait syntax and refuses lossy legacy conversion',()=>{
 const p=source();expect(parseProgram(JSON.stringify(p)).program).toEqual(p);const call=p.functions[0].body[0];if(call.op!=='invoke')throw Error('fixture');call.waitForChannels={value:'later'};expect(parseProgram(JSON.stringify(p)).program).toBeNull();call.waitForChannels={value:3};p.version=2;delete p.state;delete p.events;expect(parseProgram(JSON.stringify(p)).program).toBeNull();expect(simpleProgramSteps(JSON.stringify(p))).toBeNull();
});
it('links wait expressions in pinned modules and requires the same advertised feature',()=>{
 const child=source();child.state=[{name:'patience',initial:3}];const call=child.functions[0].body[0];if(call.op!=='invoke')throw Error('fixture');call.waitForChannels={state:'patience'};
 const module={version:1 as const,name:'Queued motion',exports:['main'],program:child};const p:BehaviourProgram={version:3,moduleVersion:1,entry:'main',resources:child.resources,state:[],events:[],imports:[{alias:'motion',hash:moduleHash(module),module,signals:{}}],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'call',op:'call',module:'motion',function:'main',args:[]}]}]};
 const linked=parseProgram(JSON.stringify(p));expect(linked.error).toBeNull();expect(linked.linked?.functions.find(f=>f.name==='motion.main')?.body[0]).toMatchObject({waitForChannels:{state:'motion.patience'}});
 const command={action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{program:JSON.stringify(p)}}]}};const capabilities=['eventPrograms.v1','behaviourPrograms.v3','programModules.v1',...capabilityFeatures(call.capability,call.arguments)];expect(()=>requireRoomCapabilities([command],{capabilities})).toThrow('channelWaits.v1');expect(()=>requireRoomCapabilities([command],{capabilities:[...capabilities,'channelWaits.v1']})).not.toThrow();
});
it('reads actual native waiting and completion without inventing channel ownership',()=>{
 const capture=JSON.parse(readFileSync('test-fixtures/browser/channelWaitState.json','utf8'));for(const phase of ['saved','waiting','running','completed']){expect(validRuleView(capture[phase].rules),phase).toBe(true);expect(validRoomOwnership(capture[phase].ownership),phase).toBe(true);}
 const wait=capture.waiting.rules.running[0];expect(wait).toMatchObject({waiting:true,nodeId:'wave'});expect(wait.status).toContain('Waiting for channels');expect(wait.waitSeconds).toBeGreaterThan(0);expect(wait.waitSeconds).toBeLessThanOrEqual(3);expect(capture.waiting.ownership.owners.some((o:{id:string})=>o.id===wait.id)).toBe(false);expect(capture.running.rules.running[0].waiting).toBe(false);expect(capture.completed.rules.outcomes.at(-1).phase).toBe('completed');
});

it('keeps the documented gesture example valid under the shared native contract',()=>{
 const markdown=readFileSync('docs/QUEST_CHANNEL_WAITS.md','utf8');const block=JSON.parse(markdown.match(/```json\n([\s\S]*?)\n```/)![1]);const p=source();p.resources=['maestro'];p.functions[0].body=[block];expect(parseProgram(JSON.stringify(p)).error).toBeNull();
});
