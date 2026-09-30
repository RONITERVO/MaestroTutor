// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useEffect,useRef,useState,useSyncExternalStore} from 'react';
import type {CatalogRequest,CatalogView} from '../../../shared/roomCatalog';
import type {CapabilityInvocation} from '../../../shared/capabilities';
import {parseProgram,type BehaviourProgram} from '../../core-sdk/room/programs';
import type {RuleSequence} from '../../core-sdk/room/rules';
import type {RoomAgentClient} from './roomAgentBridge';
import {createBrowserFileWriter,type AppFileWriter} from '../browser/fileWriter';
import {decodeModuleFile,encodeModuleFile,MODULE_FILE_MAX_BYTES,type ProgramModuleFile} from '../../core-sdk/room/programModuleFile';
import {editProgramImport} from './programImportEditing';
/** Book authoring uses the same catalog and durable one-off actions as the room agent. */
export function ProgramModuleLibrary({client,sequence,rulesRevision,dirty,disabled,onChange,onClose}:{client:RoomAgentClient;sequence:RuleSequence;rulesRevision:number;dirty:boolean;disabled:boolean;onChange:(source:string)=>void;onClose:()=>void}) {
 const {state,pending}=useSyncExternalStore(client.subscribe,client.getSnapshot);
 const program=parseProgram(sequence.program).program;
 const [base]=useState({source:sequence.program,id:sequence.id}),[query,setQuery]=useState(''),[name,setName]=useState(sequence.name),[exports,setExports]=useState<string[]>(program?[program.entry]:[]);
 const [page,setPage]=useState<Extract<CatalogView,{operation:'search'}>|null>(null),[inspection,setInspection]=useState<Extract<CatalogView,{category:'modules';operation:'inspect'}>|null>(null);
 const [alias,setAlias]=useState('module_1'),[replace,setReplace]=useState(''),[signals,setSignals]=useState<Record<string,string>>({}),[grant,setGrant]=useState(false),[error,setError]=useState(''),[runId,setRunId]=useState<string|null>(null);
 const [file,setFile]=useState<ProgramModuleFile|null>(null),[fileBusy,setFileBusy]=useState(false),[fileStatus,setFileStatus]=useState('');
 const fileInput=useRef<HTMLInputElement>(null),fileGeneration=useRef(0);
 useEffect(()=>()=>{fileGeneration.current++;},[]);
 const selectFile=async(input:File)=>{
  const generation=++fileGeneration.current;setFile(null);setRunId(null);setFileStatus('');setError('');setFileBusy(true);
  try{if(input.size>MODULE_FILE_MAX_BYTES)throw new Error('Module file exceeds 96,000 bytes.');const decoded=decodeModuleFile(new TextDecoder('utf-8',{fatal:true}).decode(await input.arrayBuffer()));if(fileGeneration.current===generation)setFile(decoded);}
  catch(e){if(fileGeneration.current===generation)setError(e instanceof Error?e.message:'Module file could not be read.');}
  finally{if(fileGeneration.current===generation)setFileBusy(false);}
 };
 const exportFile=async()=>{
  if(!inspection?.definition)return;setFileBusy(true);setFileStatus('');setError('');let writer:AppFileWriter|undefined;
  try{const source=encodeModuleFile(inspection.capability,inspection.definition);writer=await createBrowserFileWriter('maestro-module-'+inspection.capability+'.json','application/json','Maestro reusable module');await writer.write(source);await writer.close();setFileStatus('Saved: '+(writer.location?.()??'module file'));}
  catch(e){try{await writer?.abort?.();}catch{}setError(e instanceof Error?e.message:'Module export could not be confirmed.');}
  finally{setFileBusy(false);}
 };
 const stale=base.source!==sequence.program||base.id!==sequence.id;
 const native=state?.catalog&&state.catalog.operation!=='check'&&state.catalog.category==='modules'?state.catalog:null;
 const busy=fileBusy||pending||native?.pending===true;
 const selected=runId&&state?.execution?.selected?.id===runId?state.execution.selected:null;
 const runPending=selected&&['preparing','running'].includes(selected.phase);
 const module=inspection?.definition??null;
 const events=(module?.program.events??[]) as NonNullable<BehaviourProgram['events']>;
 const required=(module?.program.resources??[]) as string[];
 const previous=program?.imports?.find(i=>i.alias===replace);
 const additional=required.filter(id=>!program?.resources.includes(id));
 const signalTarget=(event:string)=>signals[event]??previous?.signals[event]??'user.'+sequence.id.slice(0,8)+'_'+alias.slice(0,8)+'_'+event.slice(5,13)+'_'+(events.findIndex(e=>e.name===event)+1);
 const send=async(catalog:CatalogRequest)=>{
  setError('');try{const result=await client.request([{action:'catalog',catalog}]);if(!result.ok){setError(result.status);return null;}return result.catalog??null;}catch(e){setError(e instanceof Error?e.message:'Library query failed.');return null;}
 };
 const search=async(offset=0)=>{const result=await send({operation:'search',category:'modules',query:offset?page?.query??query:query,offset});if(result?.operation==='search'&&result.category==='modules'){setPage(result);setInspection(null);}};
 const inspect=async(hash:string)=>{const result=await send({operation:'inspect',category:'modules',capability:hash,version:1});if(result?.operation==='inspect'&&result.category==='modules'){setInspection(result);setSignals({});setGrant(false);}};
 const execute=async(call:CapabilityInvocation)=>{
  setError('');try{const result=await client.request([{action:'execution',execution:{operation:'start',call}}],state??undefined);if(!result.ok)setError(result.status);else setRunId(result.execution?.selected?.id??null);}catch(e){setError((e instanceof Error?e.message:'Library write could not be confirmed.')+' Inspect action history before retrying.');}
 };
 const insert=()=>{if(!program||!module||!inspection)return;try{if(stale)throw new Error('The behaviour changed. Close and reopen the library.');const updated=editProgramImport(program,inspection.capability,module,{alias,replace:replace||undefined,grantResources:grant,signals:Object.fromEntries(events.map(e=>[e.name,signalTarget(e.name)]))});onChange(JSON.stringify(updated));onClose();}catch(e){setError(e instanceof Error?e.message:'Module import failed.');}};
 return <section aria-label="Reusable module library" className="program-editor">
  <div className="room-workspace-actions"><h3>Reusable modules</h3><button disabled={pending||fileBusy} onClick={onClose}>Back to program</button></div>
  <p>Save a reusable definition, then choose the exact version for a behaviour. Library edits never upgrade existing imports.</p>
  <div role="status" className={error?'room-message room-message-warning':'room-message'}>{error||native?.status||'Search to read the library. Publishing and removal are separate actions.'}</div>
  {selected&&<div className="room-message" aria-label="Library action result"><strong>{selected.phase}</strong><p>{selected.status}</p>{selected.output&&<pre>{JSON.stringify(selected.output,null,2)}</pre>}<button disabled={pending} onClick={()=>void client.request([{action:'execution',execution:{operation:'inspect',runId:selected.id}}]).catch(e=>setError(String(e)))}>Inspect library action</button></div>}
  {fileStatus&&<p role="status">{fileStatus}</p>}
  <fieldset disabled={disabled||busy||Boolean(runPending)}><legend>Module files</legend>
   <p>A module file contains its program and embedded imports. Room objects, models and motion files are not included. Importing saves a library copy; it does not run the program or grant object access.</p>
   <button disabled={!state?.capabilities?.includes('moduleLibraryFiles.v1')} onClick={()=>fileInput.current?.click()}>Choose module file</button>
   <input ref={fileInput} aria-label="Module file" type="file" accept=".json,application/json" hidden onChange={event=>{const selected=event.target.files?.[0];event.target.value='';if(selected)void selectFile(selected);}}/>
   {file&&<section aria-label="Module file preview"><h4>{file.definition.name}</h4><code style={{overflowWrap:'anywhere'}}>{file.hash}</code>
    <p>Exports: {file.definition.exports.join(', ')}</p>
    <p>Referenced objects: {(file.definition.program.resources as string[]).map(id=>state?.objects.find(o=>o.id===id)?.name??'Unavailable '+id).join(', ')||'None'}. References retain their exact IDs; missing dependencies are not replaced.</p>
    <details><summary>Imported definition and exact references</summary><pre>{JSON.stringify(file.definition,null,2)}</pre></details>
    <button onClick={()=>void execute({id:'program.module.import',version:1,arguments:{hash:file.hash,definition:file.definition}})}>Import file to library</button>
   </section>}
  </fieldset>
  <fieldset disabled={disabled||busy||Boolean(runPending)||dirty||stale||!program}><legend>Publish this saved behaviour</legend>
   <p>Apply your behaviour draft first. Publication copies the saved source and does not run it. Identical definitions share one content ID.</p>
   <label>Module name<input aria-label="Module name" maxLength={64} value={name} onChange={e=>setName(e.target.value)}/></label>
   <p>Functions other behaviours may call:</p>{program?.functions.map(fn=><label className="rule-checkbox" key={fn.name}><input type="checkbox" aria-label={'Export '+fn.name} checked={exports.includes(fn.name)} onChange={e=>setExports(e.target.checked?[...exports,fn.name]:exports.filter(n=>n!==fn.name))}/>{fn.name}</label>)}
   <button disabled={!exports.length} onClick={()=>void execute({id:'program.module.publish',version:1,arguments:{sequenceId:sequence.id,rulesRevision,name,exports}})}>Publish module</button>
  </fieldset>
  <form onSubmit={e=>{e.preventDefault();void search();}}><label>Find modules<input aria-label="Find modules" maxLength={80} value={query} onChange={e=>setQuery(e.target.value)}/></label><button disabled={pending}>Search modules</button></form>
  {page&&<><p>{page.total} matching modules{page.ready===false?' · Loading':''}</p><div className="room-object-list">{page.entries.map(entry=><button key={entry.id} disabled={pending} aria-pressed={inspection?.capability===entry.id} onClick={()=>void inspect(entry.id)}><span>{entry.label}</span><small>{entry.id.slice(0,12)}</small></button>)}</div><div className="room-workspace-actions"><button disabled={pending||page.offset===0} onClick={()=>void search(Math.max(0,page.offset-6))}>Previous modules</button><button disabled={pending||page.offset+page.entries.length>=page.total} onClick={()=>void search(page.offset+6)}>Next modules</button></div></>}
  {inspection&&<section aria-label="Inspected library module"><h3>{module?.name??'Unavailable module'}</h3><code style={{overflowWrap:'anywhere'}}>{inspection.capability}</code><p>{inspection.status}</p>
   {module&&<button disabled={busy} onClick={()=>void exportFile()}>Export module file</button>}
   {module&&<fieldset disabled={disabled||pending||stale||!program}><legend>Import or explicitly upgrade</legend>
    <p>Exports: {module.exports.join(', ')}. Imported entry functions do not start automatically.</p>
    <label>Use as<select aria-label="Import operation" value={replace} onChange={e=>{setReplace(e.target.value);if(e.target.value)setAlias(e.target.value);setSignals({});setGrant(false);}}><option value="">New import</option>{program?.imports?.map(i=><option key={i.alias} value={i.alias}>Replace {i.alias} · {i.hash.slice(0,12)}</option>)}</select></label>
    <label>Import name<input aria-label="Import name" maxLength={32} disabled={Boolean(replace)} value={alias} onChange={e=>setAlias(e.target.value)}/></label>
    {previous&&<p>Replace pin {previous.hash.slice(0,12)} with {inspection.capability.slice(0,12)}. Existing calls must remain valid; incompatible changes keep this draft open.</p>}
    {events.map(event=><label key={event.name}>Connect {event.name} ({event.type})<input aria-label={'Connect '+event.name} maxLength={37} value={signalTarget(event.name)} onChange={e=>setSignals({...signals,[event.name]:e.target.value})}/></label>)}
    {events.length>0&&<p>New caller signals are added with the shown types. Using an existing name connects to that room-wide signal.</p>}
    {required.length>0&&<p>Required objects: {required.map(id=>state?.objects.find(o=>o.id===id)?.name??'Unavailable '+id).join(', ')}</p>}
    {additional.length>0&&<label className="rule-checkbox"><input type="checkbox" aria-label="Allow additional module objects" checked={grant} onChange={e=>setGrant(e.target.checked)}/>Add these {additional.length} objects to this behaviour’s declared access</label>}
    <button onClick={insert}>{replace?'Replace pinned import in draft':'Add pinned import to draft'}</button><p>This updates the draft only. Apply saves it and cancels an old run; Start is separate.</p>
    <details><summary>Exact module definition</summary><pre>{JSON.stringify(module,null,2)}</pre></details>
   </fieldset>}
   <details><summary>Remove library copy</summary><p>Existing behaviours keep their embedded copies. The library copy is deleted without Undo.</p><button disabled={disabled||busy||Boolean(runPending)||!inspection.ready} onClick={()=>void execute({id:'program.module.remove',version:1,arguments:{hash:inspection.capability}})}>Remove this library copy</button></details>
  </section>}
 </section>;
}
