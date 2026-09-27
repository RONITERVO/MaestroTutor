// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState,useSyncExternalStore} from 'react';
import {capabilityDefinition,validateCapabilityArguments,type CapabilityDefinition,type CapabilityInvocation,type CapabilitySchema} from '../../../shared/capabilities';
import type {ExecutionRequest} from '../../../shared/roomExecutions';
import type {CatalogRequest,CatalogView} from '../../../shared/roomCatalog';
import type {RoomAgentClient} from './roomAgentBridge';
export type CatalogInsert=(call:CapabilityInvocation)=>string|null;
export type OpenCatalog=(insert?:CatalogInsert)=>void;
function initial(schema:CapabilitySchema,objects:{id:string}[]):unknown {
 if(schema.enum)return schema.enum[0];
 if(schema.type==='object')return Object.fromEntries((schema.required??[]).map(key=>[key,initial(schema.properties![key],objects)]));
 if(schema.type==='boolean')return false;
 if(schema.type==='number'||schema.type==='integer')return schema.minimum??0;
 if(schema['x-resource']==='object')return objects.find(x=>!schema.pattern||new RegExp(schema.pattern).test(x.id))?.id??'';
 return '';
}
/** Optional expert authoring on the book; queries use the same native path as the agent. */
export function CapabilityBrowser({client,onClose,onInsert}:{client:RoomAgentClient;onClose:()=>void;onInsert?:CatalogInsert}) {
 const {state,pending}=useSyncExternalStore(client.subscribe,client.getSnapshot);
 const [query,setQuery]=useState(''),[page,setPage]=useState<Extract<CatalogView,{operation:'search'}>|null>(null);
 const [definition,setDefinition]=useState<CapabilityDefinition|null>(null),[args,setArgs]=useState('{}'),[error,setError]=useState('');
 const [checked,setChecked]=useState('');
 const send=async(catalog:CatalogRequest)=>{
  setError('');try {const result=await client.request([{action:'catalog',catalog}]);if(!result.ok){setError(result.status);return null;}return result.catalog??null;}
  catch(e){setError(e instanceof Error?e.message:'The room is unavailable.');return null;}
 };
 const execute=async(execution:ExecutionRequest)=>{
  setError('');try {const result=await client.request([{action:'execution',execution}],state??undefined);if(!result.ok)setError(result.status);}
  catch(e){setError(e instanceof Error?e.message:'The action could not be confirmed. Inspect the room before retrying.');}
 };
 const search=async(offset=0)=>{const result=await send({operation:'search',query:offset?page?.query??query:query,offset});if(result?.operation==='search')setPage(result);};
 const inspect=async(id:string,version:number)=>{const result=await send({operation:'inspect',capability:id,version});if(result?.operation==='inspect'){
  setDefinition(result.definition);setChecked('');if(result.definition)setArgs(JSON.stringify(initial(result.definition.input,state?.objects??[]),null,2));else setError(result.status);
 }};
 let call:CapabilityInvocation|null=null,invalid='';
 if(definition)try {const argumentsValue:unknown=JSON.parse(args);invalid=validateCapabilityArguments(definition.id,definition.version,argumentsValue)??'';
  if(!invalid)call={id:definition.id,version:definition.version,arguments:argumentsValue as Record<string,unknown>};
 }catch{invalid='Enter valid JSON arguments.';}
 const key=call?JSON.stringify(call):'';
 const observation=state?.catalog;
 const check=checked===key&&key&&observation?.operation==='check'&&JSON.stringify(observation.call)===key?observation:null;
 const supported=state?.capabilities?.includes('catalog.v1')===true;
 return <div className="room-workspace capability-browser" aria-label="Action catalog">
  <section className="room-workspace-page room-hierarchy" aria-label="Find an action">
   <div className="room-workspace-heading"><div><span className="room-eyebrow">AVAILABLE ACTIONS</span><h1>Action catalog</h1></div><button disabled={pending} onClick={onClose}>Back to workshop</button></div>
   <p className="room-workspace-intro">Explore actions that Maestro can use. Search and check first, then run an action or add it to a behaviour.</p>
   <form onSubmit={e=>{e.preventDefault();void search();}}><label>Search actions<input value={query} maxLength={80} onChange={e=>setQuery(e.target.value)}/></label><button disabled={pending||!supported}>Search</button></form>
   {page&&<><p>{page.total} matching actions</p><div className="room-object-list">{page.entries.map(entry=><button key={entry.id} disabled={pending} aria-pressed={definition?.id===entry.id} onClick={()=>void inspect(entry.id,entry.version)}><span>{entry.label}</span><small>{entry.id} · v{entry.version}</small></button>)}</div>
    <div className="room-workspace-actions"><button disabled={pending||page.offset===0} onClick={()=>void search(Math.max(0,page.offset-page.pageSize))}>Previous actions</button><span>{page.total?Math.floor(page.offset/page.pageSize)+1:0} / {Math.ceil(page.total/page.pageSize)}</span><button disabled={pending||page.offset+page.pageSize>=page.total} onClick={()=>void search(page.offset+page.pageSize)}>Next actions</button></div></>}
  </section>
  <section className="room-workspace-page room-inspector" aria-label="Action details">
   <h2>{definition?.label??'Choose an action'}</h2>
   <div role="status" className={error||state?.execution?.storageError?'room-message room-message-warning':'room-message'}>{error||state?.execution?.storageError||(!supported?'Update the native app to browse actions.':pending?'Waiting for the room…':check?.status??state?.execution?.selected?.status??'Select an action or check its availability.')}</div>
   {definition&&<><p>{definition.id} · version {definition.version}</p>
    <label>Action arguments<textarea aria-label="Action arguments" rows={12} spellCheck={false} value={args} disabled={pending} onChange={e=>{setArgs(e.target.value);setChecked('');}}/></label>
    {invalid&&<p className="room-message room-message-warning">{invalid}</p>}
    <div className="room-workspace-actions"><button disabled={pending||!call} onClick={async()=>{if(call){const result=await send({operation:'check',call});if(result?.operation==='check')setChecked(key);}}}>Check availability</button>
     {state?.capabilities?.includes('execution.v1')&&<button disabled={pending||!call||Boolean(state?.execution?.storageError)} onClick={()=>{if(call)void execute({operation:'start',call});}}>Run action now</button>}
     {onInsert&&<button disabled={pending||!call} onClick={()=>{if(call){const error=onInsert(call);if(error)setError(error);else onClose();}}}>Add first block to draft</button>}</div>
    <p className="room-workspace-intro">{onInsert?'Adding a block changes your draft. Apply it in the workshop when ready.':'Choose a behaviour in the workshop to add an action block.'} Availability can change before a behaviour runs.</p>
    <details><summary>Argument reference</summary><p>Duration: {definition.duration}. Uses: {definition.channels.join(', ')||'no animation channel'}.</p><p>Needs: {definition.requirements.join(', ')||'no additional requirements'}.</p><pre>{JSON.stringify(definition.input,null,2)}</pre></details>
   </>}
   {state?.execution&&<section aria-label="One-off actions" className="execution-view">
    <h2>One-off actions</h2><p className="room-workspace-intro">These runs do not change saved behaviours.</p>
    {state.execution.running.map(run=><div key={run.id} className="room-message"><strong>{capabilityDefinition(run.capability)?.label??run.capability}</strong><p>{run.status}</p><div className="room-workspace-actions"><button disabled={pending} onClick={()=>void execute({operation:'inspect',runId:run.id})}>Inspect action {run.id.slice(0,6)}</button><button disabled={pending} onClick={()=>void execute({operation:'cancel',runId:run.id})}>Stop action {run.id.slice(0,6)}</button></div></div>)}
    {!state.execution.running.length&&<p>No one-off action is running.</p>}
    {state.execution.selected&&<div aria-label="Selected action" className="room-message"><strong>{state.execution.selected.phase}</strong><p>{state.execution.selected.status}</p><details><summary>Exact action</summary><pre>{JSON.stringify(state.execution.selected.call,null,2)}</pre></details></div>}
    {state.execution.outcomes.length>0&&<details><summary>Recent action results</summary><div className="room-object-list">{[...state.execution.outcomes].reverse().map(run=><button key={run.id} disabled={pending} onClick={()=>void execute({operation:'inspect',runId:run.id})}><span>{capabilityDefinition(run.capability)?.label??run.capability} · {run.phase}</span><small>{run.id.slice(0,6)} · {run.status}</small></button>)}</div></details>}
   </section>}
  </section>
 </div>;
}
