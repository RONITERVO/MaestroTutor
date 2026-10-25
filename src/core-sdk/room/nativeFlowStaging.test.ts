// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeFlowStaging.json';
import {validFactValue} from '../../../shared/behaviourFacts';
import {behaviourFact} from '../../../shared/behaviourCatalog';

it('keeps actual native input preparation distinct from execution in shared facts',()=>{
 // This capture uses one real cold native room and an explicit logical
 // execution-pressure claim; it is not a native multi-world capacity result.
 for(const sample of Object.values(native)){
  expect(validFactValue('runtime.flowDiscoveryBudget',sample.discovery)).toBe(true);
  expect(validFactValue('runtime.flowWorkSchedule',sample.execution)).toBe(true);
  expect(validFactValue('object.container.live',sample.source)).toBe(true);
  expect(validFactValue('object.container.live',sample.receiver)).toBe(true);
  expect(sample.captures).toBe(1);
 }
 expect(native.loading.pending).toBe(true);
 expect(native.loading.discovery.executions).toBe(1);
 expect(native.loading.execution).toMatchObject({executions:0,queued:1,units:0});
 expect(native.preparedWaiting.pending).toBe(false);
 expect(native.preparedWaiting.execution).toMatchObject({executions:0,queued:1,units:0});
 expect(native.preparedWaiting.source.phase).toBe('waiting');
 expect(native.preparedWaiting.source.contents.amountMl).toBe(400);
 expect(native.preparedWaiting.receiver.contents.amountMl).toBe(0);
});

it('reports later execution without backfilling the interval spent preparing or waiting',()=>{
 expect(native.flowing.execution).toMatchObject({executions:1,queued:0,units:9296});
 expect(native.flowing.source.phase).toBe('flowing');
 expect(native.flowing.receiver.contents.amountMl).toBeGreaterThan(0);
 expect(native.flowing.receiver.contents.amountMl).toBeLessThanOrEqual(18.75);
 expect(native.flowing.source.contents.amountMl+native.flowing.receiver.contents.amountMl).toBeCloseTo(400,8);
 const description=behaviourFact('runtime.flowDiscoveryBudget')?.description;
 expect(description).toContain('independently admitted');
 expect(description).toContain('Native inputs may load while liquid execution waits');
 expect(description).toContain('does not refund already booked execution');
});
