// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {capabilityDefinition,capabilityFeatures,validateCapabilityArguments} from '../../../shared/capabilities';
import {alignedConnection, type ConnectionDefinition} from '../../../shared/roomConnection';
import {requireRoomCapabilities} from '../../../shared/roomControls';
const id='object.connection.edit';
const frame={position:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1}};
const definition={kind:'slider' as const,enabled:true,breakForce:0,breakTorque:0,ownerFrame:frame,connectedFrame:frame,slide:{minimum:-.03,maximum:.12,mode:'spring',target:.04,spring:250,damper:3,speed:0,force:20}};
const args={operation:'configure',target:'a'.repeat(32),connected:'b'.repeat(32),revision:1,definition};
it('shares bounded slider settings with native schemas and requires slider support',()=>{
 expect(validateCapabilityArguments(id,1,args)).toBeNull();
 // Match native single-precision subtraction at the minimum stroke boundary.
 expect(validateCapabilityArguments(id,1,{...args,definition:{...definition,slide:{...definition.slide,minimum:.01,maximum:.015,target:.01}}})).toBeNull();
 const commands=[{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','physicalConnections.v1']})).toThrow('physicalSliders.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','physicalConnections.v1','physicalSliders.v1']})).not.toThrow();
 for(const changes of [{minimum:.12},{maximum:-.031},{maximum:-.028},{target:.121},{force:101},{speed:.51},{damper:-1},{spring:501},{mode:'code'}])expect(validateCapabilityArguments(id,1,{...args,definition:{...definition,slide:{...definition.slide,...changes}}})).not.toBeNull();
 expect(validateCapabilityArguments(id,1,{...args,connected:args.target})).not.toBeNull();
 const slide={operation:'slide',target:args.target,connected:args.connected,revision:1,distance:.07};
 expect(validateCapabilityArguments(id,1,slide)).toBeNull();expect(validateCapabilityArguments(id,1,{...slide,connected:args.target})).not.toBeNull();
 expect(capabilityDefinition(id)!.input).toBeDefined();
});
it('checks travel in connected local metres, locks perpendicular offsets and complete orientation',()=>{
 const pose={position:{x:0,y:0,z:0},rotation:frame.rotation,scale:2};
 expect(alignedConnection(definition,{...pose,position:{x:.2,y:0,z:0}},pose)).toBe(true);
 expect(alignedConnection(definition,{...pose,position:{x:.26,y:0,z:0}},pose)).toBe(false);
 expect(alignedConnection(definition,{...pose,position:{x:.2,y:.04,z:0}},pose)).toBe(false);
 expect(alignedConnection(definition,{...pose,rotation:{x:Math.SQRT1_2,y:0,z:0,w:Math.SQRT1_2}},pose)).toBe(false);
});
it('ships the button as an ordinary editable connected blueprint with no special native action',()=>{
 const module=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Resources/Programs/Modules/SpringButton.json','utf8'));
 const call=module.program.functions.find((f:{name:string})=>f.name==='create').body[0];
 expect(call.capability).toBe('object.batch.create');expect(validateCapabilityArguments(call.capability,call.version,call.arguments)).toBeNull();
 const [mount,cap]=call.arguments.blueprint.pieces;const link=call.arguments.blueprint.connections[0];
 expect(link.definition.kind).toBe('slider');expect(link.owner).toBe(cap.slot);expect(link.connected).toBe(mount.slot);
 expect(alignedConnection(link.definition as ConnectionDefinition,cap,mount)).toBe(true);
 expect(()=>requireRoomCapabilities([{action:'execution',execution:{operation:'start',call:{id:call.capability,version:call.version,arguments:call.arguments}}}],{capabilities:['execution.v1',...capabilityFeatures(call.capability,call.arguments).filter(f=>f!=='physicalSliders.v1')]})).toThrow('physicalSliders.v1');
});
