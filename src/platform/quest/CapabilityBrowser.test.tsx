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
