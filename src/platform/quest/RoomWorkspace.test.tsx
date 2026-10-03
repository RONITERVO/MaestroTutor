// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,waitFor} from '@testing-library/react';
import {afterEach,describe,expect,it} from 'vitest';
import {RoomAgentClient} from './roomAgentBridge';
import {RoomWorkspace} from './RoomWorkspace';
import {parseRecipe} from '../../core-sdk/room/recipe';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
import robot from '../../../test-fixtures/browser/recipeRobot.json';
afterEach(cleanup);
const id='b'.repeat(32);
const execution={selected:null,running:[],outcomes:[],nextRunId:'c'.repeat(32),storageError:null,recovery:null};
const state=(more:Partial<RoomAgentState>={}):RoomAgentState=>({version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',capabilities:['recipeEdits.v1','execution.v1','executionReceipts.v1','actionResults.v1'],execution,canUndo:false,canRedo:false,physicsRunning:false,visible:true,created:[],objects:[{id,objectRevision:3,name:'Robot',kind:'Assembly',position:{x:0,y:0,z:0},scale:1,color:{r:1,g:1,b:1,a:1},animated:true}],inspection:{id,objectRevision:3,recipe:parseRecipe(robot)},...more});
describe('shared visual recipe workspace',()=>{
 it('edits the native part and applies one revision-checked recipe transaction',async()=>{
  const client=new RoomAgentClient();client.receive(state());const screen=render(<RoomWorkspace client={client}/>);
  fireEvent.click(screen.getByRole('button',{name:/Head.*Neck/}));
  await act(async()=>{client.receive(state({revision:2,ack:1,inspection:{...state().inspection!,partId:'Head'}}));});
  fireEvent.click(screen.getByRole('button',{name:'Size x plus'}));
  fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
  const request=client.snapshot().request!;expect(request.version).toBe(2);expect(request.conditions).toEqual([{id,revision:3}]);expect(request.commands).toHaveLength(1);
  const invocation=request.commands[0].execution!;expect(invocation.operation).toBe('start');if(invocation.operation!=='start')throw new Error('Expected recipe invocation');expect(invocation.call.id).toBe('object.recipe.edit');
  const patch=invocation.call.arguments,changed=structuredClone(parseRecipe(robot)!);expect(patch.revision).toBe(3);expect(patch.parts).toHaveLength(1);changed.parts=changed.parts.map(p=>p.id==='Head'?(patch.parts as typeof changed.parts)[0]:p);changed.playing=false;expect(changed.parts.find(x=>x.id==='Head')!.size.x).toBeCloseTo(robot.parts.find(x=>x.id==='Head')!.size.x+.01);expect(patch.tracks).toEqual([]);
  const completed={...execution,selected:{id:invocation.runId!,capability:invocation.call.id,version:1,resources:[id],phase:'completed' as const,status:'Saved',call:invocation.call,output:{target:id,revision:5,parts:changed.parts.length,tracks:changed.tracks.length,duration:changed.duration,loop:changed.loop,playing:false,autoplay:false}}};
  await act(async()=>{client.receive(state({revision:3,sceneRevision:5,ack:2,canUndo:true,execution:completed,inspection:{id,objectRevision:5,recipe:changed},objects:[{...state().objects[0],objectRevision:5}]}));});
  await waitFor(()=>expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true));
 });
 it('keeps a dirty draft when the native object changes and requires explicit reload',()=>{
  const client=new RoomAgentClient();client.receive(state());const screen=render(<RoomWorkspace client={client}/>);
  fireEvent.click(screen.getByRole('button',{name:'Size x plus'}));
  act(()=>{client.receive(state({revision:2,sceneRevision:5,inspection:{id,objectRevision:5,recipe:parseRecipe(robot)},objects:[{...state().objects[0],objectRevision:5}]}));});
  expect(screen.getByRole('status').textContent).toContain('Your draft is kept');expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true);
  expect(client.snapshot().request).toBeNull();fireEvent.click(screen.getByRole('button',{name:'Reload latest'}));
  expect(screen.getByRole('status').textContent).not.toContain('Your draft is kept');
 });
 it('edits native quaternion animation keys and leaves unrelated tracks unchanged',async()=>{
  const client=new RoomAgentClient();client.receive(state());const screen=render(<RoomWorkspace client={client}/>);
  fireEvent.click(screen.getByRole('button',{name:/RightUpperArm.*Chest/}));
  await act(async()=>{client.receive(state({revision:2,ack:1,inspection:{...state().inspection!,partId:'RightUpperArm'}}));});
  fireEvent.click(screen.getByRole('tab',{name:'Animation'}));
  fireEvent.click(screen.getByRole('button',{name:'0.40s'}));fireEvent.click(screen.getByRole('button',{name:'Key 0.40s x plus 15 degrees'}));
  fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));const invocation=client.snapshot().request!.commands[0].execution!;if(invocation.operation!=='start')throw new Error('Expected recipe invocation');const tracks=invocation.call.arguments.tracks as NonNullable<ReturnType<typeof parseRecipe>>['tracks'];
  expect(tracks).toHaveLength(1);expect(tracks[0].keys[1].rotation).not.toEqual(robot.tracks[0].keys[1].rotation);expect(invocation.call.arguments.parts).toEqual([]);
  // Settle the simulated native request so the test does not leave a pending timer.
  await act(async()=>{client.receive(state({revision:3,ack:2}));});
 });
});

