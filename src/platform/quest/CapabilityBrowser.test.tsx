// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {act,cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import exportReceipt from '../../../test-fixtures/browser/workspaceExportReceipt.json';
import nativeSelection from '../../../test-fixtures/browser/workspaceSelection.json';
import nativeActivation from '../../../test-fixtures/browser/workspaceActivation.json';
import nativeReview from '../../../test-fixtures/browser/workspaceReview.json';
import nativePrevious from '../../../test-fixtures/browser/workspacePrevious.json';
import nativeWorkspaceRecovery from '../../../test-fixtures/browser/workspaceRecovery.json';
import nativeFreshRecovery from '../../../test-fixtures/browser/workspaceFreshRecovery.json';
import nativeHistory from '../../../test-fixtures/browser/workspaceHistory.json';
import nativeRetention from '../../../test-fixtures/browser/workspaceRetention.json';
import nativeEvidence from '../../../test-fixtures/browser/workspaceEvidence.json';
import native from '../../../test-fixtures/browser/catalogStates.json';
import nativeProgram from '../../../test-fixtures/browser/programBookState.json';
import {RoomWorkspace} from './RoomWorkspace';
import {currentInputRequest} from '../../../shared/currentCapabilityInputs';
import {type CapabilityDefinition} from '../../../shared/capabilities';
import type {DataValue} from '../../../shared/programValues';
import {RoomAgentClient} from './roomAgentBridge';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
import {validCatalogView,type CatalogView} from '../../../shared/roomCatalog';
import {validExecutionView} from '../../../shared/roomExecutions';
function catalogFixture(value:unknown):CatalogView {if(!validCatalogView(value))throw new Error('Invalid native catalog fixture');return value;}
afterEach(cleanup);
function setup(vocabulary=false,extraFeatures:string[]=[]){
 const client=new RoomAgentClient();let state=JSON.parse(JSON.stringify(nativeProgram)) as RoomAgentState;
 state={...state,capabilities:[...new Set([...state.capabilities!,...extraFeatures,'catalog.v1',...(vocabulary?['catalogVocabulary.v1','factQueries.v1']:[])])],catalog:null,visible:true,workspaceView:'rules',ack:0,revision:1};
 client.receive(state);const screen=render(<RoomWorkspace client={client}/>);
 const receive=async(catalog?:CatalogView,changed=false,more:Partial<RoomAgentState>={})=>{
  state={...state,...more,ack:client.snapshot().request?.sequence??state.ack,revision:state.revision+1,catalog:catalog??state.catalog,
   rules:changed?{...state.rules!,revision:state.rules!.revision+1}:state.rules};
  await act(async()=>{expect(client.receive(state)).toBe(true);});
 };
 return {client,screen,receive,state};
}
it('searches, inspects, checks and adds the same named block to a preserved program draft',async()=>{
 const {client,screen,receive,state}=setup();const source=JSON.parse(state.rules!.selected!.program);
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'search',query:'',offset:0}});
 await receive(catalogFixture(native.search.catalog));
 fireEvent.click(screen.getByRole('button',{name:/Play animation/}));await receive(catalogFixture(native.inspect.catalog));
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(native.ready.catalog.call.arguments)}});
 fireEvent.click(screen.getByRole('button',{name:'Check availability'}));await receive(catalogFixture(native.ready.catalog));
 expect(screen.getByText(/Ready now/)).toBeTruthy();
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify({...native.ready.catalog.call.arguments,seconds:2})}});
 expect(screen.queryByText(/Ready now/)).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Add first block to draft'}));expect(client.snapshot().request).toBeNull();
 expect(screen.getByRole('region',{name:'Function prime'})).toBeTruthy();
 fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 const sequence=client.snapshot().request!.commands[0].rule!.edits![0].sequence!;
 const saved=JSON.parse(sequence.program);expect(saved.functions[0].body[0]).toMatchObject({op:'invoke',capability:'animation.play',arguments:{seconds:2}});
 expect(saved.functions[0].body.slice(1)).toEqual(source.functions[0].body);expect(saved.functions[1]).toEqual(source.functions[1]);
 expect(saved.resources).toContain(native.ready.catalog.call.arguments.target);
 await receive();act(()=>client.cancel());
});
it('refreshes readiness without another query and keeps catalog draft additions stale after a concurrent edit',async()=>{
 const {client,screen,receive}=setup();
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 await receive(catalogFixture(native.search.catalog));fireEvent.click(screen.getByRole('button',{name:/Play animation/}));await receive(catalogFixture(native.inspect.catalog));
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(native.ready.catalog.call.arguments)}});
 fireEvent.click(screen.getByRole('button',{name:'Check availability'}));await receive(catalogFixture(native.ready.catalog));
 await receive(catalogFixture(native.occupied.catalog),true);expect(screen.getByText(/running action owns/)).toBeTruthy();expect(client.snapshot().request).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Add first block to draft'}));
 expect(screen.getByText(/draft is retained/)).toBeTruthy();expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true);
 expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});

import nativeExecutions from '../../../test-fixtures/browser/executionStates.json';
import {CapabilityBrowser} from './CapabilityBrowser';
it('starts and stops one exact action through the catalog and displays native phases without saving',async()=>{
 const client=new RoomAgentClient();let state=JSON.parse(JSON.stringify(nativeExecutions.running)) as RoomAgentState;
 // Replay the state just before the captured native start, retaining its issued receipt ID.
 state={...state,revision:1,ack:0,execution:{...state.execution!,nextRunId:nativeExecutions.running.execution.selected.id,selected:null,running:[],outcomes:[]}};
 client.receive(state);const screen=render(<CapabilityBrowser client={client} onClose={()=>{}}/>);
 const receive=async(more:Partial<RoomAgentState>)=>{state={...state,...more,revision:state.revision+1,ack:client.snapshot().request?.sequence??state.ack};await act(async()=>{expect(client.receive(state)).toBe(true);});};
 fireEvent.click(screen.getByRole('button',{name:/^Search$/}));await receive({catalog:catalogFixture(native.search.catalog)});
 fireEvent.click(screen.getByRole('button',{name:/Play animation/}));await receive({catalog:catalogFixture(native.inspect.catalog)});
 const call=nativeExecutions.running.execution.selected.call;
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(call.arguments)}});
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request!.commands).toEqual([{action:'execution',execution:{operation:'start',call,runId:nativeExecutions.running.execution.selected.id}}]);
 expect(client.snapshot().request!.conditions).toContainEqual({id:call.arguments.target,revision:state.objects.find(x=>x.id===call.arguments.target)!.objectRevision});
 await receive({execution:nativeExecutions.running.execution as RoomAgentState['execution']});
 expect(screen.getByLabelText('Selected action').textContent).toContain('running');
 fireEvent.click(screen.getByRole('button',{name:/Stop action/}));
 expect(client.snapshot().request!.commands[0]).toEqual({action:'execution',execution:{operation:'cancel',runId:nativeExecutions.running.execution.selected.id}});
 await receive({execution:nativeExecutions.cancelled.execution as RoomAgentState['execution']});
 expect(screen.queryByRole('button',{name:/Stop action/})).toBeNull();
 expect(screen.getByLabelText('Selected action').textContent).toContain('cancelled');
 expect(state.rules!.revision).toBe(nativeExecutions.running.rules.revision);act(()=>client.cancel());
});

