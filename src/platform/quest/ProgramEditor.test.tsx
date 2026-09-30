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
 function Harness(){const [value,setValue]=useState(source);return <ProgramEditor source={value} targets={[]} eventsSupported eventFieldsSupported eventSubscriptionsSupported factQueriesSupported resultsSupported structuredSupported onEditingChange={()=>{}} onChange={next=>{source=next;setValue(next);}}/>;}
 const screen=render(<Harness/>);
 fireEvent.click(screen.getByLabelText('create new variable for objectId'));
 fireEvent.change(screen.getByLabelText('push argument target variable'),{target:{value:'objectId'}});
 const result=parseProgram(source);expect(result.error).toBeNull();
 expect(result.program?.functions[0].locals).toContainEqual({name:'objectId',initial:''});
 expect(result.program?.functions[0].body[0]).toMatchObject({results:{objectId:'objectId'}});
 expect(result.program?.functions[0].body[1]).toMatchObject({bindings:{target:{var:'objectId'}}});
 expect(screen.getByText('Create object → objectId (objectId)')).toBeTruthy();
 fireEvent.click(screen.getByLabelText('Edit block push'));
 expect(screen.getByLabelText('Program JSON').getAttribute('aria-label')).toBe('Program JSON');
});


const empty:BehaviourProgram={version:2,entry:'main',resources:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[]}]};
function harness(initial=empty,objects=[{id:'maestro',name:'Maestro'},{id:'book',name:'Book'}]) {
 let source=JSON.stringify(initial);
 const onChange=vi.fn();
 function Harness(){const [value,setValue]=useState(source);return <ProgramEditor source={value} targets={objects} eventsSupported eventFieldsSupported eventSubscriptionsSupported factQueriesSupported resultsSupported structuredSupported onEditingChange={()=>{}} onChange={next=>{source=next;setValue(next);onChange(next);}}/>;}
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
 h.change('Source and channel','1');h.change('source.gesture','pointing');h.click('Update draft');
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
 h.click('Edit values play');h.change('Source and channel','0');h.click('Update draft');
 const changed=JSON.parse(h.source()).functions[0].body[0];expect(changed.arguments.source).toEqual({kind:'gesture',gesture:'greeting'});expect(changed.arguments.loop).toBeUndefined();expect(changed.arguments.seconds).toBe(2);
});

it('switches creation kinds with native examples and preserves result destinations and common expression wiring',()=>{
 const initial=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-create.json','utf8')) as BehaviourProgram;
 const first=initial.functions[0].body[0];if(first.op!=='invoke')throw new Error('Expected creation');first.bindings={x:{value:.4}};
 const h=harness(initial);h.click('Edit values create');h.change('Creation kind','1');h.click('Update draft');
 const result=JSON.parse(h.source()),changed=result.functions[0].body[0];
 expect(changed).toMatchObject({id:first.id,capability:'object.create',results:first.results,bindings:first.bindings,arguments:{kind:'recipe',name:first.arguments.name,x:first.arguments.x,scale:first.arguments.scale,recipe:{playing:false}}});
 expect(changed.arguments.recipe.parts).toHaveLength(19);expect(changed.arguments.shape).toBeUndefined();expect(changed.arguments.red).toBeUndefined();
 expect(result.functions[0].body[1]).toEqual(initial.functions[0].body[1]);expect(parseProgram(h.source()).error).toBeNull();
 h.click('Edit values create');h.change('name','My teaching robot');h.click('Update draft');
 expect(JSON.parse(h.source()).functions[0].body[0].arguments.recipe).toEqual(changed.arguments.recipe);
});

