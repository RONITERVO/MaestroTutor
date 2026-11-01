// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/scanDrawing.json';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue} from '../../../shared/behaviourFacts';
import {validateCapabilityOutput} from '../../../shared/capabilities';
import {capabilityDefinition,validateCapabilityArguments,capabilityResources} from '../../../shared/capabilities';
import {currentInputRequest,applyCurrentInputs} from '../../../shared/currentCapabilityInputs';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {ROOM_ACTION_GUIDE} from '../../../shared/prompts/room';
import type {CatalogView} from '../../../shared/roomCatalog';
const id='a'.repeat(32),state='b'.repeat(32),anchor='c'.repeat(32),room='d'.repeat(32);
it('keeps scan and layer guards shared between generated fields and agent calls',()=>{
 const schema=capabilityDefinition('drawing.layer.edit')!.input,create={operation:'atGaze',stateId:state,name:'Wall ink',width:.6,height:.4,angle:0};
 expect(validateCapabilityArguments('drawing.layer.edit',1,create)).toBeNull();expect(currentInputRequest(schema,create)).toMatchObject({capability:'room.scan',version:1});
 const rebind={operation:'rebind',target:id,revision:1,stateId:state,anchorId:anchor,x:.1,y:0,angle:0};
 const query=currentInputRequest(schema,rebind)!;if(query.operation!=='inspect')throw new Error('Expected fact inspection');expect(query).toMatchObject({capability:'object.scanDrawing',arguments:{target:id}});
 const fact:CatalogView={...query,category:'facts',definition:behaviourFact('object.scanDrawing'),available:true,value:{stateId:'e'.repeat(32),revision:9,anchor:{roomId:room,anchorId:anchor,x:0,y:0,angle:0},width:.6,height:.4,visible:true,reason:'Attached'},status:'Current ink'};
 expect(applyCurrentInputs(schema,rebind,fact)).toEqual({...rebind,stateId:'e'.repeat(32),revision:9});expect(capabilityResources('drawing.layer.edit',rebind)).toEqual([id]);expect(capabilityResources('drawing.layer.edit',create)).toEqual([]);
});
it('requires native scanned-ink support and bounded explicit placement',()=>{
 const call={id:'drawing.layer.edit',version:1,arguments:capabilityDefinition('drawing.layer.edit')!.example!};const commands=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1']})).toThrow('scanDrawingLayers.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1','scanDrawingLayers.v1']})).not.toThrow();
 for(const invalid of [{...call.arguments,width:5},{...call.arguments,stateId:'current'},{...call.arguments,angle:181},{...call.arguments,autoScan:true},{...call.arguments,operation:'rebind'}])expect(validateCapabilityArguments(call.id,1,invalid)).not.toBeNull();
 expect(ROOM_ACTION_GUIDE).toContain('no automatic relocation');expect(ROOM_ACTION_GUIDE).toContain('do not substitute a farther wall');
});

it('accepts the actual native saved-layer receipt and availability record without losing ink identities',()=>{
 expect(validExecutionView(native.receipt)).toBe(true);expect(validFactValue('object.scanDrawing',native.layer)).toBe(true);
 const selected=native.receipt.selected;expect(selected.phase).toBe('completed');expect(validateCapabilityArguments(selected.call.id,1,selected.call.arguments)).toBeNull();expect(validateCapabilityOutput(selected.call.id,1,selected.output)).toBeNull();
 expect(selected.output).toMatchObject({roomId:native.layer.anchor.roomId,anchorId:native.layer.anchor.anchorId,surface:'Canvas',temporary:false});expect(native.layer).toMatchObject({visible:true,width:.6,height:.4});
 expect(validFactValue('object.scanDrawing',{...native.layer,mesh:[]})).toBe(false);
});