import nativeRecovery from '../../../test-fixtures/browser/actionReceiptStates.json';
it('shows recovered uncertainty and storage failure in the same action catalog without starting anything',()=>{
 const client=new RoomAgentClient(),state={...nativeExecutions.running,revision:1,ack:0,execution:nativeRecovery.interrupted};
 expect(client.receive(state)).toBe(true);
 const screen=render(<CapabilityBrowser client={client} onClose={()=>{}}/>);
 expect(screen.getByLabelText('Selected action').textContent).toContain('interrupted');
 expect(screen.getByLabelText('Selected action').textContent).toContain('Some effects');
 expect(screen.queryByRole('button',{name:/Stop action/})).toBeNull();expect(client.snapshot().request).toBeNull();
 act(()=>{expect(client.receive({...state,revision:2,execution:nativeRecovery['unsaved-completion']})).toBe(true);});
 expect(screen.getByRole('status').textContent).toContain('storage failed');
 expect(screen.getByLabelText('Selected action').textContent).toContain('completed');
 expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});

it('recovers only after the explicit book action and retains failed-recovery status for retry',async()=>{
 const client=new RoomAgentClient();const token='e'.repeat(32);
 let state:RoomAgentState={...nativeExecutions.running,revision:1,ack:0,capabilities:['catalog.v1','execution.v1','executionReceipts.v1','actionRecovery.v1'],
  execution:{selected:null,running:[],outcomes:[],nextRunId:null,storageError:'Action history unavailable',recovery:{id:token,status:'Stops one-off actions and archives history; never replays old actions.'}}} as RoomAgentState;
 expect(client.receive(state)).toBe(true);const screen=render(<CapabilityBrowser client={client} onClose={()=>{}}/>);
 expect(client.snapshot().request).toBeNull();expect(screen.getByRole('region',{name:'Recover action history'}).textContent).toContain('never replays');
 fireEvent.click(screen.getByRole('button',{name:'Stop actions and recover history'}));
 expect(client.snapshot().request!.commands).toEqual([{action:'execution',execution:{operation:'recover',recoveryId:token}}]);expect(client.snapshot().request!.conditions).toEqual([]);
 state={...state,revision:2,ack:1,ok:false,status:'Storage remains unavailable'};
 await act(async()=>{client.receive(state);});expect(screen.getByRole('status').textContent).toBe('Storage remains unavailable');
 expect(client.snapshot().request).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Stop actions and recover history'}));
 state={...state,revision:3,ack:2,ok:true,status:'Recovered without replay',execution:{selected:null,running:[],outcomes:[],nextRunId:'f'.repeat(32),storageError:null,recovery:null}};
 await act(async()=>{client.receive(state);});
 expect(screen.queryByRole('button',{name:'Stop actions and recover history'})).toBeNull();expect(screen.getByRole('status').textContent).toBe('Recovered without replay');expect(client.snapshot().request).toBeNull();client.cancel();
});

import {capabilityDefinition} from '../../../shared/capabilities';
it('keeps valid catalog draft fields when changing kind after another field became invalid',async()=>{
 const {client,screen,receive}=setup(),definition=capabilityDefinition('object.create')!;
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 await receive({operation:'search',query:'',offset:0,pageSize:6,total:1,entries:[{id:definition.id,version:1,label:definition.label}],status:'Definition fixture'});
 fireEvent.click(screen.getByRole('button',{name:/Create object/}));
 await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Definition fixture'});
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify({...definition.example,name:'Keep my name',x:.4,scale:99})}});
 fireEvent.change(screen.getByLabelText('Creation kind'),{target:{value:'1'}});
 const args=JSON.parse((screen.getByLabelText('Action arguments') as HTMLTextAreaElement).value);
 expect(args).toMatchObject({kind:'recipe',name:'Keep my name',x:.4,scale:1});expect(args.recipe.parts).toHaveLength(19);
 expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});

import {behaviourEvent} from '../../../shared/behaviourEvents';
import {behaviourFact} from '../../../shared/behaviourCatalog';
it('browses event schemas without exposing action execution and clears the previous category',async()=>{
 const {client,screen,receive}=setup(true);fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'events'}});fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'search',category:'events',query:'',offset:0}});
 const definition=behaviourEvent('object.collided')!;
 await receive({operation:'search',category:'events',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Test event search'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'inspect',category:'events',capability:definition.id,version:1}});
 await receive({operation:'inspect',category:'events',capability:definition.id,version:1,definition,status:'Test event definition'});
 expect(screen.getByRole('region',{name:'Event definition'}).textContent).toContain('speed');
 expect(screen.queryByRole('button',{name:'Run action now'})).toBeNull();expect(screen.queryByRole('button',{name:'Add first block to draft'})).toBeNull();
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});
 expect(screen.queryByRole('region',{name:'Event definition'})).toBeNull();expect(screen.queryByRole('button',{name:new RegExp(definition.label)})).toBeNull();
 expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});
it('renders live false and true readings and removes stale values when unavailable or replaced',async()=>{
 const {client,screen,receive}=setup(true);fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 const definition=behaviourFact('physics.ready')!;
 await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Test fact search'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));
 const reading:CatalogView={operation:'inspect',category:'facts',capability:definition.id,version:1,definition,available:true,value:false,status:'Test current reading'};
 await receive(reading);expect(screen.getByLabelText('Current fact value').textContent).toContain('false');expect(client.snapshot().request).toBeNull();
 await receive({...reading,value:true});expect(screen.getByLabelText('Current fact value').textContent).toContain('true');
 await receive({...reading,value:null,available:false});expect(screen.getByLabelText('Current fact value').textContent).toContain('Unavailable');
 expect(screen.getByLabelText('Current fact value').textContent).not.toContain('true');
 await receive(catalogFixture(native.search.catalog));expect(screen.getByLabelText('Current fact value').textContent).toContain('Refresh this fact');
 fireEvent.click(screen.getByRole('button',{name:'Refresh fact'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'physics.ready',version:1}});
 await receive(reading);expect(screen.queryByRole('button',{name:'Run action now'})).toBeNull();act(()=>client.cancel());
});
it('keeps older runtimes on their existing action catalog',()=>{
 const {client,screen}=setup();fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));
 expect(screen.queryByLabelText('Catalog category')).toBeNull();act(()=>client.cancel());
});

it('reads selected object arguments without showing a previous target as the new result',async()=>{
 const {client,screen,receive}=setup(true);fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 const definition=behaviourFact('object.position')!;await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Fact search'});fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));
 const inspection:CatalogView={operation:'inspect',category:'facts',capability:definition.id,version:1,definition,available:false,value:null,status:'Choose inputs'};await receive(inspection);
 expect(screen.getByLabelText('Current fact value').textContent).toContain('Not read yet');fireEvent.click(screen.getByRole('button',{name:'Read fact'}));expect(client.snapshot().request?.commands[0]).toMatchObject({catalog:{arguments:{target:'book'}}});
 const book:CatalogView={...inspection,arguments:{target:'book'},available:true,value:{x:1,y:2,z:3}};await receive(book);expect(screen.getByLabelText('Current fact value').textContent).toContain('"x":1');
 fireEvent.change(screen.getByLabelText('Fact inputs target'),{target:{value:'maestro'}});expect(screen.getByLabelText('Current fact value').textContent).not.toContain('"x":1');await receive({...book,value:{x:4,y:2,z:3}});expect(screen.getByLabelText('Current fact value').textContent).toContain('Not read yet');
 fireEvent.click(screen.getByRole('button',{name:'Read fact'}));expect(client.snapshot().request?.commands[0]).toMatchObject({catalog:{arguments:{target:'maestro'}}});await receive({...book,arguments:{target:'maestro'},value:{x:5,y:2,z:3}});expect(screen.getByLabelText('Current fact value').textContent).toContain('"x":5');expect(screen.queryByRole('button',{name:'Run action now'})).toBeNull();act(()=>client.cancel());
});