it('lets users select typed collision fields through the same canonical blocks and preserves them in source',()=>{
 const initial=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-contact.json','utf8'));
 delete initial.functions[0].body[0].body[0].fields;
 const h=harness(initial);h.click('Edit values contact');
 h.change('Event field speed','speed');h.change('Event field otherKind','kind');h.change('Event field otherId','other');h.change('Event field y','height');h.click('Update draft');
 const expected=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-contact.json','utf8'));
 expect(JSON.parse(h.source())).toEqual(expected);expect(parseProgram(h.source()).error).toBeNull();
 h.click('Edit values contact');h.change('Await event','object.tapped');h.click('Update draft');
 expect(JSON.parse(h.source()).functions[0].body[0].body[0].fields).toBeUndefined();
});
it('keeps new physical events unavailable in editors connected to an older runtime',()=>{
 const source=readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-contact.json','utf8');
 const screen=render(<ProgramEditor source={source} targets={[]} eventsSupported onChange={()=>{}} onEditingChange={()=>{}}/>);
 fireEvent.click(screen.getByRole('button',{name:'Edit values contact'}));
 expect((screen.getByRole('option',{name:'object.collided'}) as HTMLOptionElement).disabled).toBe(true);
 expect(screen.getByRole('group',{name:'Store event details'}).hasAttribute('disabled')).toBe(true);
});

it('edits native subscription inputs, expressions and field destinations without losing program structure',()=>{
 const initial=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-proximity.json','utf8'));
 const h=harness(initial);h.click('Edit values near');expect(h.screen.getByRole('group',{name:'Event subscription'})).toBeTruthy();
 h.change('Event source','book');h.change('Event target','maestro');h.change('Event radius','.7');h.change('Event hysteresis','.2');h.change('Event transition','enter');
 h.change('Event radius input mode','expression');h.change('Event radius source','var:distance');h.change('Event field distance','');h.click('Update draft');
 const result=JSON.parse(h.source());const wait=result.functions[0].body[0].body[0];
 expect(wait).toMatchObject({version:1,arguments:{source:'book',target:'maestro',radius:.7,hysteresis:.2,transition:'enter'},bindings:{radius:{var:'distance'}}});
 expect(wait.fields).toEqual({inside:'inside',otherId:'other'});expect(result.functions[0].body[0].body.slice(1)).toEqual(initial.functions[0].body[0].body.slice(1));
 expect(parseProgram(h.source()).error).toBeNull();h.click('Edit values near');h.change('Await event','object.tapped');h.click('Update draft');
 const scalar=JSON.parse(h.source()).functions[0].body[0].body[0];expect(scalar.arguments).toBeUndefined();expect(scalar.bindings).toBeUndefined();expect(scalar.version).toBeUndefined();
 h.click('Edit values near');h.change('Await event','object.proximity.changed');h.click('Update draft');
 expect(JSON.parse(h.source()).functions[0].body[0].body[0]).toMatchObject({version:1,arguments:{source:'maestro',target:'book',radius:.5,hysteresis:.05,transition:'either'},bindings:{}});
});


