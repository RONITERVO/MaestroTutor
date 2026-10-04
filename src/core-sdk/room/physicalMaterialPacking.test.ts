// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityFeatures,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {validateFactArguments} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseProgram} from './programs';
const id='material.pack.tool.set';
const args={enabled:true,radius:.12,amountLitres:.25,mass:.15};
it('shares one physical packing configuration across user controls, programs and delegated actions',()=>{
 expect(validateCapabilityArguments(id,1,args)).toBeNull();expect(capabilityResources(id,args)).toEqual([]);
 expect(capabilityFeatures(id,args)).toContain('physicalMaterialPacking.v1');
 const program={version:3,resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'packing',op:'invoke',capability:id,version:1,arguments:args,bindings:{}}]}],entry:'main'};
 const parsed=parseProgram(JSON.stringify(program));expect(parsed.error).toBeNull();expect(parsed.program?.functions[0].body[0]).toMatchObject({arguments:args});
 const commands=[{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','materialPacking.v1']})).toThrow('physicalMaterialPacking.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','physicalMaterialPacking.v1']})).not.toThrow();
});
for(const patch of [{radius:0},{radius:2.01},{amountLitres:0},{amountLitres:20.01},{amountLitres:NaN},{mass:0},{mass:20.01},{enabled:'true'},{height:.1}])it(`refuses invalid physical packing parameters ${JSON.stringify(patch)}`,()=>{
 expect(validateCapabilityArguments(id,1,{...args,...patch})).not.toBeNull();
});
it('exposes packing settings and draft while retaining the shared exact-session recovery contract',()=>{
 expect(validateFactArguments('material.pack.tool',1,undefined)).toBeNull();expect(validateFactArguments('material.pack.capture',1,undefined)).toBeNull();
 expect(validateCapabilityArguments(id,1,{...args,enabled:false})).toBeNull();
 expect(validateCapabilityArguments('object.field.resolve',1,{operation:'retry',sessionId:'a'.repeat(32)})).toBeNull();
 expect(validateCapabilityArguments('object.field.resolve',1,{operation:'discard',sessionId:'stale'})).not.toBeNull();
});
