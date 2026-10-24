// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {behaviourEvent} from '../../../shared/behaviourEvents';
import {validateFactArguments} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseProgram,type BehaviourProgram} from './programs';
it('shares the native contact event with typed fields and requires contact support',()=>{
 const program=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-liquid-contact.json','utf8')) as BehaviourProgram;
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect(behaviourEvent('object.medium.contact')?.features).toContain('liquidContacts.v1');
 const commands=[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{program:JSON.stringify(program)}}]}}];
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','eventFields.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('liquidContacts.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'liquidContacts.v1']})).not.toThrow();
 program.functions[0].locals.find(v=>v.name==='who')!.initial=0;
 expect(parseProgram(JSON.stringify(program)).program).toBeNull();
});
it('distinguishes physical inputs from room-object contact targets',()=>{
 expect(validateFactArguments('input.medium.contactState',1,{side:'left'})).toBeNull();
 expect(validateFactArguments('input.medium.contactState',1,{side:'ray'})).not.toBeNull();
 expect(validateFactArguments('object.medium.contactState',1,{target:'maestro'})).toBeNull();
 expect(validateFactArguments('object.medium.contactState',1,{target:'input:left'})).not.toBeNull();
});

it('gives actionable shared feedback for malformed real-provider program shapes',()=>{
 const fixture=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-liquid-contact.json','utf8'));
 const missing=structuredClone(fixture);delete missing.events;expect(parseProgram(JSON.stringify(missing)).error).toContain('missing: events');
 const timeout=structuredClone(fixture);timeout.functions[0].body[0].timeout=0;expect(parseProgram(JSON.stringify(timeout)).error).toContain('expression must be an object');
 const state=structuredClone(fixture);const node=state.functions[0].body[1];node.state=node.variable;delete node.variable;expect(parseProgram(JSON.stringify(state)).error).toContain('setState needs a local variable field');
 const unknown=structuredClone(fixture);unknown.repeat=false;expect(parseProgram(JSON.stringify(unknown)).error).toContain('unknown: repeat');
 expect(parseProgram(JSON.stringify(fixture)).error).toBeNull();
});
