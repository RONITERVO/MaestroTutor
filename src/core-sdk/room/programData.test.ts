// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {parseProgram} from './programs';
import {MAX_PROGRAM_NUMBER,validProgramNumber,checkedDataValue,readDataType,sameDataType,validDataObservation,dataValueCost} from '../../../shared/programValues';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {validRuleView} from './rules';
const fixture=(name:string)=>readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/'+name+'.json','utf8');
const contracts=JSON.parse(fixture('program-data-contract')) as {cases:{name:string;source:string;valid:boolean}[]};
it.each(contracts.cases)('$name',c=>expect(parseProgram(c.source).program!==null).toBe(c.valid));
it('preserves typed state and functions while requiring explicit native support',()=>{
 const source=fixture('program-collections');expect(parseProgram(source).error).toBeNull();
 const commands=[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{program:source}}]}}];
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','objectEdits.v1','actionResults.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('structuredValues.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'structuredValues.v1']})).not.toThrow();
});
it('compares structural types independent of field order and rejects deep values',()=>{
 expect(sameDataType({record:{x:'number',y:{list:'text'}}},{record:{y:{list:'text'},x:'number'}})).toBe(true);
 expect(checkedDataValue({x:1,y:[]},{record:{y:{list:'text'},x:'number'}})).toEqual({record:{x:'number',y:{list:'text'}}});
 let type:unknown='number',value:unknown=1;for(let i=0;i<5;i++){type={list:type};value=[value];}
 expect(()=>readDataType(type)).toThrow('nesting');expect(()=>checkedDataValue(value)).toThrow('nesting');
});
it('accepts exact native structured observations when supplied',()=>{
 const path=process.env.MAESTRO_DATA_EVIDENCE;if(!path)return;
 const evidence=JSON.parse(readFileSync(path+'/collections.json','utf8'));
 expect(validRuleView(evidence.rules)).toBe(true);const run=evidence.rules.running[0];
 const items=JSON.parse(run.state.find((v:{name:string})=>v.name==='items').value);
 const copied=JSON.parse(run.locals.find((v:{name:string})=>v.name==='copy').value);
 expect(items).toHaveLength(2);expect(items[0].red).toBe(.2);expect(copied[0].red).toBe(.9);
 for(const o of evidence.objects){expect(o.color.r).toBeCloseTo(.2,5);expect(o.color.g).toBeCloseTo(.4,5);}
});

it('validates observed compound values and keeps full numeric lists within the shared budget',()=>{
 const full=Array.from({length:32},(_,i)=>i);expect(checkedDataValue(full)).toEqual({list:'number'});expect(dataValueCost(full)).toBe(993);
 expect(validDataObservation('[]','list')).toBe(true);expect(validDataObservation('{"empty":[]}','record')).toBe(true);
 expect(validDataObservation('not json','list')).toBe(false);expect(validDataObservation('{}','list')).toBe(false);
 expect(validDataObservation(JSON.stringify(Array.from({length:33},()=>0)),'list')).toBe(false);
 expect(validDataObservation('{"bad":1e999}','record')).toBe(false);
});

it('preserves large exact revision values through literals, records and observations',()=>{
 for(const value of [1000001,2147483647,MAX_PROGRAM_NUMBER,-MAX_PROGRAM_NUMBER]){
  expect(validProgramNumber(value)).toBe(true);expect(checkedDataValue(value)).toBe('number');
  expect(checkedDataValue({revision:value})).toEqual({record:{revision:'number'}});
  expect(validDataObservation(JSON.stringify({revision:value}),'record')).toBe(true);
 }
 for(const value of [MAX_PROGRAM_NUMBER+1,-MAX_PROGRAM_NUMBER-1,Infinity,NaN])expect(()=>checkedDataValue(value)).toThrow();
});

it('uses the generated record field ceiling without relaxing value budgets',()=>{
 const value=Object.fromEntries(Array.from({length:16},(_,i)=>['f'+i,i]));
 expect(()=>checkedDataValue(value)).not.toThrow();expect(validDataObservation(JSON.stringify(value),'record')).toBe(true);
 expect(()=>checkedDataValue({...value,overflow:1})).toThrow('16 fields');expect(validDataObservation(JSON.stringify({...value,overflow:1}),'record')).toBe(false);
 expect(()=>checkedDataValue(Object.fromEntries(Object.keys(value).map(k=>[k,'x'.repeat(128)])))).toThrow('1024');
});