it('retains a recipe draft when the native action has no completed receipt',async()=>{
 const client=new RoomAgentClient();client.receive(state());const screen=render(<RoomWorkspace client={client}/>);fireEvent.click(screen.getByRole('button',{name:'Size x plus'}));fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 await act(async()=>{client.receive(state({revision:2,ack:1}));});expect(screen.getByRole('status').textContent).toContain('draft is kept');expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(false);
});
it('does not fall back to a different recipe edit path when the native capability is unavailable',()=>{
 const client=new RoomAgentClient();client.receive(state({capabilities:[]}));const screen=render(<RoomWorkspace client={client}/>);fireEvent.click(screen.getByRole('button',{name:'Size x plus'}));fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));expect(screen.getByRole('status').textContent).toContain('draft is kept');expect(client.snapshot().request).toBeNull();
});

it('edits a visible lathe profile through the same native patch and keeps invalid drafts',async()=>{
 const client=new RoomAgentClient();client.receive(state({capabilities:[...state().capabilities!,'latheGeometry.v1']}));const screen=render(<RoomWorkspace client={client}/>);
 fireEvent.click(screen.getByRole('button',{name:'lathe'}));expect(screen.getByRole('img',{name:'Lathe cross section'})).toBeTruthy();
 fireEvent.change(screen.getByLabelText('Profile point 2 radius'),{target:{value:'-0.1'}});fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 expect(client.snapshot().request).toBeNull();expect(screen.getByRole('status').textContent).toContain('valid sizes');
 fireEvent.change(screen.getByLabelText('Profile point 2 radius'),{target:{value:'0.48'}});fireEvent.change(screen.getByLabelText('Lathe segments'),{target:{value:'32'}});
 fireEvent.click(screen.getByRole('button',{name:'lathe'}));expect((screen.getByLabelText('Profile point 2 radius') as HTMLInputElement).value).toBe('0.48');
 fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));const invocation=client.snapshot().request!.commands[0].execution!;if(invocation.operation!=='start')throw new Error('Expected shared patch');
 const edited=(invocation.call.arguments.parts as NonNullable<ReturnType<typeof parseRecipe>>['parts'])[0];expect(edited).toMatchObject({shape:'lathe',segments:32});expect(edited.profile).toHaveLength(6);expect(edited.profile![1].x).toBe(.48);
 await act(async()=>{client.receive(state({revision:2,ack:1}));});
});
it('does not offer new geometry to an older native room',()=>{
 const client=new RoomAgentClient();client.receive(state());const screen=render(<RoomWorkspace client={client}/>);expect((screen.getByRole('button',{name:'lathe'}) as HTMLButtonElement).disabled).toBe(true);expect((screen.getByRole('button',{name:'extrude'}) as HTMLButtonElement).disabled).toBe(true);expect((screen.getByRole('button',{name:'sweep'}) as HTMLButtonElement).disabled).toBe(true);
});

it('edits concave extrusion source in the same workspace and retains invalid drafts',async()=>{
 const client=new RoomAgentClient();client.receive(state({capabilities:[...state().capabilities!,'extrusionGeometry.v1']}));const screen=render(<RoomWorkspace client={client}/>);
 fireEvent.click(screen.getByRole('button',{name:'extrude'}));expect(screen.getByRole('img',{name:'Extrusion cross section'})).toBeTruthy();expect(screen.queryByLabelText('Lathe segments')).toBeNull();
 fireEvent.change(screen.getByLabelText('Profile point 4 x'),{target:{value:'-0.6'}});fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));expect(client.snapshot().request).toBeNull();
 fireEvent.change(screen.getByLabelText('Profile point 4 x'),{target:{value:'0'}});fireEvent.click(screen.getByRole('button',{name:'Insert after point 1'}));fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 const invocation=client.snapshot().request!.commands[0].execution!;if(invocation.operation!=='start')throw new Error('Expected shared patch');
 const part=(invocation.call.arguments.parts as NonNullable<ReturnType<typeof parseRecipe>>['parts'])[0];expect(part.shape).toBe('extrude');expect(part.profile).toHaveLength(7);expect(part.segments).toBe(0);
 await act(async()=>{client.receive(state({revision:2,ack:1}));});
});

it('edits swept profiles and paths through one shared patch without resetting a selected shape',async()=>{
 const client=new RoomAgentClient();client.receive(state({capabilities:[...state().capabilities!,'sweepGeometry.v1']}));const screen=render(<RoomWorkspace client={client}/>);
 fireEvent.click(screen.getByRole('button',{name:'sweep'}));expect(screen.getByRole('img',{name:'Sweep cross section'})).toBeTruthy();expect(screen.getByRole('img',{name:'Sweep path X Y'})).toBeTruthy();expect(screen.getByRole('img',{name:'Sweep path X Z'})).toBeTruthy();
 fireEvent.change(screen.getByLabelText('Path point 3 z'),{target:{value:'.6'}});fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));expect(client.snapshot().request).toBeNull();
 fireEvent.change(screen.getByLabelText('Path point 3 z'),{target:{value:'.05'}});fireEvent.click(screen.getByRole('button',{name:'Insert after path point 1'}));expect(screen.getByLabelText('Path point 7 z')).toBeTruthy();fireEvent.click(screen.getByRole('button',{name:'Remove path point 2'}));
 fireEvent.click(screen.getByRole('button',{name:'sweep'}));expect((screen.getByLabelText('Path point 3 z') as HTMLInputElement).value).toBe('0.05');fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 const invocation=client.snapshot().request!.commands[0].execution!;if(invocation.operation!=='start')throw new Error('Expected shared patch');const part=(invocation.call.arguments.parts as NonNullable<ReturnType<typeof parseRecipe>>['parts'])[0];expect(part.shape).toBe('sweep');expect(part.path).toHaveLength(6);expect(part.path![2].z).toBe(.05);expect(part.profile).toHaveLength(8);
 await act(async()=>{client.receive(state({revision:2,ack:1}));});
});
