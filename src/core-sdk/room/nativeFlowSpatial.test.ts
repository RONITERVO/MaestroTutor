// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeFlowSpatial.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts native spatial waiting in the shared pour, scoop and rain contracts',()=>{
 for(const value of [native.waiting,native.flowing,native.receiver,native.accepted])expect(validFactValue('object.container.live',value)).toBe(true);
 expect(validFactValue('object.container.scooping',native.scooping)).toBe(true);
 expect(validFactValue('object.container.rain',native.rain)).toBe(true);
 expect(native.waiting).toMatchObject({phase:'waiting',contents:{amountMl:400,savedAmountMl:400,transferredMl:0,spilledMl:0}});
 expect(native.waiting.error).not.toBe('');
 expect(native.scooping).toMatchObject({phase:'waiting',scoopedMl:0,drawnMl:0});
 expect(native.rain).toMatchObject({phase:'waiting',ready:false,exposure:0,collectedMl:0});
 expect(native.rain.reason).not.toBe('');
});
it('conserves native quantities through waiting, fresh recovery and accepted publication',()=>{
 expect(native.flowing.phase).toBe('flowing');
 expect(native.receiver.contents.amountMl).toBeGreaterThan(0);
 expect(native.receiver.contents.amountMl).toBeLessThanOrEqual(18.75);
 expect(native.flowing.contents.amountMl+native.receiver.contents.amountMl).toBeCloseTo(400,8);
 expect(native.flowing.contents.spilledMl).toBe(0);
 expect(native.flowing.contents.transferredMl).toBe(native.receiver.contents.amountMl);
 expect(native.receiver.contents.savedAmountMl).toBe(0);
 expect(native.accepted).toMatchObject({phase:'idle',error:''});
 expect(native.accepted.contents.savedAmountMl).toBe(native.receiver.contents.amountMl);
 expect(native.accepted.contents.amountMl).toBe(native.receiver.contents.amountMl);
});
it('lets human and agent programs inspect the same typed waiting result',()=>{
 const definition=behaviourFact('object.container.live')!;
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'flow',initial:native.waiting,type:definition.type}],body:[{id:'read',op:'set',variable:'flow',value:{fact:definition.id,version:1,arguments:{target:'0'.repeat(32)},bindings:{}}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect([...programFeatureRequirements(program)]).toEqual(expect.arrayContaining(['containerPouring.v1','factQueries.v1']));
 const wrong=structuredClone(program);wrong.functions[0].locals[0].initial={...native.waiting,phase:false};
 expect(parseProgram(JSON.stringify(wrong)).error).not.toBeNull();
 expect(validFactValue('object.container.live',{...native.waiting,phase:false})).toBe(false);
});
