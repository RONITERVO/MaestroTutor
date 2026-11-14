// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState} from 'react';
import {cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import {capabilityDefinition,capabilityInput,type CapabilitySchema} from '../../../shared/capabilities';
import {CapabilityFields} from './CapabilityFields';
afterEach(cleanup);
it('shows readable starter names and bundled previews while retaining the exact hash in the draft',()=>{
 const branch=capabilityDefinition('object.create')!.input.oneOf!.find(v=>(v.examples?.[0] as {kind?:string})?.kind==='template')!;
 const initial=branch.examples![0] as Record<string,unknown>;
 function Draft(){const [value,setValue]=useState<unknown>(initial);return <><CapabilityFields schema={branch} value={value} label="Creation" objects={[]} onChange={setValue}/><output>{JSON.stringify(value)}</output></>;}
 const screen=render(<Draft/>);const labels=branch.properties!.templateHash['x-enum-labels']!;const cup=Object.keys(labels).find(hash=>labels[hash]==='Cup')!,brick=Object.keys(labels).find(hash=>labels[hash]==='Building brick')!;
 expect((screen.getByLabelText('Creation templateHash') as HTMLSelectElement).value).toBe(cup);
 expect(screen.getByRole('option',{name:'Cup'})).toBeTruthy();expect(screen.getByAltText('Cup preview').getAttribute('src')).toContain(cup+'.png');
 fireEvent.change(screen.getByLabelText('Creation templateHash'),{target:{value:brick}});
 expect(JSON.parse(screen.getByRole('status').textContent!).templateHash).toBe(brick);expect(screen.getByAltText('Building brick preview').getAttribute('src')).toContain(brick+'.png');
 expect(capabilityInput('object.create',initial)?.['x-features']).toContain('creationTemplates.v1');
});
it('does not turn catalog preview metadata into an external network request',()=>{
 const schema:CapabilitySchema={type:'string',enum:['a'],'x-enum-labels':{a:'Option'},'x-enum-images':{a:'https://example.test/private'}};
 const screen=render(<CapabilityFields schema={schema} value="a" label="Option" objects={[]} onChange={()=>{}}/>);expect(screen.queryByRole('img')).toBeNull();
});
