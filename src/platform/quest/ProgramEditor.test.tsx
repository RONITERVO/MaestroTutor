// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState} from 'react';
import {readFileSync} from 'node:fs';
import {cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {ProgramEditor} from './ProgramEditor';
import {parseProgram,simpleProgramSteps,type BehaviourProgram} from '../../core-sdk/room/programs';
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


const empty:BehaviourProgram={version:2,entry:'main',resources:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[]}]};
function harness(initial=empty,objects=[{id:'maestro',name:'Maestro'},{id:'book',name:'Book'}]) {
 let source=JSON.stringify(initial);
 const onChange=vi.fn();
 function Harness(){const [value,setValue]=useState(source);return <ProgramEditor source={value} targets={objects} eventsSupported resultsSupported onEditingChange={()=>{}} onChange={next=>{source=next;setValue(next);onChange(next);}}/>;}
 const screen=render(<Harness/>);
 const change=(label:string,value:string)=>fireEvent.change(screen.getByLabelText(label),{target:{value}});
 const click=(name:string)=>fireEvent.click(screen.getByRole('button',{name}));
 return {screen,change,click,onChange,source:()=>source};
}
it('builds the shared native branch/loop/action fixture entirely with visual controls',()=>{
 const h=harness();
 // Insertions address exact branch containers, including initially empty ones.
 fireEvent.click(h.screen.getByLabelText('+ If in main'));
 h.click('Edit values block_1');
 h.change('Condition source','op:eq');h.change('Condition compared type','text');
 h.change('Condition left source','fact:maestro.state');h.change('Condition right value','speaking');h.click('Update draft');
 fireEvent.click(h.screen.getByLabelText('+ Repeat in block_1 Then'));
 fireEvent.click(h.screen.getByLabelText('+ Action in block_2 Repeat these'));
 h.click('Edit values block_3');h.change('Block action','animation.play');
 h.change('source.gesture','greeting');h.change('seconds','0.2');h.click('Update draft');
 fireEvent.click(h.screen.getByLabelText('+ Action in block_1 Otherwise'));
 expect(h.screen.queryByLabelText('Program JSON')).toBeNull();
 const fixture=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-visual.json','utf8'));
 expect(JSON.parse(h.source())).toEqual(fixture);expect(parseProgram(h.source()).error).toBeNull();
 h.click('Edit full source');expect(JSON.parse((h.screen.getByLabelText('Program JSON') as HTMLTextAreaElement).value)).toEqual(fixture);
});
it('retains invalid visual edits locally and leaves the committed draft and nested blocks unchanged',()=>{
 const initial=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-visual.json','utf8'));
 const h=harness(initial),before=h.source();
 h.click('Edit values block_3');h.change('seconds','100');h.click('Update draft');
 expect(h.screen.getByRole('alert').textContent).toContain('contract');expect(h.source()).toBe(before);
 expect((h.screen.getByLabelText('seconds') as HTMLInputElement).value).toBe('100');
 h.click('Discard editor draft');expect(h.source()).toBe(before);
 h.click('Edit values block_3');h.change('seconds','.4');h.click('Update draft');
 const result=JSON.parse(h.source());initial.functions[0].body[0].then[0].body[0].arguments.seconds=.4;
 expect(result).toEqual(initial);
});
it('preserves a complete native recipe and result binding when changing one field visually',()=>{
 const initial=JSON.parse(readFileSync('test-fixtures/browser/recipeCreationProgram.json','utf8')) as BehaviourProgram;
 const first=initial.functions[0].body[0];if(first.op!=='invoke')throw new Error('Expected recipe call');
 const h=harness(initial);h.click('Edit values '+first.id);h.change('name','My friendly robot');h.click('Update draft');
 first.arguments.name='My friendly robot';
 expect(JSON.parse(h.source())).toEqual(initial);
});
it('uses a creation variable without granting a guessed object authority when no creations exist yet',()=>{
 const initial=structuredClone(empty);initial.functions[0].locals=[{name:'created',initial:''}];
 initial.functions[0].body=[{id:'paint',op:'invoke',capability:'time.wait',version:1,arguments:{seconds:1},bindings:{}}];
 const h=harness(initial,[]);h.click('Edit values paint');h.change('Block action','object.color.set');
 h.change('target input mode','expression');h.change('target source','var:created');h.click('Update draft');
 const program=JSON.parse(h.source());expect(program.version).toBe(3);expect(program.resources).toEqual([]);
 expect(program.functions[0].body[0]).toMatchObject({bindings:{target:{var:'created'}}});
 expect(parseProgram(h.source()).error).toBeNull();
});
it('adds nested switch cases without flattening existing case bodies',()=>{
 const h=harness();fireEvent.click(h.screen.getByLabelText('+ Cases in main'));
 fireEvent.click(h.screen.getByLabelText('+ Action in block_1 Case 0'));
 h.click('Edit values block_1');h.click('Add case');h.change('Case 2','7');h.click('Update draft');
 fireEvent.click(h.screen.getByLabelText('+ Action in block_1 Case 7'));
 const result=JSON.parse(h.source()).functions[0].body[0];
 expect(result.cases.map((c:{value:number})=>c.value)).toEqual([0,7]);
 expect(result.cases[0].body[0].id).toBe('block_2');expect(result.cases[1].body[0].id).toBe('block_3');
});
it('edits event waits and calculations with the same typed locals and state',()=>{
 const h=harness();fireEvent.click(h.screen.getByLabelText('+ Event wait in main'));
 h.click('Edit values block_1');h.change('Await event','object.grabbed');
 h.change('Event object','book');h.change('Timeout seconds source','op:add');
 h.change('Timeout seconds left value','2');h.change('Timeout seconds right value','3');h.click('Update draft');
 const program=JSON.parse(h.source());expect(program.version).toBe(3);
 expect(program.functions[0].body[0]).toMatchObject({event:'object.grabbed',source:'book',timeout:{op:'add',args:[{value:2},{value:3}]}});
 expect(parseProgram(h.source()).error).toBeNull();
});
it('rejects a recursive function call created by visual controls without destroying the draft',()=>{
 const initial=structuredClone(empty);initial.functions.push({name:'helper',returns:'void',locals:[],parameters:[],body:[{id:'back',op:'call',function:'main',args:[]}]});
 const h=harness(initial),before=h.source();fireEvent.click(h.screen.getByLabelText('+ Call function in main'));
 expect(h.screen.getByRole('alert').textContent).toContain('Recursive');expect(h.source()).toBe(before);
});

