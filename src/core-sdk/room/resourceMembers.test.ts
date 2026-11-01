// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/resourceMembers.json';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';

it('validates the real native unloaded-membership result through the shared catalog',()=>{
 expect(validateFactArguments('resource.members',1,native.arguments)).toBeNull();
 expect(validFactValue('resource.members',native.page)).toBe(true);
 expect(native.page.kind).toBe('environment');
 expect(native.page.members).toHaveLength(1);
 expect(native.page.total).toBe(1);
 expect(native.page.next).toBe('');
 expect(native.page.membership).toMatch(/^[a-f0-9]{32}$/);
});
it('supports all shared membership domains and refuses malformed queries',()=>{
 for(const kind of ['environment','appearance','visibility','region']){
  expect(validateFactArguments('resource.members',1,{kind,id:'a'.repeat(32),cursor:''})).toBeNull();
 }
 for(const args of [{kind:'unknown',id:'a'.repeat(32),cursor:''},{kind:'appearance',id:'not an id',cursor:''},
  {kind:'visibility',id:'a'.repeat(32),cursor:'x'.repeat(37)},{kind:'region',id:'a'.repeat(32),offset:16}]){
  expect(validateFactArguments('resource.members',1,args)).not.toBeNull();
 }
 expect(validateFactArguments('resource.members',2,native.arguments)).not.toBeNull();
});
it('keeps membership reads feature-gated instead of guessing support on older hosts',()=>{
 const command={action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'resource.members',version:1,arguments:native.arguments}};
 expect(()=>requireRoomCapabilities([command],{capabilities:['catalog.v1','catalogVocabulary.v1','factQueries.v1']})).toThrow('resourceMembers.v1');
 expect(()=>requireRoomCapabilities([command],{capabilities:['catalog.v1','catalogVocabulary.v1','factQueries.v1','resourceMembers.v1']})).not.toThrow();
});
it('rejects malformed pages without changing the existing offset-read contracts',()=>{
 expect(validFactValue('resource.members',{...native.page,members:4})).toBe(false);
 expect(validFactValue('resource.members',{...native.page,revision:'1'})).toBe(false);
 expect(validFactValue('resource.members',{...native.page,next:false})).toBe(false);
 for(const id of ['appearance.members','visibility.members','world.region.members'])
  expect(validateFactArguments(id,1,{id:'a'.repeat(32),offset:16})).toBeNull();
});
