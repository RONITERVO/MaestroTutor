// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {insertProgramCapability} from './programCapabilityEditing';
import {parseProgram,type BehaviourProgram} from './programs';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {capabilityDefinition} from '../../../shared/capabilities';
const source=JSON.stringify({version:2,entry:'main',resources:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[]}]});
const call={id:'avatar.movement.configure',version:1,arguments:{target:'maestro',revision:1,distance:1.3,speed:.9}};
it('generates the exact native fixture with one read and explicit live preferences',()=>{
 const result=insertProgramCapability(source,call,{kind:'current',fields:['revision','distance']});
 const fixture=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/current-input-program.json','utf8'));expect(result).toEqual(fixture);
 expect(parseProgram(JSON.stringify(result)).error).toBeNull();expect(call.arguments).toEqual({target:'maestro',revision:1,distance:1.3,speed:.9});
 const action=result.functions[0].body[1];expect(action).toMatchObject({op:'invoke',arguments:call.arguments,bindings:{revision:{op:'field',args:[{var:'current_1'},{value:'revision'}]},distance:{op:'field',args:[{var:'current_1'},{value:'distance'}]}}});
 if(action.op!=='invoke')throw Error();expect(action.bindings.speed).toBeUndefined();
});
it('preserves existing code and chooses collision-free block and local names',()=>{
 const base=JSON.parse(source) as BehaviourProgram;base.functions[0].locals=[{name:'current_1',initial:0}];base.functions[0].body=[{id:'action_1',op:'if',test:{value:true},then:[{id:'read_current_1',op:'return'}],else:[]}];
 const before=JSON.stringify(base),result=insertProgramCapability(before,call,{kind:'current',fields:['revision']});expect(JSON.stringify(base)).toBe(before);
 expect(result.functions[0].body.slice(2)).toEqual(base.functions[0].body);expect(result.functions[0].body.map(n=>n.id)).toEqual(['read_current_2','action_2','action_1']);expect(result.functions[0].locals[1].name).toBe('current_2');
 const literal=insertProgramCapability(source,call);expect(literal.version).toBe(2);expect(literal.functions[0].locals).toEqual([]);expect(literal.functions[0].body[0]).toMatchObject({bindings:{}});
});
it('refuses unsupported mappings, omitted guards, duplicate fields and exhausted local budgets',()=>{
 for(const fields of [[],['distance'],['revision','revision'],['revision','target'],['revision','unknown']])expect(()=>insertProgramCapability(source,call,{kind:'current',fields})).toThrow();
 expect(()=>insertProgramCapability(source,{id:'time.wait',version:1,arguments:{seconds:1}},{kind:'current',fields:[]})).toThrow();
 const base=JSON.parse(source);base.functions[0].locals=Array.from({length:16},(_,i)=>({name:'v'+i,initial:0}));expect(()=>insertProgramCapability(JSON.stringify(base),call,{kind:'current',fields:['revision']})).toThrow();
});
it('keeps exact parameterized fact targets and needs the same native structured features',()=>{
 const d=capabilityDefinition('object.physics.configure')!,args={...d.example!,target:'a'.repeat(32)};
 const result=insertProgramCapability(source,{id:d.id,version:1,arguments:args},{kind:'current',fields:['revision','shape','mode']});
 expect(result.resources).toEqual(['a'.repeat(32)]);expect(result.functions[0].body[0]).toMatchObject({op:'set',value:{fact:'object.physics.settings',version:1,arguments:{target:'a'.repeat(32)},bindings:{}}});
 const commands=[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{program:JSON.stringify(result)}}]}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['behaviourPrograms.v3','eventPrograms.v1','objectEdits.v1','actionResults.v1','spatialSettings.v1','structuredValues.v1']})).toThrow('factQueries.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['behaviourPrograms.v3','eventPrograms.v1','objectEdits.v1','actionResults.v1','spatialSettings.v1','structuredValues.v1','factQueries.v1']})).not.toThrow();
});
