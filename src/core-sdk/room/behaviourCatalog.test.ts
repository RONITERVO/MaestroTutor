// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { expect, it } from 'vitest';
import { behaviourCatalog,behaviourFact } from '../../../shared/behaviourCatalog';
import { ruleActions, ruleEvents } from '../../../shared/prompts/rules';
import {defaultDataValue} from '../../../shared/programValues';
import { parseProgram } from './programs';

it('keeps existing book wire positions stable while adding named identities', () => {
  expect(behaviourCatalog.adapters.ruleStep.actionIds).toHaveLength(18);
  expect(behaviourCatalog.adapters.ruleStep.eventIds).toHaveLength(7);
  expect(ruleActions[2]).toBe('Wait'); expect(ruleEvents[4]).toBe('Item tapped');
  expect(behaviourCatalog.adapters.ruleStep.actionIds.indexOf('animation.library.play')).toBe(7);
  expect(behaviourCatalog.actions.every(action=>!('wireValue' in action))).toBe(true);
  expect(behaviourCatalog.actions.find(action=>action.id==='animation.play')).toMatchObject({duration:'timed',channels:['wholeTarget','upperBody','recipePart']});
  const ids=[...behaviourCatalog.actions,...behaviourCatalog.events,...behaviourCatalog.facts].map(entry=>entry.id);
  expect(new Set(ids).size).toBe(ids.length);
});

it.each(behaviourCatalog.facts)('validates $id using the exported native type', fact => {
  const definition=behaviourFact(fact.id)!,value=defaultDataValue(definition.type);
  const program={version:3,dataVersion:1,state:[],events:[],entry:'main',resources:[],functions:[{name:'main',returns:'void',parameters:[],
    locals:[{name:'result',initial:value,type:definition.type}],body:[{id:'read',op:'set',variable:'result',value:{fact:fact.id,...(definition.input?{version:definition.version,arguments:definition.example,bindings:{}}:{})}}]}]};
  expect(parseProgram(JSON.stringify(program)).error).toBeNull();
  program.functions[0].locals[0].initial=fact.type==='boolean'?'incorrect':true;
  expect(parseProgram(JSON.stringify(program)).program).toBeNull();
});
