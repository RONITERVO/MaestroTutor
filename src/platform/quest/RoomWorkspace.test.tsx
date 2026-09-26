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
const state=(more:Partial<RoomAgentState>={}):RoomAgentState=>({version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',canUndo:false,canRedo:false,physicsRunning:false,visible:true,created:[],objects:[{id,objectRevision:3,name:'Robot',kind:'Assembly',position:{x:0,y:0,z:0},scale:1,color:{r:1,g:1,b:1,a:1},animated:true}],inspection:{id,objectRevision:3,recipe:parseRecipe(robot)},...more});
describe('shared visual recipe workspace',()=>{
 it('edits the native part and applies one revision-checked recipe transaction',async()=>{
  const client=new RoomAgentClient();client.receive(state());const screen=render(<RoomWorkspace client={client}/>);
  fireEvent.click(screen.getByRole('button',{name:/Head.*Neck/}));
  await act(async()=>{client.receive(state({revision:2,ack:1,inspection:{...state().inspection!,partId:'Head'}}));});
  fireEvent.click(screen.getByRole('button',{name:'Size x plus'}));
  fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
  const request=client.snapshot().request!;expect(request.version).toBe(2);expect(request.conditions).toEqual([{id,revision:3}]);expect(request.commands).toHaveLength(1);
  const changed=parseRecipe(request.commands[0].recipe)!;expect(changed.parts.find(x=>x.id==='Head')!.size.x).toBeCloseTo(robot.parts.find(x=>x.id==='Head')!.size.x+.01);
  expect(changed.tracks).toEqual(robot.tracks);
  await act(async()=>{client.receive(state({revision:3,sceneRevision:5,ack:2,canUndo:true,inspection:{id,objectRevision:5,recipe:changed},objects:[{...state().objects[0],objectRevision:5}]}));});
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
  fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));const changed=parseRecipe(client.snapshot().request!.commands[0].recipe)!;
  expect(changed.tracks[0].keys[1].rotation).not.toEqual(robot.tracks[0].keys[1].rotation);expect(changed.tracks[1]).toEqual(robot.tracks[1]);
  // Settle the simulated native request so the test does not leave a pending timer.
  await act(async()=>{client.receive(state({revision:3,ack:2}));});
 });
});
