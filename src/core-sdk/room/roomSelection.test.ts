// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {constructionCaptureCall,constructionSelectionCall,roomObjectLabel,validConstructionSelection} from '../../../shared/roomSelection';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {currentInputRequest} from '../../../shared/currentCapabilityInputs';
import {RoomAgentClient} from './roomAgentClient';
import {buildRoomAgentPrompt} from '../../../shared/prompts';
import native from '../../../test-fixtures/browser/programBookState.json';
const objects=[{id:'a'.repeat(32),name:'Brick',objectRevision:101},{id:'b'.repeat(32),name:'Brick',objectRevision:202}];
const selection={stateId:'c'.repeat(32),collecting:false,members:objects.map(o=>o.id)};
it('shares bounded ordered selection identities without exposing created-resource authority',()=>{
 expect(validConstructionSelection(selection,objects)).toBe(true);expect(validConstructionSelection({...selection,members:[]},objects)).toBe(true);
 for(const bad of [{...selection,extra:1},{...selection,stateId:'old'},{...selection,collecting:'yes'},{...selection,members:['book']},{...selection,members:[objects[0].id,objects[0].id]},{...selection,members:['f'.repeat(32)]},{...selection,members:Array.from({length:17},(_,i)=>(i+1).toString(16).padStart(32,'0'))}])expect(validConstructionSelection(bad,objects)).toBe(false);
 const call=constructionSelectionCall(selection,[...selection.members].reverse(),true);expect(validateCapabilityArguments(call.id,call.version,call.arguments)).toBeNull();expect(capabilityResources(call.id,call.arguments)).toEqual([...selection.members].reverse());
 expect(validateCapabilityArguments(call.id,1,{...call.arguments,members:[objects[0].id,objects[0].id]})).toContain("distinct");
 expect(validateCapabilityOutput(call.id,1,selection)).toBeNull();expect(capabilityDefinition(call.id)!.output!.properties!.members.items!['x-resource']).toBeUndefined();
 expect(capabilityDefinition(call.id)!.input["x-current"]!.fields).toEqual({stateId:["stateId"]});
 expect(currentInputRequest(capabilityDefinition(call.id)!.input,call.arguments)).toEqual({operation:'inspect',category:'facts',capability:'room.selection',version:1});
});
it('prepares exact editable capture members in selected order and distinguishes duplicate labels',()=>{
 const call=constructionCaptureCall(selection,objects);expect(call.arguments).toEqual({name:'My construction',members:[{target:objects[0].id,revision:101,slot:'piece_1'},{target:objects[1].id,revision:202,slot:'piece_2'}]});expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
 expect(roomObjectLabel(objects[0],objects)).toBe('Brick · aaaaaaaa');expect(roomObjectLabel(objects[1],objects)).toBe('Brick · bbbbbbbb');expect(roomObjectLabel(objects[0],[objects[0]])).toBe('Brick');const duplicates=[objects[0],{...objects[0],id:'a'.repeat(31)+'b'}];expect(roomObjectLabel(duplicates[0],duplicates)).not.toBe(roomObjectLabel(duplicates[1],duplicates));
 expect(()=>constructionCaptureCall({...selection,members:[]},objects)).toThrow();expect(()=>constructionCaptureCall(selection,[objects[0]])).toThrow();expect(()=>constructionCaptureCall(selection,objects.map(o=>({...o,objectRevision:0})))).toThrow();
 (call.arguments.members as {revision:number}[])[0].revision=5;expect(objects[0].objectRevision).toBe(101);expect(selection.members).toEqual(objects.map(o=>o.id));
});
it('requires native selection evidence when advertised and shares it with the delegated agent',()=>{
 const base={...structuredClone(native),capabilities:[...native.capabilities,'constructionSelection.v1'],constructionSelection:selection,objects:objects.map(o=>({...native.objects[0],...o}))};
 const client=new RoomAgentClient();expect(client.receive(base)).toBe(true);expect(JSON.parse(buildRoomAgentPrompt('Save these pieces',base,[])).scene.constructionSelection).toEqual(selection);
 for(const bad of [undefined,null,{...selection,members:['f'.repeat(32)]},{...selection,stateId:'bad'}])expect(new RoomAgentClient().receive({...base,constructionSelection:bad})).toBe(false);
 expect(new RoomAgentClient().receive({...base,capabilities:native.capabilities,constructionSelection:undefined})).toBe(true);client.cancel();
});
