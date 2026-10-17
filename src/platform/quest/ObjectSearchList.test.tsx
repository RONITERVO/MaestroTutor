// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,waitFor} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import {ObjectSearchList} from './ObjectSearchList';
import {RoomWorkspace} from './RoomWorkspace';
import {RoomAgentClient} from './roomAgentBridge';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
import type {CatalogRequest,CatalogView} from '../../../shared/roomCatalog';
import {objectSearchRequest,readObjectSearchPage} from '../../../shared/objectSearch';
import native from '../../../test-fixtures/browser/objectSearch.json';
afterEach(cleanup);
const pages=native as unknown as {request:CatalogRequest;view:CatalogView}[];
const first=readObjectSearchPage(pages[0].view,pages[0].request)!;
const all=pages.flatMap(p=>readObjectSearchPage(p.view,p.request)!.entries);
const state=(extra:Partial<RoomAgentState>={}):RoomAgentState=>({version:1,session:'a'.repeat(32),revision:1,sceneRevision:1,ack:0,ok:true,status:'Ready',canUndo:false,canRedo:false,physicsRunning:false,visible:true,created:[],capabilities:['objectSearch.v1','catalog.v1','catalogVocabulary.v1','factQueries.v1','inspect.v1'],objects:all.map((e,i)=>({...e,objectRevision:1,position:{x:i/10,y:1,z:1},scale:1,color:{r:1,g:1,b:1,a:1},animated:false})),...extra});
const current=(client:RoomAgentClient)=>client.getSnapshot().state!;
async function reply(client:RoomAgentClient,view:CatalogView|null,extra:Partial<RoomAgentState>={}) {
 const before=current(client),sequence=client.snapshot().request!.sequence;
 await act(async()=>{expect(client.receive({...before,revision:before.revision+1,ack:sequence,catalog:view,...extra})).toBe(true);});
}
function start(active=true) {
 const client=new RoomAgentClient();expect(client.receive(state())).toBe(true);const selected:string[]=[];
 const screen=render(<ObjectSearchList client={client} active={active} disabled={false} onSelect={id=>selected.push(id)}/>);return {client,screen,selected};
}
it('accepts actual native pages and rejects mismatched filters, malformed cursors and incomplete pages',()=>{
 expect(first.entries).toHaveLength(3);expect(first.total).toBeGreaterThan(3);
 expect(pages.flatMap(p=>readObjectSearchPage(p.view,p.request)!.entries).map(e=>e.id)).toEqual(all.map(e=>e.id));
 expect(readObjectSearchPage(pages[0].view,objectSearchRequest('another'))).toBeNull();
 const view=structuredClone(pages[0].view) as Extract<CatalogView,{operation:'inspect'}>&{value:Record<string,unknown>};view.value.next='invalid';expect(readObjectSearchPage(view,pages[0].request)).toBeNull();
 const missing=structuredClone(pages[0].view) as typeof view;missing.value.entries=[];expect(readObjectSearchPage(missing,pages[0].request)).toBeNull();
});
it('browses forward and back with native cursors and leaves off-page objects saved',async()=>{
 const {client,screen,selected}=start();expect(client.snapshot().request!.commands[0].catalog).toEqual(pages[0].request);await reply(client,pages[0].view);
 expect(screen.getByRole('status').textContent).toContain(`1–3 of ${first.total}`);expect(screen.getAllByRole('button',{pressed:false})).toHaveLength(3);
 fireEvent.click(screen.getByRole('button',{name:'Next objects'}));expect(client.snapshot().request!.commands[0].catalog).toEqual(pages[1].request);await reply(client,pages[1].view);
 expect(current(client).objects).toHaveLength(first.total);expect(selected).toEqual([]);
 fireEvent.click(screen.getByRole('button',{name:'Previous objects'}));expect(client.snapshot().request!.commands[0].catalog).toEqual(pages[0].request);await reply(client,pages[0].view);expect(screen.getByRole('status').textContent).toContain('1–3');
});
it('requires explicit refresh after an unavailable continuation and never invents an empty world',async()=>{
 const {client,screen}=start();await reply(client,pages[0].view);fireEvent.click(screen.getByRole('button',{name:'Next objects'}));
 await reply(client,{...pages[1].view,available:false,value:null,status:'Fact unavailable'} as CatalogView);
 expect(screen.getByRole('status').textContent).toContain('Search again');expect(screen.queryByRole('button',{name:'Next objects'})).toBeNull();expect(current(client).objects).toHaveLength(first.total);
 fireEvent.click(screen.getByRole('button',{name:'Search objects'}));expect(client.snapshot().request!.commands[0].catalog).toEqual(objectSearchRequest());await reply(client,pages[0].view);
});
it('sends user filters through the shared fact and does not page using an unapplied filter',async()=>{
 const {client,screen}=start();await reply(client,pages[0].view);fireEvent.change(screen.getByLabelText('Find saved objects'),{target:{value:'apple'}});fireEvent.change(screen.getByLabelText('Object kind'),{target:{value:'Block'}});
 expect((screen.getByRole('button',{name:'Next objects'}) as HTMLButtonElement).disabled).toBe(true);fireEvent.click(screen.getByRole('button',{name:'Search objects'}));expect(client.snapshot().request!.commands[0].catalog).toEqual(objectSearchRequest('apple','Block'));
 await reply(client,{...pages[0].view,arguments:{query:'apple',kind:'Block',cursor:''},available:false,value:null} as CatalogView);
});
it('does not query while hidden or accept a late response from another room session',async()=>{
 const hidden=start(false);expect(hidden.client.snapshot().request).toBeNull();cleanup();
 const {client,screen}=start();await act(async()=>{client.receive(state({session:'b'.repeat(32)}));});
 await waitFor(()=>expect(client.snapshot().request?.session).toBe('b'.repeat(32)));expect(screen.queryByText(/1–3/)).toBeNull();await reply(client,pages[0].view);
});
it('selects a search result through the existing native inspector without deleting other objects',async()=>{
 const client=new RoomAgentClient();client.receive(state());const screen=render(<RoomWorkspace client={client}/>);await reply(client,pages[0].view);
 const row=screen.getAllByRole('button',{pressed:false})[0];fireEvent.click(row);const selected=first.entries[0];expect(client.snapshot().request!.commands).toEqual([{action:'inspect',target:selected.id}]);
 await reply(client,null,{inspection:{id:selected.id,objectRevision:1,recipe:null}});expect(screen.getByRole('heading',{name:selected.name})).toBeTruthy();expect(current(client).objects).toHaveLength(first.total);
});
