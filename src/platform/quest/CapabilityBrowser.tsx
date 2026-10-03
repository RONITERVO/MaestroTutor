import {dataTypeLabel} from '../../../shared/programValues';
import {validateFactArguments} from '../../../shared/behaviourFacts';
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useEffect,useRef,useState,useSyncExternalStore} from 'react';
import {currentInputLocations,currentInputFields,currentInputRequest,applyCurrentInputSnapshots,currentInputsIdentity,currentInputFieldLabel,type CurrentInputLocation} from '../../../shared/currentCapabilityInputs';
import {capabilityDefinition,validateCapabilityArguments,resolveCapabilitySchema,argumentValue,type CapabilityInvocation} from '../../../shared/capabilities';
import {CapabilityVariant,CapabilityFields,initialCapabilityValue} from './CapabilityFields';
import {executionForCapability,type ExecutionLane,type ExecutionRequest} from '../../../shared/roomExecutions';
import type {CatalogCategory,CatalogRequest,CatalogView} from '../../../shared/roomCatalog';
import type {ProgramCapabilityInputs} from '../../core-sdk/room/programCapabilityEditing';
import type {RoomAgentClient} from './roomAgentBridge';
export type CatalogInsert=(call:CapabilityInvocation,inputs?:ProgramCapabilityInputs)=>string|null;
export type OpenCatalog=(insert?:CatalogInsert,initialCall?:CapabilityInvocation)=>void;
/** Optional expert authoring on the book; queries use the same native path as the agent. */
export function CapabilityBrowser({client,onClose,onInsert,initialCall}:{client:RoomAgentClient;onClose:()=>void;onInsert?:CatalogInsert;initialCall?:CapabilityInvocation}) {
 const {state,pending}=useSyncExternalStore(client.subscribe,client.getSnapshot);
 const [category,setCategory]=useState<CatalogCategory>('actions');
 const [query,setQuery]=useState(''),[page,setPage]=useState<Extract<CatalogView,{operation:'search'}>|null>(null);
 const [inspection,setInspection]=useState<Extract<CatalogView,{operation:'inspect'}>|null>(null),[args,writeArgs]=useState('{}'),[error,setError]=useState('');
 const [checked,setChecked]=useState(''),[recoveryNotice,setRecoveryNotice]=useState(''),[confirming,setConfirming]=useState('');
 const draftEpoch=useRef(0),alive=useRef(true),seeded=useRef(false);
 const [loaded,setLoaded]=useState(''),[acceptedSnapshot,setAcceptedSnapshot]=useState(''),[loading,setLoading]=useState(false);
 const busy=pending||loading;
 const [programInputs,setProgramInputs]=useState<'snapshot'|'current'>('current');
 const [currentChoices,setCurrentChoices]=useState<Record<string,boolean>>({}),[loadedInputs,setLoadedInputs]=useState<Record<string,unknown>|null>(null);
 const previousSession=useRef(state?.session);
 useEffect(()=>{if(previousSession.current!==state?.session){previousSession.current=state?.session;draftEpoch.current++;setInspection(null);setLoaded('');setAcceptedSnapshot('');setChecked('');setConfirming('');}},[state?.session]);
 useEffect(()=>{alive.current=true;return()=>{alive.current=false;draftEpoch.current++;};},[]);
 const setArgs=(value:string)=>{draftEpoch.current++;writeArgs(value);setLoaded('');};
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
 const inspect=async(id:string,version:number,argumentsValue?:Record<string,unknown>,seed?:Record<string,unknown>)=>{const result=await send({operation:'inspect',...scope,capability:id,version,...(argumentsValue?{arguments:argumentsValue}:{})});if(result?.operation==='inspect'&&(result.category??'actions')===category&&result.capability===id&&result.version===version){
  setInspection(result);setCurrentChoices({});setLoadedInputs(null);setProgramInputs('current');setAcceptedSnapshot('');setChecked('');setConfirming('');if(!result.definition)setError(result.status);
  else if(result.category==='facts'&&result.definition.input)setArgs(JSON.stringify(result.arguments??result.definition.example??{},null,2));
  else if(result.category!=='events'&&result.category!=='facts'&&result.category!=='modules')setArgs(JSON.stringify(seed??result.definition.example??initialCapabilityValue(result.definition.input,state?.objects??[]),null,2));
 }};
 useEffect(()=>{if(initialCall&&!seeded.current){seeded.current=true;void inspect(initialCall.id,initialCall.version,undefined,initialCall.arguments);}},[initialCall]);
 const definition=inspection&&inspection.category!=='events'&&inspection.category!=='facts'&&inspection.category!=='modules'?inspection.definition:null;
 const currentFact=state?.catalog?.operation==='inspect'&&state.catalog.category==='facts'&&inspection?.category==='facts'&&state.catalog.capability===inspection.capability&&state.catalog.version===inspection.version&&JSON.stringify(state.catalog.arguments)===JSON.stringify(inspection.arguments)?state.catalog:null;
 let factArgs:Record<string,unknown>|undefined,factError='';if(inspection?.category==='facts'&&inspection.definition?.input)try{factArgs=JSON.parse(args);factError=validateFactArguments(inspection.capability,inspection.version,factArgs)??'';}catch{factError='Enter valid fact arguments.';}
 const factDirty=inspection?.category==='facts'&&Boolean(inspection.definition?.input)&&JSON.stringify(factArgs)!==JSON.stringify(inspection.arguments);
 let call:CapabilityInvocation|null=null,invalid='',parsedArgs:unknown;
 if(definition)try {parsedArgs=JSON.parse(args);invalid=validateCapabilityArguments(definition.id,definition.version,parsedArgs)??'';
  if(!invalid)call={id:definition.id,version:definition.version,arguments:parsedArgs as Record<string,unknown>};
 }catch{invalid='Enter valid JSON arguments.';}
 const selectedSchema=definition?resolveCapabilitySchema(definition.input,parsedArgs):undefined;
 let currentLocations:CurrentInputLocation[]=[],currentQueries:CatalogRequest[]=[],mappedFields:ReturnType<typeof currentInputFields>=[],currentError='',snapshotKey='';
 if(definition)try{
  currentLocations=currentInputLocations(definition.input,parsedArgs);mappedFields=currentInputFields(definition.input,parsedArgs);
  currentQueries=currentLocations.map(l=>currentInputRequest(l.schema,l.value));snapshotKey=currentInputsIdentity(definition.input,parsedArgs,state?.session??'');
 }catch(e){currentError=e instanceof Error?e.message:'Current values cannot be read.';}
 const hasCurrent=currentLocations.length>0;
 const snapshotReady=!hasCurrent||Boolean(snapshotKey&&snapshotKey===acceptedSnapshot);
 const liveSupported=state?.capabilities?.includes('structuredValues.v1')===true&&currentLocations.every(l=>
  (Object.keys(l.mapping.arguments).length===0||state?.capabilities?.includes('factQueries.v1')===true)&&(!l.path.some(p=>typeof p==='number')||state?.capabilities?.includes('indexedInputs.v1')===true));
 const reusable=hasCurrent&&programInputs==='current'&&liveSupported;
 const currentFields=mappedFields.filter(f=>f.guard||(currentChoices[f.path]??(loadedInputs!==null&&JSON.stringify(argumentValue(loadedInputs,f.path))===JSON.stringify(argumentValue(parsedArgs,f.path))))).map(f=>f.path);
 const editActionInputs=(value:unknown)=>{
  const choices={...currentChoices};
  for(const f of mappedFields)if(!f.guard&&JSON.stringify(argumentValue(parsedArgs,f.path))!==JSON.stringify(argumentValue(value,f.path)))choices[f.path]=false;
  setCurrentChoices(choices);setArgs(JSON.stringify(value,null,2));setChecked('');setConfirming('');
 };
 const loadCurrent=async()=>{
  if(!definition||!currentQueries.length||currentError)return;
  const epoch=draftEpoch.current,session=state?.session;
  const stillCurrent=()=>alive.current&&draftEpoch.current===epoch&&client.snapshot().session===session;
  setError('');setLoaded('');setChecked('');setConfirming('');setLoading(true);
  try{
   const views:CatalogView[]=[];
   for(let i=0;i<currentQueries.length;i++){
    if(!stillCurrent())return;
    setLoaded(`Reading current values ${i+1} of ${currentQueries.length}…`);
    const result=await client.request([{action:'catalog',catalog:currentQueries[i]}]);
    if(!stillCurrent())return;
    if(!result.ok||!result.catalog)throw new Error(result.status||'Current values are unavailable.');
    views.push(result.catalog);
   }
   const next=applyCurrentInputSnapshots(definition.input,parsedArgs,views);
   setArgs(JSON.stringify(next,null,2));setLoadedInputs(next);setCurrentChoices({});setAcceptedSnapshot(currentInputsIdentity(definition.input,next,session??''));setLoaded('Current values loaded. Review your changes before running.');
  }catch(e){if(stillCurrent()){setLoaded('');setError(e instanceof Error?e.message:'Current values could not be read.');}}
  finally{if(alive.current)setLoading(false);}
 };
 const key=call?JSON.stringify(call):'';
 const confirmation=definition?.input['x-confirmation'];
 const observation=state?.catalog;
 const check=checked===key&&key&&observation?.operation==='check'&&JSON.stringify(observation.call)===key?observation:null;
 const supported=state?.capabilities?.includes('catalog.v1')===true;
 const execution=executionForCapability(definition?.id,state?.execution);
 return <div className="room-workspace capability-browser" aria-label="Action catalog">
  <section className="room-workspace-page room-hierarchy" aria-label="Find an action">
   <div className="room-workspace-heading"><div><span className="room-eyebrow">ROOM VOCABULARY</span><h1>{category==='actions'?'Action catalog':category==='events'?'Events':'Room facts'}</h1></div><button disabled={busy} onClick={onClose}>Back to workshop</button></div>
   <p className="room-workspace-intro">Explore what Maestro can do, what programs can wait for and what they can observe. Search and inspect use the same native catalog as the agent.</p>
   {state?.capabilities?.includes('catalogVocabulary.v1')&&<label>Browse<select aria-label="Catalog category" value={category} disabled={busy} onChange={e=>{setCategory(e.target.value as CatalogCategory);setPage(null);setInspection(null);setQuery('');setChecked('');setError('');setRecoveryNotice('');}}><option value="actions">Actions</option><option value="events">Events</option><option value="facts">Room facts</option></select></label>}
   <form onSubmit={e=>{e.preventDefault();void search();}}><label>Search {category}<input value={query} maxLength={80} onChange={e=>setQuery(e.target.value)}/></label><button disabled={busy||!supported}>Search</button></form>
   {page&&<><p>{page.total} matching {category}</p><div className="room-object-list">{page.entries.map(entry=><button key={entry.id} disabled={busy} aria-pressed={inspection?.capability===entry.id} onClick={()=>void inspect(entry.id,entry.version)}><span>{entry.label}</span><small>{entry.id} · v{entry.version}</small></button>)}</div>
    <div className="room-workspace-actions"><button disabled={busy||page.offset===0} onClick={()=>void search(Math.max(0,page.offset-page.pageSize))}>Previous {category}</button><span>{page.total?Math.floor(page.offset/page.pageSize)+1:0} / {Math.ceil(page.total/page.pageSize)}</span><button disabled={busy||page.offset+page.pageSize>=page.total} onClick={()=>void search(page.offset+page.pageSize)}>Next {category}</button></div></>}
  </section>
  <section className="room-workspace-page room-inspector" aria-label="Action details">
   <h2>{(inspection?.category==='modules'?inspection.definition?.name:inspection?.definition?.label)??(category==='actions'?'Choose an action':category==='events'?'Choose an event':'Choose a fact')}</h2>
   <div role="status" className={error||execution?.storageError?'room-message room-message-warning':'room-message'}>{error||execution?.storageError||recoveryNotice||(!supported?'Update the native app to browse actions.':pending?'Waiting for the room…':category==='actions'?(check?.status??execution?.selected?.status??'Select an action or check its availability.'):category==='facts'?(currentFact?.status??'Inspect a fact to read its current value.'):(inspection?.status??'Inspect an event to see its payload and subscription rules.'))}</div>

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
    <p>{inspection.definition.id} · version {inspection.definition.version}</p><p>{inspection.definition.description}</p><p>Value type: {dataTypeLabel(inspection.definition.type)}</p>
    <div className="room-message" aria-label="Current fact value">{factDirty?<><strong>Not read yet</strong><p>Read this fact with the chosen inputs.</p></>:currentFact?.available?<><strong>Current value</strong><p>{JSON.stringify(currentFact.value)}</p></>:<><strong>Unavailable</strong><p>{currentFact?'The runtime has no reliable value now.':'Refresh this fact to read it again.'}</p></>}</div>
    {inspection.definition.input&&<><CapabilityFields schema={inspection.definition.input} value={factArgs} label="Fact inputs" objects={state?.objects??[]} onChange={value=>setArgs(JSON.stringify(value,null,2))}/>{factError&&<p className="room-message room-message-warning">{factError}</p>}</>}
    <button disabled={busy||Boolean(factError)||Boolean(inspection.definition.input)&&!state?.capabilities?.includes('factQueries.v1')} onClick={()=>void inspect(inspection.capability,inspection.version,factArgs)}>{inspection.definition.input?'Read fact':'Refresh fact'}</button>
    <p className="room-workspace-intro">Choose this fact as a condition or calculation input in a program. This reading is a snapshot; it does not subscribe to changes or run a behaviour.</p>
   </section>}
   {definition&&<><p>{definition.id} · version {definition.version}</p>{definition.description&&<p>{definition.description}</p>}
    <fieldset disabled={busy} className="capability-input-editor">
    {definition.input.oneOf&&<CapabilityVariant schema={definition.input} value={parsedArgs} objects={state?.objects??[]} onChange={editActionInputs}/>}
    {resolveCapabilitySchema(definition.input,call?.arguments)?.description&&<p>{resolveCapabilitySchema(definition.input,call?.arguments)?.description}</p>}
    {hasCurrent&&<section aria-label="Current action inputs"><p>Load {mappedFields.map(f=>currentInputFieldLabel(f.path)).join(', ')} from the room. This replaces those draft values; other inputs stay as you chose them.</p>
     <button disabled={busy||!currentQueries.length||Boolean(currentError)||currentLocations.some(l=>Object.keys(l.mapping.arguments).length>0)&&!state?.capabilities?.includes('factQueries.v1')} onClick={()=>void loadCurrent()}>Load current values</button>
     {currentError&&<p>{currentError}</p>}{loaded&&<p role="status">{loaded}</p>}
     {!snapshotReady&&<p>Load current values before checking, running or adding this action. Advanced arguments can supply an explicit snapshot.</p>}
     <p>Revision and state identifiers protect this snapshot. They are never silently refreshed when you run. Load again after a stale-edit error.</p>
    </section>}
    <details><summary>Edit action fields</summary><CapabilityFields locked={mappedFields.filter(f=>f.guard).map(f=>f.path)} schema={selectedSchema??definition.input} value={parsedArgs} label="Action inputs" objects={state?.objects??[]} onChange={editActionInputs}/></details>
    <details open={!hasCurrent}><summary>Advanced action arguments</summary><label>Action arguments<textarea aria-label="Action arguments" rows={12} spellCheck={false} value={args} disabled={busy} onChange={e=>{setArgs(e.target.value);setLoadedInputs(null);setCurrentChoices({});setChecked('');setConfirming('');try{setAcceptedSnapshot(currentInputsIdentity(definition.input,JSON.parse(e.target.value),state?.session??''));}catch{setAcceptedSnapshot('');}}}/></label></details>
    </fieldset>
    {invalid&&<p className="room-message room-message-warning">{invalid}</p>}
    {onInsert&&hasCurrent&&definition.domain!=='workspace'&&<section aria-label="Behaviour input choices"><h3>Behaviour inputs</h3>
     <label>When this behaviour runs<select aria-label="Behaviour input timing" disabled={busy} value={reusable?'current':'snapshot'} onChange={e=>setProgramInputs(e.target.value as 'current'|'snapshot')}><option value="current" disabled={!liveSupported}>Read current values at this action</option><option value="snapshot">Use this exact snapshot</option></select></label>
     {reusable?<><p>Visible Read blocks take {currentLocations.length} {currentLocations.length===1?'snapshot':'snapshots'} from {Array.from(new Set(currentLocations.map(l=>l.mapping.fact))).join(', ')} immediately before the action. Revision and state guards always come from that read. No action starts when you add or save it.</p>
      {mappedFields.filter(f=>!f.guard).map(({path:key})=><label key={key} className="rule-checkbox"><input type="checkbox" aria-label={'Keep current '+key+' when running'} disabled={busy} checked={currentFields.includes(key)} onChange={e=>setCurrentChoices({...currentChoices,[key]:e.target.checked})}/>Keep current {key} when running{!currentFields.includes(key)&&<small> · fixed to {JSON.stringify(argumentValue(parsedArgs,key))}</small>}</label>)}
      <p>The read and action stay editable in your program. A missing fact or stale/blocked action stops the run; it does not retry. Place both blocks inside a loop if each iteration needs a fresh read.</p>
     </>:<p>This keeps literal values, including revision or state identifiers. A later run can fail if the room changed. {liveSupported?'Choose current values for a reusable action.':'Update the native app to author current-value program inputs.'}</p>}
    </section>}
    <div className="room-workspace-actions"><button disabled={busy||!call||!snapshotReady} onClick={async()=>{if(call){const result=await send({operation:'check',call});if(result?.operation==='check')setChecked(key);}}}>Check availability</button>
     {state?.capabilities?.includes('execution.v1')&&<button disabled={busy||!call||!snapshotReady||Boolean(execution?.storageError)} onClick={()=>{if(call){if(confirmation)setConfirming(key);else void execute({operation:'start',call});}}}>Run action now</button>}
     {onInsert&&definition.domain!=='workspace'&&<button disabled={busy||!call||!snapshotReady} onClick={()=>{if(call){const error=onInsert(call,reusable?{kind:'current',fields:currentFields}:{kind:'snapshot'});if(error)setError(error);else onClose();}}}>{reusable?'Add read and action to draft':'Add first block to draft'}</button>}</div>
    {confirmation&&call&&confirming===key&&<section aria-label="Confirm permanent action" className="room-message room-message-warning"><p>{confirmation}</p><pre>{JSON.stringify(call.arguments,null,2)}</pre><button disabled={busy} onClick={()=>setConfirming('')}>Cancel confirmation</button><button disabled={busy||Boolean(execution?.storageError)} onClick={()=>{setConfirming('');void execute({operation:'start',call});}}>Confirm permanent action</button></section>}
    <p className="room-workspace-intro">{definition.domain==='workspace'?'Workspace maintenance runs once and cannot be added to a room behaviour.':onInsert?'Adding a block changes your draft. Apply it in the workshop when ready.':'Choose a behaviour in the workshop to add an action block.'} Availability can change before execution.</p>
    <details><summary>Argument reference</summary><p>Duration: {definition.duration}. Uses: {(resolveCapabilitySchema(definition.input,call?.arguments)?.['x-channels']??definition.channels).join(', ')||'no animation channel'}.</p><p>Needs: {(resolveCapabilitySchema(definition.input,call?.arguments)?.['x-requirements']??definition.requirements).join(', ')||'no additional requirements'}.</p><pre>{JSON.stringify(definition.input,null,2)}</pre></details>
   </>}
   {category==='actions'&&state?.execution&&<>
    <ExecutionHistory view={state.execution} pending={busy} execute={execute} recoverable={state.capabilities?.includes('actionRecovery.v1')===true}/>
    {state.execution.workspace&&<ExecutionHistory workspace view={state.execution.workspace} pending={busy} execute={execute} recoverable={state.capabilities?.includes('actionRecovery.v1')===true}/>}
   </>}
  </section>
 </div>;
}