it('runs workspace export from the shared catalog and displays the native publication receipt',async()=>{
 const {client,screen,receive}=setup(false,['workspaceArchiveExport.v1','execution.v1','actionResults.v1']);
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.change(screen.getByLabelText('Search actions'),{target:{value:'export native workspace'}});fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 const definition=capabilityDefinition('workspace.archive.export')!;
 await receive({operation:'search',query:'export native workspace',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found export'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Action definition'});
 expect(screen.getByLabelText('Action arguments').textContent).toBe('{}');fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',call:{id:definition.id,version:1,arguments:{}}}});
 const current=client.getSnapshot().state!;const next={...current,ack:client.snapshot().request!.sequence,revision:current.revision+1,execution:{selected:exportReceipt,running:[],outcomes:[Object.fromEntries(Object.entries(exportReceipt).filter(([key])=>key!=='call'))]}};
 await act(async()=>{expect(client.receive(next)).toBe(true);});
 expect(screen.getByLabelText('Action result').textContent).toContain(exportReceipt.output.location);expect(screen.getByLabelText('Action result').textContent).toContain(exportReceipt.output.manifestHash);
});

it('opens a tracked archive choice and inspects the real native preview without activating it',async()=>{
 const {client,screen,receive}=setup(true,['workspaceArchiveSelection.v1','execution.v1','actionResults.v1']);
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 const action=capabilityDefinition('workspace.archive.select')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:action.id,version:1,label:action.label}],status:'Select a file'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(action.label)}));await receive({operation:'inspect',capability:action.id,version:1,definition:action,status:'Tracked opening, not activation'});
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',call:{id:action.id,version:1,arguments:{}}}});
 const receipt=nativeSelection.receipt;
 const execution={selected:receipt,running:[],outcomes:[Object.fromEntries(Object.entries(receipt).filter(([key])=>key!=='call'))]};
 if(!validExecutionView(execution))throw new Error('Invalid native workspace selection receipt');
 await receive(undefined,false,{execution});
 expect(screen.getByLabelText('Action result').textContent).toContain(receipt.output.requestId);expect(client.snapshot().request).toBeNull();
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 const fact=behaviourFact('workspace.archive.selection')!;
 await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:fact.id,version:1,label:fact.label}],status:'Read selection'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(fact.label)}));await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,available:false,value:null,status:'Choose request'});
 fireEvent.change(screen.getByLabelText('Fact inputs requestId'),{target:{value:receipt.output.requestId}});fireEvent.click(screen.getByRole('button',{name:'Read fact'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:fact.id,version:1,arguments:{requestId:receipt.output.requestId}}});
 await receive(catalogFixture(nativeSelection.prepared));expect(screen.getByLabelText('Current fact value').textContent).toContain(nativeSelection.prepared.value.manifestHash);
 expect(screen.getByLabelText('Current fact value').textContent).toContain('prepared');expect(screen.queryByRole('button',{name:'Run action now'})).toBeNull();expect(client.snapshot().request).toBeNull();
 fireEvent.change(screen.getByLabelText('Fact inputs requestId'),{target:{value:'f'.repeat(32)}});expect(screen.getByLabelText('Current fact value').textContent).toContain('Not read yet');expect(client.snapshot().request).toBeNull();
 act(()=>client.cancel());
});

it('can start a workspace action with its own issued ID while room history is unavailable',async()=>{
 const {capabilityDefinition}=await import('../../../shared/capabilities');
 const client=new RoomAgentClient(),workspaceId='d'.repeat(32);
 let state={...JSON.parse(JSON.stringify(nativeProgram)),session:'f'.repeat(32),revision:1,ack:0,visible:true,workspaceView:'objects',objects:[],rules:null,
  capabilities:['catalog.v1','catalogVocabulary.v1','execution.v1','executionReceipts.v1','workspaceMaintenance.v1','workspaceArchiveSelection.v1','actionRecovery.v1'],
  execution:{selected:null,running:[],outcomes:[],nextRunId:null,storageError:'Room unavailable',workspace:{selected:null,running:[],outcomes:[],nextRunId:workspaceId,storageError:null}},catalog:null} as RoomAgentState;
 state.inspection=null;state.selectedId='';expect(client.receive(state)).toBe(true);const screen=render(<RoomWorkspace client={client}/>);
 const respond=async(catalog:CatalogView)=>{state={...state,revision:state.revision+1,ack:client.snapshot().request!.sequence,catalog};await act(async()=>{expect(client.receive(state)).toBe(true);});};
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:'Search'}));
 await respond({operation:'search',query:'',offset:0,pageSize:6,total:1,status:'Found',entries:[{id:'workspace.archive.select',version:1,label:'Choose workspace archive'}]});
 fireEvent.click(screen.getByRole('button',{name:/Choose workspace archive/}));await respond({operation:'inspect',capability:'workspace.archive.select',version:1,definition:capabilityDefinition('workspace.archive.select'),status:'Ready'});
 expect((screen.getByRole('button',{name:'Run action now'}) as HTMLButtonElement).disabled).toBe(false);
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',runId:workspaceId,call:{id:'workspace.archive.select'}}});
 await act(async()=>{client.cancel();});
});

it('starts the reviewed archive action through shared fields and follows its native job status separately',async()=>{
 const {client,screen,receive}=setup(true,['workspaceArchiveActivation.v1','execution.v1','actionResults.v1']);
 const receipt=nativeActivation.execution.workspace.selected;
 if(!validExecutionView(nativeActivation.execution))throw new Error('Invalid native activation execution');
 const issued={...nativeActivation.execution,workspace:{selected:null,running:[],outcomes:[],nextRunId:receipt.id,storageError:null}};
 await receive(undefined,false,{execution:issued});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const action=capabilityDefinition('workspace.archive.activate')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:action.id,version:1,label:action.label}],status:'Select activation'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(action.label)}));await receive({operation:'inspect',capability:action.id,version:1,definition:action,status:'Review exact selection'});
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(receipt.call.arguments)}});fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}});
 await receive(undefined,false,{execution:nativeActivation.execution});
 expect(screen.getByLabelText('Action result').textContent).toContain(nativeActivation.activation.requestId);
 expect(screen.getByLabelText('Action result').textContent).not.toContain('committedRevision');
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const fact=behaviourFact('workspace.archive.activation')!;
 await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:fact.id,version:1,label:fact.label}],status:'Inspect activation'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(fact.label)}));await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,available:false,value:null,status:'Choose request'});
 fireEvent.change(screen.getByLabelText('Fact inputs requestId'),{target:{value:nativeActivation.activation.requestId}});fireEvent.click(screen.getByRole('button',{name:'Read fact'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:fact.id,version:1,arguments:{requestId:nativeActivation.activation.requestId}}});
 await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,arguments:{requestId:nativeActivation.activation.requestId},available:true,value:nativeActivation.activation,status:'Available'});
 expect(screen.getByLabelText('Current fact value').textContent).toContain(nativeActivation.activation.committedRevision);
 expect(screen.getByLabelText('Current fact value').textContent).toContain('review');expect(client.snapshot().request).toBeNull();
 act(()=>client.cancel());
});

