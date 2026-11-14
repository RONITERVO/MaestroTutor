// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import {useState} from 'react';
import {act,cleanup,fireEvent,render,within} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {ObjectReferenceProvider} from './ObjectReferenceField';
import {CapabilityFields} from './CapabilityFields';
import {CapabilityBrowser} from './CapabilityBrowser';
import {ProgramBlockEditor} from './ProgramBlockEditor';
import {RoomAgentClient} from './roomAgentBridge';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
import type {CapabilitySchema} from '../../../shared/capabilities';
import {capabilityDefinition} from '../../../shared/capabilities';
import {objectReferenceRequest,readObjectReference} from '../../../shared/objectReference';
import {objectSearchRequest,readObjectSearchPage} from '../../../shared/objectSearch';
import type {CatalogRequest,CatalogView} from '../../../shared/roomCatalog';
import native from '../../../test-fixtures/browser/objectReferences.json';
import {parseProgram} from '../../core-sdk/room/programs';
import visual from '../../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-visual.json';

afterEach(cleanup);
type Read={request:CatalogRequest;view:CatalogView};
const pages=native.pages as unknown as Read[],definitions=native.definitions as unknown as Read[];
const target=native.dormantId;
const targetView=definitions.find(d=>d.request.operation==='inspect'&&d.request.arguments?.target===target)!.view;
const schema:CapabilitySchema={type:'string',maxLength:32,pattern:'^(book|maestro|[a-f0-9]{32})$','x-resource':'object'};
const state=(extra:Partial<RoomAgentState>={}):RoomAgentState=>({version:1,session:'a'.repeat(32),revision:1,sceneRevision:1,ack:0,ok:true,status:'Ready',canUndo:false,canRedo:false,physicsRunning:false,visible:true,created:[],capabilities:['objectSearch.v1','catalog.v1','catalogVocabulary.v1','factQueries.v1','structuredValues.v1'],objects:[],...extra});
const clients:RoomAgentClient[]=[];
afterEach(()=>{clients.splice(0).forEach(c=>c.cancel());});
const client=()=>{const c=new RoomAgentClient();c.receive(state());clients.push(c);return c;};
async function reply(c:RoomAgentClient,view:CatalogView|null,extra:Partial<RoomAgentState>={}) {
 const before=c.getSnapshot().state!,sequence=c.snapshot().request!.sequence;
 await act(async()=>{expect(c.receive({...before,revision:before.revision+1,ack:sequence,catalog:view,...extra})).toBe(true);});
}
function field(c:RoomAgentClient,initial=target,fieldSchema=schema,onChange=vi.fn()) {
 function Harness(){const [value,setValue]=useState(initial);return <ObjectReferenceProvider client={c}><CapabilityFields schema={fieldSchema} value={value} objects={[]} label="Target" onChange={next=>{onChange(next);setValue(next as string);}}/></ObjectReferenceProvider>;}
 return {screen:render(<Harness/>),onChange};
}
it('decodes actual native exact-ID snapshots and refuses mismatched, unavailable or malformed readings',()=>{
 const value=readObjectReference(targetView,target)!;expect(value.id).toBe(target);expect(value.revision).toBeGreaterThan(0);
 expect(readObjectReference(targetView,'book')).toBeNull();
 expect(readObjectReference({...targetView,available:false} as CatalogView,target)).toBeNull();
 const wrong=structuredClone(targetView) as Extract<CatalogView,{operation:'inspect';category:'facts'}>;wrong.value={...(wrong.value as object),revision:0} as typeof wrong.value;
 expect(readObjectReference(wrong,target)).toBeNull();
});
it('keeps an off-inventory selection and only reads its native metadata on request',async()=>{
 const c=client(),{screen,onChange}=field(c);expect(c.snapshot().request).toBeNull();expect(screen.getByText(target)).toBeTruthy();
 fireEvent.click(screen.getByRole('button',{name:'Read Target'}));expect(c.snapshot().request!.commands).toEqual([{action:'catalog',catalog:objectReferenceRequest(target)}]);await reply(c,targetView);
 expect(screen.getByText(new RegExp('Block · read at revision '+readObjectReference(targetView,target)!.revision))).toBeTruthy();expect(c.getSnapshot().state!.objects).toEqual([]);expect(onChange).not.toHaveBeenCalled();
});
it('chooses across native pages without replacing the current ID while browsing',async()=>{
 const c=client(),{screen,onChange}=field(c,'book');fireEvent.click(screen.getByRole('button',{name:'Find Target'}));await reply(c,pages[0].view);
 expect(onChange).not.toHaveBeenCalled();expect(screen.getByText('book',{selector:'code'})).toBeTruthy();
 fireEvent.click(screen.getByRole('button',{name:'Next objects'}));expect(c.snapshot().request!.commands[0].catalog).toEqual(pages[1].request);await reply(c,pages[1].view);
 expect(onChange).not.toHaveBeenCalled();const row=readObjectSearchPage(pages[1].view,pages[1].request)!.entries[0];
 fireEvent.click(screen.getByRole('button',{name:new RegExp(row.name)}));expect(onChange).toHaveBeenCalledExactlyOnceWith(row.id);expect(screen.queryByRole('button',{name:'Next objects'})).toBeNull();
});
it('applies the generated field constraints and cannot select an excluded built-in object',async()=>{
 const c=client(),{screen,onChange}=field(c,'maestro',{...schema,pattern:'^[a-f0-9]{32}$'});
 fireEvent.click(screen.getByRole('button',{name:'Find Target'}));await reply(c,pages[0].view);
 for(let i=0;i<pages.length;i++){
  if(i){fireEvent.click(screen.getByRole('button',{name:'Next objects'}));await reply(c,pages[i].view);}
  for(const row of readObjectSearchPage(pages[i].view,pages[i].request)!.entries){const button=screen.getByRole('button',{name:new RegExp(row.name)});expect((button as HTMLButtonElement).disabled).toBe(['book','maestro'].includes(row.id));}
 }
 expect(onChange).not.toHaveBeenCalled();expect(screen.getByText('maestro',{selector:'code'})).toBeTruthy();
});
it('keeps the saved ID when its native reading is unavailable and never invents an edit revision',async()=>{
 const c=client(),{screen,onChange}=field(c);fireEvent.click(screen.getByRole('button',{name:'Read Target'}));await reply(c,{...targetView,available:false,value:null} as CatalogView);
 expect(screen.getByRole('alert').textContent).toContain('saved ID has been kept');expect(screen.getByText(target)).toBeTruthy();expect(onChange).not.toHaveBeenCalled();
});
it('discards a late exact-ID reading after the draft target changes',async()=>{
 const c=client(),onChange=vi.fn();const tree=(value:string)=><ObjectReferenceProvider client={c}><CapabilityFields schema={schema} value={value} objects={[]} label="Target" onChange={onChange}/></ObjectReferenceProvider>;
 const screen=render(tree(target));fireEvent.click(screen.getByRole('button',{name:'Read Target'}));screen.rerender(tree('maestro'));await reply(c,targetView);
 expect(screen.getByText('maestro',{selector:'code'})).toBeTruthy();expect(screen.queryByText(/read at revision/)).toBeNull();expect(onChange).not.toHaveBeenCalled();
});
it('drops an in-flight reading when temporary-room identity changes even with the same session',async()=>{
 const c=client(),{screen,onChange}=field(c);fireEvent.click(screen.getByRole('button',{name:'Read Target'}));
 await reply(c,targetView,{temporaryRoom:{active:true,pending:false,id:'c'.repeat(32),saveId:'d'.repeat(32),phase:'ready',error:'',savedRevision:0}});
 expect(screen.queryByText(/read at revision/)).toBeNull();expect(onChange).not.toHaveBeenCalled();expect(c.snapshot().request).toBeNull();
});
it('retains the complete-list control when the native search feature is unavailable',()=>{
 const c=client();c.receive(state({revision:2,capabilities:[]}));const onChange=vi.fn();const screen=render(<ObjectReferenceProvider client={c}><CapabilityFields schema={schema} value="book" objects={[{id:'book',name:'Book'},{id:'maestro',name:'Maestro'}]} label="Target" onChange={onChange}/></ObjectReferenceProvider>);
 fireEvent.change(screen.getByLabelText('Target'),{target:{value:'maestro'}});expect(onChange).toHaveBeenCalledExactlyOnceWith('maestro');expect(c.snapshot().request).toBeNull();
});
it('uses the picker in nested program action fields without starting or saving the program',async()=>{
 const c=client(),program=parseProgram(JSON.stringify(visual)).program!,onChange=vi.fn();
 const node={id:'chosen',op:'invoke' as const,capability:'avatar.look',version:1,arguments:{target:'maestro',focus:{kind:'object',objectId:target},seconds:1},bindings:{}};
 const screen=render(<ObjectReferenceProvider client={c}><ProgramBlockEditor node={node} program={program} fn={program.functions[0]} objects={[]} onChange={onChange}/></ObjectReferenceProvider>);
 const choice=screen.getByRole('region',{name:'focus.objectId object choice'});fireEvent.click(within(choice).getByRole('button',{name:'Find focus.objectId'}));expect(c.snapshot().request!.commands[0].catalog).toEqual(objectSearchRequest());await reply(c,pages[0].view);expect(onChange).not.toHaveBeenCalled();
});
it('keeps native current-input guards separate from object discovery in the action catalog',async()=>{
 const c=client(),call={id:'object.create',version:1,arguments:{kind:'copy',target,revision:0,name:'',x:0,y:1,z:1}},definition=capabilityDefinition(call.id)!;
 const screen=render(<CapabilityBrowser client={c} onClose={()=>{}} initialCall={call}/>);
 await reply(c,{operation:'inspect',capability:call.id,version:1,status:'Available',definition} as CatalogView);
 expect((screen.getByRole('button',{name:'Check availability'}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.click(screen.getByRole('button',{name:'Load current values'}));expect(c.snapshot().request!.commands[0].catalog).toEqual(objectReferenceRequest(target));await reply(c,targetView);
 expect((screen.getByRole('button',{name:'Check availability'}) as HTMLButtonElement).disabled).toBe(false);
 fireEvent.click(screen.getByRole('button',{name:'Find Action inputs target'}));await reply(c,pages[0].view);
 const alternate=readObjectSearchPage(pages[0].view,pages[0].request)!.entries.find(e=>e.id!==target&&!['book','maestro'].includes(e.id))!;
 fireEvent.click(screen.getByRole('button',{name:new RegExp(alternate.name)}));
 expect((screen.getByRole('button',{name:'Check availability'}) as HTMLButtonElement).disabled).toBe(true);
 const args=JSON.parse((screen.getByLabelText('Action arguments') as HTMLTextAreaElement).value);expect(args.target).toBe(alternate.id);expect(args.revision).toBe(readObjectReference(targetView,target)!.revision);
 fireEvent.click(screen.getByRole('button',{name:'Load current values'}));expect(c.snapshot().request!.commands[0].catalog).toEqual(objectReferenceRequest(alternate.id));await reply(c,definitions.find(d=>d.request.operation==='inspect'&&d.request.arguments?.target===alternate.id)!.view);
 expect((screen.getByRole('button',{name:'Check availability'}) as HTMLButtonElement).disabled).toBe(false);
});

it('discards guarded action inputs when the temporary room changes during their read',async()=>{
 const c=client(),call={id:'object.create',version:1,arguments:{kind:'copy',target,revision:0,name:'',x:0,y:1,z:1}};
 const screen=render(<CapabilityBrowser client={c} onClose={()=>{}} initialCall={call}/>);
 await reply(c,{operation:'inspect',capability:call.id,version:1,status:'Available',definition:capabilityDefinition(call.id)!});
 fireEvent.click(screen.getByRole('button',{name:'Load current values'}));
 await reply(c,targetView,{temporaryRoom:{active:true,pending:false,id:'c'.repeat(32),saveId:'d'.repeat(32),phase:'ready',error:'',savedRevision:0}});
 expect(screen.queryByRole('button',{name:'Check availability'})).toBeNull();expect(screen.queryByText('Current values loaded. Review your changes before running.')).toBeNull();
});
