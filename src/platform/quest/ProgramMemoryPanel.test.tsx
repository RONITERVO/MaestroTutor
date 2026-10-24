// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {act,cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {useState} from 'react';
import {ProgramMemoryPanel} from './ProgramMemoryPanel';
import {ProgramEditor} from './ProgramEditor';
import {RoomAgentClient} from './roomAgentBridge';
import {validRuleView} from '../../core-sdk/room/rules';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
import type {BehaviourProgram} from '../../core-sdk/room/programs';
afterEach(cleanup);
const A='a'.repeat(32),C='c'.repeat(32),program=readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-memory.json','utf8');
function state():RoomAgentState{return {version:1,capabilities:['behaviourPrograms.v3','eventPrograms.v1','rememberedVariables.v1','temporaryMemory.v1','execution.v1','actionResults.v1'],session:'b'.repeat(32),revision:1,sceneRevision:1,ack:0,ok:true,status:'Ready',canUndo:false,canRedo:false,physicsRunning:false,visible:true,workspaceView:'rules',created:[],objects:[],rules:{revision:1,canUndo:false,canRedo:false,readOnly:false,status:'Ready',sequences:[{id:A,name:'Counter',steps:4,repeat:false,program:true}],selected:{id:A,name:'Counter',interruption:0,repeat:false,program},bindings:[],buttons:[],bindingPage:0,bindingCount:0,running:[],queued:0,memory:{ready:true,pending:false,busy:false,temporary:false,sessionId:'f'.repeat(32),error:'',revision:C,programId:A,page:0,count:1,programs:[{id:A,name:'Counter',cells:1}],cells:[{id:C,name:'count',typeJson:'"number"',valueJson:'7',saved:true,declared:true}]}}};}
it('sends a typed edit through the shared catalog with exact observed revisions',()=>{
 const client=new RoomAgentClient(),initial=state();expect(validRuleView(initial.rules)).toBe(true);expect(client.receive(initial)).toBe(true);const screen=render(<ProgramMemoryPanel client={client}/>);
 fireEvent.click(screen.getByText('Edit count'));fireEvent.change(screen.getByLabelText('Remembered value'),{target:{value:'12'}});fireEvent.click(screen.getByText('Save remembered value'));
 expect(client.snapshot().request?.commands).toEqual([{action:'execution',execution:{operation:'start',call:{id:'program.memory.edit',version:1,arguments:{kind:'set',sessionId:'f'.repeat(32),programId:A,variableId:C,revision:C,rulesRevision:1,valueJson:'12'}}}}]);
});
it('retains a stale human draft without silently refreshing its write guards',()=>{
 const client=new RoomAgentClient();client.receive(state());const screen=render(<ProgramMemoryPanel client={client}/>);fireEvent.click(screen.getByText('Edit count'));fireEvent.change(screen.getByLabelText('Remembered value'),{target:{value:'12'}});
 const updated=state();updated.revision=2;updated.rules!.memory!.revision='d'.repeat(32);updated.rules!.memory!.cells[0].valueJson='8';act(()=>{client.receive(updated);});
 expect((screen.getByLabelText('Remembered value') as HTMLInputElement).value).toBe('12');expect(screen.getByText('Saved values or declarations changed. Discard this draft and inspect again.')).toBeTruthy();fireEvent.click(screen.getByText('Save remembered value'));expect(client.snapshot().request).toBeNull();
});
it('requires an explicit reset confirmation and exposes removed behaviour groups',()=>{
 const client=new RoomAgentClient(),initial=state();initial.rules!.memory!.programs.push({id:'e'.repeat(32),name:'Removed behaviour',cells:2});client.receive(initial);const screen=render(<ProgramMemoryPanel client={client}/>);
 expect(screen.getByText('Removed behaviour eeeeeeee')).toBeTruthy();fireEvent.click(screen.getByText('Reset all saved values'));expect(client.snapshot().request).toBeNull();fireEvent.click(screen.getByText('Confirm memory reset'));expect(client.snapshot().request?.commands[0].execution).toMatchObject({operation:'start',call:{id:'program.memory.edit',arguments:{kind:'reset',variableId:'',programId:A,revision:C,rulesRevision:1}}});
});
it('keeps memory edits unavailable while the target runs or a write is pending',()=>{
 const client=new RoomAgentClient(),initial=state();initial.rules!.memory!.busy=true;client.receive(initial);const screen=render(<ProgramMemoryPanel client={client}/>);expect((screen.getByText('Edit count') as HTMLButtonElement).disabled).toBe(true);fireEvent.click(screen.getByText('Stop for memory editing'));expect(client.snapshot().request?.commands[0].rule).toEqual({action:'stop',target:A});
});
it('authors scope and save blocks through the same validated program without JSON editing',()=>{
 const initial=JSON.parse(program) as BehaviourProgram;delete initial.memoryVersion;delete initial.state![0].memory;initial.functions[0].body=initial.functions[0].body.filter(n=>n.op!=='checkpoint');let source=JSON.stringify(initial);
 function Harness(){const [value,setValue]=useState(source);return <ProgramEditor source={value} onChange={next=>{source=next;setValue(next);}} targets={[]} onEditingChange={()=>{}} eventsSupported rememberedSupported/>;}
 const screen=render(<Harness/>);fireEvent.click(screen.getByText('Edit state & signals'));fireEvent.change(screen.getByLabelText('State 1 scope'),{target:{value:'remembered'}});fireEvent.click(screen.getByText('Update draft'));const declaration=JSON.parse(source).state[0];expect(declaration.memory).toMatch(/^[a-f0-9]{32}$/);
 fireEvent.click(screen.getByLabelText('+ Save remembered values in main'));expect(JSON.parse(source).functions[0].body.at(-1)).toMatchObject({op:'checkpoint'});expect(screen.getByText('Save remembered values')).toBeTruthy();
 fireEvent.click(screen.getByText('Edit state & signals'));fireEvent.change(screen.getByLabelText('State 1 name'),{target:{value:'score'}});fireEvent.click(screen.getByText('Update draft'));expect(JSON.parse(source).state[0]).toMatchObject({name:'score',memory:declaration.memory});
});

