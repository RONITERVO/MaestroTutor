// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityInput,validateCapabilityArguments,type CapabilitySchema} from '../../../shared/capabilities';
import {validateFactArguments,validFactValue} from '../../../shared/behaviourFacts';
import {boundedCapabilityCall,validCatalogView} from '../../../shared/roomCatalog';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validRoomOwnership} from '../../../shared/roomOwnership';
import {invocationResourceLimit,programResourceLimit} from '../../../shared/programLimits';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';
import {RoomAgentClient} from './roomAgentClient';
import {roomCommandConditions} from './roomObjectContext';
import type {RoomAgentState,RoomCommand} from './roomAgent';
import {ROOM_OBJECT_OVERVIEW_FEATURE} from '../../../shared/roomObjectOverview';
const creation=capabilityDefinition('object.create')!;
const examples=creation.input.oneOf!.map(branch=>branch.examples![0] as Record<string,unknown>);
const batch=()=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-batch-create.json','utf8')) as BehaviourProgram;
it.each(examples)('shares immediate/default and deferred loading for creation kind $kind',example=>{
 for(const loading of [undefined,'now','onDemand']){
  const args={...structuredClone(example),...(loading===undefined?{}:{loading})};
  expect(validateCapabilityArguments('object.create',1,args)).toBeNull();
  expect(boundedCapabilityCall({id:'object.create',version:1,arguments:args})).toBe(true);
  const input=capabilityInput('object.create',args)!;
  expect(input.required).not.toContain('loading');
  expect(input.properties!.loading.enum).toEqual(['now','onDemand']);
  expect(input.properties!.loading['x-enum-labels']?.onDemand).toBe('When needed');
 }
 for(const loading of [null,true,'later',{},['onDemand']])expect(validateCapabilityArguments('object.create',1,{...example,loading})).not.toBeNull();
});
it('requires deferred support only when the optional loading field is used and retains result wiring',()=>{
 for(const loading of [undefined,'now','onDemand']){
  const program=batch(),call=program.functions[0].body[0];if(call.op!=='invoke')throw new Error('Expected creation');
  if(loading!==undefined)call.arguments.loading=loading;
  const parsed=parseProgram(JSON.stringify(program));expect(parsed.error).toBeNull();expect(parsed.program).toEqual(program);
  expect(programFeatureRequirements(program).has('deferredCreation.v1')).toBe(loading!==undefined);
  expect(call.results).toEqual({objectIds:'pieces',slots:'slots'});
  expect(validateCapabilityArguments('object.batch.create',1,call.arguments)).toBeNull();
 }
});
it('keeps the optional choice in the common form schema and preserves geometry validation',()=>{
 const schema=capabilityDefinition('object.batch.create')!.input as CapabilitySchema;
 expect(schema.properties!.loading['x-features']).toContain('deferredCreation.v1');expect(schema.required).not.toContain('loading');
 const program=batch(),call=program.functions[0].body[0];if(call.op!=='invoke')throw new Error('Expected creation');
 call.arguments.loading='onDemand';
 const args=call.arguments as {blueprint:{pieces:Record<string,unknown>[]};loading:string};
 args.blueprint.pieces[1].slot=args.blueprint.pieces[0].slot;
 expect(validateCapabilityArguments('object.batch.create',1,args)).not.toBeNull();
});

it('keeps saved-capacity observations and membership paging available beyond the former limit',()=>{
 expect(validateFactArguments('world.creationBudget',1,undefined)).toBeNull();
 expect(validFactValue('world.creationBudget',{saved:256,savedLimit:256,remaining:0})).toBe(true);
 for(const fact of ['world.region.members','appearance.members','visibility.members']){
  expect(validateFactArguments(fact,1,{id:'a'.repeat(32),offset:240})).toBeNull();
  expect(validateFactArguments(fact,1,{id:'a'.repeat(32),offset:300})).not.toBeNull();
 }
});

it('round-trips full-area action checks and receipts while preserving the smaller program bound',()=>{
 const resources=Array.from({length:256},(_,i)=>i.toString(16).padStart(32,'0'));
 const call={id:'world.region.save',version:1,arguments:{id:'a'.repeat(32),revision:1,name:'District',members:resources,loading:'independent'}};
 expect(validateCapabilityArguments(call.id,call.version,call.arguments)).toBeNull();
 const check={operation:'check',call,valid:true,available:true,occupied:false,resources,status:'Ready'};
 expect(validCatalogView(check)).toBe(true);
 const summary={id:'b'.repeat(32),capability:call.id,version:1,resources,phase:'interrupted',status:'Interrupted; inspect before retrying'};
 expect(validExecutionView({selected:{...summary,call},running:[],outcomes:[summary]})).toBe(true);
 const tooMany=Array.from({length:invocationResourceLimit+1},(_,i)=>i.toString(16).padStart(32,'0'));
 expect(validCatalogView({...check,resources:tooMany})).toBe(false);
 expect(validExecutionView({selected:null,running:[],outcomes:[{...summary,resources:tooMany}]})).toBe(false);
 const owner={id:'area-edit',label:'Edit area',role:'program',allowsGrab:false,claims:resources.map(target=>({target,channel:'regionMembership'}))};
 expect(validRoomOwnership({suspended:false,error:'',owners:[owner]})).toBe(true);
 expect(validRoomOwnership({suspended:false,error:'',owners:[{...owner,claims:tooMany.map(target=>({target,channel:'regionMembership'}))}]})).toBe(false);
 expect(programResourceLimit).toBe(66);expect(invocationResourceLimit).toBe(258);
});

it('sends every large-area revision through the shared client inside its existing wire budget',async()=>{
 const members=Array.from({length:256},(_,i)=>i.toString(16).padStart(32,'0'));
 const basis=members.map((id,i)=>({id,objectRevision:i+1}));
 const command:RoomCommand={action:'execution',execution:{operation:'start',call:{id:'world.region.save',version:1,
  arguments:{id:'e'.repeat(32),revision:1,name:'Quiet district',members,loading:'independent'}}}};
 const state:RoomAgentState={version:1,session:'f'.repeat(32),revision:1,sceneRevision:1,ack:0,ok:true,status:'Ready',created:[],
  canUndo:false,canRedo:false,physicsRunning:false,workspaceReady:true,objects:[],
  capabilities:['execution.v1','worldRegions.v1','objectSearch.v1',ROOM_OBJECT_OVERVIEW_FEATURE],
  objectOverview:{version:1,scope:'context',exhaustive:false,limit:4}};
 const client=new RoomAgentClient();expect(client.receive(state)).toBe(true);
 expect(()=>roomCommandConditions([command],basis.slice(0,255))).toThrow('Read the current saved object');
 const result=client.request([command],state,basis);
 try{
  const wire=client.snapshot();expect(wire.request?.conditions).toEqual(basis.map(({id,objectRevision})=>({id,revision:objectRevision})));
  expect(wire.request?.version).toBe(2);expect(JSON.stringify(wire).length).toBeLessThanOrEqual(32768);
  expect(client.receive({...state,revision:2,ack:wire.request!.sequence})).toBe(true);await result;
 }finally{client.cancel();await result.catch(()=>{});}
});
