// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState} from 'react';
import {readFileSync} from 'node:fs';
import {cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import {declarationDraft,editProgramDeclarations} from './programDeclarationEditing';
import {ProgramBlockEditor} from './ProgramBlockEditor';
import {parseProgram,type BehaviourProgram,type ProgramNode} from '../../core-sdk/room/programs';
afterEach(cleanup);
const source=()=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-channel-wait.json','utf8')) as BehaviourProgram;
it('edits a typed wait timeout, preserves it across action changes, and removes it explicitly',()=>{
 const p=source();p.functions[0].locals=[{name:'patience',initial:2}];let latest:ProgramNode=p.functions[0].body[0];
 function Harness(){const [node,set]=useState(latest);return <ProgramBlockEditor node={node} program={p} fn={p.functions[0]} objects={[]} channelWaitsSupported onChange={n=>{latest=n;set(n);}}/>;}
 const screen=render(<Harness/>);fireEvent.change(screen.getByLabelText('Channel timeout seconds source'),{target:{value:'var:patience'}});expect(latest).toMatchObject({waitForChannels:{var:'patience'}});fireEvent.change(screen.getByLabelText('Block action'),{target:{value:'time.wait'}});expect(latest).toMatchObject({capability:'time.wait',waitForChannels:{var:'patience'}});p.functions[0].body[0]=latest;expect(parseProgram(JSON.stringify(p)).error).toBeNull();
 fireEvent.click(screen.getByLabelText('Wait for free channels'));expect(latest.op==='invoke'&&latest.waitForChannels).toBeUndefined();fireEvent.click(screen.getByLabelText('Wait for free channels'));expect(latest).toMatchObject({waitForChannels:{value:5}});
});
it('shows existing waits read-only when the connected runtime does not advertise support',()=>{
 const p=source(),change=()=>{throw Error('Must not edit unsupported wait')};const screen=render(<ProgramBlockEditor node={p.functions[0].body[0]} program={p} fn={p.functions[0]} objects={[]} onChange={change}/>);expect((screen.getByLabelText('Wait for free channels') as HTMLInputElement).closest('fieldset')?.disabled).toBe(true);
});

it('renames declarations used by a wait timeout without leaving stale references',()=>{
 const p=source();p.state=[{name:'patience',initial:3}];const call=p.functions[0].body[0];if(call.op!=='invoke')throw Error('fixture');call.waitForChannels={state:'patience'};const draft=declarationDraft(p);draft.state[0].name='timeout';const renamed=editProgramDeclarations(p,draft);expect(renamed.functions[0].body[0]).toMatchObject({waitForChannels:{state:'timeout'}});expect(parseProgram(JSON.stringify(renamed)).error).toBeNull();
});
