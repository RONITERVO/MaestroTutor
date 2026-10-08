// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {act,cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {useState} from 'react';
import {ResourceChoiceFields} from './ResourceChoiceFields';
import {CapabilityBrowser} from './CapabilityBrowser';
import {RoomAgentClient} from './roomAgentBridge';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {capabilityDefinition} from '../../../shared/capabilities';
import type {CatalogView} from '../../../shared/roomCatalog';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
import native from '../../../test-fixtures/browser/programBookState.json';
afterEach(cleanup);
const first={id:'a'.repeat(32),name:'Landscape',revision:3},second={id:'b'.repeat(32),name:'Landscape',revision:8};
const definition=()=>capabilityDefinition('object.visibility.assign')!;
const fact=(id:string,value:unknown,args:Record<string,unknown>={offset:0}):CatalogView=>({operation:'inspect',category:'facts',capability:id,version:1,definition:behaviourFact(id)!,arguments:args,available:true,value,status:'Available'}) as CatalogView;
const list=(entries=[first,second],offset=0,total=entries.length)=>fact('visibility.layers',{offset,total,pageSize:3,entries},{offset});
function setup(){
 const client=new RoomAgentClient();let state={...structuredClone(native),ack:0,revision:1,catalog:null,capabilities:[...new Set([...native.capabilities,'catalog.v1','catalogVocabulary.v1','factQueries.v1','visibilityLayers.v1','structuredValues.v1'])]} as RoomAgentState;expect(client.receive(state)).toBe(true);
 const receive=async(catalog:CatalogView|null,more:Partial<RoomAgentState>={})=>{state={...state,...more,revision:state.revision+1,ack:client.snapshot().request?.sequence??state.ack,catalog};await act(async()=>{expect(client.receive(state)).toBe(true);});};
 return {client,receive,session:state.session};
}
it('distinguishes duplicate names, selects exact pairs, clears explicitly and never executes from selection or refresh',async()=>{
 const {client,receive,session}=setup(),changed=vi.fn();
 function Harness(){const [value,setValue]=useState(definition().example!);return <><ResourceChoiceFields schema={definition().input} value={value} onChange={(v,fields)=>{changed(v,fields);setValue(v as typeof value);}} client={client} ready session={session}/><pre data-testid="draft">{JSON.stringify(value)}</pre></>;}
 const screen=render(<Harness/>);expect(client.snapshot().request).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Load saved visual layer'}));expect(client.snapshot().request?.commands[0]).toMatchObject({action:'catalog',catalog:{capability:'visibility.layers',arguments:{offset:0}}});await receive(list());
 expect(screen.getByRole('option',{name:new RegExp(first.id)})).toBeTruthy();expect(screen.getByRole('option',{name:new RegExp(second.id)})).toBeTruthy();
 fireEvent.change(screen.getByLabelText('Choose visual layer'),{target:{value:JSON.stringify([second.id,8])}});expect(changed).toHaveBeenLastCalledWith({...definition().example,layerId:second.id,layerRevision:8},['layerId','layerRevision']);expect(client.snapshot().request).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Load saved visual layer'}));expect(screen.queryByLabelText('Choose visual layer')).toBeNull();await receive(list([{...second,revision:9}]));expect(JSON.parse(screen.getByTestId('draft').textContent!).layerRevision).toBe(8);expect(changed).toHaveBeenCalledTimes(1);
 fireEvent.change(screen.getByLabelText('Choose visual layer'),{target:{value:'empty'}});expect(changed).toHaveBeenLastCalledWith(definition().example,['layerId','layerRevision']);expect(client.snapshot().request).toBeNull();client.cancel();
});
it('pages explicitly and refuses late responses after a newer draft or closed form',async()=>{
 const {client,receive,session}=setup(),changed=vi.fn(),s=definition().input,v=definition().example!;
 const props={schema:s,value:v,onChange:changed,client,ready:true,session};const screen=render(<ResourceChoiceFields {...props}/>);
 fireEvent.click(screen.getByRole('button',{name:'Load saved visual layer'}));await receive(list([first,second,{...first,id:'c'.repeat(32)}],0,4));
 fireEvent.click(screen.getByRole('button',{name:'Next visual layer'}));expect(client.snapshot().request?.commands[0].catalog).toMatchObject({arguments:{offset:3}});await receive(list([{...first,id:'d'.repeat(32)}],3,4));
 fireEvent.click(screen.getByRole('button',{name:'Previous visual layer'}));screen.rerender(<ResourceChoiceFields {...props} value={{...v,target:'book'}}/>);await receive(list());expect(screen.queryByLabelText('Choose visual layer')).toBeNull();expect(changed).not.toHaveBeenCalled();
 fireEvent.click(screen.getByRole('button',{name:'Load saved visual layer'}));screen.unmount();await receive(list());expect(changed).not.toHaveBeenCalled();client.cancel();
});
it('keeps a manually selected saved identity literal in reusable programs even if it matches current values',async()=>{
 const {client,receive}=setup(),insert=vi.fn(()=>null),d=definition(),screen=render(<CapabilityBrowser client={client} onClose={()=>{}} onInsert={insert} initialCall={{id:d.id,version:1,arguments:d.example!}}/>);
 await receive({operation:'inspect',capability:d.id,version:1,definition:d,status:'Available'});
 expect((screen.getByRole('button',{name:'Load saved visual layer'}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.click(screen.getByRole('button',{name:'Load current values'}));await receive(fact('object.visibility',{target:'maestro',revision:12,layerId:first.id,layerRevision:3,opacity:.5,realDepth:false,temporary:false},{target:'maestro'}));
 fireEvent.click(screen.getByRole('button',{name:'Load saved visual layer'}));await receive(list());
 expect((screen.getByLabelText('Choose visual layer') as HTMLSelectElement).value).toBe(''); // Explicitly choosing the already bound layer must still fire a change.
 fireEvent.change(screen.getByLabelText('Choose visual layer'),{target:{value:JSON.stringify([first.id,3])}});
 expect((screen.getByLabelText('Keep current layerId when running') as HTMLInputElement).checked).toBe(false);expect((screen.getByLabelText('Keep current layerRevision when running') as HTMLInputElement).checked).toBe(false);
 fireEvent.click(screen.getByRole('button',{name:'Add read and action to draft'}));expect(insert).toHaveBeenCalledWith({id:d.id,version:1,arguments:{target:'maestro',revision:12,layerId:first.id,layerRevision:3}},{kind:'current',fields:['revision']});expect(client.snapshot().request).toBeNull();client.cancel();
});
