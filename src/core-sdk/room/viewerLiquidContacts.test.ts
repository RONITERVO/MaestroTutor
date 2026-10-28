// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeViewerWaterContact.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {behaviourEvent} from '../../../shared/behaviourEvents';
import {validFactValue} from '../../../shared/behaviourFacts';
import {validateCapabilityValue} from '../../../shared/capabilities';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('shares actual native dry, wet and unavailable viewer observations',()=>{
 for(const state of [native.dry,native.wet,native.unavailable])
  expect(validFactValue('viewer.medium.contactState',state)).toBe(true);
 expect(native.dry).toMatchObject({known:true,immersed:false,kind:'viewer',bodyId:''});
 expect(native.wet).toMatchObject({known:true,immersed:true,kind:'viewer',liquid:'Water'});
 expect(native.wet.depthMetres).toBeCloseTo(.2,3);
 expect(native.unavailable).toMatchObject({known:false,immersed:false,bodyId:''});
 expect(native.unavailable.reason).not.toBe('');
 expect(native.workBudget.inputSamples).toBe(3);
 expect(native.workBudget.units).toBeLessThanOrEqual(native.workBudget.unitLimit);
 expect(validFactValue('viewer.medium.contactState',{...native.wet,privateCameraFrame:'not part of the fact'})).toBe(false);
});

it('carries viewer immersion through the existing water-contact event',()=>{
 const event=behaviourEvent('object.medium.contact')!;
 expect(native.events).toHaveLength(1);
 expect(native.events[0]).toMatchObject({participantId:'input:viewer',kind:'viewer',phase:'entered'});
 expect(validateCapabilityValue(native.events[0],event.fields!)).toBeNull();
 expect(validateCapabilityValue({...native.events[0],participantId:'some camera'},event.fields!)).not.toBeNull();
 expect(event.features).toContain('viewerLiquidContacts.v1');
});

it('uses the same editable program vocabulary and advertises native support',()=>{
 const program=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-liquid-contact.json','utf8')) as BehaviourProgram;
 program.dataVersion=1;
 program.functions[0].locals.push({name:'immersion',initial:native.dry});
 program.functions[0].body.unshift({id:'observeViewer',op:'set',variable:'immersion',value:{fact:'viewer.medium.contactState'}});
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect([...programFeatureRequirements(program)]).toEqual(expect.arrayContaining(['viewerLiquidContacts.v1','liquidContacts.v1']));
 expect(behaviourFact('viewer.medium.contactState')?.description).toContain('global real-room policy');
});
