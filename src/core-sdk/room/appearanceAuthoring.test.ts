// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/appearanceAuthoring.json';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {validFactValue} from '../../../shared/behaviourFacts';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validCatalogView} from '../../../shared/roomCatalog';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {programResourceLimit} from '../../../shared/programLimits';
import {parseProgram} from './programs';
import {moduleHash,validModuleRecord} from '../../../shared/programModuleIdentity';
it('accepts actual native appearance receipts and bounded observations through the shared contracts',()=>{
 const {call,...summary}=native.receipt;
 expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
 expect(validateCapabilityOutput(call.id,1,native.receipt.output)).toBeNull();
 expect(validExecutionView({selected:native.receipt,running:[],outcomes:[summary]})).toBe(true);
 for(const [id,value] of [['appearance.definition',native.definition],['object.appearances',native.bindings],['object.appearance.targets',native.targets],['appearance.members',native.members]] as const)expect(validFactValue(id,value),id).toBe(true);
 const commands=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('appearances.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','appearances.v1']})).not.toThrow();
 expect(capabilityResources(call.id,call.arguments)).toEqual([call.arguments.target]);
});
it('rejects conflicting opacity/inheritance and unstable imported-slot addresses before dispatch',()=>{
 const save=capabilityDefinition('appearance.save')!.example as {style:Record<string,unknown>};
 expect(validateCapabilityArguments('appearance.save',1,save)).toBeNull();
 for(const style of [{...save.style,renderMode:'opaque'},{...save.style,renderMode:'inherit'},{...save.style,cutoff:.2},{...save.style,grain:-.5},{...save.style,shading:-.1},{...save.style,opacity:'0.4'},{...save.style,textureUrl:'https://example.com/image'}])expect(validateCapabilityArguments('appearance.save',1,{...save,style})).not.toBeNull();
 const assign=native.call.arguments;
 for(const binding of [{...assign.binding,partId:'wrong'},{...assign.binding,kind:'material',materialIndex:0},{...assign.binding,kind:'part'},{...assign.binding,tint:'red'}])expect(validateCapabilityArguments('object.appearance.bind',1,{...assign,binding})).not.toBeNull();
 expect(validateCapabilityArguments('object.appearance.bind',1,{...assign,binding:{...assign.binding,kind:'material',modelHash:'a'.repeat(64),materialIndex:0}})).toBeNull();
});
it('claims every object in a shared edit and keeps native/web program, receipt and module ceilings aligned',()=>{
 expect(programResourceLimit).toBe(66);
 const members=Array.from({length:programResourceLimit},(_,i)=>i.toString(16).padStart(32,'0'));
 const args={...capabilityDefinition('appearance.save')!.example,members};
 const call={id:'appearance.save',version:1,arguments:args};
 expect(validateCapabilityArguments(call.id,1,args)).toBeNull();expect(capabilityResources(call.id,args)).toEqual(members);
 expect(validCatalogView({operation:'check',call,valid:true,available:true,occupied:false,resources:members,status:'Ready'})).toBe(true);
 expect(validateCapabilityArguments(call.id,1,{...args,members:[...members,'book']})).not.toBeNull();
 const program={version:3,entry:'main',resources:members,state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect(parseProgram(JSON.stringify({...program,resources:[...members,'book']})).program).toBeNull();
 const module={version:1,name:'Shared edit',exports:['main'],program};
 expect(validModuleRecord(module,moduleHash(module))).toBe(true);
 const summary={...native.receipt,id:'f'.repeat(32),capability:call.id,call,resources:members,output:{id:'a'.repeat(32),revision:1,temporary:false}};
 const {call:ignored,...brief}=summary;void ignored;
 expect(validExecutionView({selected:summary,running:[],outcomes:[brief]})).toBe(true);
});

it('uses exact imported image identities without changing independent surface semantics',()=>{
 const save=capabilityDefinition('appearance.save')!.example as {style:Record<string,unknown>};
 const style={...save.style,patternMode:'image',imageHash:'a'.repeat(64)};
 expect(validateCapabilityArguments('appearance.save',1,{...save,style})).toBeNull();
 for(const imageHash of ['', '../private', 'https://example.com/image', 'A'.repeat(64)])expect(validateCapabilityArguments('appearance.save',1,{...save,style:{...style,imageHash}})).not.toBeNull();
 for(const patternMode of ['inherit','replace'])expect(validateCapabilityArguments('appearance.save',1,{...save,style:{...style,patternMode}})).not.toBeNull();
 expect(validateCapabilityArguments('image.import',1,{operation:'select'})).toBeNull();
 expect(validateCapabilityArguments('image.import',1,{operation:'select',url:'https://example.com/image'})).not.toBeNull();
 expect(validateCapabilityArguments('image.import',1,{operation:'accept',requestId:'a'.repeat(32),imageHash:'b'.repeat(64)})).toBeNull();
});

it('selects exact current chat-image offers without allowing URLs or bytes in program arguments',()=>{
 expect(validateCapabilityArguments('image.import',1,{operation:'selectChat',offerSet:'a'.repeat(32),imageHash:'b'.repeat(64)})).toBeNull();
 for(const extra of [{url:'https://example.com/image'},{data:'AAAA'},{offerSet:'stale'},{imageHash:'../file'}])
  expect(validateCapabilityArguments('image.import',1,{operation:'selectChat',offerSet:'a'.repeat(32),imageHash:'b'.repeat(64),...extra})).not.toBeNull();
});
