// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import {act,cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {RoomAgentClient} from './roomAgentBridge';
import {CapabilityBrowser} from './CapabilityBrowser';
import {ConstructionSelectionControls} from './ConstructionSelectionControls';
import {roomRevisionBasis,roomCommandConditions} from '../../core-sdk/room/roomObjectContext';
import type {RoomAgentState,RoomCommand} from '../../core-sdk/room/roomAgent';
import {capabilityDefinition} from '../../../shared/capabilities';
import {objectReferenceRequest,readObjectReference} from '../../../shared/objectReference';
import type {CatalogView} from '../../../shared/roomCatalog';
import native from '../../../test-fixtures/browser/objectReferences.json';
const definitions=native.definitions.filter(d=>!['book','maestro'].includes(d.request.arguments.target)).slice(0,2);
const ids=definitions.map(d=>d.request.arguments.target),views=definitions.map(d=>d.view as unknown as CatalogView),reads=views.map((v,i)=>readObjectReference(v,ids[i])!);
const basis=reads.map(r=>({id:r.id,objectRevision:r.revision}));
const state=(extra:Partial<RoomAgentState>={}):RoomAgentState=>({version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',created:[],canUndo:false,canRedo:false,physicsRunning:false,visible:true,objects:[],constructionSelection:{stateId:'b'.repeat(32),members:[],collecting:false},capabilities:['catalog.v1','catalogVocabulary.v1','factQueries.v1','structuredValues.v1','execution.v1','executionReceipts.v1','objectEdits.v1','constructionSelection.v1','constructionCapture.v1','moduleLibrary.v1','creationPrototypes.v1','actionResults.v1'],execution:{nextRunId:'c'.repeat(32),selected:null,running:[],outcomes:[],recovery:null,storageError:null},...extra});
const clients:RoomAgentClient[]=[];const make=(extra:Partial<RoomAgentState>={})=>{const c=new RoomAgentClient();clients.push(c);expect(c.receive(state(extra))).toBe(true);return c;};
afterEach(()=>{cleanup();clients.splice(0).forEach(c=>c.cancel());});
async function reply(c:RoomAgentClient,extra:Partial<RoomAgentState>={}){const current=c.getSnapshot().state!;await act(async()=>{expect(c.receive({...current,revision:current.revision+1,ack:c.snapshot().request?.sequence??current.ack,...extra})).toBe(true);});}
const deletes:RoomCommand[]=ids.map(target=>({action:'delete',target}));
it('combines exact native definition reads within one reviewed scope and never aliases inventory',()=>{
 const earlier=state({catalog:views[0]}),latest=state({revision:2,catalog:views[1]});expect(roomRevisionBasis(latest,[earlier])).toEqual(basis);expect(latest.objects).toEqual([]);
 const unavailable={...views[0],available:false,value:null} as CatalogView;expect(roomRevisionBasis(state({catalog:unavailable}),[earlier,latest])).toEqual([basis[1]]);
 expect(()=>roomRevisionBasis({...latest,session:'f'.repeat(32)},[earlier])).toThrow('another room');expect(()=>roomRevisionBasis({...latest,temporaryRoom:{active:true,pending:false,id:'d'.repeat(32),saveId:'e'.repeat(32),phase:'ready',error:'',savedRevision:0}},[earlier])).toThrow('another room');
});
it('prepares only missing targets and preserves the already reviewed revision when newer observations arrive',async()=>{
 const c=make(),reviewed=state(),known={...basis[0],objectRevision:basis[0].objectRevision-1};const result=c.requestPrepared(deletes,reviewed,[known]);expect(c.snapshot().request!.commands).toEqual([{action:'catalog',catalog:objectReferenceRequest(ids[1])}]);expect(c.snapshot().request!.conditions).toEqual([]);
 await reply(c,{catalog:views[1]});expect(c.snapshot().request!.commands).toEqual(deletes);expect(c.snapshot().request!.conditions).toEqual([{id:ids[0],revision:known.objectRevision},{id:ids[1],revision:reads[1].revision}]);await reply(c,{ok:false,status:'Target changed'});expect((await result).ok).toBe(false);
});
it('never sends a partial edit when a later exact-object read fails',async()=>{
 const c=make(),outcome=c.requestPrepared(deletes).catch(e=>e);expect(c.snapshot().request!.commands[0].catalog).toEqual(objectReferenceRequest(ids[0]));await reply(c,{catalog:views[0]});expect(c.snapshot().request!.commands[0].catalog).toEqual(objectReferenceRequest(ids[1]));await reply(c,{ok:false,status:'Object unavailable',catalog:null});expect((await outcome).message).toContain('Object unavailable');expect(c.snapshot().request).toBeNull();
});
it('does not continue preparation across a temporary-room boundary',async()=>{
 const c=make(),outcome=c.requestPrepared(deletes).catch(e=>e);await reply(c,{catalog:views[0],temporaryRoom:{active:true,pending:false,id:'d'.repeat(32),saveId:'e'.repeat(32),phase:'ready',error:'',savedRevision:0}});expect((await outcome).message).toContain('session changed');expect(c.snapshot().request).toBeNull();
});
it('refuses mismatched definition replies and reads neither aliases nor deleted receipt targets',async()=>{
 const c=make(),outcome=c.requestPrepared(deletes).catch(e=>e);await reply(c,{catalog:views[1]});expect((await outcome).message).toContain('cannot be read');expect(c.snapshot().request).toBeNull();
 const commands:RoomCommand[]=[{action:'create',reference:'new_box',name:'Box',kind:'block'},{action:'paint',target:'new_box',color:{r:1,g:0,b:0,a:1}}];const prepared=c.prepare(commands);expect(prepared).not.toBeInstanceOf(Promise);expect(c.snapshot().request).toBeNull();
 const old=c.requestPrepared([{action:'execution',execution:{operation:'start',runId:'e'.repeat(32),call:{id:'object.delete',version:1,arguments:{target:ids[0]}}}}]);expect(c.snapshot().request!.commands[0].action).toBe('execution');expect(c.snapshot().request!.conditions).toEqual([{id:ids[0],revision:0}]);await reply(c,{ok:false,status:'Old receipt unavailable'});await old;
});
it('uses reviewed nested capture reads after the periodic inventory changes, without rereading or substituting guards',async()=>{
 const c=make(),definition=capabilityDefinition('program.module.captureConstruction')!,members=ids.map((target,i)=>({target,revision:0,slot:'piece_'+i}));const screen=render(<CapabilityBrowser client={c} onClose={()=>{}} initialCall={{id:definition.id,version:1,arguments:{name:'Pieces',members}}}/>);
 await reply(c,{catalog:{operation:'inspect',capability:definition.id,version:1,definition,status:'Capture'}});fireEvent.click(screen.getByRole('button',{name:'Load current values'}));expect(c.snapshot().request!.commands[0].catalog).toEqual(objectReferenceRequest(ids[0]));await reply(c,{catalog:views[0]});await reply(c,{catalog:views[1]});
 await reply(c,{objects:[{id:ids[0],objectRevision:reads[0].revision+20,name:'Changed',kind:'Block',position:{x:0,y:0,z:0},scale:1,color:{r:1,g:1,b:1,a:1},animated:false}]});fireEvent.click(screen.getByRole('button',{name:'Run action now'}));
 expect(c.snapshot().request!.commands[0].action).toBe('execution');expect(c.snapshot().request!.conditions).toEqual(basis.map(({id,objectRevision})=>({id,revision:objectRevision})));expect(c.snapshot().request!.commands[0].execution).toMatchObject({call:{arguments:{members:ids.map((target,i)=>({target,revision:reads[i].revision,slot:'piece_'+i}))}}});await reply(c,{ok:false,status:'Piece changed'});expect(screen.getByText('Piece changed',{exact:true})).toBeTruthy();expect((screen.getByLabelText('Action arguments') as HTMLTextAreaElement).value).toContain(ids[0]);
});
it('keeps authoritative ordered selection off inventory and prepares every member before requesting a reorder',async()=>{
 const selection={stateId:'b'.repeat(32),collecting:false,members:ids},c=make({constructionSelection:selection}),onCapture=vi.fn();const screen=render(<ConstructionSelectionControls client={c} disabled={false} onCapture={onCapture}/>);fireEvent.click(screen.getByRole('button',{name:'Move '+ids[1]+' earlier'}));expect(c.snapshot().request!.commands[0].catalog).toEqual(objectReferenceRequest(ids[1]));expect((screen.getByRole('button',{name:'Clear pieces'}) as HTMLButtonElement).disabled).toBe(true);
 await reply(c,{catalog:views[1]});await reply(c,{catalog:views[0]});expect(c.snapshot().request!.commands[0].execution).toMatchObject({call:{id:'room.selection.set',arguments:{stateId:selection.stateId,members:[ids[1],ids[0]],collecting:false}}});expect(c.snapshot().request!.conditions).toEqual([...basis].reverse().map(({id,objectRevision})=>({id,revision:objectRevision})));await reply(c,{ok:false,status:'Selection changed elsewhere'});expect(c.getSnapshot().state!.constructionSelection!.members).toEqual(ids);expect(screen.getByRole('alert').textContent).toContain('changed elsewhere');expect(onCapture).not.toHaveBeenCalled();
});

it('allows exact refresh queries without reusing conflicting edit evidence',()=>{
 const stale=[basis[0],{...basis[0],objectRevision:basis[0].objectRevision+1}];
 for(const action of ['inspect','motions'] as const)expect(roomCommandConditions([{action,target:ids[0]}],stale)).toEqual({version:2,conditions:[]});
 expect(()=>roomCommandConditions([{action:'delete',target:ids[0]}],stale)).toThrow('Conflicting');
 expect(()=>roomCommandConditions([{action:'delete',target:ids[0]}],[])).toThrow('Read the current saved object');
});
it('reads selected piece names on demand without changing membership or reserving a write revision',async()=>{
 const selection={stateId:'b'.repeat(32),collecting:true,members:ids},c=make({constructionSelection:selection,capabilities:[...state().capabilities!,'objectSearch.v1']}),onCapture=vi.fn();
 const screen=render(<ConstructionSelectionControls client={c} disabled={false} onCapture={onCapture}/>);
 fireEvent(screen.getByText('Construction pieces · 2 selected').closest('details')!,new Event('toggle'));
 expect(c.snapshot().request).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Read Piece 1 · origin'}));expect(c.snapshot().request!.commands).toEqual([{action:'catalog',catalog:objectReferenceRequest(ids[0])}]);
 await reply(c,{catalog:views[0]});expect(screen.getByRole('region',{name:'Piece 1 · origin object reference'}).textContent).toContain((reads[0].name||reads[0].kind)+' · read at revision '+reads[0].revision);expect(c.getSnapshot().state!.objects).toEqual([]);expect(c.getSnapshot().state!.constructionSelection).toEqual(selection);
 fireEvent.click(screen.getByRole('button',{name:'Remove '+ids[0]+' from construction'}));
 expect(c.snapshot().request!.commands).toEqual([{action:'catalog',catalog:objectReferenceRequest(ids[1])}]);
 await reply(c,{catalog:views[1]});expect(c.snapshot().request!.commands[0].execution).toMatchObject({call:{arguments:{members:[ids[1]]}}});await reply(c,{ok:false,status:'Selection changed'});
 expect(c.getSnapshot().state!.constructionSelection).toEqual(selection);expect(onCapture).not.toHaveBeenCalled();
});