it('rejects drafts from the previous room session even when values and revisions are unchanged',()=>{
 const client=new RoomAgentClient();client.receive(state());const screen=render(<ProgramMemoryPanel client={client}/>);fireEvent.click(screen.getByText('Edit count'));
 const next=state();next.revision=2;next.rules!.memory!.sessionId='e'.repeat(32);next.rules!.memory!.temporary=true;act(()=>{client.receive(next);});
 expect(screen.getByText(/Temporary remembered values:/)).toBeTruthy();fireEvent.click(screen.getByText('Save remembered value'));expect(client.snapshot().request).toBeNull();expect(screen.getByText('Saved values or declarations changed. Discard this draft and inspect again.')).toBeTruthy();
});
it('keeps older native observations readable but disables edits without a session guard',()=>{
 const client=new RoomAgentClient(),older=state();delete older.rules!.memory!.sessionId;delete older.rules!.memory!.temporary;expect(client.receive(older)).toBe(true);const screen=render(<ProgramMemoryPanel client={client}/>);expect((screen.getByText('Edit count') as HTMLButtonElement).disabled).toBe(true);expect(screen.getByText(/Update the native app before editing/)).toBeTruthy();
});
it('shows temporary reset scope and sends the captured session identity',()=>{
 const client=new RoomAgentClient(),initial=state();initial.rules!.memory!.temporary=true;client.receive(initial);const screen=render(<ProgramMemoryPanel client={client}/>);fireEvent.click(screen.getByText('Reset count'));expect(screen.getByText(/Reset in this temporary session/)).toBeTruthy();fireEvent.click(screen.getByText('Confirm memory reset'));expect(client.snapshot().request?.commands[0].execution).toMatchObject({call:{arguments:{kind:'reset',sessionId:'f'.repeat(32)}}});
});
