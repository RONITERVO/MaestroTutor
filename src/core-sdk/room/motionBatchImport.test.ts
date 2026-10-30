// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/motionBatchImport.json';
import {capabilityDefinition,validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
it('distinguishes native batch receipts from the file outcomes and preserves exact motion IDs',()=>{
 for(const view of [native.select,native.category,native.start])expect(validExecutionView(view)).toBe(true);
 for(const value of [native.before,native.choosing,native.ready,native.tagged,native.after])expect(validFactValue('motion.import.batch.status',value)).toBe(true);
 for(const value of [native.file,native.failedFile])expect(validFactValue('motion.import.batch.file',value)).toBe(true);
 expect(native.select.selected.phase).toBe('completed');expect(native.choosing.phase).toBe('selecting');expect(native.ready.counts.saved).toBe(0);
 expect(native.start.selected.phase).toBe('completed');expect(native.start.selected.output.phase).toBe('running');expect(native.after.phase).toBe('partial');
 expect(native.after.counts).toEqual({files:3,saved:2,failed:1,waiting:0});expect(native.file.motionIds).toHaveLength(1);expect(native.failedFile.state).toBe('failed');
 expect(native.start.selected.call.arguments.version).toBe(native.tagged.version);expect(capabilityDefinition('motion.import.batch')).toMatchObject({duration:'instant',ownership:'importSession',channels:[]});
});
it('uses the shared capability feature, versioned controls and bounded per-file pages',()=>{
 for(const view of [native.select,native.category,native.start]){
  const call=view.selected.call;expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();const commands=[{action:'execution',execution:{operation:'start',call}}];
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('motionBatchImport.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','motionBatchImport.v1']})).not.toThrow();
 }
 const args=native.start.selected.call.arguments;
 for(const invalid of [{...args,version:0},{...args,requestId:''},{...args,path:'C:/private.glb'},{...args,manual:true},{operation:'select',requestId:args.requestId}])expect(validateCapabilityArguments('motion.import.batch',1,invalid)).not.toBeNull();
 expect(validateCapabilityArguments('motion.import.batch',1,{operation:'category',requestId:args.requestId,version:1,category:'dancing'})).toBeNull();
 expect(validateCapabilityArguments('motion.import.batch',1,{operation:'category',requestId:args.requestId,version:1,category:'bad\nname'})).not.toBeNull();
 const query={requestId:args.requestId,index:0,motionOffset:0};expect(validateFactArguments('motion.import.batch.file',1,query)).toBeNull();expect(validateFactArguments('motion.import.batch.file',1,{...query,motionOffset:32})).not.toBeNull();
 expect(validateFactArguments('motion.import.batch.file',1,{...query,index:1023})).toBeNull();expect(validateFactArguments('motion.import.batch.file',1,{...query,index:1024})).not.toBeNull();
 expect(validFactValue('motion.import.batch.file',{...native.file,error:'x'.repeat(129)})).toBe(false);
});
