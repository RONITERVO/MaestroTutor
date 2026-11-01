// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {validateFactArguments} from '../shared/behaviourFacts';
import {imageMaintenanceFactRequest} from './probe-native-image-maintenance';

it('omits the arguments field for the real no-input image selection fact',()=>{
 const request=imageMaintenanceFactRequest('image.import.selection',{});
 expect(request).toEqual({operation:'inspect',category:'facts',capability:'image.import.selection',version:1});
 expect(Object.hasOwn(request,'arguments')).toBe(false);
 expect(validateFactArguments(request.capability,request.version,request.arguments)).toBeNull();
 expect(validateFactArguments(request.capability,request.version,{})).toBe('This fact has no arguments');
});
it.each([
 ['image.library.review',{imageHash:'a'.repeat(64)}],
 ['image.chat.library',{offset:0}],
] as const)('preserves required arguments for %s', (id,args)=>{
 const request=imageMaintenanceFactRequest(id,args);
 expect(request.arguments).toEqual(args);expect(Object.hasOwn(request,'arguments')).toBe(true);
 expect(validateFactArguments(request.capability,request.version,request.arguments)).toBeNull();
});
it('refuses invalid or unknown requests rather than silently discarding their inputs',()=>{
 expect(()=>imageMaintenanceFactRequest('image.import.selection',{imageHash:'a'.repeat(64)})).toThrow('no-input fact');
 expect(()=>imageMaintenanceFactRequest('image.library.review',{})).toThrow();
 expect(()=>imageMaintenanceFactRequest('image.import.selection',{},2)).toThrow('fact version');
 expect(()=>imageMaintenanceFactRequest('image.missing',{})).toThrow('Unknown image maintenance fact');
});
