// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import {useState} from 'react';
import {act,cleanup,fireEvent,render,within} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import {ConstructionSelectionControls} from './ConstructionSelectionControls';
import {CapabilityBrowser} from './CapabilityBrowser';
import {RoomAgentClient} from './roomAgentBridge';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
import {capabilityDefinition,capabilityResources,type CapabilityInvocation} from '../../../shared/capabilities';
import {readObjectSearchPage} from '../../../shared/objectSearch';
import {objectReferenceRequest} from '../../../shared/objectReference';
import type {CatalogRequest,CatalogView} from '../../../shared/roomCatalog';
import native from '../../../test-fixtures/browser/objectReferences.json';
import base from '../../../test-fixtures/browser/programBookState.json';

const pages=native.pages as unknown as {request:CatalogRequest;view:CatalogView}[];
const definitions=native.definitions as unknown as {request:CatalogRequest;view:CatalogView}[];
const entries=pages.flatMap(p=>readObjectSearchPage(p.view,p.request)!.entries);
const first=readObjectSearchPage(pages[0].view,pages[0].request)!.entries.find(e=>e.id!=='book'&&e.id!=='maestro')!;
const later=readObjectSearchPage(pages[1].view,pages[1].request)!.entries.find(e=>e.id!=='book'&&e.id!=='maestro')!;
const clients:RoomAgentClient[]=[];
afterEach(()=>{cleanup();clients.splice(0).forEach(c=>c.cancel());});
function setup(members:string[]=[],extra:Partial<RoomAgentState>={}) {
 const client=new RoomAgentClient();clients.push(client);
 let state={...structuredClone(base),session:'d'.repeat(32),ack:0,revision:1,inspection:null,constructionSelection:{stateId:'c'.repeat(32),members,collecting:false},objects:entries.map(e=>({...base.objects[0],...e,objectRevision:999})),capabilities:[...new Set([...base.capabilities,'objectSearch.v1','constructionSelection.v1','constructionCapture.v1','moduleLibrary.v1','creationPrototypes.v1','catalog.v1','catalogVocabulary.v1','factQueries.v1','structuredValues.v1'])],...extra} as RoomAgentState;
 expect(client.receive(state)).toBe(true);
 function Harness({active=true}:{active?:boolean}) {const [call,setCall]=useState<CapabilityInvocation|null>(null);return call?<CapabilityBrowser client={client} onClose={()=>setCall(null)} initialCall={call}/>:<ConstructionSelectionControls client={client} disabled={false} active={active} onCapture={setCall}/>;}
 const screen=render(<Harness/>);
 const receive=async(more:Partial<RoomAgentState>={})=>{state={...state,...more,revision:state.revision+1,ack:client.snapshot().request?.sequence??state.ack};await act(async()=>{expect(client.receive(state)).toBe(true);});};
 const reply=(view:CatalogView,more:Partial<RoomAgentState>={})=>receive({ok:true,catalog:view,...more});
 const acknowledge=async()=>{const input=client.snapshot().request!.commands[0].execution!;if(input.operation!=='start')throw Error('Expected native selection');const args=input.call.arguments;const selection={stateId:crypto.randomUUID().replace(/-/g,''),members:args.members as string[],collecting:args.collecting as boolean};const summary={id:input.runId!,capability:input.call.id,version:1,resources:capabilityResources(input.call.id,args),phase:'completed' as const,status:'Selected',output:selection};await receive({ok:true,constructionSelection:selection,execution:{...state.execution!,nextRunId:crypto.randomUUID().replace(/-/g,''),selected:{...summary,call:input.call},outcomes:[summary]}});};
 const expand=()=>{const details=screen.container.querySelector('details')!;details.open=true;fireEvent(details,new Event('toggle'));};
 const search=()=>within(screen.getByRole('region',{name:'Construction piece search'}));
 const order=()=>within(screen.getByRole('list',{name:'Construction piece order'}));
 return {client,screen,reply,receive,acknowledge,expand,search,order,Harness};
}
it('searches only when requested and keeps native order/origin across pages, removal and failed selection',async()=>{
 const {client,screen,reply,receive,acknowledge,expand,search,order}=setup();expect(client.snapshot().request).toBeNull();expand();expect(client.snapshot().request).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Find construction pieces'}));expect(client.snapshot().request!.commands[0].catalog).toEqual(pages[0].request);await reply(pages[0].view);
 fireEvent.click(search().getByRole('button',{name:new RegExp(first.name)}));expect(screen.queryByRole('list')).toBeNull();await acknowledge();expect(order().getAllByRole('listitem')[0].textContent).toContain(first.name+' · origin');
 fireEvent.click(search().getByRole('button',{name:'Next objects'}));await reply(pages[1].view);expect(order().getAllByRole('listitem')).toHaveLength(1);
 fireEvent.click(search().getByRole('button',{name:new RegExp(later.name)}));expect(client.snapshot().request!.commands[0].execution).toMatchObject({call:{arguments:{members:[first.id,later.id]}}});await receive({ok:false,status:'Selection changed; retry'});expect(order().getAllByRole('listitem')).toHaveLength(1);expect(search().getByRole('button',{name:new RegExp(later.name)}).getAttribute('aria-pressed')).toBe('false');
 fireEvent.click(search().getByRole('button',{name:new RegExp(later.name)}));await acknowledge();
 fireEvent.click(order().getByRole('button',{name:'Move '+later.name+' earlier'}));await acknowledge();expect(order().getAllByRole('listitem')[0].textContent).toContain(later.name+' · origin');
 fireEvent.click(order().getByRole('button',{name:'Remove '+first.name+' from construction'}));await acknowledge();expect(order().getAllByRole('listitem')).toHaveLength(1);
 fireEvent.click(order().getByRole('button',{name:'Locate '+later.name}));expect(client.snapshot().request!.commands).toEqual([{action:'inspect',target:later.id}]);await receive();
});
it('retains chosen pieces when a continuation expires and can refresh without changing selection',async()=>{
 const {client,screen,reply,expand,search,order}=setup([first.id]);expand();fireEvent.click(screen.getByRole('button',{name:'Find construction pieces'}));await reply(pages[0].view);
 fireEvent.click(search().getByRole('button',{name:'Next objects'}));await reply({...pages[1].view,available:false,value:null} as CatalogView);
 expect(search().getByRole('status').textContent).toContain('Search again');expect(order().getAllByRole('listitem')[0].textContent).toContain(first.name+' · origin');
 fireEvent.click(search().getByRole('button',{name:'Search objects'}));expect(client.snapshot().request!.commands[0].catalog).toEqual(pages[0].request);await reply(pages[0].view);expect(search().getByRole('button',{name:new RegExp(first.name)}).getAttribute('aria-pressed')).toBe('true');
});
it('allows removal at the 16-piece limit but excludes new pieces and built-ins',async()=>{
 const extras=Array.from({length:15},(_,i)=>({...base.objects[0],id:(i+1).toString(16).padStart(32,'0'),name:'Extra '+i}));
 const objects=entries.map(e=>({...base.objects[0],...e,objectRevision:1}));const {screen,reply,expand,search,acknowledge}=setup([first.id,...extras.map(e=>e.id)],{objects:[...objects,...extras] as RoomAgentState['objects']});
 expand();fireEvent.click(screen.getByRole('button',{name:'Find construction pieces'}));await reply(pages[0].view);
 for(const e of readObjectSearchPage(pages[0].view,pages[0].request)!.entries)expect((search().getByRole('button',{name:new RegExp(e.name)}) as HTMLButtonElement).disabled).toBe(e.id!==first.id);
 fireEvent.click(search().getByRole('button',{name:new RegExp(first.name)}));await acknowledge();expect(screen.getAllByRole('listitem')).toHaveLength(15);expect((search().getByRole('button',{name:/Book/}) as HTMLButtonElement).disabled).toBe(true);
});
it('does not start a search while the workshop is hidden or the details are closed',async()=>{
 const {client,screen,Harness,expand,reply}=setup();expand();screen.rerender(<Harness active={false}/>);fireEvent.click(screen.getByRole('button',{name:'Find construction pieces'}));expect(client.snapshot().request).toBeNull();
 const details=screen.container.querySelector('details')!;details.open=false;fireEvent(details,new Event('toggle'));screen.rerender(<Harness/>);expect(client.snapshot().request).toBeNull();expand();await reply(pages[0].view);
});
it.each([false,true])('reads all exact member revisions in draft order atomically (second unavailable: %s)',async(unavailable)=>{
 const members=[later.id,first.id],{client,screen,reply,expand}=setup(members);expand();fireEvent.click(screen.getByRole('button',{name:'Review reusable construction'}));const definition=capabilityDefinition('program.module.captureConstruction')!;
 await reply({operation:'inspect',capability:definition.id,version:1,definition,status:'Ready'});
 const args=()=>JSON.parse((screen.getByLabelText('Action arguments') as HTMLTextAreaElement).value);expect(args().members).toEqual(members.map((target,i)=>({target,revision:0,slot:'piece_'+(i+1)})));
 expect((screen.getByRole('button',{name:'Check availability'}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.click(screen.getByRole('button',{name:'Load current values'}));
 for(let i=0;i<members.length;i++){
  expect(client.snapshot().request!.commands).toEqual([{action:'catalog',catalog:objectReferenceRequest(members[i])}]);expect(args().members.every((m:{revision:number})=>m.revision===0)).toBe(true);
  const view=definitions.find(d=>JSON.stringify(d.request)===JSON.stringify(objectReferenceRequest(members[i])))!.view;
  await reply(i===1&&unavailable?{...view,available:false,value:null} as CatalogView:view);
 }
 const ready=!(screen.getByRole('button',{name:'Check availability'}) as HTMLButtonElement).disabled;expect(ready,screen.container.textContent??undefined).toBe(!unavailable);
 expect(args().members.map((m:{target:string})=>m.target)).toEqual(members);expect(args().members.every((m:{revision:number})=>unavailable?m.revision===0:m.revision>0&&m.revision!==999)).toBe(true);
 expect((screen.getByRole('button',{name:'Run action now'}) as HTMLButtonElement).disabled).toBe(unavailable);expect(client.snapshot().request).toBeNull();
});
