// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState} from 'react';
import {readFileSync} from 'node:fs';
import {cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import {ProgramEditor} from './ProgramEditor';
import {parseProgram,type BehaviourProgram} from '../../core-sdk/room/programs';
afterEach(cleanup);
it('lets a human wire a native creation result into the next action without writing source',()=>{
 const initial=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-create.json','utf8')) as BehaviourProgram;
 const first=initial.functions[0].body[0],second=initial.functions[0].body[1];
 if(first.op!=='invoke'||second.op!=='invoke')throw new Error('Expected calls');
 delete first.results;second.bindings={};initial.resources=[String(second.arguments.target)];
 initial.functions[0].locals=[];let source=JSON.stringify(initial);
 function Harness(){const [value,setValue]=useState(source);return <ProgramEditor source={value} targets={[]} eventsSupported resultsSupported onEditingChange={()=>{}} onChange={next=>{source=next;setValue(next);}}/>;}
 const screen=render(<Harness/>);
 fireEvent.click(screen.getByLabelText('create new variable for objectId'));
 fireEvent.change(screen.getByLabelText('push argument target variable'),{target:{value:'objectId'}});
 const result=parseProgram(source);expect(result.error).toBeNull();
 expect(result.program?.functions[0].locals).toContainEqual({name:'objectId',initial:''});
 expect(result.program?.functions[0].body[0]).toMatchObject({results:{objectId:'objectId'}});
 expect(result.program?.functions[0].body[1]).toMatchObject({bindings:{target:{var:'objectId'}}});
 expect(screen.getByText('Create shape → objectId (objectId)')).toBeTruthy();
 fireEvent.click(screen.getByLabelText('Edit block push'));
 expect(screen.getByLabelText('Program JSON').getAttribute('aria-label')).toBe('Program JSON');
});
