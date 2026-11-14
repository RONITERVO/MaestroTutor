// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import {act,cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import {RoomAgentClient} from './roomAgentBridge';
import {RoomWorkspace} from './RoomWorkspace';
import {roomCommandConditions,roomRevisionBasis} from '../../core-sdk/room/roomObjectContext';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
import {parseRecipe} from '../../core-sdk/room/recipe';
import robot from '../../../test-fixtures/browser/recipeRobot.json';
import nativeInspection from '../../../test-fixtures/browser/objectInspection.json';
const id='b'.repeat(32),other='d'.repeat(32),session='a'.repeat(32);
const item={id,objectRevision:3,name:'Saved robot',kind:'Assembly',position:{x:0,y:0,z:0},scale:1,color:{r:1,g:1,b:1,a:1},animated:false};
const execution={selected:null,running:[],outcomes:[],nextRunId:'c'.repeat(32),storageError:null,recovery:null};
const state=(more:Partial<RoomAgentState>={}):RoomAgentState=>({version:1,session,revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',capabilities:['recipeEdits.v1','execution.v1','executionReceipts.v1','actionResults.v1'],execution,canUndo:false,canRedo:false,physicsRunning:false,visible:true,created:[],objects:[],inspection:{id,objectRevision:3,item:{...item},recipe:parseRecipe(robot)},...more});
const clients:RoomAgentClient[]=[];
const client=()=>{const c=new RoomAgentClient();clients.push(c);expect(c.receive(state())).toBe(true);return c;};
afterEach(()=>{cleanup();clients.splice(0).forEach(c=>c.cancel());});
it('accepts self-contained inspection without its object in the list, rejecting inconsistent metadata',()=>{
 const c=client();expect(roomRevisionBasis(c.lease()!.state())).toEqual([{id,objectRevision:3}]);
 for(const inspection of [{...state().inspection!,item:{...item,id:other}},{...state().inspection!,item:{...item,objectRevision:4}},{...state().inspection!,item:{...item,scale:NaN}},{...state().inspection!,item:undefined}])expect(c.receive(state({revision:2,inspection}))).toBe(false);
 expect(c.receive(state({revision:2,objects:[{...item,objectRevision:4}]}))).toBe(false);
 expect(c.receive(state({revision:2,objects:[item,item]}))).toBe(false);
 expect(c.receive(state({revision:2,objects:[item],inspection:{...state().inspection!,item:undefined}}))).toBe(true);
});
it('edits an off-list recipe through its native inspection revision with no substituted target',async()=>{
 const c=client(),screen=render(<RoomWorkspace client={c}/>);expect(screen.getByRole('heading',{name:'Saved robot'})).toBeTruthy();fireEvent.click(screen.getByRole('button',{name:'Size x plus'}));fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 const request=c.snapshot().request!;expect(request.version).toBe(2);expect(request.conditions).toEqual([{id,revision:3}]);expect(request.commands[0].execution).toMatchObject({operation:'start',call:{id:'object.recipe.edit',arguments:{target:id,revision:3}}});await act(async()=>{c.receive(state({revision:2,ack:1}));});
});
it('keeps the author reviewed revision when newer native state arrives before dispatch',async()=>{
 const c=client(),planned=c.lease()!.state();expect(c.receive(state({revision:2,inspection:{...state().inspection!,objectRevision:4,item:{...item,objectRevision:4}}}))).toBe(true);
 const pending=c.request([{action:'resize',target:id,scale:2}],planned);expect(c.snapshot().request!.conditions).toEqual([{id,revision:3}]);c.receive(state({revision:3,ack:1,ok:false,status:'Target changed'}));expect((await pending).ok).toBe(false);
});
it('retains a dirty recipe across target revision and temporary-room changes but refuses applying it',()=>{
 const c=client(),screen=render(<RoomWorkspace client={c}/>);fireEvent.click(screen.getByRole('button',{name:'Size x plus'}));
 act(()=>{c.receive(state({revision:2,temporaryRoom:{active:true,pending:false,id:'e'.repeat(32),saveId:'f'.repeat(32),phase:'ready',error:'',savedRevision:0}}));});expect(screen.getByRole('status').textContent).toContain('Your draft is kept');expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true);expect(c.snapshot().request).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Reload latest'}));expect(screen.getByRole('status').textContent).not.toContain('Your draft is kept');
 act(()=>{c.receive(state({revision:3,inspection:{...state().inspection!,objectRevision:4,item:{...item,objectRevision:4}}}));});fireEvent.click(screen.getByRole('button',{name:'Size x plus'}));
 act(()=>{c.receive(state({revision:4,inspection:null}));});expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true);expect(screen.getByRole('heading',{name:'Saved robot'})).toBeTruthy();expect(c.snapshot().request).toBeNull();
});
it('supports simple off-list edits and keeps the explicit inspected target',async()=>{
 const c=client();c.receive(state({revision:2,inspection:{id,objectRevision:3,item:{...item,kind:'Block'},recipe:null}}));const screen=render(<RoomWorkspace client={c}/>);fireEvent.click(screen.getByRole('button',{name:'Larger'}));expect(c.snapshot().request).toMatchObject({conditions:[{id,revision:3}],commands:[{action:'resize',target:id,scale:1.1}]});await act(async()=>{c.receive(state({revision:3,ack:1}));});
});
it('refuses a missing modern target before publishing or consuming a command sequence',()=>{
 const c=client();expect(()=>c.lease()!.execute([{action:'delete',target:other}],4,roomRevisionBasis(state()))).toThrow('Read the current saved object');expect(c.snapshot().request).toBeNull();
 expect(()=>roomCommandConditions([{action:'delete',target:id}],[{id,objectRevision:3},{id,objectRevision:4}])).toThrow('Conflicting');
});
it('includes every nested capability resource and allows only earlier creation aliases',()=>{
 const commands=[{action:'execution' as const,execution:{operation:'start' as const,call:{id:'object.layout.transform',version:1,arguments:{members:[{target:id},{target:other}]}}}}];
 expect(()=>roomCommandConditions(commands,[{id,objectRevision:3}])).toThrow(other);expect(roomCommandConditions(commands,[{id,objectRevision:3},{id:other,objectRevision:7}]).conditions).toEqual([{id,revision:3},{id:other,revision:7}]);
 expect(roomCommandConditions([{action:'create',reference:'new_ball',name:'Ball',kind:'ball'},{action:'paint',target:'new_ball',color:{r:1,g:0,b:0,a:1}}],[]).conditions).toEqual([]);
 expect(()=>roomCommandConditions([{action:'delete',target:'new_ball'},{action:'create',reference:'new_ball',name:'Ball',kind:'ball'}],[])).toThrow('new_ball');
});
it('permits inspection without revision evidence and preserves legacy scene guards',()=>{
 expect(roomCommandConditions([{action:'inspect',target:id}],[])).toEqual({version:2,conditions:[]});expect(roomCommandConditions([{action:'delete',target:id}],[{id}])).toEqual({version:1,conditions:[]});
});
it('invalidates an old lease and refuses an old expected snapshot after temporary-room change',async()=>{
 const c=client(),old=c.lease()!,planned=old.state();expect(c.receive(state({revision:2,temporaryRoom:{active:true,pending:false,id:'e'.repeat(32),saveId:'f'.repeat(32),phase:'ready',error:'',savedRevision:0}}))).toBe(true);expect(old.valid()).toBe(false);
 await expect(c.request([{action:'delete',target:id}],planned)).rejects.toThrow('session changed');await expect(old.execute([{action:'delete',target:id}],4,roomRevisionBasis(planned))).rejects.toThrow('unavailable');expect(c.snapshot().request).toBeNull();
});