it('creates, reuses and renames a typed function entirely through visual controls',()=>{
 const h=harness();
 h.click('+ Function');h.change('Function name','scaledDelay');h.change('Function return type','number');
 h.click('Add parameter');h.change('Parameter 1 name','seconds');
 h.click('Add parameter');h.change('Parameter 2 name','factor');h.click('Update draft');
 h.click('Edit values return_1');h.change('Return value source','op:mul');
 h.change('Return value left source','var:seconds');h.change('Return value right source','var:factor');h.click('Update draft');
 h.click('Edit function main');h.click('Add local variable');h.change('Variable 1 name','delay');h.click('Update draft');
 for(const [index,seconds] of [[1,'.25'],[3,'1']] as const){
  fireEvent.click(h.screen.getByLabelText('+ Call function in main'));
  h.click('Edit values block_'+index);h.change('Argument seconds value',seconds);h.change('Argument factor value','2');h.change('Function result','delay');h.click('Update draft');
  fireEvent.click(h.screen.getByLabelText('+ Action in main'));
  h.click('Edit values block_'+(index+1));h.change('seconds input mode','expression');h.change('seconds source','var:delay');h.click('Update draft');
 }
 h.click('Edit function scaledDelay');h.change('Function name','computeDelay');h.change('Parameter 2 name','scale');h.click('Move parameter 2 up');h.click('Update draft');
 const expected=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-functions.json','utf8'));
 expect(JSON.parse(h.source())).toEqual(expected);expect(h.screen.queryByLabelText('Program JSON')).toBeNull();
 expect(h.screen.getAllByText('Pause · delay seconds')).toHaveLength(2);
 // Adding an action to a typed function puts it before its final return, so it actually executes.
 fireEvent.click(h.screen.getByLabelText('+ Action in computeDelay'));
 expect(JSON.parse(h.source()).functions[1].body.map((n:{op:string})=>n.op)).toEqual(['invoke','return']);
});
it('keeps invalid function changes open and protects the starting signature',()=>{
 const h=harness();h.click('Edit function main');
 expect((h.screen.getByRole('button',{name:'Add parameter'}) as HTMLButtonElement).disabled).toBe(true);
 h.change('Function name','not a name');h.click('Update draft');
 expect(h.screen.getByRole('alert').textContent).toContain('function name');expect(JSON.parse(h.source())).toEqual(empty);
 expect((h.screen.getByLabelText('Function name') as HTMLInputElement).value).toBe('not a name');
 h.change('Function name','start');h.click('Update draft');expect(JSON.parse(h.source()).entry).toBe('start');
});
it('refuses a stale signature editor after the incoming program changes',()=>{
 const source=JSON.stringify(empty),onChange=vi.fn(),editing=vi.fn();
 const screen=render(<ProgramEditor source={source} targets={[]} onChange={onChange} onEditingChange={editing}/>);
 fireEvent.click(screen.getByRole('button',{name:'Edit function main'}));
 fireEvent.change(screen.getByLabelText('Function name'),{target:{value:'start'}});
 const changed=structuredClone(empty);changed.functions[0].locals.push({name:'newData',initial:7});
 screen.rerender(<ProgramEditor source={JSON.stringify(changed)} targets={[]} onChange={onChange} onEditingChange={editing}/>);
 fireEvent.click(screen.getByRole('button',{name:'Update draft'}));
 expect(screen.getByRole('alert').textContent).toContain('Program changed');expect(onChange).not.toHaveBeenCalled();
});

it('creates a list of typed records and edits its data without source',()=>{
 const h=harness();h.click('Edit function main');h.click('Add local variable');h.change('Variable 1 name','items');
 h.change('Variable 1 type','list');h.change('Variable 1 type item type','record');
 h.change('Variable 1 type item type new field name','red');h.click('Add Variable 1 type item type field');
 h.change('Variable 1 type item type new field name','id');h.click('Add Variable 1 type item type field');h.change('Variable 1 type item type id type','text');
 h.click('Add Initial value 1 item');h.change('Initial value 1 item 1 red','.2');h.change('Initial value 1 item 1 id','book');h.click('Update draft');
 const program=JSON.parse(h.source());expect(program.version).toBe(3);expect(program.dataVersion).toBe(1);
 expect(program.functions[0].locals[0]).toEqual({name:'items',type:{list:{record:{red:'number',id:'text'}}},initial:[{red:.2,id:'book'}]});
 fireEvent.click(h.screen.getByLabelText('+ Set variable in main'));h.click('Edit values block_1');
 const source=h.screen.getByLabelText('Assigned value source') as HTMLSelectElement;
 const append=Array.from(source.options).find(o=>o.textContent==='append in items')!;h.change('Assigned value source',append.value);
 h.change('Assigned value item value red','.7');h.change('Assigned value item value id','maestro');h.click('Update draft');
 expect(JSON.parse(h.source()).functions[0].body[0]).toMatchObject({op:'set',value:{op:'append',args:[{var:'items'},{value:{red:.7,id:'maestro'}}]}});
 expect(h.screen.queryByLabelText('Program JSON')).toBeNull();expect(parseProgram(h.source()).error).toBeNull();
});

it('keeps an inferred nested list type when a human removes its last initial item',()=>{
 const initial=structuredClone(empty);initial.version=3;initial.dataVersion=1;initial.state=[];initial.events=[];
 initial.functions[0].locals=[{name:'group',initial:{ids:['book']}}];
 const h=harness(initial);h.click('Edit function main');h.click('Remove Initial value 1 ids item 1');
 h.click('Update draft');expect(parseProgram(h.source()).error).toBeNull();
 expect(JSON.parse(h.source()).functions[0].locals[0]).toEqual({name:'group',initial:{ids:[]},type:{record:{ids:{list:'text'}}}});
 h.click('Edit function main');h.click('Add Initial value 1 ids item');h.change('Initial value 1 ids item 1','maestro');h.click('Update draft');
 expect(JSON.parse(h.source()).functions[0].locals[0].initial).toEqual({ids:['maestro']});
});