it('submits the exact inspected review through shared inputs and reads its completion as a separate fact',async()=>{
 const {client,screen,receive}=setup(true,['workspaceReview.v1','execution.v1','actionResults.v1']);
 const receipt=nativeReview.execution.workspace.selected;
 if(!validExecutionView(nativeReview.execution))throw new Error('Invalid native review execution');
 const issued={...nativeReview.execution,workspace:{selected:null,running:[],outcomes:[],nextRunId:receipt.id,storageError:null}};
 await receive(undefined,false,{execution:issued});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const action=capabilityDefinition('workspace.review.complete')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:action.id,version:1,label:action.label}],status:'Inspect review'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(action.label)}));await receive({operation:'inspect',capability:action.id,version:1,definition:action,status:'Confirm exact inspected content'});
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(receipt.call.arguments)}});fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}});
 await receive(undefined,false,{execution:nativeReview.execution});
 expect(screen.getByLabelText('Action result').textContent).toContain(nativeReview.completed.requestId);
 expect(screen.getByLabelText('Action result').textContent).not.toContain('committedRevision');
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const fact=behaviourFact('workspace.review')!;
 await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:fact.id,version:1,label:fact.label}],status:'Inspect completion'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(fact.label)}));await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,available:false,value:null,status:'Choose request'});
 fireEvent.change(screen.getByLabelText('Fact inputs requestId'),{target:{value:nativeReview.completed.requestId}});fireEvent.click(screen.getByRole('button',{name:'Read fact'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:fact.id,version:1,arguments:{requestId:nativeReview.completed.requestId}}});
 await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,arguments:{requestId:nativeReview.completed.requestId},available:true,value:nativeReview.completed,status:'Available'});
 expect(screen.getByLabelText('Current fact value').textContent).toContain(nativeReview.completed.committedRevision);
 expect(screen.getByLabelText('Current fact value').textContent).toContain('completed');expect(client.snapshot().request).toBeNull();
 act(()=>client.cancel());
});

it('selects an exact previous workspace through ordinary action inputs and follows its verified preview',async()=>{
 const {client,screen,receive}=setup(true,['workspacePrevious.v1','execution.v1','actionResults.v1']);
 const receipt=nativePrevious.selectionExecution.workspace.selected;
 if(!validExecutionView(nativePrevious.selectionExecution))throw new Error('Invalid native previous-workspace execution');
 const issued={...nativePrevious.selectionExecution,workspace:{selected:null,running:[],outcomes:[],nextRunId:receipt.id,storageError:null}};
 await receive(undefined,false,{execution:issued});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const action=capabilityDefinition('workspace.previous.select')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:action.id,version:1,label:action.label}],status:'Inspect previous'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(action.label)}));await receive({operation:'inspect',capability:action.id,version:1,definition:action,status:'Select exact previous identity'});
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(receipt.call.arguments)}});fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}});
 await receive(undefined,false,{execution:nativePrevious.selectionExecution});
 expect(screen.getByLabelText('Action result').textContent).toContain(nativePrevious.selection.requestId);
 expect(screen.getByLabelText('Action result').textContent).not.toContain(nativePrevious.selection.generationId);
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const fact=behaviourFact('workspace.archive.selection')!;
 await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:fact.id,version:1,label:fact.label}],status:'Inspect preview'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(fact.label)}));await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,available:false,value:null,status:'Choose request'});
 fireEvent.change(screen.getByLabelText('Fact inputs requestId'),{target:{value:nativePrevious.selection.requestId}});fireEvent.click(screen.getByRole('button',{name:'Read fact'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:fact.id,version:1,arguments:{requestId:nativePrevious.selection.requestId}}});
 await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,arguments:{requestId:nativePrevious.selection.requestId},available:true,value:nativePrevious.selection,status:'Available'});
 expect(screen.getByLabelText('Current fact value').textContent).toContain(nativePrevious.selection.generationId);
 expect(screen.getByLabelText('Current fact value').textContent).toContain('prepared');expect(client.snapshot().request).toBeNull();
 act(()=>client.cancel());
});

it('commits the exact recovery preview and reads preservation and review status through the shared catalog',async()=>{
 const {client,screen,receive}=setup(true,['workspaceRecovery.v1','execution.v1','actionResults.v1']);
 const receipt=nativeWorkspaceRecovery.opening.workspace.selected;
 if(!validExecutionView(nativeWorkspaceRecovery.opening))throw new Error('Invalid native recovery execution');
 await receive(undefined,false,{execution:{...nativeWorkspaceRecovery.opening,workspace:{selected:null,running:[],outcomes:[],nextRunId:receipt.id,storageError:null}}});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const action=capabilityDefinition('workspace.recovery.commit')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:action.id,version:1,label:action.label}],status:'Inspect recovery'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(action.label)}));await receive({operation:'inspect',capability:action.id,version:1,definition:action,status:'Confirm exact preserved recovery'});
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(receipt.call.arguments)}});fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}});
 await receive(undefined,false,{execution:nativeWorkspaceRecovery.opening});
 expect(screen.getByLabelText('Action result').textContent).toContain(nativeWorkspaceRecovery.completed.requestId);
 expect(screen.getByLabelText('Action result').textContent).not.toContain(nativeWorkspaceRecovery.completed.evidenceHash);
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const fact=behaviourFact('workspace.recovery')!;
 await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:fact.id,version:1,label:fact.label}],status:'Inspect completion'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(fact.label)}));
 await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,arguments:{},available:true,value:nativeWorkspaceRecovery.completed,status:'Available'});
 const displayed=screen.getByLabelText('Current fact value').textContent;
 for(const value of [nativeWorkspaceRecovery.completed.requestId,nativeWorkspaceRecovery.completed.evidenceHash,nativeWorkspaceRecovery.completed.committedRevision,'review'])expect(displayed).toContain(value);
 expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});

it('chooses a fresh recovery preview explicitly without carrying the retained candidate identity',async()=>{
 const {client,screen,receive}=setup(true,['workspaceRecovery.v1','execution.v1','actionResults.v1']);
 const receipt=nativeFreshRecovery.opening.workspace.selected;
 if(!validExecutionView(nativeFreshRecovery.opening))throw new Error('Invalid native fresh recovery execution');
 await receive(undefined,false,{execution:{...nativeFreshRecovery.opening,workspace:{selected:null,running:[],outcomes:[],nextRunId:receipt.id,storageError:null}}});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const definition=capabilityDefinition('workspace.recovery.select')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Choose recovery source'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Prepare the requested source'});
 const base={requestId:nativeFreshRecovery.completed.requestId,originHash:nativeFreshRecovery.completed.originHash};
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify({...base,source:{kind:'retained',generationId:nativeWorkspaceRecovery.candidate.generationId,manifestHash:nativeWorkspaceRecovery.candidate.manifestHash}})}});
 fireEvent.change(screen.getByLabelText('Recovery source'),{target:{value:'1'}});
 expect(JSON.parse((screen.getByLabelText('Action arguments') as HTMLTextAreaElement).value)).toEqual({...base,source:{kind:'fresh'}});
 expect(client.snapshot().request).toBeNull();fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',call:{id:'workspace.recovery.select',version:1,arguments:{...base,source:{kind:'fresh'}}}}});
 act(()=>client.cancel());
});


it('uses the generated history reset form and reports preserved evidence separately from workspace recovery',async()=>{
 const {client,screen,receive}=setup(true,['workspaceHistory.v1','execution.v1','actionResults.v1']);
 const receipt=nativeHistory.resetExecution.workspace.selected;
 if(!validExecutionView(nativeHistory.resetExecution))throw new Error('Invalid native history repair execution');
 await receive(undefined,false,{execution:{...nativeHistory.resetExecution,workspace:{selected:null,running:[],outcomes:[],nextRunId:receipt.id,storageError:null}}});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const definition=capabilityDefinition('workspace.history.reset')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Inspect history reset'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Reset inspected history'});
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(receipt.call.arguments)}});fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}});
 await receive(undefined,false,{execution:nativeHistory.resetExecution});
 expect(screen.getByLabelText('Action result').textContent).toContain(nativeHistory.reset.evidenceId);
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const fact=behaviourFact('workspace.history')!;
 await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:fact.id,version:1,label:fact.label}],status:'Inspect reset outcome'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(fact.label)}));await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,arguments:{},available:true,value:nativeHistory.status,status:'Available'});
 const value=screen.getByLabelText('Current fact value').textContent;
 for(const text of [nativeHistory.reset.requestId,nativeHistory.reset.evidenceId,'reset','recovery'])expect(value).toContain(text);
 expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});