it('accepts actual native dormant inspection metadata without inventory and emits its exact revision',async()=>{
 const c=new RoomAgentClient();clients.push(c);const native={...structuredClone(nativeInspection),objects:[]} as unknown as RoomAgentState;expect(c.receive(native)).toBe(true);expect(c.lease()!.state().inspection!.item!.runtimeState).toBe('unavailable');
 const pending=c.request([{action:'resize',target:native.inspection!.id,scale:1.5}]);expect(c.snapshot().request!.conditions).toEqual([{id:native.inspection!.id,revision:native.inspection!.objectRevision}]);c.receive({...native,revision:native.revision+1,ack:c.snapshot().request!.sequence});await pending;
});

it('reconciles an older action identity without granting a new edit on a missing target',async()=>{
 const c=client(),call={id:'object.delete',version:1,arguments:{target:id}},oldId='e'.repeat(32);
 c.receive(state({revision:2,inspection:null,objects:[],capabilities:[...state().capabilities!,'objectEdits.v1']}));
 const pending=c.request([{action:'execution',execution:{operation:'start',runId:oldId,call}}]);expect(c.snapshot().request).toMatchObject({version:2,conditions:[{id,revision:0}],commands:[{action:'execution',execution:{operation:'start',runId:oldId,call}}]});
 c.receive(state({revision:3,ack:1,inspection:null,capabilities:[...state().capabilities!,'objectEdits.v1'],ok:false,status:'Expired action identity'}));expect((await pending).ok).toBe(false);
 expect(()=>c.request([{action:'execution',execution:{operation:'start',call}}])).toThrow('Read the current saved object');expect(c.snapshot().request).toBeNull();
});
