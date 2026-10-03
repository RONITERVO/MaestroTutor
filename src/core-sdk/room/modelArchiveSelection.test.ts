// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/modelArchive.json';
import {validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
it('reads the native ZIP list and acknowledgement separately from the actual unsaved preview',()=>{
 expect(validFactValue('model.import.archive',native.ready)).toBe(true);expect(validFactValue('model.import.archive',native.archive)).toBe(true);
 expect(validFactValue('model.import.selection',native.preview)).toBe(true);expect(validExecutionView(native.receipt)).toBe(true);
 expect(native.ready.phase).toBe('archive');expect(native.ready.count).toBe(3);expect(native.preview.phase).toBe('preview');expect(native.preview.accepted.destination).toBe('');
 expect(native.receipt.selected.output.modelHash).toBe('');expect(native.receipt.selected.output.destination).toBe('');expect(native.preview.preview.modelHash).toHaveLength(64);
 const bounded={...native.archive,count:1024,total:1024,offset:1021,focusedIndex:1023,entries:Array.from({length:3},(_,i)=>({index:1021+i,name:'\u2028'.repeat(21),kibibytes:65536}))};expect(validFactValue('model.import.archive',bounded)).toBe(true);
 expect(native.archive.version).toBe(native.receipt.selected.call.arguments.version+1);expect(native.archive.focusedIndex).toBe(native.receipt.selected.call.arguments.index);
});
it('requires the ZIP feature, current request/version and bounded indices without granting paths',()=>{
 const call=native.receipt.selected.call;expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
 const commands=[{action:'execution',execution:{operation:'start',call}}];expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','modelImport.v1']})).toThrow('modelArchiveImport.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','modelImport.v1','modelArchiveImport.v1']})).not.toThrow();
 for(const args of [{...call.arguments,version:0},{...call.arguments,index:1024},{...call.arguments,requestId:''},{...call.arguments,path:'secret.glb'}])expect(validateCapabilityArguments(call.id,1,args)).not.toBeNull();
 expect(validateCapabilityArguments(call.id,1,{operation:'files',requestId:call.arguments.requestId,version:native.archive.version})).toBeNull();
 const query={requestId:call.arguments.requestId,query:'model',offset:0};expect(validateFactArguments('model.import.archive',1,query)).toBeNull();
 for(const args of [{...query,offset:1024},{...query,query:'x'.repeat(81)},{...query,path:'/private'}])expect(validateFactArguments('model.import.archive',1,args)).not.toBeNull();
});
