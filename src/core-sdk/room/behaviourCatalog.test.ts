// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { expect, it } from 'vitest';
import { behaviourCatalog } from '../../../shared/behaviourCatalog';
import { ruleActions, ruleEvents } from '../../../shared/prompts/rules';
import { parseProgram } from './programs';

it('keeps existing book wire positions stable while adding named identities', () => {
  expect(behaviourCatalog.adapters.ruleStep.actionIds).toHaveLength(12);
  expect(behaviourCatalog.adapters.ruleStep.eventIds).toHaveLength(7);
  expect(ruleActions[2]).toBe('Wait'); expect(ruleEvents[4]).toBe('Item tapped');
  expect(behaviourCatalog.adapters.ruleStep.actionIds.indexOf('animation.library.play')).toBe(7);
  expect(behaviourCatalog.actions.every(action=>!('wireValue' in action))).toBe(true);
  expect(behaviourCatalog.actions.find(action=>action.id==='avatar.gesture.play')).toMatchObject({duration:'timed',channels:['wholeTarget']});
  const ids=[...behaviourCatalog.actions,...behaviourCatalog.events,...behaviourCatalog.facts].map(entry=>entry.id);
  expect(new Set(ids).size).toBe(ids.length);
});

it.each(behaviourCatalog.facts)('validates $id using the exported native type', fact => {
  const value=fact.type==='boolean'?false:fact.type==='number'?0:'';
  const program={version:2,entry:'main',resources:[],functions:[{name:'main',returns:'void',parameters:[],
    locals:[{name:'result',initial:value}],body:[{id:'read',op:'set',variable:'result',value:{fact:fact.id}}]}]};
  expect(parseProgram(JSON.stringify(program)).error).toBeNull();
  program.functions[0].locals[0].initial=fact.type==='boolean'?'incorrect':true;
  expect(parseProgram(JSON.stringify(program)).program).toBeNull();
});
