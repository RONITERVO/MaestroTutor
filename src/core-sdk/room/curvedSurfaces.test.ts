// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import cases from '../../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/curved-surfaces-contract.json';
import {capabilityDefinition,validateCapabilityValue} from '../../../shared/capabilities';
import {invocationFeatureRequirements} from '../../../shared/programFeatures';
import {surfaceRenderedPointCount,validSurfaceGeometry} from '../../../shared/surfaceGeometry';
it.each(cases)('shares native curved surface admission: $name',entry=>{
 const schema=capabilityDefinition('object.surface.edit')!.input.oneOf!.find(s=>s.properties!.operation.enum![0]==='configure')!.properties!.definition;
 expect(validateCapabilityValue(entry.definition,schema)===null).toBe(entry.valid);
 expect(validSurfaceGeometry(entry.definition)).toBe(entry.valid);
});
it('requires curved support only for explicit geometry fields and retains planar calls',()=>{
 const args={...capabilityDefinition('object.surface.edit')!.example!};
 expect([...invocationFeatureRequirements('object.surface.edit',args)]).not.toContain('curvedDrawingSurfaces.v1');
 args.definition=cases.find(c=>c.name==='sphere')!.definition;
 expect([...invocationFeatureRequirements('object.surface.edit',args)]).toContain('curvedDrawingSurfaces.v1');
});
it('counts curve subdivision against the same per-stroke geometry budget',()=>{
 const s={shape:'sphere',curvatureRadius:.065,width:.28,height:.12},points=[{x:-.12,y:0},{x:.12,y:0}];
 expect(surfaceRenderedPointCount(s,points,.003)).toBe(44);
 expect(surfaceRenderedPointCount({...s,shape:'plane'},points,.003)).toBe(2);
 expect(surfaceRenderedPointCount(s,Array.from({length:512},(_,i)=>points[i%2]),.003)).toBe(2049);
 expect(surfaceRenderedPointCount({shape:'sphere',curvatureRadius:4,width:4,height:4},[{x:-1.8,y:-1},{x:1.8,y:1}],.001)).toBeGreaterThan(60);
});
