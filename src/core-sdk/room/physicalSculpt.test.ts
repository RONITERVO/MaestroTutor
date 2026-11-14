// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {validateFactArguments} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {validCreationPrototypeGeometry} from '../../../shared/creationPrototype';
import {parseProgram} from './programs';
const id='object.sculptTip.edit',target='a'.repeat(32);
const definition={part:'',position:{x:0,y:0,z:.11},rotation:{x:0,y:0,z:0,w:1},mode:'lower',radius:.08,height:.03,enabled:true};
const args={operation:'configure',target,revision:1,definition};
it('uses the shared program and creation data for reusable physical sculpt tips',()=>{
 expect(capabilityDefinition(id)).toBeTruthy();expect(validateCapabilityArguments(id,1,args)).toBeNull();expect(capabilityResources(id,args)).toEqual([target]);
 const program={version:3,resources:[target],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'tip',op:'invoke',capability:id,version:1,arguments:args,bindings:{}}]}],entry:'main'};
 const parsed=parseProgram(JSON.stringify(program));expect(parsed.error).toBeNull();expect(parsed.program?.functions[0].body[0]).toMatchObject({arguments:args});
 const source={geometry:{kind:'block'},surfaces:[],drawingTips:[],sculptTips:[{...definition,version:1}]};expect(validCreationPrototypeGeometry(source)).toBe(true);expect(validCreationPrototypeGeometry({...source,sculptTips:[{...definition,part:'missing'}]})).toBe(false);
 expect(validCreationPrototypeGeometry({...source,drawingTips:[{part:'',position:{x:0,y:0,z:0},version:1,enabled:true}]})).toBe(false);expect(validCreationPrototypeGeometry({...source,drawingTips:[{part:'',position:{x:0,y:0,z:0},version:1,enabled:false}]})).toBe(true);
});
it('rejects unsupported sculpt tip geometry and requires native physical support',()=>{
 const commands=[{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('physicalSculpting.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','physicalSculpting.v1']})).not.toThrow();
 for(const change of [{radius:0},{radius:2.1},{height:-.01},{height:.51},{mode:'melt'},{part:'bad name'},{position:{x:10,y:10,z:10}},{rotation:{x:0,y:0,z:0,w:0}}])expect(validateCapabilityArguments(id,1,{...args,definition:{...definition,...change}})).not.toBeNull();
 expect(validateCapabilityArguments(id,1,{operation:'remove',target,revision:1})).toBeNull();
});
it('requires the retained session for retry and exposes bounded exact paths',()=>{
 const sessionId='b'.repeat(32);expect(validateCapabilityArguments('field.tool.set',1,{mode:'lower',radius:.08,height:.03})).toBeNull();
 for(const operation of ['retry','discard'])expect(validateCapabilityArguments('object.field.resolve',1,{operation,sessionId})).toBeNull();
 expect(validateCapabilityArguments('object.field.resolve',1,{operation:'retry'})).not.toBeNull();expect(validateCapabilityArguments('object.field.resolve',1,{operation:'retry',sessionId:'stale'})).not.toBeNull();
 expect(validateFactArguments('object.field.capture.path',1,{sessionId,offset:32})).toBeNull();for(const offset of [-1,33])expect(validateFactArguments('object.field.capture.path',1,{sessionId,offset})).not.toBeNull();
});
