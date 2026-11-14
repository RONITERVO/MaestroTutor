// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {validateFactArguments} from '../shared/behaviourFacts';
import {maintenanceFactRequest} from './probe-maintenance-facts';

it.each(['audio.import.selection','audio.instances'])('omits the arguments field for the real no-input %s fact',capability=>{
 const request=maintenanceFactRequest(capability,{});
 expect(request).toEqual({operation:'inspect',category:'facts',capability,version:1});
 expect(Object.hasOwn(request,'arguments')).toBe(false);
 expect(validateFactArguments(request.capability,request.version,request.arguments)).toBeNull();
 expect(validateFactArguments(request.capability,request.version,{})).toBe('This fact has no arguments');
});
it.each([
 ['audio.library.review',{assetHash:'a'.repeat(64)}],
 ['audio.chat.library',{offset:0}],
] as const)('preserves required arguments for %s', (id,args)=>{
 const request=maintenanceFactRequest(id,args);
 expect(request.arguments).toEqual(args);expect(Object.hasOwn(request,'arguments')).toBe(true);
 expect(validateFactArguments(request.capability,request.version,request.arguments)).toBeNull();
});
it('refuses invalid or unknown requests rather than silently discarding their inputs',()=>{
 expect(()=>maintenanceFactRequest('audio.import.selection',{assetHash:'a'.repeat(64)})).toThrow('no-input fact');
 expect(()=>maintenanceFactRequest('audio.library.review',{})).toThrow();
 expect(()=>maintenanceFactRequest('audio.import.selection',{},2)).toThrow('fact version');
 expect(()=>maintenanceFactRequest('audio.missing',{})).toThrow('Unknown maintenance fact');
});
