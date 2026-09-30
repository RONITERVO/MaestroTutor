// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {act,cleanup,fireEvent,render} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {RoomAgentClient} from './roomAgentBridge';
import {ProgramModuleLibrary} from './ProgramModuleLibrary';
import {moduleHash,type ModuleRecord} from '../../../shared/programModuleIdentity';
import type {CatalogView} from '../../../shared/roomCatalog';
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
import type {RuleSequence} from '../../core-sdk/room/rules';
afterEach(cleanup);
const program={version:3,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[]}]};
const sequence:RuleSequence={id:'b'.repeat(32),name:'Wave',interruption:0,repeat:false,program:JSON.stringify(program)};
const module:ModuleRecord={version:1,name:'Reusable wave',exports:['main'],program:{...program,resources:['maestro']}};
const hash=moduleHash(module);
function setup(dirty=false){
 const client=new RoomAgentClient();let state:RoomAgentState={version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',canUndo:false,canRedo:false,physicsRunning:false,visible:true,created:[],objects:[],capabilities:['catalog.v1','programModules.v1','moduleLibrary.v1','execution.v1','executionReceipts.v1'],execution:{selected:null,running:[],outcomes:[],nextRunId:'e'.repeat(32),storageError:null}};
 expect(client.receive(state)).toBe(true);const onChange=vi.fn(),onClose=vi.fn();
 const props={client,sequence,rulesRevision:4,dirty,disabled:false,onChange,onClose},screen=render(<ProgramModuleLibrary {...props}/>);
 const receive=async(catalog?:CatalogView)=>{state={...state,revision:state.revision+1,ack:client.snapshot().request?.sequence??state.ack,catalog};await act(async()=>{expect(client.receive(state)).toBe(true);});};
 return {client,screen,onChange,onClose,receive,props};
}
it('publishes only saved source through the same identified capability used by the agent',async()=>{
 const {client,screen,receive,props}=setup();fireEvent.click(screen.getByRole('button',{name:'Publish module'}));
 expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',runId:'e'.repeat(32),call:{id:'program.module.publish',version:1,arguments:{sequenceId:sequence.id,rulesRevision:4,name:'Wave',exports:['main']}}}});
 await receive();screen.rerender(<ProgramModuleLibrary {...props} dirty/>);expect(screen.getByRole('button',{name:'Publish module'}).matches(':disabled')).toBe(true);
});
it('searches and imports a pinned draft only after granting its required object',async()=>{
 const {client,screen,receive,onChange,onClose}=setup();fireEvent.click(screen.getByRole('button',{name:'Search modules'}));
 expect(client.snapshot().request?.commands[0].catalog).toEqual({operation:'search',category:'modules',query:'',offset:0});
 await receive({operation:'search',category:'modules',query:'',offset:0,pageSize:6,total:1,entries:[{id:hash,version:1,label:module.name}],revision:2,ready:true,pending:false,status:'Found'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(module.name)}));await receive({operation:'inspect',category:'modules',capability:hash,version:1,definition:module,revision:2,ready:true,pending:false,status:'Inspected'});
 fireEvent.click(screen.getByRole('button',{name:'Add pinned import to draft'}));expect(onChange).not.toHaveBeenCalled();expect(screen.getByRole('status').textContent).toContain('additional objects');
 fireEvent.click(screen.getByLabelText('Allow additional module objects'));fireEvent.click(screen.getByRole('button',{name:'Add pinned import to draft'}));
 expect(JSON.parse(onChange.mock.calls[0][0]).imports[0]).toEqual({alias:'module_1',hash,module,signals:{}});expect(onClose).toHaveBeenCalledOnce();expect(client.snapshot().request).toBeNull();
});
it('blocks stale draft publication and import while retaining the visible draft',()=>{
 const {screen,props}=setup();screen.rerender(<ProgramModuleLibrary {...props} sequence={{...sequence,program:JSON.stringify({...program,state:[{name:'value',initial:1}]})}}/>);
 expect(screen.getByRole('button',{name:'Publish module'}).matches(':disabled')).toBe(true);
});
