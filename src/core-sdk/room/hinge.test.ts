// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseProgram} from './programs';
const id='object.hinge.edit';
const args:Record<string,unknown>&{target:string;connected:string}={...capabilityDefinition(id)!.example!,target:'a'.repeat(32),connected:'b'.repeat(32)};
it('shares hinge configuration, both resources and the exact editable program call',()=>{
 expect(validateCapabilityArguments(id,1,args)).toBeNull();expect(capabilityResources(id,args)).toEqual([args.target,args.connected]);
 const program={version:3,resources:[args.target,args.connected],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'hinge',op:'invoke',capability:id,version:1,arguments:args,bindings:{}}]}],entry:'main'};
 const parsed=parseProgram(JSON.stringify(program));expect(parsed.error).toBeNull();expect(parsed.program?.functions[0].body[0]).toMatchObject({arguments:args});
});
it('requires hinge support and refuses out-of-range native settings',()=>{
 const commands=[{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('physicalHinges.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','physicalHinges.v1']})).not.toThrow();
 const def=args.definition as {drive:Record<string,unknown>};
 for(const changes of [{speed:361},{force:21},{mode:'script'},{spring:-1}])expect(validateCapabilityArguments(id,1,{...args,definition:{...def,drive:{...def.drive,...changes}}})).not.toBeNull();
 expect(validateCapabilityArguments(id,1,{operation:'remove',target:args.target,revision:1})).toBeNull();
});
it('rejects self connections, reversed limits and spring targets outside the enabled range',()=>{
 const def=args.definition as {drive:Record<string,unknown>;limits:Record<string,unknown>};
 expect(validateCapabilityArguments(id,1,{...args,connected:args.target})).not.toBeNull();
 expect(validateCapabilityArguments(id,1,{...args,definition:{...def,limits:{...def.limits,minimum:90,maximum:0}}})).not.toBeNull();
 expect(validateCapabilityArguments(id,1,{...args,definition:{...def,limits:{...def.limits,enabled:true},drive:{...def.drive,mode:'spring',target:100}}})).not.toBeNull();
});
