// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {CapabilityFields,changeCapabilityVariant,initialCapabilityValue} from './CapabilityFields';
import {capabilityDefinition,resolveCapabilitySchema,validateCapabilityArguments} from '../../../shared/capabilities';
afterEach(cleanup);
it('shows native float values readably without changing an untouched draft',()=>{
 const onChange=vi.fn();
 const {getByLabelText,rerender}=render(<CapabilityFields label="Cloud cover" objects={[]} schema={{type:'number'}} value={Math.fround(.6)} onChange={onChange}/>);
 expect((getByLabelText('Cloud cover') as HTMLInputElement).value).toBe('0.6');expect(onChange).not.toHaveBeenCalled();
 rerender(<CapabilityFields label="Cloud cover" objects={[]} schema={{type:'number'}} value={Math.fround(.04)} onChange={onChange}/>);
 expect((getByLabelText('Cloud cover') as HTMLInputElement).value).toBe('0.04');expect(onChange).not.toHaveBeenCalled();
 fireEvent.change(getByLabelText('Cloud cover'),{target:{value:'0.2'}});expect(onChange).toHaveBeenCalledExactlyOnceWith(.2);
});
it('retains double quantities and exact integer guards rather than rounding all numbers',()=>{
 const value=123456.789123456,onChange=vi.fn();
 const {getByLabelText,rerender}=render(<CapabilityFields label="Quantity" objects={[]} schema={{type:'number'}} value={value} onChange={onChange}/>);
 expect((getByLabelText('Quantity') as HTMLInputElement).value).toBe(String(value));
 rerender(<CapabilityFields label="Revision" objects={[]} schema={{type:'integer'}} value={2147483647} onChange={onChange}/>);
 expect((getByLabelText('Revision') as HTMLInputElement).value).toBe('2147483647');expect(onChange).not.toHaveBeenCalled();
});

it('lets the book switch to an imported clip without retaining the inactive tone recipe',()=>{
 const action=capabilityDefinition('audio.source.edit')!,call=action.example!;
 const schema=resolveCapabilitySchema(action.input,call)!.properties!.definition;
 const draft=changeCapabilityVariant(schema,1,call.definition,[]);
 expect(draft.kind).toBe('clip');expect(draft.name).toBe('Robot greeting');expect(draft).not.toHaveProperty('tone');
 draft.clip={assetHash:'b'.repeat(64),seconds:1};
 expect(validateCapabilityArguments(action.id,action.version,{...call,definition:draft})).toBeNull();
 const onChange=vi.fn(),screen=render(<CapabilityFields label="Sound" schema={schema} value={draft} objects={[]} onChange={onChange}/>);
 expect(screen.getByLabelText('Sound clip assetHash')).toBeTruthy();expect(screen.queryByLabelText('Sound tone wave')).toBeNull();
 fireEvent.change(screen.getByLabelText('Sound clip seconds'),{target:{value:'2'}});
 expect(onChange).toHaveBeenCalledWith({...draft,clip:{assetHash:'b'.repeat(64),seconds:2}});
 const tone=changeCapabilityVariant(schema,0,draft,[]);expect(tone.kind).toBe('tone');expect(tone).not.toHaveProperty('clip');
});

it('uses the same book variant form for a live endpoint without keeping clip or tone fields',()=>{
 const action=capabilityDefinition('audio.source.edit')!,call=action.example!;
 const schema=resolveCapabilitySchema(action.input,call)!.properties!.definition;
 const draft=changeCapabilityVariant(schema,2,call.definition,[]);draft.stream={endpoint:'https://radio.example/live.mp3'};
 expect(draft.kind).toBe('stream');expect(draft).not.toHaveProperty('tone');expect(draft).not.toHaveProperty('clip');
 expect(validateCapabilityArguments(action.id,action.version,{...call,definition:draft})).toBeNull();
 const onChange=vi.fn(),screen=render(<CapabilityFields label="Sound" schema={schema} value={draft} objects={[]} onChange={onChange}/>);
 fireEvent.change(screen.getByLabelText('Sound stream endpoint'),{target:{value:'https://other.example/live.mp3'}});
 expect(onChange).toHaveBeenCalledWith({...draft,stream:{endpoint:'https://other.example/live.mp3'}});
});

