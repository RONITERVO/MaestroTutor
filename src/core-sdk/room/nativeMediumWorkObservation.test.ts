// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeMediumWorkObservation.json';
import {validFactValue} from '../../../shared/behaviourFacts';

it('reads real native medium facts through waiting, refusal, resumed physics and pause',()=>{
 for(const observed of Object.values(native))expect(validFactValue('object.medium',observed)).toBe(true);
 expect(native.waiting.applied).toBe(false);expect(native.waiting.displacementM3).toBe(0);
 expect(native.waiting.reason).toContain('waiting for work capacity');
 expect(native.refused.applied).toBe(false);expect(native.refused.reason).toContain('exceeds work capacity');
 expect(native.resumed.applied).toBe(true);expect(native.resumed.displacementM3).toBeGreaterThan(0);
 expect(native.resumed.buoyancyNewtons.y).toBeGreaterThan(0);
 expect(native.paused.applied).toBe(false);expect(native.paused.buoyancyNewtons).toEqual({x:0,y:0,z:0});
});
