// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityFeatures,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {parseProgram} from './programs';
const id='object.layout.apply';
const args=()=>({placements:Array.from({length:16},(_,i)=>({target:i.toString(16).padStart(32,'0'),position:{x:i*.1,y:1,z:0},rotation:{x:0,y:0,z:0,w:1},scale:1}))});
it('preserves a bounded layout and requires every member in the shared program authority',()=>{
 const arguments_=args(),resources=arguments_.placements.map(p=>p.target);
 expect(validateCapabilityArguments(id,1,arguments_)).toBeNull();expect(capabilityResources(id,arguments_)).toEqual(resources);expect(capabilityFeatures(id,arguments_)).toContain('layoutEdits.v1');
 const program={version:3,resources,state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'reset',op:'invoke',capability:id,version:1,arguments:arguments_,bindings:{}}]}],entry:'main'};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();program.resources=resources.slice(0,-1);expect(parseProgram(JSON.stringify(program)).error).not.toBeNull();
 expect(capabilityDefinition(id)?.output?.properties?.count).toMatchObject({minimum:1,maximum:16});
});
it('rejects duplicate members, protected objects, oversized batches, non-unit rotations and diagonal out-of-bounds poses',()=>{
 for(const mutate of [
  (value:ReturnType<typeof args>)=>{value.placements[15].target=value.placements[0].target;},
  (value:ReturnType<typeof args>)=>{value.placements[0].target='book';},
  (value:ReturnType<typeof args>)=>{value.placements.push({...value.placements[0],target:'f'.repeat(32)});},
  (value:ReturnType<typeof args>)=>{value.placements[0].rotation.w=0;},
  (value:ReturnType<typeof args>)=>{value.placements[0].position={x:25,y:25,z:25};},
 ]){const value=args();mutate(value);expect(validateCapabilityArguments(id,1,value)).not.toBeNull();}
});
