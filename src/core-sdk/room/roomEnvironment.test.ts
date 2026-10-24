// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/roomEnvironment.json';
import {capabilityDefinition,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {currentInputMapping,applyCurrentInputs} from '../../../shared/currentCapabilityInputs';
import {behaviourFact} from '../../../shared/behaviourCatalog';
it('distinguishes native request acceptance from loaded, aligned and running state',()=>{
 for(const value of [native.before,native.loading,native.loaded,native.showing,native.hidden]){expect(validFactValue('room.environment',value)).toBe(true);expect(validateCapabilityOutput('room.environment.set',1,value)).toBeNull();}
 expect(validExecutionView(native.loadReceipt)).toBe(true);expect(native.loadReceipt.selected.phase).toBe('completed');expect(native.loadReceipt.selected.output).toMatchObject({phase:'permission',busy:true,surfaces:{ready:false,physicsRunning:false}});
 expect(native.loaded).toMatchObject({phase:'loaded',busy:false,surfaces:{ready:false,physicsRunning:false}});
 for(const [request,value] of [[native.loadRequest,native.before],[native.showRequest,native.loaded],[native.hideRequest,native.showing]] as const){expect(validateCapabilityArguments(request.call.id,1,request.call.arguments)).toBeNull();expect(request.call.arguments.stateId).toBe(value.stateId);}
 expect(native.showing.surfaces).toMatchObject({showing:true,visible:true});expect(native.hidden.surfaces).toMatchObject({showing:false,visible:false});
 expect(capabilityDefinition('room.environment.set')).toMatchObject({duration:'instant',channels:[]});
});
it('requires the advertised feature and exact cancel identity without force or implicit scan options',()=>{
 const call=native.loadRequest.call;const commands=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('roomEnvironment.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','roomEnvironment.v1']})).not.toThrow();
 for(const operation of ['load','scan','show','hide'])expect(validateCapabilityArguments(call.id,1,{...call.arguments,operation})).toBeNull();
 const cancel={operation:'cancel',stateId:native.loading.stateId,requestId:native.loading.requestId};expect(validateCapabilityArguments(call.id,1,cancel)).toBeNull();
 for(const args of [{...call.arguments,operation:'toggle'},{...call.arguments,force:true},{...call.arguments,requestSceneCaptureIfNoDataFound:true},{operation:'cancel',stateId:cancel.stateId},{...cancel,requestId:'other'}])expect(validateCapabilityArguments(call.id,1,args)).not.toBeNull();
 expect(currentInputMapping(capabilityDefinition(call.id)!.input,cancel)?.guards).toEqual(['stateId','requestId']);
 const value=applyCurrentInputs(capabilityDefinition(call.id)!.input,cancel,{operation:'inspect',category:'facts',capability:'room.environment',version:1,definition:behaviourFact('room.environment')!,available:true,value:native.loading,status:'Native room request'});
 expect(value).toEqual(cancel);
});
