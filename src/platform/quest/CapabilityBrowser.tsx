// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState,useSyncExternalStore} from 'react';
import {capabilityDefinition,validateCapabilityArguments,resolveCapabilitySchema,type CapabilityInvocation} from '../../../shared/capabilities';
import {CapabilityVariant,initialCapabilityValue} from './CapabilityFields';
import type {ExecutionRequest} from '../../../shared/roomExecutions';
import type {CatalogCategory,CatalogRequest,CatalogView} from '../../../shared/roomCatalog';
import type {RoomAgentClient} from './roomAgentBridge';
export type CatalogInsert=(call:CapabilityInvocation)=>string|null;
export type OpenCatalog=(insert?:CatalogInsert)=>void;
/** Optional expert authoring on the book; queries use the same native path as the agent. */
export function CapabilityBrowser({client,onClose,onInsert}:{client:RoomAgentClient;onClose:()=>void;onInsert?:CatalogInsert}) {
 const {state,pending}=useSyncExternalStore(client.subscribe,client.getSnapshot);
 const [category,setCategory]=useState<CatalogCategory>('actions');
 const [query,setQuery]=useState(''),[page,setPage]=useState<Extract<CatalogView,{operation:'search'}>|null>(null);
 const [inspection,setInspection]=useState<Extract<CatalogView,{operation:'inspect'}>|null>(null),[args,setArgs]=useState('{}'),[error,setError]=useState('');
 const [checked,setChecked]=useState(''),[recoveryNotice,setRecoveryNotice]=useState('');
 const send=async(catalog:CatalogRequest)=>{
  setError('');setRecoveryNotice('');try {const result=await client.request([{action:'catalog',catalog}]);if(!result.ok){setError(result.status);return null;}return result.catalog??null;}
  catch(e){setError(e instanceof Error?e.message:'The room is unavailable.');return null;}
 };
 const execute=async(execution:ExecutionRequest)=>{
  setError('');setRecoveryNotice('');try {const result=await client.request([{action:'execution',execution}],state??undefined);if(!result.ok)setError(result.status);else if(execution.operation==='recover')setRecoveryNotice(result.status);}
  catch(e){setError(e instanceof Error?e.message:'The action could not be confirmed. Inspect the room before retrying.');}
 };
 const scope=category==='actions'?{}:{category};
 const search=async(offset=0)=>{setInspection(null);const result=await send({operation:'search',...scope,query:offset?page?.query??query:query,offset});if(result?.operation==='search'&&(result.category??'actions')===category)setPage(result);};
 const inspect=async(id:string,version:number)=>{const result=await send({operation:'inspect',...scope,capability:id,version});if(result?.operation==='inspect'&&(result.category??'actions')===category&&result.capability===id&&result.version===version){
  setInspection(result);setChecked('');if(!result.definition)setError(result.status);
  else if(result.category!=='events'&&result.category!=='facts')setArgs(JSON.stringify(result.definition.example??initialCapabilityValue(result.definition.input,state?.objects??[]),null,2));
 }};
 const definition=inspection&&inspection.category!=='events'&&inspection.category!=='facts'?inspection.definition:null;
 const currentFact=state?.catalog?.operation==='inspect'&&state.catalog.category==='facts'&&inspection?.category==='facts'&&state.catalog.capability===inspection.capability&&state.catalog.version===inspection.version?state.catalog:null;
 let call:CapabilityInvocation|null=null,invalid='',parsedArgs:unknown;
 if(definition)try {parsedArgs=JSON.parse(args);invalid=validateCapabilityArguments(definition.id,definition.version,parsedArgs)??'';
  if(!invalid)call={id:definition.id,version:definition.version,arguments:parsedArgs as Record<string,unknown>};
 }catch{invalid='Enter valid JSON arguments.';}
 const key=call?JSON.stringify(call):'';
 const observation=state?.catalog;
 const check=checked===key&&key&&observation?.operation==='check'&&JSON.stringify(observation.call)===key?observation:null;
 const supported=state?.capabilities?.includes('catalog.v1')===true;
 return <div className="room-workspace capability-browser" aria-label="Action catalog">
  <section className="room-workspace-page room-hierarchy" aria-label="Find an action">
   <div className="room-workspace-heading"><div><span className="room-eyebrow">ROOM VOCABULARY</span><h1>{category==='actions'?'Action catalog':category==='events'?'Events':'Room facts'}</h1></div><button disabled={pending} onClick={onClose}>Back to workshop</button></div>
   <p className="room-workspace-intro">Explore what Maestro can do, what programs can wait for and what they can observe. Search and inspect use the same native catalog as the agent.</p>
   {state?.capabilities?.includes('catalogVocabulary.v1')&&<label>Browse<select aria-label="Catalog category" value={category} disabled={pending} onChange={e=>{setCategory(e.target.value as CatalogCategory);setPage(null);setInspection(null);setQuery('');setChecked('');setError('');setRecoveryNotice('');}}><option value="actions">Actions</option><option value="events">Events</option><option value="facts">Room facts</option></select></label>}
   <form onSubmit={e=>{e.preventDefault();void search();}}><label>Search {category}<input value={query} maxLength={80} onChange={e=>setQuery(e.target.value)}/></label><button disabled={pending||!supported}>Search</button></form>
   {page&&<><p>{page.total} matching {category}</p><div className="room-object-list">{page.entries.map(entry=><button key={entry.id} disabled={pending} aria-pressed={inspection?.definition?.id===entry.id} onClick={()=>void inspect(entry.id,entry.version)}><span>{entry.label}</span><small>{entry.id} · v{entry.version}</small></button>)}</div>
    <div className="room-workspace-actions"><button disabled={pending||page.offset===0} onClick={()=>void search(Math.max(0,page.offset-page.pageSize))}>Previous {category}</button><span>{page.total?Math.floor(page.offset/page.pageSize)+1:0} / {Math.ceil(page.total/page.pageSize)}</span><button disabled={pending||page.offset+page.pageSize>=page.total} onClick={()=>void search(page.offset+page.pageSize)}>Next {category}</button></div></>}
  </section>
  <section className="room-workspace-page room-inspector" aria-label="Action details">
   <h2>{inspection?.definition?.label??(category==='actions'?'Choose an action':category==='events'?'Choose an event':'Choose a fact')}</h2>
   <div role="status" className={error||state?.execution?.storageError?'room-message room-message-warning':'room-message'}>{error||state?.execution?.storageError||recoveryNotice||(!supported?'Update the native app to browse actions.':pending?'Waiting for the room…':category==='actions'?(check?.status??state?.execution?.selected?.status??'Select an action or check its availability.'):category==='facts'?(currentFact?.status??'Inspect a fact to read its current value.'):(inspection?.status??'Inspect an event to see its payload and subscription rules.'))}</div>
   {category==='actions'&&state?.execution?.recovery&&state.capabilities?.includes('actionRecovery.v1')&&<section aria-label="Recover action history" className="room-message room-message-warning"><h3>Recover action history</h3><p>{state.execution.recovery.status}</p><button disabled={pending} onClick={()=>void execute({operation:'recover',recoveryId:state.execution!.recovery!.id})}>Stop actions and recover history</button></section>}
   {inspection?.category==='events'&&inspection.definition&&<section aria-label="Event definition">
    <p>{inspection.definition.id} · version {inspection.definition.version}</p><p>{inspection.definition.description}</p>
    <p>Event value: {inspection.definition.valueType}. {inspection.definition.objectEvent?'Filter by an exact object ID, or leave source empty for any object.':'Source must be empty.'}</p>
    <p>Requires: {(inspection.definition.features??[]).join(', ')}.</p>
    {inspection.definition.input&&<><h3>Subscription inputs</h3><p>Evaluated when a running program reaches this wait. Inspecting does not start a subscription.</p><pre>{JSON.stringify(inspection.definition.example,null,2)}</pre></>}
    <h3>Event fields</h3>
    {Object.entries(inspection.definition.fields?.properties??{}).map(([name,field])=><div className="room-message" key={name}><strong>{name}</strong> · {field.type==='string'?'text':field.type}{field.enum&&<p>{field.enum.join(', ')}</p>}</div>)}
    {!inspection.definition.fields&&<p>This event has only its primary value.</p>}
    <p className="room-workspace-intro">Use an Event wait block in the workshop. Only a running program that has reached that wait receives the event. Inspecting here does not enable it.</p>
    <details><summary>Exact event definition</summary><pre>{JSON.stringify(inspection.definition,null,2)}</pre></details>
   </section>}
   {inspection?.category==='facts'&&inspection.definition&&<section aria-label="Fact definition">
    <p>{inspection.definition.id} · version {inspection.definition.version}</p><p>{inspection.definition.description}</p><p>Value type: {inspection.definition.type}</p>
    <div className="room-message" aria-label="Current fact value">{currentFact?.available?<><strong>Current value</strong><p>{JSON.stringify(currentFact.value)}</p></>:<><strong>Unavailable</strong><p>{currentFact?'The runtime has no reliable value now.':'Refresh this fact to read it again.'}</p></>}</div>
    <button disabled={pending} onClick={()=>void inspect(inspection.capability,inspection.version)}>Refresh fact</button>
    <p className="room-workspace-intro">Choose this fact as a condition or calculation input in a program. This reading is a snapshot; it does not subscribe to changes or run a behaviour.</p>
   </section>}
   {definition&&<><p>{definition.id} · version {definition.version}</p>{definition.description&&<p>{definition.description}</p>}
    {definition.input.oneOf&&<CapabilityVariant schema={definition.input} value={parsedArgs} objects={state?.objects??[]} onChange={value=>{setArgs(JSON.stringify(value,null,2));setChecked('');}}/>}
    {resolveCapabilitySchema(definition.input,call?.arguments)?.description&&<p>{resolveCapabilitySchema(definition.input,call?.arguments)?.description}</p>}
    <label>Action arguments<textarea aria-label="Action arguments" rows={12} spellCheck={false} value={args} disabled={pending} onChange={e=>{setArgs(e.target.value);setChecked('');}}/></label>
    {invalid&&<p className="room-message room-message-warning">{invalid}</p>}
    <div className="room-workspace-actions"><button disabled={pending||!call} onClick={async()=>{if(call){const result=await send({operation:'check',call});if(result?.operation==='check')setChecked(key);}}}>Check availability</button>
     {state?.capabilities?.includes('execution.v1')&&<button disabled={pending||!call||Boolean(state?.execution?.storageError)} onClick={()=>{if(call)void execute({operation:'start',call});}}>Run action now</button>}
     {onInsert&&<button disabled={pending||!call} onClick={()=>{if(call){const error=onInsert(call);if(error)setError(error);else onClose();}}}>Add first block to draft</button>}</div>
    <p className="room-workspace-intro">{onInsert?'Adding a block changes your draft. Apply it in the workshop when ready.':'Choose a behaviour in the workshop to add an action block.'} Availability can change before a behaviour runs.</p>
    <details><summary>Argument reference</summary><p>Duration: {definition.duration}. Uses: {(resolveCapabilitySchema(definition.input,call?.arguments)?.['x-channels']??definition.channels).join(', ')||'no animation channel'}.</p><p>Needs: {(resolveCapabilitySchema(definition.input,call?.arguments)?.['x-requirements']??definition.requirements).join(', ')||'no additional requirements'}.</p><pre>{JSON.stringify(definition.input,null,2)}</pre></details>
   </>}
   {category==='actions'&&state?.execution&&<section aria-label="One-off actions" className="execution-view">
    <h2>One-off actions</h2><p className="room-workspace-intro">These runs do not change saved behaviours.</p>
    {state.execution.running.map(run=><div key={run.id} className="room-message"><strong>{capabilityDefinition(run.capability)?.label??run.capability}</strong><p>{run.status}</p><div className="room-workspace-actions"><button disabled={pending} onClick={()=>void execute({operation:'inspect',runId:run.id})}>Inspect action {run.id.slice(0,6)}</button><button disabled={pending} onClick={()=>void execute({operation:'cancel',runId:run.id})}>Stop action {run.id.slice(0,6)}</button></div></div>)}
    {!state.execution.running.length&&<p>No one-off action is running.</p>}
    {state.execution.selected&&<div aria-label="Selected action" className="room-message"><strong>{state.execution.selected.phase}</strong><p>{state.execution.selected.status}</p>{state.execution.selected.output&&<><h3>Action result</h3><pre aria-label="Action result">{JSON.stringify(state.execution.selected.output,null,2)}</pre></>}<details><summary>Exact action</summary><pre>{JSON.stringify(state.execution.selected.call,null,2)}</pre></details></div>}
    {state.execution.outcomes.length>0&&<details><summary>Recent action results</summary><div className="room-object-list">{[...state.execution.outcomes].reverse().map(run=><button key={run.id} disabled={pending} onClick={()=>void execute({operation:'inspect',runId:run.id})}><span>{capabilityDefinition(run.capability)?.label??run.capability} · {run.phase}</span><small>{run.id.slice(0,6)} · {run.status}</small></button>)}</div></details>}
   </section>}
  </section>
 </div>;
}
