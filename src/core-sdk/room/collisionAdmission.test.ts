// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/collisionAdmission.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts real Unity collision admission totals while a destroyed collider still owns capacity',()=>{
 expect(validFactValue('runtime.collisionBudget',native.budget)).toBe(true);
 expect(native.budget).toEqual({entries:129,colliders:2048,meshColliders:0,sourceTriangles:0,entryLimit:256,colliderLimit:2048,triangleLimit:1048576});
 expect(validFactValue('runtime.collisionResource',native.retiring)).toBe(true);
 expect(native.retiring).toMatchObject({target:'last-slot',role:'collision',kind:'compound',phase:'retiring',colliders:1,activeColliders:0});
 expect(native.retiring.worldId).toHaveLength(32);expect(native.retiring.regionId).toHaveLength(32);
 expect(native.retiring.leaseId).toHaveLength(32);
});
it('exposes independent limits through shared passive facts with explicit measurement exclusions',()=>{
 const fact=behaviourFact('runtime.collisionBudget');
 expect(fact?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(fact?.description).toContain('not measured PhysX memory or frame time');
 expect(fact?.description).toContain('Default item/book/avatar proxies, scans, navigation');
 expect(fact?.description).toContain('Saved quotas are independent');
 expect(fact?.description).toContain('No read starts physics');
 for(const id of ['runtime.collisionResources','runtime.collisionResource'])expect(behaviourFact(id)?.description).toContain('runtime.collisionBudget');
});
it('rejects malformed usage and unknown telemetry instead of accepting extra runtime state',()=>{
 expect(validFactValue('runtime.collisionBudget',{...native.budget,privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.collisionBudget',{...native.budget,colliderLimit:'unknown'})).toBe(false);
 expect(validFactValue('runtime.collisionBudget',{...native.budget,sourceTriangles:NaN})).toBe(false);
 const {entryLimit:omitted,...incomplete}=native.budget;expect(omitted).toBe(256);
 expect(validFactValue('runtime.collisionBudget',incomplete)).toBe(false);
});
it('requires runtime diagnostics when a user or agent stores a program reading collision limits',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'budget',initial:native.budget}],body:[{id:'read',op:'set',variable:'budget',value:{fact:'runtime.collisionBudget'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
});
