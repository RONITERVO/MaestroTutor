// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {act,cleanup,fireEvent,render,waitFor} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import {RoomWorkspace} from './RoomWorkspace';
import {RoomAgentClient} from './roomAgentBridge';
import {capabilityDefinition,capabilityResources} from '../../../shared/capabilities';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
import type {ConstructionSelection} from '../../../shared/roomSelection';
import native from '../../../test-fixtures/browser/programBookState.json';
afterEach(cleanup);
function setup(){
 const client=new RoomAgentClient(),objects=[{...native.objects.find(o=>o.id!=='book'&&o.id!=='maestro')!,id:'a'.repeat(32),name:'Brick',objectRevision:20},{...native.objects[0],id:'b'.repeat(32),name:'Brick',kind:'Block',objectRevision:21}],selection={stateId:'c'.repeat(32),members:[],collecting:false};
 let state={...structuredClone(native),session:'d'.repeat(32),ack:0,revision:1,visible:true,workspaceView:'objects',inspection:null,objects,constructionSelection:selection,capabilities:[...new Set([...native.capabilities,'constructionSelection.v1','constructionCapture.v1','moduleLibrary.v1','creationPrototypes.v1','catalog.v1','catalogVocabulary.v1','factQueries.v1'])]} as RoomAgentState;
 expect(client.receive(state)).toBe(true);const screen=render(<RoomWorkspace client={client}/>);fireEvent.click(screen.getByText('Construction pieces · 0 selected'));
 const receive=async(more:Partial<RoomAgentState>={})=>{state={...state,...more,revision:state.revision+1,ack:client.snapshot().request?.sequence??state.ack};await act(async()=>{expect(client.receive(state)).toBe(true);});};
 const acknowledge=async(selection:ConstructionSelection)=>{
  const request=client.snapshot().request!.commands[0].execution!;if(request.operation!=='start')throw Error('Expected shared action');
  const summary={id:request.runId!,capability:request.call.id,version:1,resources:capabilityResources(request.call.id,request.call.arguments),phase:'completed' as const,status:'Selection changed',output:{...selection}};
  await receive({ok:true,status:'Selection changed',constructionSelection:selection,execution:{...state.execution!,nextRunId:crypto.randomUUID().replace(/-/g,''),selected:{...summary,call:request.call},outcomes:[summary]}});
 };
 return {client,screen,receive,acknowledge,objects};
}
it('uses shared selection calls, supports locate/order, and opens an editable capture draft without starting it',async()=>{
 const {client,screen,receive,acknowledge,objects}=setup();fireEvent.click(screen.getByLabelText('Include Brick · aaaaaaaa'));
 expect(client.snapshot().request?.commands[0].execution).toMatchObject({operation:'start',call:{id:'room.selection.set',arguments:{stateId:'c'.repeat(32),members:[objects[0].id],collecting:false}}});
 await acknowledge({stateId:'e'.repeat(32),members:[objects[0].id],collecting:false});fireEvent.click(screen.getByLabelText('Include Brick · bbbbbbbb'));await acknowledge({stateId:'f'.repeat(32),members:objects.map(o=>o.id),collecting:false});
 fireEvent.click(screen.getByRole('button',{name:'Locate Brick · bbbbbbbb'}));expect(client.snapshot().request!.commands).toEqual([{action:'inspect',target:objects[1].id}]);await receive();
 fireEvent.click(screen.getByRole('button',{name:'Move Brick · bbbbbbbb earlier'}));await acknowledge({stateId:'1'.repeat(32),members:[objects[1].id,objects[0].id],collecting:false});
 fireEvent.click(screen.getByRole('button',{name:'Review reusable construction'}));await waitFor(()=>expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'inspect',capability:'program.module.captureConstruction',version:1}}));
 const definition=capabilityDefinition('program.module.captureConstruction')!;await receive({catalog:{operation:'inspect',capability:definition.id,version:1,definition,status:'Capture ready to edit'}});
 const args=JSON.parse((screen.getByLabelText('Action arguments') as HTMLTextAreaElement).value);expect(args.members).toEqual([{target:objects[1].id,revision:21,slot:'piece_1'},{target:objects[0].id,revision:20,slot:'piece_2'}]);expect(client.snapshot().request).toBeNull();expect((screen.getByRole('button',{name:'Run action now'}) as HTMLButtonElement).disabled).toBe(true);client.cancel();
});
it('keeps native selection authoritative when a change fails and clears only through an explicit action',async()=>{
 const {client,screen,receive,acknowledge,objects}=setup();fireEvent.click(screen.getByRole('button',{name:'Collect in room'}));await acknowledge({stateId:'e'.repeat(32),members:[],collecting:true});await receive({constructionSelection:{stateId:'f'.repeat(32),members:[objects[0].id],collecting:true}});expect(screen.getByRole('button',{name:'Finish collecting'})).toBeTruthy();
 fireEvent.click(screen.getByLabelText('Include Brick · bbbbbbbb'));await receive({ok:false,status:'Construction selection changed; read it again'});expect(screen.getByRole('alert').textContent).toContain('changed');expect((screen.getByLabelText('Include Brick · bbbbbbbb') as HTMLInputElement).checked).toBe(false);
 fireEvent.click(screen.getByRole('button',{name:'Clear pieces'}));expect(client.snapshot().request?.commands[0].execution).toMatchObject({call:{arguments:{stateId:'f'.repeat(32),members:[],collecting:false}}});await acknowledge({stateId:'1'.repeat(32),members:[],collecting:false});act(()=>client.cancel());
});

