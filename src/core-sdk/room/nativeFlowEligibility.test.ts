// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeFlowEligibility.json';
import {validFactValue} from '../../../shared/behaviourFacts';

it('keeps initially full native vessels waiting for rain cover before they drain',()=>{
 const r=native.rain;
 for(const value of [r.waiting,r.flowing,r.accepted])expect(validFactValue('object.container.live',value)).toBe(true);
 for(const value of [r.waitingRain,r.flowingRain])expect(validFactValue('object.container.rain',value)).toBe(true);
 expect(r.waiting).toMatchObject({phase:'waiting',contents:{amountMl:500,savedAmountMl:500,transferredMl:0,spilledMl:0}});
 expect(r.waiting.error).not.toBe('');
 expect(r.waitingRain).toMatchObject({phase:'waiting',ready:false,exposure:0,collectedMl:0});
 expect(r.flowing.contents.amountMl).toBeLessThan(500);
 expect(r.flowing.contents.amountMl+r.flowing.contents.spilledMl).toBeCloseTo(500,8);
 expect(r.flowingRain).toMatchObject({phase:'flowing',ready:false,exposure:0,collectedMl:0});
 expect(r.flowingRain.reason).toBe('Opening is covered');
 expect(r.accepted).toMatchObject({phase:'idle',contents:{amountMl:r.flowing.contents.amountMl,savedAmountMl:r.flowing.contents.amountMl}});
});
it('conserves a native same-pass cascade while newly possible stream inputs load',()=>{
 const c=native.cascade;
 for(const value of Object.values(c))expect(validFactValue('object.container.live',value)).toBe(true);
 expect(c.waitingSource).toMatchObject({phase:'waiting',contents:{amountMl:400,savedAmountMl:400,transferredMl:0,spilledMl:0}});
 expect(c.waitingReceiver).toMatchObject({phase:'waiting',contents:{amountMl:0,savedAmountMl:0,transferredMl:0,spilledMl:0}});
 const from=c.flowingSource.contents,to=c.flowingReceiver.contents;
 expect(from.transferredMl).toBeGreaterThan(0);expect(from.spilledMl).toBe(0);
 expect(to.amountMl).toBeGreaterThan(0);expect(to.spilledMl).toBeGreaterThan(0);
 expect(to.amountMl+to.spilledMl).toBeCloseTo(from.transferredMl,8);
 expect(from.amountMl+to.amountMl+to.spilledMl).toBeCloseTo(400,8);
 expect(c.acceptedSource).toMatchObject({phase:'idle',contents:{amountMl:from.amountMl,savedAmountMl:from.amountMl}});
 expect(c.acceptedReceiver).toMatchObject({phase:'idle',contents:{amountMl:to.amountMl,savedAmountMl:to.amountMl}});
});
