// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {createHash} from 'node:crypto';
import {readFileSync,readdirSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityInput,capabilityFeatures,validateCapabilityArguments,type CapabilityInvocation} from '../../../shared/capabilities';
import {invocationStep} from './capabilitySteps';
import {parseProgram} from './programs';
const directory='unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/';
const entries=readdirSync(directory).filter(name=>name.endsWith('.json')).sort().map(name=>{const bytes=readFileSync(directory+name);return {hash:createHash('sha256').update(bytes).digest('hex'),...JSON.parse(bytes.toString('utf8'))};});
it.each(entries)('keeps $id valid through the public recipe schema and pins the template bytes',entry=>{
 const call:CapabilityInvocation={id:'object.create',version:1,arguments:{kind:'recipe',name:entry.name,x:.3,y:1.3,z:.65,scale:1,recipe:entry.definition.recipe,collision:entry.definition.collision,physics:entry.definition.physics}};
 expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();expect(()=>invocationStep(call,'create')).toThrow('components');
 const template={...call,arguments:{kind:'template',templateHash:entry.hash,name:'',x:.3,y:1.3,z:.65,scale:1}};
 expect(validateCapabilityArguments(template.id,1,template.arguments)).toBeNull();expect(capabilityInput(template.id,template.arguments)?.properties?.templateHash['x-enum-labels']?.[entry.hash]).toBe(entry.name);
 expect(validateCapabilityArguments(template.id,1,{...template.arguments,templateHash:entry.id})).not.toBeNull();expect(()=>invocationStep(template,'create')).toThrow();
 expect(entry.definition.recipe.playing).toBe(false);
 const program={version:3,resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'create',op:'invoke',capability:call.id,version:1,arguments:call.arguments,bindings:{}}]}],entry:'main'};
 // The structural program round trip must preserve all three components.
 const parsed=parseProgram(JSON.stringify(program));expect(parsed.error).toBeNull();expect(parsed.program?.functions[0].body[0]).toMatchObject({arguments:call.arguments});
});
it('advertises creation features without changing the legacy recipe default',()=>{
 const definition=capabilityDefinition('object.create')!;const branch=definition.input.oneOf!.find(v=>v.examples?.[0]&&((v.examples[0] as {kind:string}).kind==='recipe'))!;
 const args=branch.examples![0] as Record<string,unknown>;expect(args).not.toHaveProperty('physics');expect(args).not.toHaveProperty('collision');
 expect(capabilityFeatures('object.create',{...args,physics:{mode:'solid',shape:'automatic',mass:1}})).toContain('creationComponents.v1');
});