it('preserves unavailable source until a complete valid repair is accepted',()=>{
 const source=' { preserved incomplete source ',onChange=vi.fn(),editing=vi.fn();
 const screen=render(<ProgramEditor source={source} targets={[]} onEditingChange={editing} onChange={onChange}/>);
 fireEvent.click(screen.getByRole('button',{name:'Repair source'}));
 expect((screen.getByLabelText('Program JSON') as HTMLTextAreaElement).value).toBe(source);
 fireEvent.change(screen.getByLabelText('Program JSON'),{target:{value:'still invalid'}});
 fireEvent.click(screen.getByRole('button',{name:'Update draft'}));expect(onChange).not.toHaveBeenCalled();
 fireEvent.click(screen.getByRole('button',{name:'Discard editor draft'}));
 fireEvent.click(screen.getByRole('button',{name:'Repair source'}));
 expect((screen.getByLabelText('Program JSON') as HTMLTextAreaElement).value).toBe(source);
 fireEvent.change(screen.getByLabelText('Program JSON'),{target:{value:JSON.stringify(empty)}});
 fireEvent.click(screen.getByRole('button',{name:'Update draft'}));
 expect(onChange).toHaveBeenCalledWith(JSON.stringify(empty));expect(editing).toHaveBeenLastCalledWith(false);
});

it('authors a named-only rotation module with schema controls and preserves it outside legacy tray adapters',()=>{
 const h=harness();fireEvent.click(h.screen.getByLabelText('+ Action in main'));
 h.click('Edit values block_1');h.change('Block action','object.rotation.set');
 h.change('target','book');h.change('pitch','20');h.change('yaw','90');h.change('roll','-10');h.click('Update draft');
 const expected={id:'block_1',op:'invoke',capability:'object.rotation.set',version:1,arguments:{target:'book',pitch:20,yaw:90,roll:-10},bindings:{}};
 const result=parseProgram(h.source());expect(result.error).toBeNull();expect(result.program?.functions[0].body).toEqual([expected]);
 expect(result.program).toEqual(JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-rotation.json','utf8')));expect(simpleProgramSteps(h.source())).toBeNull();
 expect(h.screen.queryByLabelText('Program JSON')).toBeNull();h.click('Edit values block_1');
 h.change('yaw','181');h.click('Update draft');expect(h.screen.getByRole('alert').textContent).toContain('contract');
 expect(JSON.parse(h.source()).functions[0].body[0]).toEqual(expected);
});

it('changes the typed animation channel with one shared form and keeps nested expressions explicit',()=>{
 const initial=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-visual.json','utf8')) as BehaviourProgram;
 const h=harness(initial);h.click('Edit values block_3');
 h.change('Animation source and channel','1');h.change('source.gesture','pointing');h.click('Update draft');
 const program=JSON.parse(h.source());const node=program.functions[0].body[0].then[0].body[0];
 expect(node.id).toBe('block_3');expect(node.capability).toBe('animation.play');expect(node.arguments.channel).toBe('upperBody');expect(node.arguments.seconds).toBe(.2);
 h.click('Edit values block_3');h.change('source.gesture input mode','expression');h.change('source.gesture value','speaking');h.click('Update draft');
 const changed=JSON.parse(h.source()).functions[0].body[0].then[0].body[0];expect(changed.bindings).toEqual({'source.gesture':{value:'speaking'}});
 expect(changed.arguments.source.gesture).toBe('pointing');expect(parseProgram(h.source()).error).toBeNull();
});
it('keeps an exact library motion ID when editing duration and removes incompatible fields only on an explicit source change',()=>{
 const initial=structuredClone(empty),motion='b'.repeat(32);initial.resources=['maestro'];
 initial.functions[0].body=[{id:'play',op:'invoke',capability:'animation.play',version:1,arguments:{target:'maestro',source:{kind:'library',motionId:motion},channel:'wholeTarget',seconds:1,loop:true},bindings:{}}];
 const h=harness(initial);h.click('Edit values play');h.change('seconds','2');h.click('Update draft');
 expect(JSON.parse(h.source()).functions[0].body[0].arguments.source).toEqual({kind:'library',motionId:motion});
 h.click('Edit values play');h.change('Animation source and channel','0');h.click('Update draft');
 const changed=JSON.parse(h.source()).functions[0].body[0];expect(changed.arguments.source).toEqual({kind:'gesture',gesture:'greeting'});expect(changed.arguments.loop).toBeUndefined();expect(changed.arguments.seconds).toBe(2);
});