function ExecutionHistory({view,pending,execute,recoverable,workspace=false}:{view:ExecutionLane;pending:boolean;execute:(request:ExecutionRequest)=>Promise<void>;recoverable:boolean;workspace?:boolean}) {
 const title=workspace?'Workspace actions':'One-off actions';
 return <section aria-label={title} className="execution-view">
  <h2>{title}</h2><p className="room-workspace-intro">These runs do not change saved behaviours.</p>
  {view.recovery&&recoverable&&<section aria-label={workspace?'Recover workspace action history':'Recover action history'} className="room-message room-message-warning"><h3>{workspace?'Recover workspace action history':'Recover action history'}</h3><p>{view.recovery.status}</p><button disabled={pending} onClick={()=>void execute({operation:'recover',recoveryId:view.recovery!.id})}>{workspace?'Stop workspace actions and recover history':'Stop actions and recover history'}</button></section>}
  {view.storageError&&<p className="room-message room-message-warning">{view.storageError}</p>}
  {view.running.map(run=><div key={run.id} className="room-message"><strong>{capabilityDefinition(run.capability)?.label??run.capability}</strong><p>{run.status}</p><div className="room-workspace-actions"><button disabled={pending} onClick={()=>void execute({operation:'inspect',runId:run.id})}>Inspect action {run.id.slice(0,6)}</button><button disabled={pending} onClick={()=>void execute({operation:'cancel',runId:run.id})}>Stop action {run.id.slice(0,6)}</button></div></div>)}
  {!view.running.length&&<p>{workspace?'No workspace action is running.':'No one-off action is running.'}</p>}
  {view.selected&&<div aria-label={workspace?'Selected workspace action':'Selected action'} className="room-message"><strong>{view.selected.phase}</strong><p>{view.selected.status}</p>{view.selected.output&&<><h3>Action result</h3><pre aria-label="Action result">{JSON.stringify(view.selected.output,null,2)}</pre></>}<details><summary>Exact action</summary><pre>{JSON.stringify(view.selected.call,null,2)}</pre></details></div>}
  {view.outcomes.length>0&&<details><summary>{workspace?'Recent workspace results':'Recent action results'}</summary><div className="room-object-list">{[...view.outcomes].reverse().map(run=><button key={run.id} disabled={pending} onClick={()=>void execute({operation:'inspect',runId:run.id})}><span>{capabilityDefinition(run.capability)?.label??run.capability} · {run.phase}</span><small>{run.id.slice(0,6)} · {run.status}</small></button>)}</div></details>}
 </section>;
}
