// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/importReadback.json';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {validExecutionView} from '../../../shared/roomExecutions';
import {dataValueCost} from '../../../shared/programValues';
it('retains all exact motion IDs in the native outcome and exposes bounded pages in the same order',()=>{
 expect(validExecutionView(native.execution)).toBe(true);expect(validFactValue('model.import.selection',native.summary)).toBe(true);
 const ids=native.execution.selected.output.motionIds;expect(ids).toHaveLength(32);expect(new Set(ids).size).toBe(32);expect(native.summary.accepted.motionCount).toBe(32);expect(native.summary.accepted).not.toHaveProperty('motionIds');
 for(const page of native.pages){expect(validateFactArguments('model.import.motions',1,page.arguments)).toBeNull();expect(validFactValue('model.import.motions',page.value)).toBe(true);expect(dataValueCost(page.value)).toBeLessThanOrEqual(1024);expect(page.value.motionCount).toBe(32);expect(page.value.motionIds).toHaveLength(8);expect(page.value.requestId).toBe(native.summary.requestId);expect(page.value.modelHash).toBe(native.summary.preview.modelHash);}
 expect(native.pages.map(p=>p.value.motionOffset)).toEqual([0,8,16,24]);expect(native.pages.flatMap(p=>p.value.motionIds)).toEqual(ids);
});
it('keeps global budgets and exact request/offset validation instead of truncating identities',()=>{
 const args=native.pages[0].arguments,value=native.pages[0].value;
 for(const invalid of [{...args,motionOffset:-1},{...args,motionOffset:32},{...args,requestId:''},{...args,path:'private.glb'},{requestId:args.requestId}])expect(validateFactArguments('model.import.motions',1,invalid)).not.toBeNull();
 expect(validFactValue('model.import.motions',{...value,motionIds:native.execution.selected.output.motionIds})).toBe(false);
 expect(validFactValue('model.import.selection',{...native.summary,accepted:{...native.summary.accepted,motionIds:[]}})).toBe(false);
 expect(dataValueCost({name:'\u2028'.repeat(100)})).toBeGreaterThan(600);
});