it('shows the published evidence result and submits removal with its exact native export receipt',async()=>{
 const {client,screen,receive}=setup(true,['workspaceEvidence.v1','execution.v1','actionResults.v1']);
 const receipt=nativeEvidence.removeExecution.workspace.selected;
 if(!validExecutionView(nativeEvidence.exportExecution)||!validExecutionView(nativeEvidence.removeExecution))throw new Error('Invalid native evidence execution');
 await receive(undefined,false,{execution:{...nativeEvidence.exportExecution,workspace:{...nativeEvidence.exportExecution.workspace,nextRunId:receipt.id}}});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));
 expect(screen.getByLabelText('Action result').textContent).toContain(nativeEvidence.export.location);
 expect(screen.getByLabelText('Action result').textContent).toContain(nativeEvidence.export.archiveHash);
 fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const definition=capabilityDefinition('workspace.evidence.remove')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Inspect evidence removal'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Remove the requested exported evidence'});
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(receipt.call.arguments)}});fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}});
 await receive(undefined,false,{execution:nativeEvidence.removeExecution});
 expect(screen.getByLabelText('Action result').textContent).toContain('true');
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const fact=behaviourFact('workspace.evidence')!;
 await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:fact.id,version:1,label:fact.label}],status:'Inspect actual removal'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(fact.label)}));await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,arguments:{},available:true,value:nativeEvidence.status,status:'Available'});
 const value=screen.getByLabelText('Current fact value').textContent;
 expect(value).toContain(receipt.id);expect(value).toContain('removed');expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});


it('exports an inspected retained workspace using shared forms and displays portable identity and exclusions',async()=>{
 const {client,screen,receive}=setup(true,['workspaceRetention.v1','execution.v1','actionResults.v1']);
 const receipt=nativeRetention.exportExecution.workspace.selected;
 if(!validExecutionView(nativeRetention.exportExecution))throw new Error('Invalid native retained export execution');
 await receive(undefined,false,{execution:{...nativeRetention.inspectExecution,workspace:{selected:null,running:[],outcomes:[],nextRunId:receipt.id,storageError:null}}});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const definition=capabilityDefinition('workspace.retention.export')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Inspect retained export'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Export inspected inactive content'});
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(receipt.call.arguments)}});fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}});
 await receive(undefined,false,{execution:nativeRetention.exportExecution});
 const output=screen.getByLabelText('Action result').textContent;
 for(const value of [nativeRetention.export.location,nativeRetention.export.originalManifestHash,nativeRetention.export.manifestHash,nativeRetention.export.archiveHash,'excludedFiles'])expect(output).toContain(value);
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:'Search'}));
 const fact=behaviourFact('workspace.retention.entry')!;
 await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:fact.id,version:1,label:fact.label}],status:'Inspect retained metadata'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(fact.label)}));
 await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,arguments:{inspectionId:nativeRetention.inspection.inspectionId,index:0},available:true,value:nativeRetention.entry,status:'Available'});
 expect(screen.getByLabelText('Current fact value').textContent).toContain(nativeRetention.entry.generationId);
 expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});

import nativeRemoval from '../../../test-fixtures/browser/workspaceRemoval.json';
it('requires confirmation of the exact disposal call and clears it on edits or cancellation',async()=>{
 const {client,screen,receive}=setup(true,['workspaceDisposal.v1','execution.v1','actionResults.v1']);
 const receipt=nativeRemoval.removeExecution.workspace.selected;
 if(!validExecutionView(nativeRemoval.previewExecution)||!validExecutionView(nativeRemoval.removeExecution))throw new Error('Invalid native removal execution');
 await receive(undefined,false,{execution:{...nativeRemoval.previewExecution,workspace:{...nativeRemoval.previewExecution.workspace,nextRunId:receipt.id}}});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));
 expect(screen.getByLabelText('Action result').textContent).toContain(nativeRemoval.preview.fingerprint);
 fireEvent.click(screen.getByRole('button',{name:'Search'}));const definition=capabilityDefinition('workspace.retention.remove')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Inspect disposal'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Confirm permanent discard'});
 expect(screen.queryByRole('button',{name:'Add first block to draft'})).toBeNull();
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(receipt.call.arguments)}});
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));expect(client.snapshot().request).toBeNull();
 expect(screen.getByRole('region',{name:'Confirm permanent action'}).textContent).toContain('No Undo');
 fireEvent.click(screen.getByRole('button',{name:'Cancel confirmation'}));expect(screen.queryByRole('button',{name:'Confirm permanent action'})).toBeNull();expect(client.snapshot().request).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify({...receipt.call.arguments,fingerprint:'a'.repeat(64)})}});
 expect(screen.queryByRole('button',{name:'Confirm permanent action'})).toBeNull();
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(receipt.call.arguments)}});
 expect(screen.queryByRole('button',{name:'Confirm permanent action'})).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));fireEvent.click(screen.getByRole('button',{name:'Confirm permanent action'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}});
 await receive(undefined,false,{execution:nativeRemoval.removeExecution});expect(screen.getByLabelText('Action result').textContent).toContain('true');
 expect(screen.queryByRole('button',{name:'Confirm permanent action'})).toBeNull();expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});

import nativeAuthoring from '../../../test-fixtures/browser/animationAuthoring.json';
it('authors motion from typed book fields and shows the native receipt without starting playback',async()=>{
 const {client,screen,receive}=setup(true,['animationAuthoring.v1','execution.v1','actionResults.v1']);
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 const definition=capabilityDefinition('animation.author')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found animation authoring'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));
 await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Authoring contract'});
 fireEvent.click(screen.getByText('Edit action fields'));
 fireEvent.change(screen.getByLabelText('Animation edit'),{target:{value:'0'}});
 fireEvent.change(screen.getByLabelText('Action inputs revision'),{target:{value:2}});
 fireEvent.change(screen.getByLabelText('Action inputs replace'),{target:{value:'true'}});
 fireEvent.change(screen.getByLabelText('Action inputs frames 1 scale'),{target:{value:1}});
 fireEvent.click(screen.getByRole('button',{name:'Clear Action inputs frames 1 joints'}));
 fireEvent.click(screen.getByRole('button',{name:'Add Action inputs frames entry'}));
 fireEvent.change(screen.getByLabelText('Action inputs frames 2 time'),{target:{value:.5}});
 fireEvent.change(screen.getByLabelText('Action inputs frames 2 scale'),{target:{value:1}});
 fireEvent.click(screen.getByRole('button',{name:'Clear Action inputs frames 2 joints'}));
 const args=JSON.parse((screen.getByLabelText('Action arguments') as HTMLTextAreaElement).value);
 expect(args).toMatchObject({operation:'frames',target:'maestro',revision:2,replace:true,frames:[{time:0,scale:1,joints:null},{time:.5,scale:1,joints:null}]});
 fireEvent.click(screen.getByRole('button',{name:'Check availability'}));
 const call={id:definition.id,version:1,arguments:args};
 expect(client.snapshot().request?.commands[0]).toMatchObject({catalog:{operation:'check',call}});
 await receive({operation:'check',call,valid:true,available:true,occupied:false,resources:['maestro'],status:'Ready now'});
 expect(screen.getByText('Ready now')).toBeTruthy();
 fireEvent.change(screen.getByLabelText('Action inputs frames 2 time'),{target:{value:1}});
 expect(screen.queryByText('Ready now')).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',call:{id:'animation.author',arguments:{frames:[{time:0},{time:1}]}}}});
 await receive(undefined,false,{execution:nativeAuthoring.execution as RoomAgentState['execution']});
 expect(screen.getByLabelText('Action result').textContent).toContain('"frames": 3');
 expect(screen.queryByRole('button',{name:/Stop action/})).toBeNull();
 fireEvent.change(screen.getByLabelText('Animation edit'),{target:{value:'3'}});
 expect(Array.from((screen.getByLabelText('Action inputs target') as HTMLSelectElement).options).map(x=>x.value)).toEqual(['maestro']);
 expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});

