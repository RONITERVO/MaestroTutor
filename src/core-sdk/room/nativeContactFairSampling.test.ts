// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeContactFairSampling.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue} from '../../../shared/behaviourFacts';

it('accepts native fair sampling without inventing contact during a missed interval',()=>{
 expect(native.competitors).toBeGreaterThan(0);
 const first=native.trace[0];const last=native.trace[native.trace.length-1];
 expect(first.contact.known).toBe(false);expect(last.contact.known).toBe(true);expect(last.contact.immersed).toBe(true);
 expect(last.schedule.step).toBeGreaterThan(first.schedule.step);
 expect(first.schedule.queued).toBeGreaterThan(0);
 for(const sample of native.trace){
  expect(validFactValue('runtime.contactWorkBudget',sample.budget)).toBe(true);
  expect(validFactValue('runtime.contactWorkSchedule',sample.schedule)).toBe(true);
  expect(validFactValue('input.medium.contactState',sample.contact)).toBe(true);
  expect(sample.schedule.step).toBe(sample.flowStep);expect(sample.schedule.step).toBe(sample.physicsStep);
  expect(sample.schedule.executions+sample.schedule.queued).toBe(sample.budget.reservations);
  expect(sample.schedule.units).toBe(sample.budget.units);
  expect(sample.schedule.units).toBeLessThanOrEqual(sample.schedule.unitLimit);
  expect(sample.schedule.executions).toBeLessThanOrEqual(sample.schedule.executionLimit);
  expect(sample.budget.reservations).toBeLessThanOrEqual(sample.schedule.queueLimit);
  expect(sample.contact.sampleSpeedMetresPerSecond).toBe(0);expect(sample.events).toBe(0);
 }
 expect(last.contact.bodyId).toHaveLength(32);
});
it('shares passive contact scheduling and honest observation-gap semantics with human and agent',()=>{
 expect(behaviourFact('runtime.contactWorkSchedule')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.contactWorkSchedule')?.description).toContain('Reading cannot advance the clock');
 expect(behaviourFact('input.medium.contactState')?.description).toContain('missed sampling opportunity');
 expect(native.trace[0].schedule).toMatchObject({queueLimit:64,executionLimit:32,unitLimit:524288});
 expect(validFactValue('runtime.contactWorkSchedule',{...native.trace[0].schedule,step:'later'})).toBe(false);
 expect(validFactValue('runtime.contactWorkSchedule',{...native.trace[0].schedule,startSampling:true})).toBe(false);
});
