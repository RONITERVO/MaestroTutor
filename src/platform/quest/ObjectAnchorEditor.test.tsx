// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState} from 'react';
import {cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it} from 'vitest';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {factArgumentType} from '../../../shared/behaviourFacts';
import {ProgramValueEditor} from './ProgramValueEditor';
import type {Expression} from '../../core-sdk/room/programs';
afterEach(cleanup);
it('lets users bind a nested anchor fact field and change the literal variant without stale bindings',()=>{
 let value:Expression={fact:'object.anchor',version:1,arguments:{holder:{kind:'recipePart',objectId:'1'.repeat(32),part:'RightHand',revision:1}},bindings:{}};
 function Harness(){const [v,set]=useState(value);return <ProgramValueEditor label="Anchor" value={v} type={behaviourFact('object.anchor')!.type} objects={[{id:'1'.repeat(32),name:'Robot'}]} sources={[{name:'robot',type:'text',kind:'var'}]} onChange={next=>{value=next;set(next);}}/>;}
 const screen=render(<Harness/>);fireEvent.change(screen.getByLabelText('Anchor fact holder.objectId mode'),{target:{value:'expression'}});fireEvent.change(screen.getByLabelText('Anchor fact holder.objectId expression source'),{target:{value:'var:robot'}});
 if(!('fact' in value))throw Error('fixture');expect(value.bindings?.['holder.objectId']).toEqual({var:'robot'});
 fireEvent.change(screen.getByLabelText('Attachment point'),{target:{value:'2'}});
 if(!('fact' in value))throw Error('fixture');expect(value.arguments?.holder).toEqual({kind:'object',objectId:'1'.repeat(32),revision:1});expect(factArgumentType(value.fact,'holder.objectId',value.arguments)).toBe('text');expect(value.bindings?.['holder.objectId']).toEqual({var:'robot'});
});
