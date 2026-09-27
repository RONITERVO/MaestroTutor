// @vitest-environment jsdom
import nativeProgram from '../../../test-fixtures/browser/programBookState.json';
import {act,cleanup,fireEvent,render,waitFor} from '@testing-library/react';
import {afterEach,describe,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {parseProgram,sequenceProgram,simpleProgramSteps,type BehaviourProgram} from '../../core-sdk/room/programs';
import {RuleWorkspace} from './RuleWorkspace';
import {RoomAgentClient} from './roomAgentBridge';
import {newRuleStep,type RuleView} from '../../core-sdk/room/rules';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
afterEach(cleanup);
const id='b'.repeat(32),step='c'.repeat(32);
const rules=():RuleView=>({revision:4,canUndo:true,canRedo:false,readOnly:false,status:'Ready',sequences:[{id,name:'Wave',steps:2,repeat:false}],selected:{id,name:'Wave',interruption:0,repeat:false,program:JSON.stringify(sequenceProgram([{...newRuleStep(1),id:step},{...newRuleStep(),id:'d'.repeat(32)}]))},bindings:[],buttons:[],bindingPage:0,bindingCount:0,running:[],queued:0});
const state=(more:Partial<RoomAgentState>={}):RoomAgentState=>({version:1,capabilities:['behaviourPrograms.v3'],session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',canUndo:false,canRedo:false,physicsRunning:false,visible:true,workspaceView:'rules',created:[],objects:[{id:'maestro',objectRevision:3,name:'Maestro',kind:'Maestro',position:{x:0,y:0,z:0},scale:1,color:{r:1,g:1,b:1,a:1},animated:false}],rules:rules(),...more});
describe('shared behaviour blocks',()=>{
 it('reorders stable steps through one revision-checked native operation',async()=>{
  const client=new RoomAgentClient();expect(client.receive(state())).toBe(true);const screen=render(<RuleWorkspace client={client}/>);
  fireEvent.click(screen.getByRole('button',{name:'Move step 1 down'}));fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
  const request=client.snapshot().request!;expect(request.commands[0].rule?.revision).toBe(4);
  const updated=request.commands[0].rule!.edits![0].sequence!;expect(simpleProgramSteps(updated.program)![1].id).toBe(step);
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

const programSource=readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-prime.json','utf8');
const programState=():RoomAgentState=>state({capabilities:['behaviourPrograms.v3'],rules:{...rules(),sequences:[{id,name:'Prime wave',steps:11,program:true,repeat:false}],selected:{id,name:'Prime wave',interruption:0,repeat:false,program:programSource}}});
describe('program block and source editing',()=>{
 it('round trips a user block edit without losing functions, then sends the same program through the native contract',async()=>{
  const client=new RoomAgentClient();client.receive(programState());const screen=render(<RuleWorkspace client={client}/>);
  expect(screen.getAllByText('Maestro · 0.1 seconds')).toHaveLength(2);expect(screen.getByRole('region',{name:'Function prime'})).toBeTruthy();fireEvent.click(screen.getByRole('button',{name:'Edit block call_prime'}));
  const node=JSON.parse((screen.getByLabelText('Program JSON') as HTMLTextAreaElement).value);node.args[0].value=12;
  fireEvent.change(screen.getByLabelText('Program JSON'),{target:{value:JSON.stringify(node)}});expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true);expect(client.snapshot().request).toBeNull();
  fireEvent.click(screen.getByRole('button',{name:'Update draft'}));expect(screen.getByText('answer = prime(12)')).toBeTruthy();
  fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));const updated=client.snapshot().request!.commands[0].rule!.edits![0].sequence!;
  const program=parseProgram(updated.program).program!;expect(program.functions[1]).toEqual(JSON.parse(programSource).functions[1]);expect(program.functions[0].body[0].id).toBe('call_prime');expect(updated).not.toHaveProperty('steps');
  await act(async()=>{client.receive({...programState(),revision:2,ack:1,rules:{...programState().rules!,revision:5,selected:updated}});});
  expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true);
 });
 it('retains unfinished source on a concurrent physical edit, rejects invalid edits and displays native progress/outcomes',()=>{
  const client=new RoomAgentClient(),original=programState();client.receive({...original,rules:{...original.rules!,running:[{id:'e'.repeat(32),sequenceId:id,preparing:false,nodeId:'prime_wave',functionName:'main',status:'Running',locals:[{name:'answer',type:'boolean',value:'True'}]}],outcomes:[{id:'0'.repeat(32),sequenceId:id,phase:'completed',nodeId:'prime_wave',status:'Program completed'}]}});const screen=render(<RuleWorkspace client={client}/>);
  expect(screen.getByLabelText('Live program values').textContent).toContain('answer');expect(screen.getByText(/Last run: completed/)).toBeTruthy();expect(screen.container.querySelector('[data-node-id="prime_wave"]')!.classList.contains('rule-action-active')).toBe(true);
  fireEvent.click(screen.getByRole('button',{name:'Edit full source'}));fireEvent.change(screen.getByLabelText('Program JSON'),{target:{value:'{bad'}});fireEvent.click(screen.getByRole('button',{name:'Update draft'}));expect(screen.getByRole('alert')).toBeTruthy();expect(client.snapshot().request).toBeNull();
  act(()=>{client.receive({...original,revision:2,rules:{...original.rules!,revision:5}});});expect((screen.getByLabelText('Program JSON') as HTMLTextAreaElement).value).toBe('{bad');expect(screen.getByRole('status').textContent).toContain('draft is retained');
  fireEvent.click(screen.getByRole('button',{name:'Reload latest'}));expect(screen.queryByLabelText('Program JSON')).toBeNull();expect(screen.getByRole('region',{name:'Function prime'})).toBeTruthy();
 });
 it('opens the same canonical source from simple controls and blocks saving to an older native app',()=>{
  const client=new RoomAgentClient();client.receive(state());const screen=render(<RuleWorkspace client={client}/>);
  fireEvent.click(screen.getByRole('button',{name:'Functions & code'}));fireEvent.click(screen.getByRole('button',{name:'Edit full source'}));
  const program=JSON.parse((screen.getByLabelText('Program JSON') as HTMLTextAreaElement).value) as BehaviourProgram;
  expect(program.functions[0].body.map(n=>n.id)).toEqual(simpleProgramSteps(rules().selected!.program)!.map(s=>s.id));expect(program.resources).toEqual(['maestro']);
  act(()=>{client.receive(state({revision:2,capabilities:['behaviourPrograms.v1']}));});
  expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true);expect(screen.getByRole('status').textContent).toContain('Update the native app');
  client.cancel();
 });
});

 it('keeps a large valid program and concurrent local traces within the expanded room observation boundary',()=>{
  const state=JSON.parse(JSON.stringify(nativeProgram)) as RoomAgentState;
  state.rules!.selected!.program=state.rules!.selected!.program!.padEnd(24000,' ');
  const run=state.rules!.running[0];state.rules!.running=Array.from({length:8},(_,i)=>({...run,id:i.toString(16).padStart(32,'0'),locals:Array.from({length:24},(_,j)=>({name:'value_'+j,type:'text',value:'x'.repeat(128)}))}));
  state.rules!.outcomes=Array.from({length:16},(_,i)=>({id:(i+20).toString(16).padStart(32,'0'),sequenceId:run.sequenceId,phase:'completed',status:'x'.repeat(500)}));
  expect(JSON.stringify(state).length).toBeGreaterThan(65536);expect(new RoomAgentClient().receive(state)).toBe(true);
  expect(new RoomAgentClient().receive({...state,padding:'x'.repeat(327680)})).toBe(false);
 });

it('retains an invalid simple numeric draft for correction without flattening or dispatching it',async()=>{
 const client=new RoomAgentClient();client.receive(state());const screen=render(<RuleWorkspace client={client}/>);
 fireEvent.change(screen.getByLabelText('Step 1 seconds'),{target:{value:'-1'}});fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 expect(screen.getByRole('status').textContent).toContain('Check the name');expect((screen.getByLabelText('Step 1 seconds') as HTMLInputElement).value).toBe('-1');expect(client.snapshot().request).toBeNull();
 fireEvent.change(screen.getByLabelText('Step 1 seconds'),{target:{value:'1'}});fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 const updated=client.snapshot().request!.commands[0].rule!.edits![0].sequence!;expect(updated).not.toHaveProperty('steps');expect(simpleProgramSteps(updated.program)![0]).toMatchObject({id:step,seconds:1});
 await act(async()=>{client.receive(state({revision:2,ack:1,rules:{...rules(),revision:5,selected:updated}}));});
});

it('adds event blocks without flattening the current program and exposes shared state/signals',async()=>{
 const source=readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-events.json','utf8');
 const current=state({capabilities:['behaviourPrograms.v3','eventPrograms.v1'],rules:{...rules(),selected:{id,name:'Reactive',interruption:0,repeat:false,program:source},running:[{id:'1'.repeat(32),sequenceId:id,preparing:false,waiting:true,waitEvent:'object.tapped',waitSeconds:0,nodeId:'wait',functionName:'main',status:'Waiting for object.tapped',state:[{name:'count',type:'number',value:'2'}],locals:[]}]}});
 const client=new RoomAgentClient();expect(client.receive(current)).toBe(true);const screen=render(<RuleWorkspace client={client}/>);
 expect(screen.getByLabelText('Live program values').textContent).toContain('state.count');
 fireEvent.change(screen.getByLabelText('Value for user.wave'),{target:{value:'7'}});fireEvent.click(screen.getByRole('button',{name:'Send event'}));
 expect(client.snapshot().request!.commands).toEqual([{action:'rules',rule:{action:'signal',revision:4,eventName:'user.wave',value:7}}]);
 await act(async()=>{client.receive({...current,revision:2,ack:1});});
 fireEvent.click(screen.getByRole('button',{name:'+ Timer in main'}));fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 const saved=client.snapshot().request!.commands[0].rule!.edits![0].sequence!;
 const original=parseProgram(source).program!,updated=parseProgram(saved.program).program!;
 expect(updated.state).toEqual(original.state);expect(updated.events).toEqual(original.events);const previousLoop=original.functions[0].body[0],loop=updated.functions[0].body[0];if(previousLoop.op!=='forever'||loop.op!=='forever')throw new Error('Expected Forever');expect(loop.body.slice(0,-1)).toEqual(previousLoop.body);expect(loop.body[loop.body.length-1].op).toBe('sleep');
 await act(async()=>{client.receive({...current,revision:3,ack:2,rules:{...current.rules!,revision:5,selected:saved}});});
});


it('authors the upper-body capability through the same simple editor and preserves stable node IDs',async()=>{
 const client=new RoomAgentClient();client.receive(state());const screen=render(<RuleWorkspace client={client}/>);
 fireEvent.change(screen.getByLabelText('Step 1 action'),{target:{value:'9'}});
 fireEvent.change(screen.getByLabelText('Step 1 gesture'),{target:{value:'1'}});
 expect((screen.getByRole('option',{name:'Walk'}) as HTMLOptionElement).disabled).toBe(true);
 fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 const updated=client.snapshot().request!.commands[0].rule!.edits![0].sequence!;
 expect(JSON.parse(updated.program).functions[0].body[0]).toMatchObject({id:step,capability:'avatar.gesture.upperBody',version:1,arguments:{target:'maestro',gesture:'pointing',seconds:2.5}});
 await act(async()=>{client.receive(state({revision:2,ack:1,rules:{...rules(),revision:5,selected:updated}}));});
 expect((screen.getByLabelText('Step 1 action') as HTMLSelectElement).value).toBe('9');
});

it('authors physical impulse blocks through the same named contract without fake durations',async()=>{
 const client=new RoomAgentClient(),ballId='e'.repeat(32),initial=state();
 initial.objects.push({...initial.objects[0],id:ballId,name:'Ball',kind:'Ball'});
 expect(client.receive(initial)).toBe(true);const screen=render(<RuleWorkspace client={client}/>);
 fireEvent.change(screen.getByLabelText('Step 1 action'),{target:{value:'10'}});
 expect(screen.queryByLabelText('Step 1 seconds')).toBeNull();
 expect((screen.getByLabelText('Step 1 target') as HTMLSelectElement).value).toBe(ballId);
 fireEvent.change(screen.getByLabelText('Step 1 impulse y'),{target:{value:'1.2'}});
 fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 const updated=client.snapshot().request!.commands[0].rule!.edits![0].sequence!;
 const program=parseProgram(updated.program).program!;
 expect(program.functions[0].body[0]).toMatchObject({op:'invoke',capability:'object.physics.impulse',arguments:{target:ballId,x:0,y:1.2,z:0}});
 expect((program.functions[0].body[0] as any).arguments).not.toHaveProperty('seconds');
 await act(async()=>{client.receive({...initial,revision:2,ack:1,rules:{...initial.rules!,revision:5,selected:updated}});});
 fireEvent.change(screen.getByLabelText('Step 1 action'),{target:{value:'11'}});
 expect(screen.queryByLabelText('Step 1 impulse y')).toBeNull();expect(screen.queryByLabelText('Step 1 seconds')).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));
 expect(JSON.parse(client.snapshot().request!.commands[0].rule!.edits![0].sequence!.program).functions[0].body[0].arguments).toEqual({target:ballId});
 act(()=>client.cancel());
});