import nativeRecording from '../../../test-fixtures/browser/recordingSessions.json';
it('starts and finishes an exact recording session from typed fields while distinguishing the live take from its receipt',async()=>{
 const {client,screen,receive}=setup(true,['animationRecording.v1','execution.v1','actionResults.v1']);
 await receive(undefined,false,{execution:{...nativeRecording.start,selected:null,running:[],outcomes:[],nextRunId:nativeRecording.start.selected.id} as RoomAgentState['execution']});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 const definition=capabilityDefinition('animation.record')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found recorder'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Recorder definition'});
 fireEvent.click(screen.getByText('Edit action fields'));
 fireEvent.change(screen.getByLabelText('Action inputs sessionId'),{target:{value:nativeRecording.before.sessionId}});
 fireEvent.change(screen.getByLabelText('Action inputs revision'),{target:{value:nativeRecording.start.selected.call.arguments.revision}});
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativeRecording.start.selected.call,runId:nativeRecording.start.selected.id}});
 await receive(undefined,false,{execution:nativeRecording.start as RoomAgentState['execution']});
 expect(screen.getByLabelText('Selected action').textContent).toContain('completed');expect(screen.getByLabelText('Action result').textContent).toContain('"phase": "recording"');expect(screen.queryByRole('button',{name:/Stop action/})).toBeNull();
 fireEvent.change(screen.getByLabelText('Recording operation'),{target:{value:'1'}});
 expect(screen.queryByLabelText('Action inputs revision')).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativeRecording.finish.selected.call,runId:nativeRecording.finish.selected.id}});
 await receive(undefined,false,{execution:nativeRecording.finish as RoomAgentState['execution']});expect(screen.getByLabelText('Action result').textContent).toContain('"phase": "saved"');
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 const fact=behaviourFact('animation.recording')!;await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:fact.id,version:1,label:fact.label}],status:'Recorder fact'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(fact.label)}));await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,available:true,value:nativeRecording.idle,status:'New idle session'});
 expect(screen.getByLabelText('Current fact value').textContent).toContain(nativeRecording.idle.sessionId);expect(screen.getByLabelText('Current fact value').textContent).toContain('"phase":"idle"');expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});

import nativeAvatar from '../../../test-fixtures/browser/avatarSelection.json';
it('chooses an exact Maestro model from typed book fields and displays the ready native receipt',async()=>{
 const {client,screen,receive}=setup(true,['avatarModels.v1','execution.v1','actionResults.v1']);
 await receive(undefined,false,{execution:{...nativeAvatar.library,selected:null,running:[],outcomes:[],nextRunId:nativeAvatar.selection.selected.id} as RoomAgentState['execution']});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 const definition=capabilityDefinition('avatar.model.select')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found model selection'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Model contract'});
 fireEvent.click(screen.getByText('Edit action fields'));
 expect(Array.from((screen.getByLabelText('Action inputs target') as HTMLSelectElement).options).map(x=>x.value)).toEqual(['maestro']);
 fireEvent.change(screen.getByLabelText('Action inputs modelHash'),{target:{value:nativeAvatar.library.selected.output.entries[0].modelHash}});
 fireEvent.change(screen.getByLabelText('Action inputs revision'),{target:{value:nativeAvatar.before.revision}});
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativeAvatar.selection.selected.call,runId:nativeAvatar.selection.selected.id}});
 await receive(undefined,false,{execution:nativeAvatar.selection as RoomAgentState['execution']});
 expect(screen.getByLabelText('Action result').textContent).toContain(nativeAvatar.after.displayedHash);expect(screen.queryByRole('button',{name:/Stop action/})).toBeNull();
 fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 const fact=behaviourFact('avatar.model')!;await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:fact.id,version:1,label:fact.label}],status:'Model fact'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(fact.label)}));await receive({operation:'inspect',category:'facts',capability:fact.id,version:1,definition:fact,available:true,value:nativeAvatar.after,status:'Ready model'});
 expect(screen.getByLabelText('Current fact value').textContent).toContain('"phase":"ready"');expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});

import nativePosing from '../../../test-fixtures/browser/posingSessions.json';
it('shares native pose identities and versions through typed joint fields and a finish receipt',async()=>{
 const {client,screen,receive}=setup(true,['animationPosing.v1','execution.v1','actionResults.v1']);
 await receive(undefined,false,{execution:{...nativePosing.start,selected:null,running:[],outcomes:[],nextRunId:nativePosing.start.selected.id} as RoomAgentState['execution']});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 const definition=capabilityDefinition('animation.pose')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found posing'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Pose definition'});
 fireEvent.click(screen.getByText('Edit action fields'));
 fireEvent.change(screen.getByLabelText('Action inputs sessionId'),{target:{value:nativePosing.before.sessionId}});
 fireEvent.change(screen.getByLabelText('Action inputs revision'),{target:{value:nativePosing.before.revision}});
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativePosing.start.selected.call,runId:nativePosing.start.selected.id}});
 await receive(undefined,false,{execution:nativePosing.start as RoomAgentState['execution']});expect(screen.getByLabelText('Action result').textContent).toContain('"phase": "posing"');expect(screen.queryByRole('button',{name:/Stop action/})).toBeNull();
 fireEvent.change(screen.getByLabelText('Pose operation'),{target:{value:'1'}});fireEvent.change(screen.getByLabelText('Action inputs version'),{target:{value:nativePosing.active.version}});
 fireEvent.click(screen.getByText('Action inputs joints · 1 entries'));const joint=nativePosing.rotate.selected.call.arguments.joints[0];fireEvent.change(screen.getByLabelText('Action inputs joints 1 joint'),{target:{value:joint.joint}});
 for(const field of ['x','y','z','w'] as const)fireEvent.change(screen.getByLabelText('Action inputs joints 1 rotation '+field),{target:{value:joint.rotation[field]}});
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativePosing.rotate.selected.call,runId:nativePosing.rotate.selected.id}});
 await receive(undefined,false,{execution:nativePosing.rotate as RoomAgentState['execution']});fireEvent.change(screen.getByLabelText('Pose operation'),{target:{value:'3'}});fireEvent.change(screen.getByLabelText('Action inputs version'),{target:{value:nativePosing.edited.version}});
 expect(screen.queryByLabelText('Action inputs joints 1 joint')).toBeNull();fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativePosing.finish.selected.call,runId:nativePosing.finish.selected.id}});
 await receive(undefined,false,{execution:nativePosing.finish as RoomAgentState['execution']});expect(screen.getByLabelText('Action result').textContent).toContain('"phase": "saved"');expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});

import nativeModelImport from '../../../test-fixtures/browser/modelSelection.json';
it('chooses and accepts the exact native model preview through typed book fields',async()=>{
 const {client,screen,receive}=setup(true,['modelImport.v1','execution.v1','actionResults.v1']);
 await receive(undefined,false,{execution:{...nativeModelImport.select,selected:null,running:[],outcomes:[],nextRunId:nativeModelImport.select.selected.id} as RoomAgentState['execution']});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 const definition=capabilityDefinition('model.import')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Import definition'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Import definition'});
 fireEvent.click(screen.getByText('Edit action fields'));fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativeModelImport.select.selected.call,runId:nativeModelImport.select.selected.id}});
 await receive(undefined,false,{execution:nativeModelImport.select as RoomAgentState['execution']});
 fireEvent.change(screen.getByLabelText('Import operation'),{target:{value:'2'}});
 fireEvent.change(screen.getByLabelText('Action inputs requestId'),{target:{value:nativeModelImport.preview.requestId}});
 fireEvent.change(screen.getByLabelText('Action inputs modelHash'),{target:{value:nativeModelImport.preview.preview.modelHash}});
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativeModelImport.accept.selected.call,runId:nativeModelImport.accept.selected.id}});
 await receive(undefined,false,{execution:nativeModelImport.accept as RoomAgentState['execution']});expect(screen.getByLabelText('Action result').textContent).toContain(nativeModelImport.after.accepted.objectId);
 fireEvent.change(screen.getByLabelText('Import operation'),{target:{value:'3'}});expect(screen.getByLabelText('Action inputs revision')).toBeTruthy();
 fireEvent.change(screen.getByLabelText('Import operation'),{target:{value:'5'}});expect(screen.queryByLabelText('Action inputs revision')).toBeNull();expect(screen.queryByLabelText('Action inputs target')).toBeNull();act(()=>client.cancel());
});

