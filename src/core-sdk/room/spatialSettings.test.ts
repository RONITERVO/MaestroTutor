// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/spatialSettings.json';
import walk from '../../../test-fixtures/browser/walkSettings.json';
import clips from '../../../test-fixtures/browser/walkClipPages.json';
import {capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
const views=[native.physics,native.movement,native.walk,walk.embedded,walk.library];
it('validates native saved settings and exact post-save object revisions',()=>{
 for(const view of views){expect(validExecutionView(view)).toBe(true);expect(view.selected.phase).toBe('completed');expect(view.selected.output.temporary).toBe(false);}
 for(const value of [native.beforePhysics,native.afterPhysics])expect(validFactValue('object.physics.settings',value)).toBe(true);
 for(const value of [native.beforeMovement,native.afterMovement])expect(validFactValue('avatar.movement.settings',value)).toBe(true);
 for(const value of [native.beforeWalk,native.walkBeforeSave,native.afterWalk,walk.before,walk.after])expect(validFactValue('avatar.walk.settings',value)).toBe(true);
 expect(native.physics.selected.call.arguments.revision).toBe(native.beforePhysics.revision);
 // Physics placement capture can advance the object again between the save receipt and this later fact.
 expect(native.physics.selected.output.revision).toBeGreaterThan(native.beforePhysics.revision);
 expect(native.afterPhysics.revision).toBeGreaterThanOrEqual(native.physics.selected.output.revision);
 expect(native.afterPhysics).toMatchObject({target:native.physics.selected.output.target,mode:native.physics.selected.call.arguments.mode,shape:native.physics.selected.call.arguments.shape,mass:native.physics.selected.call.arguments.mass});
 expect(native.movement.selected.call.arguments.revision).toBe(native.beforeMovement.revision);expect(native.movement.selected.output.revision).toBe(native.afterMovement.revision);
 expect(native.walk.selected.call.arguments.revision).toBe(native.walkBeforeSave.revision);expect(native.afterMovement.live.active).toBe(false);
 expect(walk.after.selection.motionId).toBe(walk.library.selected.call.arguments.motionId);expect(walk.embedded.selected.call.arguments.modelHash).toBe(walk.before.selection.modelHash);
});
it('requires complete bounded settings and exact clip identities without reserved target overrides',()=>{
 for(const view of views){const call=view.selected.call;expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();expect(capabilityResources(call.id,call.arguments)).toEqual([call.arguments.target]);const commands=[{action:'execution',execution:{operation:'start',call}}];expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('spatialSettings.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','spatialSettings.v1']})).not.toThrow();}
 const physics=native.physics.selected.call;for(const bad of [{...physics.arguments,target:'book'},{...physics.arguments,target:'maestro'},{...physics.arguments,mass:20.1},{...physics.arguments,revision:0},{...physics.arguments,mode:'cloth'}])expect(validateCapabilityArguments(physics.id,1,bad)).not.toBeNull();
 const movement=native.movement.selected.call;for(const bad of [{...movement.arguments,distance:.79},{...movement.arguments,speed:1.21},{...movement.arguments,force:true}])expect(validateCapabilityArguments(movement.id,1,bad)).not.toBeNull();
 const embedded=walk.embedded.selected.call;for(const bad of [{...embedded.arguments,modelHash:''},{...embedded.arguments,clipIndex:32},{...embedded.arguments,clipIndex:-1},{...embedded.arguments,clipIndex:.5},{...embedded.arguments,source:'included'}])expect(validateCapabilityArguments(embedded.id,1,bad)).not.toBeNull();
});
it('keeps all 32 exact embedded indexes across bounded native fact pages, including an empty end page',()=>{
 for(const page of clips){expect(validFactValue('avatar.walk.clips',page.value)).toBe(true);expect(page.value.modelHash).toBe(page.arguments.modelHash);expect(page.value.offset).toBe(page.arguments.offset);expect(page.value.entries.length).toBeLessThanOrEqual(3);}
 expect(clips.flatMap(p=>p.value.entries.map(e=>e.index))).toEqual(Array.from({length:32},(_,i)=>i));expect(clips[clips.length-1]?.value.entries).toEqual([]);
});
