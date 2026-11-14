// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,within} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import {RuleWorkspace} from './RuleWorkspace';
import {ObjectReferenceProvider,ObjectReferenceSummary} from './ObjectReferenceField';
import {RoomAgentClient} from './roomAgentBridge';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
import {newRuleStep,type RuleStep,type RuleView} from '../../core-sdk/room/rules';
import {stepTargetSchema,stepInvocation} from '../../core-sdk/room/capabilitySteps';
import {sequenceProgram,simpleProgramSteps} from '../../core-sdk/room/programs';
import {validateCapabilityValue} from '../../../shared/capabilities';
import {readObjectSearchPage} from '../../../shared/objectSearch';
import {objectReferenceRequest} from '../../../shared/objectReference';
import type {CatalogRequest,CatalogView} from '../../../shared/roomCatalog';
import native from '../../../test-fixtures/browser/objectReferences.json';
import base from '../../../test-fixtures/browser/programBookState.json';
const pages=native.pages as unknown as {request:CatalogRequest;view:CatalogView}[];
const definitions=native.definitions as unknown as {request:CatalogRequest;view:CatalogView}[];
const first=readObjectSearchPage(pages[0].view,pages[0].request)!.entries.find(e=>e.id!=='book'&&e.id!=='maestro')!;
const later=readObjectSearchPage(pages[1].view,pages[1].request)!.entries.find(e=>e.id!=='book'&&e.id!=='maestro')!;
const clients:RoomAgentClient[]=[];
afterEach(()=>{cleanup();clients.splice(0).forEach(c=>c.cancel());});
function setup(step:RuleStep={...newRuleStep(0),id:'c'.repeat(32),targetId:later.id},extra:Partial<RoomAgentState>={}) {
 const client=new RoomAgentClient();clients.push(client);const sequence={id:'b'.repeat(32),name:'Wave',interruption:0,repeat:false,program:JSON.stringify(sequenceProgram([step]))};
 const rules:RuleView={revision:4,canUndo:false,canRedo:false,readOnly:false,status:'Ready',sequences:[{id:sequence.id,name:sequence.name,steps:1,repeat:false}],selected:sequence,bindings:[],buttons:[],bindingPage:0,bindingCount:0,running:[],queued:0};
 let state={...structuredClone(base),session:'d'.repeat(32),ack:0,revision:1,inspection:null,workspaceView:'rules',rules,objects:[],selectedId:later.id,capabilities:[...new Set([...base.capabilities,'objectSearch.v1','catalog.v1','catalogVocabulary.v1','factQueries.v1','structuredValues.v1'])],...extra} as RoomAgentState;
 expect(client.receive(state)).toBe(true);
 const tree=(active=true)=><ObjectReferenceProvider client={client} active={active}><RuleWorkspace client={client}/></ObjectReferenceProvider>;
 const screen=render(tree());
 const receive=async(more:Partial<RoomAgentState>={})=>{state={...state,...more,revision:state.revision+1,ack:client.snapshot().request?.sequence??state.ack};await act(async()=>{expect(client.receive(state)).toBe(true);});};
 const reply=(catalog:CatalogView)=>receive({catalog});
 return {client,screen,receive,reply,tree};
}
it('gets simple-step restrictions from the native variants and keeps draft targets independent of visible objects',()=>{
 for(let action=0;action<18;action++){
  const step=newRuleStep(action),schema=stepTargetSchema(step),call=stepInvocation(step);
  expect(schema!==undefined,call.id).toBe(![2,12,13].includes(action));
  if(schema){expect(schema['x-resource']).toBe('object');expect(validateCapabilityValue('',schema)).not.toBeNull();expect(validateCapabilityValue('maestro',schema)===null).toBe(![10,11,16,17].includes(action));expect(validateCapabilityValue(first.id,schema)===null).toBe(![1,4,5,9].includes(action));}
 }
});
it('preserves an off-inventory action target while reading and paging, then saves only an explicit choice',async()=>{
 const {client,screen,reply}=setup();expect(client.snapshot().request).toBeNull();const field=within(screen.getByRole('region',{name:'Step 1 target object choice'}));expect(field.getByText(later.id)).toBeTruthy();
 fireEvent.click(field.getByRole('button',{name:'Read Step 1 target'}));expect(client.snapshot().request!.commands[0].catalog).toEqual(objectReferenceRequest(later.id));await reply(definitions.find(d=>d.request.operation==='inspect'&&d.request.arguments?.target===later.id)!.view);expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.click(field.getByRole('button',{name:'Find Step 1 target'}));await reply(pages[0].view);fireEvent.click(field.getByRole('button',{name:'Next objects'}));await reply(pages[1].view);expect(field.getByText(later.id)).toBeTruthy();expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.click(field.getByRole('button',{name:'Previous objects'}));await reply(pages[0].view);fireEvent.click(field.getByRole('button',{name:new RegExp(first.name)}));expect(client.snapshot().request).toBeNull();fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));const command=client.snapshot().request!.commands[0];expect(command.action).toBe('rules');expect(command.rule?.action).toBe('edit');expect(simpleProgramSteps(command.rule!.edits![0].sequence!.program)![0].targetId).toBe(first.id);
});
it('requires an explicit creation target after changing a Maestro-only action to impulse, even without visible creations',async()=>{
 const {client,screen,reply}=setup({...newRuleStep(1),id:'c'.repeat(32)});fireEvent.change(screen.getByLabelText('Step 1 action'),{target:{value:'10'}});const field=within(screen.getByRole('region',{name:'Step 1 target object choice'}));expect(field.getByText('Choose an object')).toBeTruthy();
 fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));expect(client.snapshot().request).toBeNull();expect(screen.getByRole('status').textContent).toContain('Check the name, targets');
 fireEvent.click(field.getByRole('button',{name:'Find Step 1 target'}));await reply(pages[0].view);expect((field.getByRole('button',{name:/Book/}) as HTMLButtonElement).disabled).toBe(true);fireEvent.click(field.getByRole('button',{name:'Next objects'}));await reply(pages[1].view);expect((field.getByRole('button',{name:/Maestro/}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.click(field.getByRole('button',{name:new RegExp(later.name)}));fireEvent.click(screen.getByRole('button',{name:'Apply changes'}));const saved=client.snapshot().request!.commands[0].rule!.edits![0].sequence!;expect(simpleProgramSteps(saved.program)![0]).toMatchObject({action:10,targetId:later.id,impulse:{x:0,y:.6,z:0}});
});
it('retains a compatible off-inventory target on action change and uses Maestro only for a restricted variant',()=>{
 const {client,screen}=setup();fireEvent.change(screen.getByLabelText('Step 1 action'),{target:{value:'10'}});expect(within(screen.getByRole('region',{name:'Step 1 target object choice'})).getByText(later.id)).toBeTruthy();fireEvent.change(screen.getByLabelText('Step 1 action'),{target:{value:'1'}});expect(within(screen.getByRole('region',{name:'Step 1 target object choice'})).getByText('maestro',{selector:'code'})).toBeTruthy();expect(client.snapshot().request).toBeNull();
});
it('binds a trigger to the explicitly chosen saved object rather than the visible inventory',async()=>{
 const {client,screen,reply}=setup();fireEvent.change(screen.getByLabelText('When'),{target:{value:'4'}});const field=within(screen.getByRole('region',{name:'Trigger source object choice'}));fireEvent.click(field.getByRole('button',{name:'Find Trigger source'}));await reply(pages[0].view);fireEvent.click(field.getByRole('button',{name:new RegExp(first.name)}));expect(client.snapshot().request).toBeNull();fireEvent.click(screen.getByRole('button',{name:'Add trigger'}));expect(client.snapshot().request!.commands[0].rule?.edits?.[0]).toMatchObject({kind:'bind',binding:{sourceId:first.id,trigger:4}});
});
it('opens the exact off-inventory inspector and starts a new behaviour with an explicit Maestro gesture',async()=>{
 const {client,screen,receive}=setup();fireEvent.click(screen.getByRole('button',{name:'Objects'}));expect(client.snapshot().request!.commands).toEqual([{action:'inspect',target:later.id}]);await receive();fireEvent.click(screen.getByRole('button',{name:'+ Behaviour'}));const saved=client.snapshot().request!.commands[0].rule!.edits![0].sequence!;expect(simpleProgramSteps(saved.program)![0]).toMatchObject({action:1,targetId:'maestro'});
});
it('does not start a deferred search after hiding the behaviour workspace',async()=>{
 const {client,screen,reply,tree}=setup();const field=within(screen.getByRole('region',{name:'Step 1 target object choice'}));fireEvent.click(field.getByRole('button',{name:'Find Step 1 target'}));screen.rerender(tree(false));await reply(pages[0].view);expect(screen.queryByRole('button',{name:'Next objects'})).toBeNull();expect(client.snapshot().request).toBeNull();screen.rerender(tree());expect(client.snapshot().request).toBeNull();
});
it('offers an exact native read for a read-only reference and keeps its ID after an unavailable reply',async()=>{
 const {client,screen,reply}=setup();screen.rerender(<ObjectReferenceProvider client={client}><ObjectReferenceSummary id={later.id} objects={[]} label="Module requirement"/></ObjectReferenceProvider>);expect(screen.getByText('Not yet read')).toBeTruthy();expect(screen.queryByRole('button',{name:/Find/})).toBeNull();expect(client.snapshot().request).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Read Module requirement'}));const view=definitions.find(d=>d.request.operation==='inspect'&&d.request.arguments?.target===later.id)!.view;await reply({...view,available:false,value:null} as CatalogView);expect(screen.getByText(later.id)).toBeTruthy();expect(screen.getByRole('alert').textContent).toContain('saved ID has been kept');expect(screen.queryByText(/Missing object|Unavailable object/)).toBeNull();expect(client.snapshot().request).toBeNull();
});

it('retains a dirty draft but refuses applying it across a temporary-room boundary at the same rule revision',async()=>{
 const {client,screen,receive}=setup();fireEvent.change(screen.getByLabelText('Step 1 seconds'),{target:{value:'1'}});expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(false);
 await receive({temporaryRoom:{active:true,pending:false,id:'c'.repeat(32),saveId:'e'.repeat(32),phase:'ready',error:'',savedRevision:0}});expect(screen.getByRole('status').textContent).toContain('Behaviours changed');expect((screen.getByRole('button',{name:'Apply changes'}) as HTMLButtonElement).disabled).toBe(true);expect((screen.getByLabelText('Step 1 seconds') as HTMLInputElement).value).toBe('1');expect(client.snapshot().request).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Reload latest'}));expect(screen.getByRole('status').textContent).not.toContain('Behaviours changed');expect(client.snapshot().request).toBeNull();
});
