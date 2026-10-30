// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {behaviourFact} from '../../../shared/behaviourCatalog';
const id='world.presentation.set',stateId='a'.repeat(32);
it.each([0,.25,.5,.75,1])('accepts bounded backdrop opacity %s with an independent depth preference',backdropOpacity=>{
 for(const realDepth of [true,false])expect(validateCapabilityArguments(id,1,{stateId,backdropOpacity,realDepth})).toBeNull();
});
it.each([-1,1.001,NaN,Infinity,'0.5',null])('refuses invalid opacity %s before reaching native rendering',backdropOpacity=>{
 expect(validateCapabilityArguments(id,1,{stateId,backdropOpacity,realDepth:true})).not.toBeNull();
});
it('requires the shared current view identity and exposes explicit depth eligibility',()=>{
 const input=capabilityDefinition(id)!.input;
 expect(input['x-features']).toContain('worldPresentation.v1');
 expect(input['x-current']?.fact).toBe('world.presentation');
 expect(validateCapabilityArguments(id,1,{backdropOpacity:.5,realDepth:true})).not.toBeNull();
 expect(validateCapabilityArguments(id,1,{stateId,backdropOpacity:.5,realDepth:true,realCollisions:false})).not.toBeNull();
 expect(behaviourFact('world.presentation')).toBeTruthy();
 expect(validateCapabilityOutput(id,1,{stateId,backdropOpacity:1,realDepth:true,depthEligible:false,virtualView:true,headTracked:true,focused:true})).toBeNull();
});
