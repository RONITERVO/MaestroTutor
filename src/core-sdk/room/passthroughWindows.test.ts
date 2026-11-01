// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {validCreationPrototypeGeometry} from '../../../shared/creationPrototype';
import {parseProgram} from './programs';
const target='a'.repeat(32),id='object.window.edit';
const args={operation:'save',target,revision:1,window:'Window',surface:'Canvas',shape:'ellipse',reveal:.5};
it('uses one revision-guarded command in forms, agent dispatch and saved programs',()=>{
 expect(validateCapabilityArguments(id,1,args)).toBeNull();expect(capabilityResources(id,args)).toEqual([target]);
 const schema=capabilityDefinition(id)!.input;expect(schema.oneOf?.every(v=>v['x-current']?.fact==='object.windows')).toBe(true);
 const commands=[{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1']})).toThrow('passthroughWindows.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1','passthroughWindows.v1']})).not.toThrow();
 const program={version:3,resources:[target],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'window',op:'invoke',capability:id,version:1,arguments:args,bindings:{}}]}],entry:'main'};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
});
it('rejects invalid reveal, shapes and stale field spelling before native dispatch',()=>{
 for(const patch of [{reveal:-.1},{reveal:1.01},{reveal:NaN},{shape:'sphere'},{surface:'bad name'},{window:''},{opacity:.5}])expect(validateCapabilityArguments(id,1,{...args,...patch})).not.toBeNull();
 expect(validateCapabilityArguments(id,1,{operation:'remove',target,revision:1,window:'Window'})).toBeNull();
});
it('portable prototypes retain windows and require a real plane with unique references',()=>{
 const surface={version:1,id:'Canvas',part:'',position:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1},width:1,height:1,enabled:false,strokes:[]};
 const window={version:1,id:'Window',surface:'Canvas',shape:'rectangle',reveal:1};
 const p={version:4,geometry:{kind:'block'},surfaces:[surface],drawingTips:[],windows:[window]};
 expect(validCreationPrototypeGeometry(p)).toBe(true);
 for(const patch of [{version:3},{windows:[{...window,surface:'Absent'}]},{windows:[window,{...window,id:'Other'}]},{surfaces:[{...surface,version:2,shape:'cylinder',curvatureRadius:1}]}])expect(validCreationPrototypeGeometry({...p,...patch})).toBe(false);
});
