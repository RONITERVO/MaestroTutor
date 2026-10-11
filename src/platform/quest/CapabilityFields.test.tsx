// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {CapabilityFields,changeCapabilityVariant} from './CapabilityFields';
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
