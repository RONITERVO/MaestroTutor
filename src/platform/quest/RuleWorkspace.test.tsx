// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,waitFor} from '@testing-library/react';
import {afterEach,describe,expect,it} from 'vitest';
import {RuleWorkspace} from './RuleWorkspace';
import {RoomAgentClient} from './roomAgentBridge';
import {newRuleStep,type RuleView} from '../../core-sdk/room/rules';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
afterEach(cleanup);
const id='b'.repeat(32),step='c'.repeat(32);
const rules=():RuleView=>({revision:4,canUndo:true,canRedo:false,readOnly:false,status:'Ready',sequences:[{id,name:'Wave',steps:2,repeat:false}],selected:{id,name:'Wave',interruption:0,repeat:false,steps:[{...newRuleStep(1),id:step},{...newRuleStep(),id:'d'.repeat(32)}]},bindings:[],buttons:[],bindingPage:0,bindingCount:0,running:[],queued:0});
const state=(more:Partial<RoomAgentState>={}):RoomAgentState=>({version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',canUndo:false,canRedo:false,physicsRunning:false,visible:true,workspaceView:'rules',created:[],objects:[{id:'maestro',objectRevision:3,name:'Maestro',kind:'Maestro',position:{x:0,y:0,z:0},scale:1,color:{r:1,g:1,b:1,a:1},animated:false}],rules:rules(),...more});
describe('shared behaviour blocks',()=>{
 it('reorders stable steps through one revision-checked native operation',async()=>{
  const client=new RoomAgentClient();expect(client.receive(state())).toBe(true);const screen=render(<RuleWorkspace client={client}/>);
  fireEvent.click(screen.getByRole('button',{name:'Move step 1 down'}));fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
  const request=client.snapshot().request!;expect(request.commands[0].rule?.revision).toBe(4);
  const updated=request.commands[0].rule!.edits![0].sequence!;expect(updated.steps[1].id).toBe(step);
  await act(async()=>{client.receive(state({revision:2,ack:1,rules:{...rules(),revision:5,selected:updated}}));});
  await waitFor(()=>expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true));
 });
 it('retains a stale draft and rejects silently overwriting a physical-tool edit',()=>{
  const client=new RoomAgentClient();client.receive(state());const screen=render(<RuleWorkspace client={client}/>);
  fireEvent.change(screen.getByLabelText('Name'),{target:{value:'My changed behaviour'}});
  act(()=>{client.receive(state({revision:2,rules:{...rules(),revision:5}}));});
  expect(screen.getByRole('status').textContent).toContain('draft is retained');expect((screen.getByLabelText('Name') as HTMLInputElement).value).toBe('My changed behaviour');
  expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true);expect(client.snapshot().request).toBeNull();
 });
 it('adds a state trigger and mounted button through the shared client',async()=>{
  const client=new RoomAgentClient();client.receive(state());const screen=render(<RuleWorkspace client={client}/>);
  fireEvent.click(screen.getByRole('button',{name:'Add trigger'}));expect(client.snapshot().request!.commands[0].rule!.edits![0].binding?.trigger).toBe(0);
  await act(async()=>{client.receive(state({revision:2,ack:1,rules:{...rules(),revision:5}}));});
  fireEvent.click(screen.getByRole('button',{name:'+ Left controller'}));expect(client.snapshot().request!.commands[0].rule!.edits![0]).toEqual({kind:'button',target:id,mount:1});
  await act(async()=>{client.receive(state({revision:3,ack:2,rules:{...rules(),revision:6}}));});
 });
});
