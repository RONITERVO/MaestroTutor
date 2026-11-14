// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,it,expect} from 'vitest';
import {validCreationPrototypeGeometry} from '../../../shared/creationPrototype';
import {validStaticSupportSource} from '../../../shared/staticSupport';
import {validCreationBatchGeometry} from '../../../shared/creationBatch';
import {capabilityDefinition,validateCapabilityValue,type CapabilitySchema} from '../../../shared/capabilities';
import {validPrototypeResources} from '../../../shared/creationResources';

// These are semantic checks after catalog structural validation. Native source
// ownership/readiness and collision mesh budgets are independently enforced.
const prototype=()=>({version:11,geometry:{kind:'block'},physics:{mode:'fixed',shape:'automatic'},staticSupport:{version:1,enabled:true},surfaces:[],drawingTips:[]});
const valid=(patch:Record<string,unknown>={})=>validCreationPrototypeGeometry({...prototype(),...patch});
const collision=(...shapes:string[])=>({version:1,shapes:shapes.map((shape,i)=>({id:`Shape${i}`,shape,position:{x:0,y:0,z:0},size:{x:.1,y:.1,z:.1},rotation:{x:0,y:0,z:0,w:1},segments:['cylinder','ring'].includes(shape)?8:0,innerRadius:shape==='ring'?.2:0}))});
const modelGeometry={version:1,scaleMode:'fitted',metresPerUnit:1,pivot:'center',meshCollision:false,walkable:false};
describe('static support portable semantic parity',()=>{
 it('uses the reusable schema format for inline source compatibility before dispatch',()=>{
  const branch=capabilityDefinition('object.create')!.input.oneOf!.find(v=>v.properties?.kind.enum?.includes('recipe'))!;
  const schema=structuredClone(branch) as CapabilitySchema;schema.format='staticSupportSource';schema.properties!.staticSupport={type:'object',properties:{version:{type:'integer',minimum:1,maximum:1},enabled:{type:'boolean'}},required:['version','enabled'],additionalProperties:false};
  const args={...(branch.examples![0] as Record<string,unknown>),staticSupport:{version:1,enabled:true}};
  expect(validateCapabilityValue(args,schema)).toBeNull();
  expect(validateCapabilityValue({...args,physics:{mode:'solid',shape:'automatic',mass:1}},schema)).not.toBeNull();
  expect(validateCapabilityValue({...args,physics:{mode:'fixed',shape:'sphere',mass:1}},schema)).not.toBeNull();
  expect(validateCapabilityValue({...args,collision:collision('box','sphere')},schema)).not.toBeNull();
  expect(validateCapabilityValue({...args,collision:collision('box','ring')},schema)).toBeNull();
  expect(validateCapabilityValue({...args,staticSupport:{version:1,enabled:true,guess:true}},schema)).not.toBeNull();
 });
 it('preserves explicit support through batch semantic validation while omitted sources stay off',()=>{
  const source={kind:'recipe',recipe:{playing:false},staticSupport:{version:1,enabled:true},physics:{mode:'fixed',shape:'automatic'},collision:collision('box')};
  const batch={blueprint:{version:1,pieces:[{slot:'First',name:'Platform',source,position:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1},scale:1}]},position:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1},scale:1};
  expect(validCreationBatchGeometry(batch)).toBe(true);source.physics.mode='solid';expect(validCreationBatchGeometry(batch)).toBe(false);source.staticSupport.enabled=false;expect(validCreationBatchGeometry(batch)).toBe(true);
  expect(validStaticSupportSource({})).toBe(true);expect(validStaticSupportSource({staticSupport:{version:2,enabled:false}})).toBe(false);
 });

 it('keeps legacy prototypes default-off and requires version11 only for enabled support',()=>{
  expect(valid()).toBe(true);
  for(const version of [1,2,4,5,6,7,8,9,10]){
   expect(valid({version,staticSupport:undefined})).toBe(true);
   expect(valid({version,staticSupport:{version:1,enabled:false}})).toBe(true);
   expect(valid({version})).toBe(false);
  }
  for(const staticSupport of [{version:0,enabled:false},{version:2,enabled:true},{version:1,enabled:'true'}])expect(valid({staticSupport})).toBe(false);
 });
 it('requires fixed physics and rejects recorded root motion',()=>{
  for(const mode of ['solid','bouncy'])expect(valid({physics:{mode,shape:'automatic'}})).toBe(false);
  expect(valid({motion:{loop:false,frames:[{time:0,position:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1},scale:1}]}})).toBe(false);
 });
 it('distinguishes a selected box from automatic compatible meshes and refuses partial mixtures',()=>{
  expect(valid({geometry:{kind:'cylinder'}})).toBe(false);
  expect(valid({geometry:{kind:'cylinder'},physics:{mode:'fixed',shape:'box'}})).toBe(true);
  expect(valid({physics:{mode:'fixed',shape:'sphere'}})).toBe(false);
  expect(valid({geometry:{kind:'cylinder'},collision:collision('box','cylinder','ring')})).toBe(true);
  expect(valid({collision:collision('box','sphere')})).toBe(false);
  expect(valid({collision:collision('sphere'),physics:{mode:'fixed',shape:'box'}})).toBe(true);
 });
 it('never treats an imported bounding proxy or legacy mesh provider as ordinary support',()=>{
  const geometry={kind:'model',modelHash:'a'.repeat(64)};
  expect(valid({geometry,modelGeometry})).toBe(false);
  expect(valid({geometry,modelGeometry,physics:{mode:'fixed',shape:'box'}})).toBe(false);
  expect(valid({geometry,modelGeometry,collision:collision('box','ring')})).toBe(true);
  expect(valid({geometry,modelGeometry:{...modelGeometry,meshCollision:true,walkable:true}})).toBe(false);
  expect(valid({geometry,modelGeometry:{...modelGeometry,meshCollision:true,walkable:true},staticSupport:{version:1,enabled:false}})).toBe(true);
 });
 it('does not regress shared resources when a supported prototype is captured at version11',()=>{
  expect(valid({environmentProfile:'a'.repeat(32),visibilityLayer:'b'.repeat(32)})).toBe(true);
  expect(validPrototypeResources({version:11,geometry:{kind:'block'},environmentProfile:'a'.repeat(32)})).toBe(true);
  expect(validPrototypeResources({version:11,geometry:{kind:'block'},visibilityLayer:'b'.repeat(32)})).toBe(true);
  expect(validPrototypeResources({version:12,geometry:{kind:'block'},environmentProfile:'a'.repeat(32)})).toBe(false);
 });
});