it('authors and renames the native state-and-signal fixture entirely through visual controls',()=>{
 const h=harness();h.click('Edit state & signals');h.click('Add state variable');h.change('State 1 name','counter');
 h.click('Add state variable');h.change('State 2 name','history');h.change('State 2 type','list');h.change('State 2 type item type','number');
 h.click('Add named signal');h.change('Signal 1 name','user.input');h.click('Add named signal');h.change('Signal 2 name','user.output');h.click('Update draft');
 h.click('+ Function');h.change('Function name','remember');h.click('Add parameter');h.change('Parameter 1 name','amount');h.click('Update draft');
 fireEvent.click(h.screen.getByLabelText('+ Set state in remember'));h.click('Edit values block_1');h.change('Assigned value source','op:add');h.change('Assigned value left source','state:counter');h.change('Assigned value right source','var:amount');h.click('Update draft');
 fireEvent.click(h.screen.getByLabelText('+ Set state in remember'));h.click('Edit values block_2');h.change('Assignment destination','history');
 const choices=h.screen.getByLabelText('Assigned value source') as HTMLSelectElement;h.change('Assigned value source',Array.from(choices.options).find(o=>o.textContent==='append in history')!.value);h.change('Assigned value item source','var:amount');h.click('Update draft');
 fireEvent.click(h.screen.getByLabelText('+ Send event in remember'));h.click('Edit values block_3');h.change('Send named event','user.output');h.change('Event payload source','state:counter');h.click('Update draft');
 h.click('Edit function main');h.click('Add local variable');h.change('Variable 1 name','amount');h.click('Update draft');
 fireEvent.click(h.screen.getByLabelText('+ Event wait in main'));h.click('Edit values block_4');h.change('Await event','user.input');h.click('Update draft');
 fireEvent.click(h.screen.getByLabelText('+ If in main'));h.click('Edit values block_5');h.change('Condition source','var:received');h.click('Update draft');
 fireEvent.click(h.screen.getByLabelText('+ Call function in block_5 Then'));h.click('Edit values block_6');h.change('Argument amount source','var:amount');h.click('Update draft');
 fireEvent.click(h.screen.getByLabelText('+ Forever in main'));
 h.click('Edit state & signals');h.change('State 1 name','total');h.change('State 2 name','amounts');h.change('Signal 1 name','user.add');h.change('Signal 2 name','user.stored');h.click('Update draft');
 const expected=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-declarations.json','utf8'));expect(JSON.parse(h.source())).toEqual(expected);expect(h.screen.queryByLabelText('Program JSON')).toBeNull();
});
it('keeps invalid declaration drafts for repair and refuses to delete a used state',()=>{
 const initial=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-declarations.json','utf8'));const h=harness(initial),before=h.source();
 h.click('Edit state & signals');h.change('Signal 1 name','maestro.speaking.enter');h.click('Update draft');expect(h.screen.getByRole('alert').textContent).toContain('custom event');expect(h.source()).toBe(before);
 h.change('Signal 1 name','user.add');h.click('Remove state 1');h.click('Update draft');expect(h.screen.getByRole('alert').textContent).toContain('still used');expect(h.source()).toBe(before);h.click('Discard editor draft');
});
it('refuses stale declaration edits after an incoming program change',()=>{
 const initial=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-declarations.json','utf8')),onChange=vi.fn();const source=JSON.stringify(initial);
 const screen=render(<ProgramEditor source={source} targets={[]} eventsSupported structuredSupported onChange={onChange} onEditingChange={()=>{}}/>);
 fireEvent.click(screen.getByRole('button',{name:'Edit state & signals'}));fireEvent.change(screen.getByLabelText('State 1 name'),{target:{value:'sum'}});initial.state[0].initial=3;
 screen.rerender(<ProgramEditor source={JSON.stringify(initial)} targets={[]} eventsSupported structuredSupported onChange={onChange} onEditingChange={()=>{}}/>);fireEvent.click(screen.getByRole('button',{name:'Update draft'}));
 expect(screen.getByRole('alert').textContent).toContain('Program changed');expect(onChange).not.toHaveBeenCalled();
});

