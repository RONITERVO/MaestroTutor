// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/includedMotions.json';
import {capabilityDefinition,validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {applyCurrentInputs,currentInputRequest} from '../../../shared/currentCapabilityInputs';
it('shares the native package identity, bounded observation and completed installation receipt',()=>{
 expect(validFactValue('motion.pack.included',native.before)).toBe(true);expect(validFactValue('motion.pack.included',native.after)).toBe(true);
 expect(validExecutionView(native.receipt)).toBe(true);expect(native.before.counts.catalogued).toBe(0);expect(native.after.counts.catalogued).toBe(1);
 expect(native.after.package).toEqual(native.before.package);expect(native.receipt.selected.output).toEqual({manifestHash:native.after.package.manifestHash,added:1,preserved:0,restored:0});
 const bounded={...native.after,status:'x'.repeat(126),package:{...native.after.package,packId:'p'.repeat(80),name:'x'.repeat(126),revision:1000000},counts:{motions:1024,kibibytes:131072,catalogued:1024,removed:1024}};
 expect(validFactValue('motion.pack.included',bounded)).toBe(true);
 expect(validFactValue('motion.pack.included',{...bounded,package:{...bounded.package,avatarHash:'x'.repeat(129)}})).toBe(false);
});
it('requires an advertised package capability and exact identities without arbitrary paths or automatic playback',()=>{
 const call=native.receipt.selected.call;expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
 const commands=[{action:'execution',execution:{operation:'start',call}}];expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('includedMotions.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','includedMotions.v1']})).not.toThrow();
 const restore={operation:'restore',manifestHash:call.arguments.manifestHash,motionId:'a'.repeat(32)};expect(validateCapabilityArguments(call.id,1,restore)).toBeNull();
 for(const args of [{...restore,motionId:''},{...call.arguments,manifestHash:''},{...call.arguments,path:'/private'},{...call.arguments,play:true},{...call.arguments,overwrite:true}])expect(validateCapabilityArguments(call.id,1,args)).not.toBeNull();
});
it('fills the book action from an explicit package observation without replacing the selected restore identity',()=>{
 const definition=capabilityDefinition('motion.pack.install')!,args={operation:'restore',manifestHash:'0'.repeat(64),motionId:'a'.repeat(32)};
 const query=currentInputRequest(definition.input,args);expect(query).toEqual({operation:'inspect',category:'facts',capability:'motion.pack.included',version:1});
 if(query.operation!=='inspect'||query.category!=='facts')throw new Error('Expected a package fact query');
 const reading={...query,category:'facts',definition:behaviourFact('motion.pack.included')!,available:true,value:native.before,status:'Package observed'} as const;
 expect(applyCurrentInputs(definition.input,args,reading)).toEqual({...args,manifestHash:native.before.package.manifestHash});
 expect(()=>applyCurrentInputs(definition.input,args,{...reading,available:false,value:null})).toThrow();
});
