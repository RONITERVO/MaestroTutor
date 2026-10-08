// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {CapabilityFields} from './CapabilityFields';
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
