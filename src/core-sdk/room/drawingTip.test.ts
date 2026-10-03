// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseProgram} from './programs';
const id='object.drawingTip.edit';
const args={operation:'configure',target:'a'.repeat(32),revision:1,definition:{part:'',position:{x:0,y:0,z:.08},rotation:{x:0,y:0,z:0,w:1},color:{r:.1,g:.5,b:.9,a:1},radius:.004,enabled:true}};
it('shares drawing tip fields, stable target resources and exact program arguments',()=>{
 expect(capabilityDefinition(id)).toBeTruthy();expect(validateCapabilityArguments(id,1,args)).toBeNull();expect(capabilityResources(id,args)).toEqual([args.target]);
 const program={version:3,resources:[args.target],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'tip',op:'invoke',capability:id,version:1,arguments:args,bindings:{}}]}],entry:'main'};
 const parsed=parseProgram(JSON.stringify(program));expect(parsed.error).toBeNull();expect(parsed.program?.functions[0].body[0]).toMatchObject({arguments:args});
});
it('requires native tip support and rejects unsupported colours, anchors and widths',()=>{
 const commands=[{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('drawingTips.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','drawingTips.v1']})).not.toThrow();
 for(const changes of [{radius:0},{radius:.021},{part:'bad name'},{color:{r:1,g:1,b:1,a:0}}])expect(validateCapabilityArguments(id,1,{...args,definition:{...args.definition,...changes}})).not.toBeNull();
 expect(validateCapabilityArguments(id,1,{operation:'remove',target:args.target,revision:1})).toBeNull();
});