it('shows the solid move handle through the shared action and disables edits during a grip',async()=>{
 const {client,screen,receive,objects}=setup();const selection={stateId:'c'.repeat(32),members:objects.map(o=>o.id),collecting:false};
 await receive({constructionSelection:selection,constructionManipulation:{stateId:selection.stateId,visible:false,holding:false,error:''},capabilities:[...native.capabilities,'constructionSelection.v1','constructionManipulation.v1','constructionCapture.v1']});
 fireEvent.click(screen.getByRole('button',{name:'Move together in room'}));const request=client.snapshot().request!.commands[0].execution!;expect(request).toMatchObject({operation:'start',call:{id:'room.selection.manipulate',arguments:{stateId:selection.stateId,members:selection.members,visible:true}}});
 if(request.operation!=='start')throw Error('Expected native invocation');const output={stateId:selection.stateId,visible:true,holding:false,error:''},summary={id:request.runId!,capability:request.call.id,version:1,resources:selection.members,phase:'completed' as const,status:'Handle ready',output};
 await receive({constructionManipulation:output,execution:{nextRunId:'1'.repeat(32),running:[],outcomes:[summary],selected:{...summary,call:request.call},storageError:null}});expect(screen.getByRole('button',{name:'Hide move handle'})).toBeTruthy();
 await receive({constructionManipulation:{...output,holding:true}});expect((screen.getByRole('button',{name:'Hide move handle'}) as HTMLButtonElement).disabled).toBe(true);expect((screen.getByRole('button',{name:'Clear pieces'}) as HTMLButtonElement).disabled).toBe(true);expect(screen.getByText('Arranging pieces. Release the handle to save one edit.')).toBeTruthy();client.cancel();
});

it('opens shared grip snapping settings for review without executing or guessing current settings',async()=>{
 const {client,screen,receive}=setup();await receive({capabilities:[...native.capabilities,'constructionSelection.v1','constructionSnapping.v1']});
 fireEvent.click(screen.getByRole('button',{name:'Review grip snapping'}));
 await waitFor(()=>expect(client.snapshot().request?.commands[0]).toEqual({action:'catalog',catalog:{operation:'inspect',capability:'room.selection.snapSettings',version:1}}));
 const definition=capabilityDefinition('room.selection.snapSettings')!;await receive({catalog:{operation:'inspect',capability:definition.id,version:1,definition,status:'Snapping settings ready'}});
 expect(screen.getByRole('button',{name:'Run action now'})).toBeTruthy();expect((screen.getByRole('button',{name:'Run action now'}) as HTMLButtonElement).disabled).toBe(true);
 expect(client.snapshot().request).toBeNull();act(()=>client.cancel());
});
