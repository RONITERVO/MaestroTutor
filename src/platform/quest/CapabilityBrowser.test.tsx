// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {act,cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/catalogStates.json';
import nativeProgram from '../../../test-fixtures/browser/programBookState.json';
import {RoomWorkspace} from './RoomWorkspace';
import {RoomAgentClient} from './roomAgentBridge';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
import type {CatalogView} from '../../../shared/roomCatalog';
afterEach(cleanup);
function setup(){
 const client=new RoomAgentClient();let state=JSON.parse(JSON.stringify(nativeProgram)) as RoomAgentState;
 state={...state,capabilities:[...state.capabilities!,'catalog.v1'],catalog:null,visible:true,workspaceView:'rules',ack:0,revision:1};
 client.receive(state);const screen=render(<RoomWorkspace client={client}/>);
 const receive=async(catalog?:CatalogView,changed=false)=>{
  state={...state,ack:client.snapshot().request?.sequence??state.ack,revision:state.revision+1,catalog:catalog??state.catalog,
   rules:changed?{...state.rules!,revision:state.rules!.revision+1}:state.rules};
  await act(async()=>{expect(client.receive(state)).toBe(true);});
 };
 return {client,screen,receive,state};
}
it('searches, inspects, checks and adds the same named block to a preserved program draft',async()=>{
 const {client,screen,receive,state}=setup();const source=JSON.parse(state.rules!.selected!.program);
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'search',query:'',offset:0}});
 await receive(native.search.catalog as CatalogView);
 fireEvent.click(screen.getByRole('button',{name:/Recorded animation/}));await receive(native.inspect.catalog as CatalogView);
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(native.ready.catalog.call.arguments)}});
 fireEvent.click(screen.getByRole('button',{name:'Check availability'}));await receive(native.ready.catalog as CatalogView);
 expect(screen.getByText(/Ready now/)).toBeTruthy();
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify({...native.ready.catalog.call.arguments,seconds:2})}});
 expect(screen.queryByText(/Ready now/)).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Add first block to draft'}));expect(client.snapshot().request).toBeNull();
 expect(screen.getByRole('region',{name:'Function prime'})).toBeTruthy();
 fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 const sequence=client.snapshot().request!.commands[0].rule!.edits![0].sequence!;
 const saved=JSON.parse(sequence.program);expect(saved.functions[0].body[0]).toMatchObject({op:'invoke',capability:'animation.recording.play',arguments:{seconds:2}});
 expect(saved.functions[0].body.slice(1)).toEqual(source.functions[0].body);expect(saved.functions[1]).toEqual(source.functions[1]);
 expect(saved.resources).toContain(native.ready.catalog.call.arguments.target);
 await receive();act(()=>client.cancel());
});
it('refreshes readiness without another query and keeps catalog draft additions stale after a concurrent edit',async()=>{
 const {client,screen,receive}=setup();
 fireEvent.click(screen.getByRole('button',{name:'Action catalog'}));fireEvent.click(screen.getByRole('button',{name:/^Search$/}));
 await receive(native.search.catalog as CatalogView);fireEvent.click(screen.getByRole('button',{name:/Recorded animation/}));await receive(native.inspect.catalog as CatalogView);
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(native.ready.catalog.call.arguments)}});
 fireEvent.click(screen.getByRole('button',{name:'Check availability'}));await receive(native.ready.catalog as CatalogView);
 await receive(native.occupied.catalog as CatalogView,true);expect(screen.getByText(/running behaviour currently owns/)).toBeTruthy();expect(client.snapshot().request).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Add first block to draft'}));
 expect(screen.getByText(/draft is retained/)).toBeTruthy();expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true);
 expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});

import nativeExecutions from '../../../test-fixtures/browser/executionStates.json';
import {CapabilityBrowser} from './CapabilityBrowser';
it('starts and stops one exact action through the catalog and displays native phases without saving',async()=>{
 const client=new RoomAgentClient();let state=JSON.parse(JSON.stringify(nativeExecutions.running)) as RoomAgentState;
 state={...state,revision:1,ack:0,execution:{selected:null,running:[],outcomes:[]}};
 client.receive(state);const screen=render(<CapabilityBrowser client={client} onClose={()=>{}}/>);
 const receive=async(more:Partial<RoomAgentState>)=>{state={...state,...more,revision:state.revision+1,ack:client.snapshot().request?.sequence??state.ack};await act(async()=>{expect(client.receive(state)).toBe(true);});};
 fireEvent.click(screen.getByRole('button',{name:/^Search$/}));await receive({catalog:native.search.catalog as CatalogView});
 fireEvent.click(screen.getByRole('button',{name:/Recorded animation/}));await receive({catalog:native.inspect.catalog as CatalogView});
 const call=nativeExecutions.running.execution.selected.call;
 fireEvent.change(screen.getByLabelText('Action arguments'),{target:{value:JSON.stringify(call.arguments)}});
 fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(client.snapshot().request!.commands).toEqual([{action:'execution',execution:{operation:'start',call}}]);
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
