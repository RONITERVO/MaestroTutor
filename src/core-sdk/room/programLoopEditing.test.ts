// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {loopProgram,sequenceProgramEditError} from './programLoopEditing';
import {insertProgramCapability} from './programCapabilityEditing';
import {parseProgram,type BehaviourProgram} from './programs';
const action={id:'wave',op:'invoke',capability:'animation.play',version:1,arguments:{target:'maestro',source:{kind:'gesture',gesture:'greeting'},channel:'upperBody',seconds:1},bindings:{seconds:{var:'count'}}};
export const cycleSource=JSON.stringify({version:2,entry:'main',resources:['maestro'],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'count',initial:0}],body:[
 {id:'count_cycle',op:'set',variable:'count',value:{op:'add',args:[{var:'count'},{value:1}]}},action,{id:'finish_cycle',op:'return'},{...action,id:'never_reached'},
]}]});
it('generates the exact native loop fixture while preserving cycle locals and returns',()=>{
 expect(parseProgram(cycleSource).error).toBeNull();const converted=loopProgram(cycleSource,.5);
 expect(converted).toEqual(JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-repeat-conversion.json','utf8')));
 expect(converted.functions[0]).toEqual(JSON.parse(cycleSource).functions[0]);expect(converted.entry).toBe('repeat_entry_1');
 expect(converted.functions[1].body).toEqual([{id:'repeat_loop_1',op:'forever',body:[{id:'repeat_cycle_1',op:'call',function:'main',args:[]},{id:'repeat_delay_1',op:'sleep',seconds:{value:.5}}]}]);
 expect(sequenceProgramEditError(true,JSON.stringify(converted))).toContain('Convert');expect(sequenceProgramEditError(false,JSON.stringify(converted))).toBeNull();expect(sequenceProgramEditError(true,cycleSource)).toBeNull();
});
it('avoids symbol collisions and refuses invalid delays, oversized programs and repeated conversion',()=>{
 const source=JSON.parse(cycleSource) as BehaviourProgram;source.functions.push({name:'repeat_entry_1',returns:'void',parameters:[],locals:[],body:[{id:'repeat_loop_1',op:'return'}]});
 const converted=loopProgram(JSON.stringify(source),1);expect(converted.entry).toBe('repeat_entry_2');expect(converted.functions[2].body[0].id).toBe('repeat_loop_2');
 for(const delay of [0,.09,3601,NaN,Infinity])expect(()=>loopProgram(cycleSource,delay)).toThrow('delay');
 expect(()=>loopProgram(JSON.stringify(converted),1)).toThrow('already supports loops');
 while(source.functions.length<16)source.functions.push({name:'extra_'+source.functions.length,returns:'void',parameters:[],locals:[],body:[]});
 const full=JSON.stringify(source);expect(parseProgram(full).error).toBeNull();expect(()=>loopProgram(full,1)).toThrow();expect(JSON.stringify(source)).toBe(full);
});
it('places fresh reads in the chosen cycle function without changing the loop or unrelated functions',()=>{
 const converted=loopProgram(cycleSource,.5),source=JSON.stringify(converted);
 const call={id:'avatar.movement.configure',version:1,arguments:{target:'maestro',revision:1,distance:1.3,speed:.9}};
 const result=insertProgramCapability(source,call,{kind:'current',fields:['revision','distance']},'main');
 expect(result.functions[1]).toEqual(converted.functions[1]);expect(result.functions[0].body.slice(2)).toEqual(converted.functions[0].body);
 expect(result.functions[0].body.slice(0,2).map(node=>node.op)).toEqual(['set','invoke']);expect(result.functions[0].locals).toHaveLength(2);
 expect(()=>insertProgramCapability(source,call,{kind:'snapshot'},'missing')).toThrow('existing function');expect(JSON.stringify(converted)).toBe(source);
});

it('retains value-returning cycle functions and enforces the additional call-depth cost',()=>{
 const source=JSON.parse(cycleSource) as BehaviourProgram;source.functions[0].returns='number';source.functions[0].body=[{id:'returned_value',op:'return',value:{value:7}}];
 const converted=loopProgram(JSON.stringify(source),1);expect(converted.functions[0]).toEqual(source.functions[0]);expect(parseProgram(JSON.stringify(converted)).error).toBeNull();
 const chain:BehaviourProgram={version:2,entry:'f0',resources:[],functions:Array.from({length:8},(_,i)=>({name:'f'+i,returns:'void',parameters:[],locals:[],body:i===7?[]:[{id:'call'+i,op:'call',function:'f'+(i+1),args:[]}]}))};
 const full=JSON.stringify(chain);expect(parseProgram(full).error).toBeNull();expect(()=>loopProgram(full,1)).toThrow('call depth');expect(JSON.stringify(chain)).toBe(full);
});