it('edits exported calls visually and inspects pinned module internals without changing them',()=>{
 const initial=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-modules.json','utf8')) as BehaviourProgram,h=harness(initial);
 expect(h.screen.getByLabelText('Pinned module first')).toBeTruthy();expect(h.screen.getByLabelText('Imported function first.privateAdd')).toBeTruthy();
 expect(h.screen.queryByLabelText('Edit values first.change')).toBeNull();expect(h.screen.queryByLabelText('Edit block first.change')).toBeNull();
 h.click('Edit values first');expect((h.screen.getByLabelText('Called function') as HTMLSelectElement).value).toBe('first.add');
 expect(h.screen.queryByRole('option',{name:'first.privateAdd'})).toBeNull();h.change('Called function','second.add');h.change('Argument amount value','7');h.click('Update draft');
 const edited=JSON.parse(h.source()) as BehaviourProgram;expect(edited.functions[0].body[0]).toMatchObject({module:'second',function:'add',args:[{value:7}]});expect(edited.imports).toEqual(initial.imports);
});
it('shows a native qualified module block as running without exposing an edit button',()=>{
 const source=readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-modules-nested.json','utf8');
 const screen=render(<ProgramEditor source={source} targets={[]} onChange={()=>{}} onEditingChange={()=>{}} run={{id:'a'.repeat(32),sequenceId:'b'.repeat(32),preparing:false,nodeId:'first.inner.change',functionName:'first.inner.privateAdd',status:'Running',state:[{name:'first.inner.count',type:'number',value:'3'}]}}/>);
 expect(screen.container.querySelector('[data-node-id="first.inner.change"]')?.className).toContain('rule-action-active');expect(screen.getByLabelText('Live program values').textContent).toContain('state.first.inner.count');expect(screen.queryByLabelText('Edit values first.inner.change')).toBeNull();
});

it('edits physics motion observations using native-generated fields and preserves the reaction',()=>{
 const initial=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-physics-motion.json','utf8'));
 const h=harness(initial,[{id:'b'.repeat(32),name:'Ball'},{id:'book',name:'Book'}]);h.click('Edit values settling');
 expect(h.screen.getByRole('group',{name:'Event subscription'})).toBeTruthy();expect([...h.screen.getByLabelText('Object').querySelectorAll('option')].some(x=>x.value==='book')).toBe(false);
 h.change('Speed threshold (m/s)','.03');h.change('Spin threshold (rad/s)','.2');h.change('Quiet period (seconds)','.8');h.change('Detect','either');h.change('When watching starts','baseline');
 h.change('Speed threshold (m/s) input mode','expression');h.change('Speed threshold (m/s) source','var:speed');h.change('Event field angularSpeed','');h.click('Update draft');
 const result=JSON.parse(h.source());expect(result.functions[0].body[0]).toMatchObject({arguments:{speedThreshold:.03,angularThreshold:.2,quietSeconds:.8,transition:'either',initial:'baseline'},bindings:{speedThreshold:{var:'speed'}},fields:{settled:'settled',speed:'speed',quietSeconds:'quiet'}});
 expect(result.functions[0].body.slice(1)).toEqual(initial.functions[0].body.slice(1));expect(parseProgram(h.source()).error).toBeNull();
});

it('edits a structured fact target and state binding through the same canonical expression',()=>{
 const original=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-object-facts.json','utf8')) as BehaviourProgram;
 const h=harness(original);h.click('Edit values read_before');h.change('Assigned value fact target mode','literal');h.change('Assigned value fact target','maestro');h.click('Update draft');
 expect(parseProgram(h.source()).program?.functions[0].body[0]).toMatchObject({value:{fact:'object.position',arguments:{target:'maestro'},bindings:{}}});
 h.click('Edit values read_before');h.change('Assigned value fact target mode','expression');h.change('Assigned value fact target expression source','state:target');h.click('Update draft');
 expect(parseProgram(h.source()).program?.functions[0].body[0]).toMatchObject({value:{bindings:{target:{state:'target'}}}});expect(parseProgram(h.source()).program?.resources).toEqual([]);
});
