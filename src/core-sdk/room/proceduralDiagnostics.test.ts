// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/proceduralReservations.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';

it('accepts actual Unity procedural reservations through preparation, use and deferred destruction',()=>{
 expect(validFactValue('runtime.proceduralBudget',native.budget)).toBe(true);
 expect(native.budget).toMatchObject({instances:1,parts:1,customVertices:300,instanceLimit:128,partLimit:512,customVertexLimit:524288});
 for(const value of [native.preparing,native.ready,native.active,native.retiring])expect(validFactValue('runtime.proceduralReservation',value)).toBe(true);
 expect(native.ready).toEqual({...native.preparing,phase:'ready'});
 expect(native.active).toEqual({...native.ready,active:true});
 expect(native.retiring).toEqual({...native.ready,phase:'retiring'});
 expect(native.ready.worldId).toHaveLength(32);expect(native.ready.regionId).toHaveLength(32);
 expect(native.ready.reservationId).toHaveLength(32);expect(native.ready.target).toBe('resource-probe');
});
it('keeps passive native resource facts available to the shared catalog with bounded arguments',()=>{
 expect(behaviourFact('runtime.proceduralBudget')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.proceduralReservation')?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 for(const index of [0,127])expect(validateFactArguments('runtime.proceduralReservation',1,{index})).toBeNull();
 for(const args of [{index:-1},{index:128},{index:.5},{index:'0'},{index:0,evict:true}])expect(validateFactArguments('runtime.proceduralReservation',1,args)).not.toBeNull();
 expect(behaviourFact('runtime.proceduralBudget')?.description).toContain('not measured RAM/VRAM');
 expect(behaviourFact('runtime.proceduralReservation')?.description).toContain('cannot activate, unload or delete');
});
it('rejects unsupported telemetry instead of accepting an unbounded resource inventory',()=>{
 expect(validFactValue('runtime.proceduralBudget',{...native.budget,customVertices:'unknown'})).toBe(false);
 expect(validFactValue('runtime.proceduralReservation',{...native.ready,privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.proceduralReservation',{...native.ready,active:'yes'})).toBe(false);
});
