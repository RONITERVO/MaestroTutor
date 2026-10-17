// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {checkedDataValue} from '../../../shared/programValues';
import {requireRoomCapabilities} from '../../../shared/roomControls';
const call={id:'workspace.archive.export',version:1,arguments:{}};
it('uses one native export capability for humans, agents and programs without accepting paths',()=>{
 expect(capabilityDefinition(call.id)).toMatchObject({duration:'completion',input:{'x-features':['workspaceArchiveExport.v1']}});
 expect(validateCapabilityArguments(call.id,1,{})).toBeNull();
 for(const args of [{path:'/private/credentials'},{name:'backup.zip'},{includeChat:true}])expect(validateCapabilityArguments(call.id,1,args)).not.toBeNull();
 const commands=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('workspaceArchiveExport.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','workspaceArchiveExport.v1']})).not.toThrow();
 const program={version:3,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'export',op:'invoke',capability:call.id,version:1,arguments:{},bindings:{}}]}]};
 const edits=[{action:'rules',rule:{action:'edit',edits:[{sequence:{program:JSON.stringify(program)}}]}}];
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','actionResults.v1'];
 expect(()=>requireRoomCapabilities(edits,{capabilities})).toThrow('workspaceArchiveExport.v1');
 expect(()=>requireRoomCapabilities(edits,{capabilities:[...capabilities,'workspaceArchiveExport.v1']})).not.toThrow();
});
it('requires a bounded publication receipt instead of exposing a private capture path',()=>{
 const output={location:'Downloads/Maestro/maestro-workspace-'+ 'a'.repeat(32)+'.zip',sizeKiB:524288,manifestHash:'b'.repeat(64),files:5,models:0,motions:0,sounds:0,images:0,modules:0,unavailablePrograms:0,missingModels:0,missingMotions:0,missingSounds:0,missingImages:0,missingControllerPrograms:0};
 expect(validateCapabilityOutput(call.id,1,output)).toBeNull();
 for(const value of Object.values(output))expect(()=>checkedDataValue(value,typeof value==='number'?'number':'text')).not.toThrow();
 for(const bad of [{...output,location:'/cache/backup.zip'},{...output,sizeKiB:524289},{...output,manifestHash:''},{...output,privatePath:'/cache/backup.zip'}])expect(validateCapabilityOutput(call.id,1,bad)).not.toBeNull();
});
