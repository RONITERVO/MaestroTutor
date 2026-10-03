// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {describe,it,expect} from 'vitest';
import {validateCapabilityArguments,capabilityFeatures} from '../../../shared/capabilities';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/snap-points-contract.json','utf8')) as {name:string;capability:string;arguments:Record<string,unknown>;valid:boolean}[];
describe('shared snap-point contracts',()=>{
 for(const row of cases)it(row.name,()=>{expect(validateCapabilityArguments(row.capability,1,row.arguments)===null).toBe(row.valid);});
 it('requires native snap support and physical connection support only for joining',()=>{
  for(const row of cases.filter(r=>r.valid)){
   const features=capabilityFeatures(row.capability,row.arguments);expect(features).toContain(row.capability==='room.selection.snapSettings'?'constructionSnapping.v1':'snapPoints.v1');
   if('scale' in row.arguments)expect(features).toContain('constructionSnapping.v1');
   if(row.capability==='object.layout.snap')expect(features.includes('physicalConnections.v1')).toBe(row.arguments.mode==='join');
  }
 });
 it('keeps the included brick points editable and physically separated by its simple collision height',()=>{
  const brick=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/brick.json','utf8')).definition;
  const [top,bottom]=brick.snapPoints;expect([top.id,bottom.id]).toEqual(['Top','Bottom']);expect(top.family).toBe(bottom.family);
  expect(top.frame.position.y-bottom.frame.position.y).toBeCloseTo(brick.collision.shapes[0].size.y);
 });
});
