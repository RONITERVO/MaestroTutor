// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {applyCurrentInputs} from '../../../shared/currentCapabilityInputs';
import {parseProgram} from './programs';
import {validExecutionView} from '../../../shared/roomExecutions';
import type {CatalogView} from '../../../shared/roomCatalog';
import native from '../../../test-fixtures/browser/surfaceAuthoring.json';
it('preserves exact native ink calls, receipts and erase/Undo observations',()=>{
 const call=native.after.execution.selected.call;
 for(const view of [native.after.execution,native.erased.execution]){expect(validExecutionView(view)).toBe(true);expect(validateCapabilityArguments(view.selected.call.id,1,view.selected.call.arguments)).toBeNull();expect(validateCapabilityOutput(view.selected.call.id,1,view.selected.output)).toBeNull();}
 expect(capabilityResources(call.id,call.arguments)).toEqual([call.arguments.target]);
 expect(native.read.catalog.value.points).toEqual(call.arguments.points);expect(native.erasedRead.catalog.value.surfaces[0].strokes).toBe(0);expect(native.undo.catalog.value.surfaces[0].strokes).toBe(1);
 const program={version:3,resources:[call.arguments.target],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'ink',op:'invoke',capability:call.id,version:1,arguments:call.arguments,bindings:{}}]}],entry:'main'};
 const parsed=parseProgram(JSON.stringify(program));expect(parsed.error).toBeNull();expect(parsed.program?.functions[0].body[0]).toMatchObject({arguments:call.arguments});
});
it('refreshes the object revision without rewriting the selected surface or ink',()=>{
 const call=native.after.execution.selected.call,schema=capabilityDefinition(call.id)!.input;
 expect(applyCurrentInputs(schema,{...call.arguments,revision:1},native.current.catalog as CatalogView)).toEqual(call.arguments);
});
it('requires surface support and rejects invalid brush ranges before dispatch',()=>{
 const call=native.after.execution.selected.call,commands=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1']})).toThrow('drawingSurfaces.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1','drawingSurfaces.v1']})).not.toThrow();
 for(const changes of [{radius:0},{radius:.021},{points:[]},{surface:'bad name'},{stroke:'unknown'},{red:2}])expect(validateCapabilityArguments(call.id,1,{...call.arguments,...changes})).not.toBeNull();
 for(const mode of ['off','space','surface','surfaceErase'])expect(validateCapabilityArguments('drawing.tool.set',1,{mode,red:1,green:1,blue:1,radius:.003})).toBeNull();
 expect(native.toolRead.catalog.value.mode).toBe('surface');
});
