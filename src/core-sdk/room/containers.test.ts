// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {describe,it,expect} from 'vitest';
import {validateCapabilityArguments,capabilityFeatures} from '../../../shared/capabilities';
import {behaviourEvent} from '../../../shared/behaviourEvents';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseProgram,type BehaviourProgram} from './programs';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/containers-contract.json','utf8')) as {name:string;capability:string;arguments:Record<string,unknown>;valid:boolean}[];
describe('shared liquid container contracts',()=>{
 for(const row of cases)it(row.name,()=>{expect(validateCapabilityArguments(row.capability,1,row.arguments)===null).toBe(row.valid);});
 it('requires native container support for both editing and measured transfer',()=>{for(const row of cases.filter(r=>r.valid))expect(capabilityFeatures(row.capability,row.arguments)).toContain('containers.v1');});
});

it('uses the same native pouring-event program and rejects unsupported runtimes or wrong field types',()=>{
 const program=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-container-pour.json','utf8')) as BehaviourProgram;
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect(behaviourEvent('object.container.poured')?.features).toContain('containerPouring.v1');
 const commands=[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{program:JSON.stringify(program)}}]}}];
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','eventFields.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('containerPouring.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'containerPouring.v1']})).not.toThrow();
 program.functions[0].locals.find(v=>v.name==='ml')!.initial='wrong type';
 expect(parseProgram(JSON.stringify(program)).program).toBeNull();
});
