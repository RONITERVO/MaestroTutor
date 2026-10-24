// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState} from 'react';
import {readFileSync} from 'node:fs';
import {cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import {ProgramBlockEditor} from './ProgramBlockEditor';
import {parseProgram,type BehaviourProgram,type ProgramNode} from '../../core-sdk/room/programs';
afterEach(cleanup);
const source=()=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-anchor-zone.json','utf8')) as BehaviourProgram;
const objects=[{id:'0'.repeat(32),name:'Ball'},{id:'1'.repeat(32),name:'Robot'},{id:'maestro',name:'Maestro'},{id:'book',name:'Book'}];
it('lets humans bind nested event inputs and removes incompatible bindings when changing anchor kind',()=>{
 const p=source();let latest:ProgramNode=p.functions[0].body[0];
 function Harness(){const [node,set]=useState(latest);return <ProgramBlockEditor node={node} program={p} fn={p.functions[0]} objects={objects} eventFieldsSupported eventSubscriptionsSupported onChange={n=>{latest=n;set(n);}}/>;}
 const screen=render(<Harness/>);fireEvent.change(screen.getByLabelText('Event holder.revision input mode'),{target:{value:'expression'}});fireEvent.change(screen.getByLabelText('Event holder.revision source'),{target:{value:'var:distance'}});
 fireEvent.change(screen.getByLabelText('Event offset.z'),{target:{value:'.4'}});expect(latest).toMatchObject({arguments:{offset:{z:.4}},bindings:{'holder.revision':{var:'distance'}}});
 fireEvent.change(screen.getByLabelText('Attachment point'),{target:{value:'1'}});expect(latest).toMatchObject({arguments:{holder:{kind:'avatarHand',objectId:'maestro',hand:'left',avatarHash:''}},bindings:{}});expect(screen.queryByLabelText('Event holder.revision input mode')).toBeNull();
 p.resources.push('maestro');p.functions[0].body[0]=latest;expect(parseProgram(JSON.stringify(p)).error).toBeNull();
});
it('edits optional pickup reach through the same generated action schema',()=>{
 const p=source(),branch=p.functions[0].body[1];if(branch.op!=='if')throw Error('fixture');let latest=branch.then[0];
 function Harness(){const [node,set]=useState(latest);return <ProgramBlockEditor node={node} program={p} fn={p.functions[0]} objects={objects} onChange={n=>{latest=n;set(n);}}/>;}
 const screen=render(<Harness/>);fireEvent.change(screen.getByLabelText('reach.radius'),{target:{value:'.18'}});fireEvent.change(screen.getByLabelText('reach.physics'),{target:{value:'false'}});expect(latest).toMatchObject({arguments:{reach:{radius:.18,physics:false}}});
 fireEvent.click(screen.getByLabelText('Include reach'));expect(latest.op==='invoke'&&latest.arguments.reach).toBeUndefined();branch.then[0]=latest;expect(parseProgram(JSON.stringify(p)).error).toBeNull();
});
