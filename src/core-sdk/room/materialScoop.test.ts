// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityFeatures,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {validateFactArguments} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseProgram} from './programs';
const id='object.sculptTip.edit',target='a'.repeat(32);
const definition={part:'',position:{x:0,y:.007,z:.09},rotation:{x:-.70710677,y:0,z:0,w:.70710677},mode:'scoop',radius:.08,amountLitres:.25,enabled:true};
const args={operation:'configure',target,revision:1,definition};
it('configures physical material tools through the same typed human and agent action',()=>{
 expect(validateCapabilityArguments(id,1,args)).toBeNull();expect(capabilityResources(id,args)).toEqual([target]);expect(capabilityFeatures(id,args)).toContain('physicalMaterialTools.v1');
 const program={version:3,resources:[target],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'scoop',op:'invoke',capability:id,version:1,arguments:args,bindings:{}}]}],entry:'main'};
 const parsed=parseProgram(JSON.stringify(program));expect(parsed.error).toBeNull();expect(parsed.program?.functions[0].body[0]).toMatchObject({arguments:args});
 const commands=[{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','physicalSculpting.v1']})).toThrow('physicalMaterialTools.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','physicalSculpting.v1','physicalMaterialTools.v1']})).not.toThrow();
});
it('rejects incompatible or unbounded scoop parameters without changing shape authoring',()=>{
 for(const patch of [{amountLitres:0},{amountLitres:20.01},{amountLitres:NaN},{amountLitres:Infinity},{height:.03},{radius:0},{part:'bad name'},{rotation:{x:0,y:0,z:0,w:0}}])expect(validateCapabilityArguments(id,1,{...args,definition:{...definition,...patch}})).not.toBeNull();
 const {amountLitres:_,...shape}=definition;
 expect(validateCapabilityArguments(id,1,{...args,definition:{...shape,mode:'lower',height:.03}})).toBeNull();
 expect(validateCapabilityArguments(id,1,{...args,definition:{...shape,mode:'lower',height:.03,amountLitres:.25}})).not.toBeNull();
 expect(capabilityFeatures(id,{...args,definition:{...shape,mode:'lower',height:.03}})).not.toContain('physicalMaterialTools.v1');
});
it('exposes the same retained session and bounded material observation to both editors',()=>{
 expect(validateFactArguments('object.material.capture',1,undefined)).toBeNull();
 expect(validateCapabilityArguments('object.field.resolve',1,{operation:'retry',sessionId:'b'.repeat(32)})).toBeNull();
 expect(validateCapabilityArguments('object.field.resolve',1,{operation:'discard',sessionId:'old'})).not.toBeNull();
});