import nativeMotionBatch from '../../../test-fixtures/browser/motionBatchImport.json';
it('shares animation batch selection, category and start through generated book fields',async()=>{
 const {client,screen,receive}=setup(true,['motionBatchImport.v1','execution.v1','actionResults.v1']);
 await receive(undefined,false,{execution:{...nativeMotionBatch.select,selected:null,running:[],outcomes:[],nextRunId:nativeMotionBatch.select.selected.id} as RoomAgentState['execution']});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));const definition=capabilityDefinition('motion.import.batch')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Batch definition'});fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));
 await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Batch definition'});fireEvent.click(screen.getByText('Edit action fields'));
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativeMotionBatch.select.selected.call,runId:nativeMotionBatch.select.selected.id}});
 await receive(undefined,false,{execution:nativeMotionBatch.select as RoomAgentState['execution']});fireEvent.change(screen.getByLabelText('Batch operation'),{target:{value:'1'}});
 fireEvent.change(screen.getByLabelText('Action inputs requestId'),{target:{value:nativeMotionBatch.ready.requestId}});fireEvent.change(screen.getByLabelText('Action inputs version'),{target:{value:nativeMotionBatch.ready.version}});fireEvent.change(screen.getByLabelText('Action inputs category'),{target:{value:'gesture'}});
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativeMotionBatch.category.selected.call,runId:nativeMotionBatch.category.selected.id}});
 await receive(undefined,false,{execution:nativeMotionBatch.category as RoomAgentState['execution']});fireEvent.change(screen.getByLabelText('Batch operation'),{target:{value:'2'}});expect(screen.queryByLabelText('Action inputs category')).toBeNull();
 fireEvent.change(screen.getByLabelText('Action inputs version'),{target:{value:nativeMotionBatch.tagged.version}});fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativeMotionBatch.start.selected.call,runId:nativeMotionBatch.start.selected.id}});
 await receive(undefined,false,{execution:nativeMotionBatch.start as RoomAgentState['execution']});expect(screen.getByLabelText('Action result').textContent).toContain('"phase": "running"');
 fireEvent.change(screen.getByLabelText('Batch operation'),{target:{value:'4'}});expect(screen.queryByLabelText('Action inputs version')).toBeNull();act(()=>client.cancel());
});

import nativeImportReadback from '../../../test-fixtures/browser/importReadback.json';
it('reads exact imported motion pages from generated fact inputs without reusing stale page values',async()=>{
 const {client,screen,receive}=setup(true,['modelImport.v1']);const definition=behaviourFact('model.import.motions')!;
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.change(screen.getByLabelText('Catalog category'),{target:{value:'facts'}});fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 await receive({operation:'search',category:'facts',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Motion pages'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',category:'facts',capability:definition.id,version:1,definition,arguments:definition.example,available:false,value:null,status:'Choose the import request'});
 fireEvent.change(screen.getByLabelText('Fact inputs requestId'),{target:{value:nativeImportReadback.summary.requestId}});
 for(const page of nativeImportReadback.pages){
  fireEvent.change(screen.getByLabelText('Fact inputs motionOffset'),{target:{value:page.arguments.motionOffset}});expect(screen.getByLabelText('Current fact value').textContent).not.toContain(page.value.motionIds[0]);fireEvent.click(screen.getByRole('button',{name:'Read fact'}));
  expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:definition.id,version:1,arguments:page.arguments}});
  await receive({operation:'inspect',category:'facts',capability:definition.id,version:1,definition,arguments:page.arguments,available:true,value:page.value,status:'Exact native motion page'});expect(screen.getByLabelText('Current fact value').textContent).toContain(page.value.motionIds[0]);
 }
 act(()=>client.cancel());
});

async function loadCurrentDraft(screen:ReturnType<typeof setup>['screen'],receive:ReturnType<typeof setup>['receive'],definition:CapabilityDefinition,value:unknown){
 const args=JSON.parse((screen.getByLabelText('Action arguments') as HTMLTextAreaElement).value);
 const query=currentInputRequest(definition.input,args);if(query.operation!=='inspect')throw new Error('Expected fact query');
 fireEvent.click(screen.getByRole('button',{name:'Load current values'}));
 await receive({...query,category:'facts',definition:behaviourFact(query.capability)!,available:true,value:value as DataValue,status:'Current native snapshot'});
}

import nativeController from '../../../test-fixtures/browser/controllerConfiguration.json';
it('edits independent sticks and binds a saved program through generated controller fields',async()=>{
 const {client,screen,receive}=setup(true,['controllerConfiguration.v1','execution.v1','actionResults.v1']);
 await receive(undefined,false,{execution:{...nativeController.movement,selected:null,running:[],outcomes:[],nextRunId:nativeController.movement.selected.id} as RoomAgentState['execution']});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));const definition=capabilityDefinition('controller.configure')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Controller definition'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Controller definition'});fireEvent.click(screen.getByText('Edit action fields'));
 fireEvent.change(screen.getByLabelText('Controller settings'),{target:{value:'1'}});
 await loadCurrentDraft(screen,receive,definition,nativeController.before);
 for(const key of ['deadZone','userSpeed','userStick'] as const)fireEvent.change(screen.getByLabelText('Action inputs '+key),{target:{value:nativeController.movement.selected.call.arguments[key]}});
 expect(screen.queryByLabelText('Action inputs programId')).toBeNull();fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativeController.movement.selected.call,runId:nativeController.movement.selected.id}});
 await receive(undefined,false,{execution:nativeController.movement as RoomAgentState['execution']});fireEvent.change(screen.getByLabelText('Controller settings'),{target:{value:'3'}});
 await loadCurrentDraft(screen,receive,definition,nativeController.afterMovement);
 for(const key of ['button','programId'] as const)fireEvent.change(screen.getByLabelText('Action inputs '+key),{target:{value:nativeController.button.selected.call.arguments[key]}});
 expect(screen.queryByLabelText('Action inputs userSpeed')).toBeNull();fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:nativeController.button.selected.call,runId:nativeController.button.selected.id}});
 await receive(undefined,false,{execution:nativeController.button as RoomAgentState['execution']});expect(screen.getByLabelText('Action result').textContent).toContain(nativeController.after.configurationId);act(()=>client.cancel());
});

