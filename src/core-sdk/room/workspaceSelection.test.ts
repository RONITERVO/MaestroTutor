// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
const requestId='a'.repeat(32);
it('shares exact selection/cancellation requests without accepting paths or activating content',()=>{
 const id='workspace.archive.select';expect(capabilityDefinition(id)).toMatchObject({duration:'instant',input:{'x-features':['workspaceArchiveSelection.v1']}});
 expect(validateCapabilityArguments(id,1,{})).toBeNull();expect(validateCapabilityOutput(id,1,{requestId})).toBeNull();
 for(const args of [{path:'/private/settings'},{uri:'content://files/one'},{activate:true}])expect(validateCapabilityArguments(id,1,args)).not.toBeNull();
 expect(validateCapabilityArguments('workspace.archive.cancel',1,{requestId})).toBeNull();
 expect(validateCapabilityArguments('workspace.archive.cancel',1,{requestId:'../other'})).not.toBeNull();
 const commands=[{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:{}}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('workspaceArchiveSelection.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','workspaceArchiveSelection.v1']})).not.toThrow();
});
it('reads a bounded preview through the ordinary fact vocabulary and refuses private path fields',()=>{
 const id='workspace.archive.selection';expect(behaviourFact(id)).toMatchObject({features:['factQueries.v1']});
 expect(validateFactArguments(id,1,{requestId})).toBeNull();expect(validateFactArguments(id,1,{requestId,path:'/cache/one'})).not.toBeNull();
 const summary={files:1400,models:32,motions:1024,modules:256,unavailablePrograms:32,missingModels:32,missingMotions:1024,missingControllerPrograms:4};
 const value={requestId,phase:'prepared',name:'"'.repeat(96),error:'"'.repeat(96),generationId:'b'.repeat(32),manifestHash:'c'.repeat(64),summary};
 expect(validFactValue(id,value)).toBe(true);expect(validFactValue(id,{...value,path:'/cache/private'})).toBe(false);
 expect(validFactValue(id,{...value,summary:{...summary,files:1_000_001}})).toBe(false);
});