it('lets attention switch between object, place and user without hidden stale targets',()=>{
 const action=capabilityDefinition('avatar.look')!,call=action.example!,schema=action.input.properties!.focus;
 const point=changeCapabilityVariant(schema,2,call.focus,[]);point.point={x:2,y:1.6,z:2};
 expect(point.kind).toBe('point');expect(point).not.toHaveProperty('objectId');
 expect(validateCapabilityArguments(action.id,action.version,{...call,focus:point})).toBeNull();
 const onChange=vi.fn(),screen=render(<CapabilityFields label="Focus" schema={schema} value={point} objects={[]} onChange={onChange}/>);
 fireEvent.change(screen.getByLabelText('Focus point x'),{target:{value:'3'}});
 expect(onChange).toHaveBeenCalledWith({...point,point:{x:3,y:1.6,z:2}});
 const person=changeCapabilityVariant(schema,0,point,[]);
 expect(person).toEqual({kind:'user'});expect(validateCapabilityArguments(action.id,action.version,{...call,focus:person})).toBeNull();
 const object=changeCapabilityVariant(schema,1,person,[]);object.objectId='book';
 expect(object).toEqual({kind:'object',objectId:'book'});expect(validateCapabilityArguments(action.id,action.version,{...call,focus:object})).toBeNull();
});

it('requires an explicit object choice regardless of observed order or an example identity',()=>{
 const target=capabilityDefinition('object.delete')!.input.properties!.target;
 const objects=[{id:'a'.repeat(32),name:'Apple'},{id:'b'.repeat(32),name:'Tree'}];
 for(const observed of [objects,[...objects].reverse(),[]])expect(initialCapabilityValue({...target,examples:[objects[0].id]},observed)).toBe('');
 expect(initialCapabilityValue({...target,enum:objects.map(o=>o.id)},objects)).toBe('');
 expect(initialCapabilityValue({...target,enum:[objects[1].id]},[])).toBe(objects[1].id);
});
it('preserves an explicitly chosen off-view target when changing compatible action variants',()=>{
 const schema=capabilityDefinition('avatar.look')!.input.properties!.focus;
 const objectId='a'.repeat(32);
 expect(changeCapabilityVariant(schema,1,{kind:'object',objectId},[])).toEqual({kind:'object',objectId});
 expect(changeCapabilityVariant(schema,1,{kind:'user'},[{id:'book'}])).toEqual({kind:'object',objectId:''});
});


it('keeps horizontal destination when switching between ground and surface movement, and removes inactive height',()=>{
 const action=capabilityDefinition('object.navigate')!,pool='a'.repeat(32);
 const walk={target:'maestro',mode:'walk',destination:{x:2,y:-1,z:3},speed:.6};
 const surfaceIndex=action.input.oneOf!.findIndex(branch=>branch.properties?.mode.enum?.includes('surface'));
 const surface=changeCapabilityVariant(action.input,surfaceIndex,walk,[]);
 expect(surface.mode).toBe('surface');expect(surface.target).toBe('maestro');expect(surface.destination).toEqual({x:2,z:3});
 surface.habitat=pool;surface.immersion=.6;
 expect(validateCapabilityArguments(action.id,action.version,surface)).toBeNull();
 expect(validateCapabilityArguments(action.id,action.version,{...surface,destination:{x:2,y:-1,z:3}})).not.toBeNull();
 const onChange=vi.fn(),screen=render(<CapabilityFields label="Movement fields" schema={action.input} value={surface} objects={[]} onChange={onChange}/>);
 expect(screen.getByLabelText('Movement fields destination x')).toBeTruthy();
 expect(screen.queryByLabelText('Movement fields destination y')).toBeNull();
 fireEvent.change(screen.getByLabelText('Movement fields immersion'),{target:{value:'0.7'}});
 expect(onChange).toHaveBeenCalledWith({...surface,immersion:.7});
 const walkAgain=changeCapabilityVariant(action.input,0,surface,[]);
 expect(walkAgain.destination).toEqual({x:2,y:0,z:3});expect(walkAgain).not.toHaveProperty('habitat');expect(walkAgain).not.toHaveProperty('immersion');
 expect(validateCapabilityArguments(action.id,action.version,walkAgain)).toBeNull();
});
