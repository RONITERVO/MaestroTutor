// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {act,cleanup,fireEvent,render,waitFor} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {useState} from 'react';
import {CapabilityFields} from './CapabilityFields';
import {RoomAgentClient} from './roomAgentBridge';
import {ProgramModuleLibrary} from './ProgramModuleLibrary';
import {encodeModuleFile,decodeModuleFile} from '../../core-sdk/room/programModuleFile';
import {registerNativeFileWriter} from '../browser/fileWriter';
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
 const client=new RoomAgentClient();let state:RoomAgentState={version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',canUndo:false,canRedo:false,physicsRunning:false,visible:true,created:[],objects:[],capabilities:['catalog.v1','programModules.v1','moduleLibrary.v1','moduleLibraryFiles.v1','execution.v1','executionReceipts.v1'],execution:{selected:null,running:[],outcomes:[],nextRunId:'e'.repeat(32),storageError:null}};
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

it('previews a file before dispatching the exact shared import action without editing or starting a behaviour',async()=>{
 const {client,screen,onChange,receive}=setup();const source=encodeModuleFile(hash,module);
 fireEvent.change(screen.getByLabelText('Module file'),{target:{files:[{size:source.length,arrayBuffer:async()=>new TextEncoder().encode(source).buffer}]}});
 await waitFor(()=>expect(screen.getByLabelText('Module file preview').textContent).toContain(hash));expect(client.snapshot().request).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Import file to library'}));expect(client.snapshot().request?.commands[0]).toMatchObject({action:'execution',execution:{operation:'start',call:{id:'program.module.import',version:1,arguments:{hash,definition:module}}}});
 expect(onChange).not.toHaveBeenCalled();await receive();
});
it('rejects invalid UTF-8 before any native request',async()=>{
 const {client,screen}=setup();fireEvent.change(screen.getByLabelText('Module file'),{target:{files:[{size:1,arrayBuffer:async()=>new Uint8Array([255]).buffer}]}});
 await waitFor(()=>expect(screen.getByRole('status').textContent).toMatch(/encoded data|encoding/i));expect(screen.queryByLabelText('Module file preview')).toBeNull();expect(client.snapshot().request).toBeNull();
});
it('exports the exact inspected module and reports success only after file close',async()=>{
 const {client,screen,receive}=setup();fireEvent.click(screen.getByRole('button',{name:'Search modules'}));
 await receive({operation:'search',category:'modules',query:'',offset:0,pageSize:6,total:1,entries:[{id:hash,version:1,label:module.name}],revision:2,ready:true,pending:false,status:'Found'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(module.name)}));await receive({operation:'inspect',category:'modules',capability:hash,version:1,definition:module,revision:2,ready:true,pending:false,status:'Inspected'});
 let finish!:()=>void;const close=vi.fn(()=>new Promise<void>(resolve=>{finish=resolve;}));const lines:string[]=[];
 const unregister=registerNativeFileWriter(async(name,mime)=>{expect(name).toContain(hash);expect(mime).toBe('application/json');return {write:async text=>{lines.push(text);},close,location:()=>'/Downloads/test.json'};});
 try{fireEvent.click(screen.getByRole('button',{name:'Export module file'}));await waitFor(()=>expect(close).toHaveBeenCalledOnce());expect(screen.queryByText('Saved: /Downloads/test.json')).toBeNull();
  await act(async()=>{finish();});expect(screen.getByText('Saved: /Downloads/test.json')).toBeTruthy();expect(decodeModuleFile(lines.join('')).definition).toEqual(module);expect(client.snapshot().request).toBeNull();
 }finally{unregister();}
});

it('keeps invalid and whitespace-preserving module source drafts editable in the generic action form',()=>{
 const changed=vi.fn();function Editor(){const [value,setValue]=useState<unknown>(module);return <CapabilityFields schema={{type:'object',format:'programModule'}} label="definition" value={value} objects={[]} onChange={next=>{setValue(next);changed(next);}}/>;}
 const screen=render(<Editor/>),input=screen.getByLabelText('definition');fireEvent.change(input,{target:{value:'{'}});expect((input as HTMLTextAreaElement).value).toBe('{');expect(changed).toHaveBeenLastCalledWith(null);expect(screen.getByRole('alert')).toBeTruthy();
 const source='  '+JSON.stringify(module);fireEvent.change(input,{target:{value:source}});expect((input as HTMLTextAreaElement).value).toBe(source);expect(changed).toHaveBeenLastCalledWith(module);expect(screen.queryByRole('alert')).toBeNull();
});

it('keeps the module preview open when the parent rejects a repeat-changing edit',async()=>{
 const {screen,receive,onChange,onClose}=setup();onChange.mockReturnValue('Convert sequence Repeat first.');
 fireEvent.click(screen.getByRole('button',{name:'Search modules'}));await receive({operation:'search',category:'modules',query:'',offset:0,pageSize:6,total:1,entries:[{id:hash,version:1,label:module.name}],revision:2,ready:true,pending:false,status:'Found'});
 fireEvent.click(screen.getByRole('button',{name:new RegExp(module.name)}));await receive({operation:'inspect',category:'modules',capability:hash,version:1,definition:module,revision:2,ready:true,pending:false,status:'Inspected'});
 fireEvent.click(screen.getByLabelText('Allow additional module objects'));fireEvent.click(screen.getByRole('button',{name:'Add pinned import to draft'}));
 expect(onChange).toHaveBeenCalledOnce();expect(onClose).not.toHaveBeenCalled();expect(screen.getByRole('status').textContent).toContain('Convert sequence Repeat');
});
