// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativePhysicsFlowScheduling.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue} from '../../../shared/behaviourFacts';

it('shares native coupled admission without opposite physics and liquid opportunities',()=>{
 expect(native.forceCompetitors).toBeGreaterThan(0);expect(native.flowCompetitors).toBe(32);
 expect(native.trace[0].forceSchedule.executions).toBe(native.forceCompetitors);
 expect(native.trace[0].forceBudget.reservations).toBe(native.forceCompetitors+4);
 const first=native.trace[0];const last=native.trace[native.trace.length-1];
 expect(first.liquidReady).toBe(false);expect(last.liquidReady).toBe(true);
 expect(last.forceSchedule.step).toBeGreaterThan(first.forceSchedule.step);
 for(const sample of native.trace){
  expect(validFactValue('runtime.physicsWorkBudget',sample.forceBudget)).toBe(true);
  expect(validFactValue('runtime.flowWorkBudget',sample.flowBudget)).toBe(true);
  expect(validFactValue('runtime.physicsWorkSchedule',sample.forceSchedule)).toBe(true);
  expect(validFactValue('runtime.flowWorkSchedule',sample.flowSchedule)).toBe(true);
  expect(sample.forceSchedule.step).toBe(sample.flowSchedule.step);
  for(const budget of [sample.forceSchedule,sample.flowSchedule]){
   expect(budget.executions).toBeLessThanOrEqual(budget.executionLimit);
   expect(budget.units).toBeLessThanOrEqual(budget.unitLimit);
   expect(budget.executions+budget.queued).toBeLessThanOrEqual(budget.queueLimit);
  }
  expect(sample.bodies).toHaveLength(4);
  for(const body of sample.bodies)expect(body.ready).toBe(body.groupReady);
 }
 for(const body of first.bodies){expect(body.simulating).toBe(false);expect(body.motionHeld).toBe(true);}
 for(const body of last.bodies){expect(body.simulating).toBe(true);expect(body.motionHeld).toBe(false);expect(body.speedX).toBeGreaterThan(2);}
});
it('exposes passive bounded physics scheduling through the common capability catalog',()=>{
 const fact=behaviourFact('runtime.physicsWorkSchedule');
 expect(fact?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(fact?.description).toContain('Reading cannot advance the clock');
 expect(native.trace[0].forceSchedule).toMatchObject({executionLimit:128,queueLimit:128,unitLimit:524288});
 expect(validFactValue('runtime.physicsWorkSchedule',{...native.trace[0].forceSchedule,step:'later'})).toBe(false);
 expect(validFactValue('runtime.physicsWorkSchedule',{...native.trace[0].forceSchedule,startPhysics:true})).toBe(false);
});
