// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeFlowFairScheduling.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue} from '../../../shared/behaviourFacts';

it('accepts the real native waiting-to-flow transition under continuing competing demand',()=>{
 expect(native.incumbents).toBe(8);expect(native.vesselsPerGroup).toBe(16);
 expect(native.trace.length).toBeLessThanOrEqual(32);
 expect(native.trace[0].contents.phase).toBe('waiting');
 expect(native.trace[0].contents.contents.amountMl).toBe(400);
 const last=native.trace[native.trace.length-1];
 expect(last.contents.phase).toBe('flowing');expect(last.contents.contents.amountMl).toBeLessThan(400);
 expect(last.schedule.step).toBeGreaterThan(native.trace[0].schedule.step);
 for(const sample of native.trace){
  expect(validFactValue('runtime.flowWorkBudget',sample.budget)).toBe(true);
  expect(validFactValue('runtime.flowWorkSchedule',sample.schedule)).toBe(true);
  expect(sample.schedule.executions).toBeLessThanOrEqual(sample.schedule.executionLimit);
  expect(sample.schedule.units).toBeLessThanOrEqual(sample.schedule.unitLimit);
  expect(sample.budget.reservations).toBe(native.incumbents+1);
  expect(sample.schedule.executions+sample.schedule.queued).toBe(sample.budget.reservations);
 }
});
it('shares bounded scheduling evidence without changing the saved world model',()=>{
 const fact=behaviourFact('runtime.flowWorkSchedule');
 expect(fact?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(fact?.description).toContain('Reading cannot advance the clock');
 const first=native.trace[0].schedule;
 expect(first).toMatchObject({executionLimit:32,queueLimit:64,unitLimit:524288});
 expect(validFactValue('runtime.flowWorkSchedule',{...first,step:'later'})).toBe(false);
 expect(validFactValue('runtime.flowWorkSchedule',{...first,startPhysics:true})).toBe(false);
});
