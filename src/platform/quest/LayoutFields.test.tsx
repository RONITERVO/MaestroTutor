// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState} from 'react';
import {cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import {capabilityDefinition,validateCapabilityArguments} from '../../../shared/capabilities';
import {CapabilityFields} from './CapabilityFields';
afterEach(cleanup);
it('edits distinct member placements in the generated controls and catches duplicate selections',()=>{
 const definition=capabilityDefinition('object.layout.apply')!,objects=[{id:'a'.repeat(32),name:'Left tower'},{id:'b'.repeat(32),name:'Right tower'}];
 const initial=structuredClone(definition.example!);(initial.placements as {target:string}[])[0].target=objects[0].id;
 function Draft(){const [value,setValue]=useState<unknown>(initial);return <><CapabilityFields schema={definition.input} value={value} label="Layout" objects={objects} onChange={setValue}/><output>{JSON.stringify(value)}</output><button disabled={validateCapabilityArguments(definition.id,1,value)!==null}>Apply</button></>;}
 const screen=render(<Draft/>);fireEvent.click(screen.getByText('Add Layout placements entry'));expect((screen.getByText('Apply') as HTMLButtonElement).disabled).toBe(true);
 fireEvent.change(screen.getByLabelText('Layout placements 2 target'),{target:{value:objects[1].id}});fireEvent.change(screen.getByLabelText('Layout placements 2 scale'),{target:{value:'1'}});fireEvent.change(screen.getByLabelText('Layout placements 2 position x'),{target:{value:'2.5'}});
 expect((screen.getByText('Apply') as HTMLButtonElement).disabled).toBe(false);const value=JSON.parse(screen.getByRole('status').textContent!);expect(value.placements[0].target).toBe(objects[0].id);expect(value.placements[1]).toMatchObject({target:objects[1].id,position:{x:2.5},scale:1});
});