import nativeModes from '../../../test-fixtures/browser/controllerModes.json';
it('uses generated live-mode fields and each current native identity without injecting movement',async()=>{
 const {client,screen,receive}=setup(true,['controllerModes.v1','execution.v1','actionResults.v1']);
 await receive(undefined,false,{execution:{...nativeModes.enable,selected:null,running:[],outcomes:[],nextRunId:nativeModes.enable.selected.id} as RoomAgentState['execution']});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));const definition=capabilityDefinition('controller.mode.set')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Live mode definition'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Live mode definition'});fireEvent.click(screen.getByText('Edit action fields'));
 let current=nativeModes.before;
 for(const view of [nativeModes.enable,nativeModes.virtualView,nativeModes.user,nativeModes.mixed]){
  await loadCurrentDraft(screen,receive,definition,current);
  for(const key of ['operation'] as const)fireEvent.change(screen.getByLabelText('Action inputs '+key),{target:{value:view.selected.call.arguments[key]}});
  fireEvent.click(screen.getByRole('button',{name:'Run action now'}));expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:view.selected.call,runId:view.selected.id}});
  await receive(undefined,false,{execution:view as RoomAgentState['execution']});expect(screen.getByLabelText('Action result').textContent).toContain(view.selected.output.stateId);current=view.selected.output;
 }
 act(()=>client.cancel());
});

import nativeSpatial from '../../../test-fixtures/browser/spatialSettings.json';
import nativeWalkSettings from '../../../test-fixtures/browser/walkSettings.json';
it.each([
 {label:'object physics',view:nativeSpatial.physics},
 {label:'Maestro movement',view:nativeSpatial.movement},
 {label:'included walk',view:nativeSpatial.walk},
 {label:'embedded walk',view:nativeWalkSettings.embedded},
 {label:'library walk',view:nativeWalkSettings.library},
])('authors $label through native generated fields',async({view})=>{
 const {client,screen,receive,state}=setup(true,['spatialSettings.v1','execution.v1','actionResults.v1']);
 const object={...state.objects.find(o=>o.id==='maestro')!,id:nativeSpatial.beforePhysics.target,name:'Settings block',kind:'Block'};
 await receive(undefined,false,{objects:[...state.objects,object]});fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));
 await receive(undefined,false,{execution:{...view,selected:null,running:[],outcomes:[],nextRunId:view.selected.id} as RoomAgentState['execution']});
   const definition=capabilityDefinition(view.selected.call.id)!;fireEvent.click(screen.getByRole('button',{name:/^Search$/}));await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Settings definition'});
   fireEvent.click(screen.getByRole('button',{name:new RegExp('^'+definition.label+'\\s*'+definition.id+' · v1$')}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Settings definition'});fireEvent.click(screen.getByText('Edit action fields'));
   const args=view.selected.call.arguments;if('source' in args)fireEvent.change(screen.getByLabelText('Walking animation source'),{target:{value:String(['included','library','embedded'].indexOf(args.source))}});
   if('target' in args)fireEvent.change(screen.getByLabelText('Action inputs target'),{target:{value:args.target}});
   const before=view===nativeSpatial.physics?nativeSpatial.beforePhysics:view===nativeSpatial.movement?nativeSpatial.beforeMovement:view===nativeSpatial.walk?nativeSpatial.walkBeforeSave:{...nativeWalkSettings.before,revision:args.revision};
   await loadCurrentDraft(screen,receive,definition,before);
   for(const [key,value] of Object.entries(args)){if(key==='source'||key==='revision')continue;fireEvent.change(screen.getByLabelText('Action inputs '+key),{target:{value}});}
   fireEvent.click(screen.getByRole('button',{name:'Run action now'}));expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:view.selected.call,runId:view.selected.id}});
   await receive(undefined,false,{execution:view as RoomAgentState['execution']});expect(screen.getByLabelText('Action result').textContent).toContain(String(view.selected.output.revision));
 act(()=>client.cancel());
});

import nativeSimulation from '../../../test-fixtures/browser/physicsSimulation.json';
it('uses generated physics start and pause fields with each current native identity',async()=>{
 const {client,screen,receive}=setup(true,['physicsSimulation.v1','execution.v1','actionResults.v1']);
 await receive(undefined,false,{execution:{...nativeSimulation.start,selected:null,running:[],outcomes:[],nextRunId:nativeSimulation.start.selected.id} as RoomAgentState['execution']});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));const definition=capabilityDefinition('physics.simulation.set')!;
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Physics definition'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Physics definition'});fireEvent.click(screen.getByText('Edit action fields'));
 let current=nativeSimulation.before;
 for(const view of [nativeSimulation.start,nativeSimulation.pause]){
  await loadCurrentDraft(screen,receive,definition,current);
  for(const key of ['operation'] as const)fireEvent.change(screen.getByLabelText('Action inputs '+key),{target:{value:view.selected.call.arguments[key]}});
  fireEvent.click(screen.getByRole('button',{name:'Run action now'}));expect(client.snapshot().request?.commands[0]).toEqual({action:'execution',execution:{operation:'start',call:view.selected.call,runId:view.selected.id}});
  await receive(undefined,false,{execution:view as RoomAgentState['execution']});expect(screen.getByLabelText('Action result').textContent).toContain(view.selected.output.stateId);current=view.selected.output;
 }
 act(()=>client.cancel());
});

it('requires an explicit settings snapshot, preserves edits, invalidates changed targets and ignores a late read',async()=>{
 const {client,screen,receive,state}=setup(true,['spatialSettings.v1','execution.v1']);
 const definition=capabilityDefinition('object.physics.configure')!,before=nativeSpatial.beforePhysics;
 await receive(undefined,false,{objects:[...state.objects,{...state.objects[0],id:before.target,kind:'Block',name:'Settings block'}]});
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:'Search'}));
 await receive({operation:'search',query:'',offset:0,total:1,pageSize:6,entries:[{id:definition.id,version:1,label:definition.label}],status:'Found'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(definition.label)}));await receive({operation:'inspect',capability:definition.id,version:1,definition,status:'Action'});
 fireEvent.click(screen.getByText('Edit action fields'));
 expect((screen.getByRole('button',{name:'Run action now'}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.change(screen.getByLabelText('Action inputs target'),{target:{value:before.target}});
 await loadCurrentDraft(screen,receive,definition,before);
 const args=()=>JSON.parse((screen.getByLabelText('Action arguments') as HTMLTextAreaElement).value);
 expect(args()).toEqual({target:before.target,revision:before.revision,mode:before.mode,shape:before.shape,mass:before.mass});
 expect((screen.getByLabelText('Action inputs revision') as HTMLInputElement).readOnly).toBe(true);
 fireEvent.change(screen.getByLabelText('Action inputs mass'),{target:{value:2}});
 expect(args()).toMatchObject({revision:before.revision,mode:before.mode,shape:before.shape,mass:2});
 expect((screen.getByRole('button',{name:'Run action now'}) as HTMLButtonElement).disabled).toBe(false);
 const fact={operation:'inspect',category:'facts',capability:'object.physics.settings',version:1,definition:behaviourFact('object.physics.settings')!,arguments:{target:before.target},available:true,value:{...before,revision:before.revision+1},status:'Changed externally'} as const;
 await receive(fact);expect(args().revision).toBe(before.revision); // Observations never silently advance the guard.
 fireEvent.click(screen.getByRole('button',{name:'Load current values'}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:fact.capability,version:1,arguments:{target:before.target}}});
 const draft={...args(),mass:3}; // A competing draft edit must win even if an event is delivered to a disabled field.
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(draft)}});
 await receive(fact);expect(args()).toEqual(draft);
 fireEvent.change(screen.getByLabelText('Action inputs target'),{target:{value:'0'.repeat(32)}});
 expect((screen.getByRole('button',{name:'Run action now'}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.change(screen.getByLabelText('Action inputs target'),{target:{value:before.target}});
 fireEvent.click(screen.getByRole('button',{name:'Load current values'}));
 await receive({...fact,available:false,value:null,status:'Object is unavailable'});
 expect(screen.getByText('Object is unavailable')).toBeTruthy();expect(args()).toEqual(draft);
 fireEvent.click(screen.getByRole('button',{name:'Load current values'}));
 await receive(fact,false,{session:'f'.repeat(32)});
 expect(screen.queryByRole('button',{name:'Load current values'})).toBeNull();expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});
