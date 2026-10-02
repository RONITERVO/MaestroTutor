// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState,useSyncExternalStore} from 'react';
import {checkedDataValue,readDataType,type DataValue} from '../../../shared/programValues';
import {ProgramDataValueEditor} from './ProgramDataEditor';
import type {ProgramMemoryCell} from '../../core-sdk/room/programMemory';
import type {RoomAgentClient} from './roomAgentBridge';
export function ProgramMemoryPanel({client,disabled=false}:{client:RoomAgentClient;disabled?:boolean}) {
 const {state,pending}=useSyncExternalStore(client.subscribe,client.getSnapshot),rules=state?.rules,memory=rules?.memory;
 const [error,setError]=useState(''),[runId,setRunId]=useState<string|null>(null);
 const [edit,setEdit]=useState<{cell:ProgramMemoryCell;value:DataValue;revision:string;rulesRevision:number;programId:string}|null>(null);
 const [reset,setReset]=useState<{id:string;revision:string;rulesRevision:number;programId:string}|null>(null);
 if(!memory||!rules)return null;
 const unavailable=disabled||pending||!memory.ready||memory.pending||Boolean(memory.error),blocked=unavailable||memory.busy;
 const changed=(draft:{revision:string;rulesRevision:number;programId:string})=>draft.revision!==memory.revision||draft.rulesRevision!==rules.revision||draft.programId!==memory.programId;
 const groups=new Map(rules.sequences.map(s=>[s.id,s.name]));for(const group of memory.programs)if(!groups.has(group.id))groups.set(group.id,group.name+' '+group.id.slice(0,8));
 const query=async(programId:string,page=0)=>{setError('');try{const result=await client.request([{action:'rules',rule:{action:'memory',...(programId?{target:programId}:{}),page}}]);if(!result.ok)setError(result.status);}catch(e){setError(String(e));}};
 const write=async(args:Record<string,unknown>)=>{
  if(blocked||args.revision!==memory.revision||args.rulesRevision!==rules.revision||args.programId!==memory.programId){setError('Saved values or declarations changed. Inspect again before editing.');return;}
  setError('');try {const result=await client.request([{action:'execution',execution:{operation:'start',call:{id:'program.memory.edit',version:1,arguments:args}}}],state??undefined);
   if(!result.ok)setError(result.status);else {setRunId(result.execution?.selected?.id??null);setEdit(null);setReset(null);}
  }catch(e){setError((e instanceof Error?e.message:'Memory edit was not confirmed')+'. Inspect the action history and remembered values before retrying.');}
 };
 const outcome=runId&&state?.execution?.selected?.id===runId?state.execution.selected:null;
 return <details className="program-memory-panel"><summary>Remembered values</summary>
  <p>Values saved by this workspace, including values left by removed declarations. An explicit start loads saved values. Reset keeps the behaviour and uses its initial value on the next start. Memory edits have no Undo.</p>
  <label>Remembered behaviour<select aria-label="Remembered behaviour" value={memory.programId} disabled={pending||Boolean(edit)||Boolean(reset)} onChange={e=>void query(e.target.value)}><option value="">Selected behaviour</option>{Array.from(groups,([id,name])=><option key={id} value={id}>{name}</option>)}</select></label>
  {(!memory.ready||memory.pending||memory.error||error)&&<p role="status">{error||memory.error||(memory.pending?'Saving remembered values…':'Loading remembered values…')}</p>}
  {memory.error&&<p>Unavailable files are preserved. Use workspace recovery or a verified workspace backup; a new workspace starts with empty memory.</p>}
  {memory.busy&&<p>Stop this behaviour and wait for its accepted saves before editing these values.</p>}
  {memory.busy&&<button disabled={pending} onClick={()=>void client.request([{action:'rules',rule:{action:'stop',target:memory.programId}}]).catch(e=>setError(String(e)))}>Stop for memory editing</button>}
  {outcome&&<div aria-label="Memory action result"><strong>{outcome.phase}</strong><p>{outcome.status}</p><button disabled={pending} onClick={()=>void client.request([{action:'execution',execution:{operation:'inspect',runId:outcome.id}}]).catch(e=>setError(String(e)))}>Inspect memory action</button></div>}
  {memory.cells.map(cell=><div className="program-memory-cell" key={cell.id}><strong>{cell.name}</strong> · {cell.saved?'Saved':'Initial; not saved'}{!cell.declared?' · No current declaration':''}<pre>{cell.valueJson}</pre>
   <button disabled={blocked||Boolean(edit)||Boolean(reset)} onClick={()=>setEdit({cell,value:JSON.parse(cell.valueJson) as DataValue,revision:memory.revision,rulesRevision:rules.revision,programId:memory.programId})}>Edit {cell.name}</button>
   <button disabled={blocked||!cell.saved||Boolean(edit)||Boolean(reset)} onClick={()=>setReset({id:cell.id,revision:memory.revision,rulesRevision:rules.revision,programId:memory.programId})}>Reset {cell.name}</button>
  </div>)}
  {memory.count===0&&<p>No remembered variables for this behaviour.</p>}
  {edit&&<fieldset disabled={blocked||changed(edit)}><legend>Edit saved {edit.cell.name}</legend><ProgramDataValueEditor label="Remembered value" type={readDataType(JSON.parse(edit.cell.typeJson))} value={edit.value} onChange={value=>setEdit({...edit,value})}/><button disabled={blocked||changed(edit)} onClick={()=>{try{checkedDataValue(edit.value,readDataType(JSON.parse(edit.cell.typeJson)));void write({kind:'set',programId:edit.programId,variableId:edit.cell.id,revision:edit.revision,rulesRevision:edit.rulesRevision,valueJson:JSON.stringify(edit.value)});}catch(e){setError(String(e));}}}>Save remembered value</button></fieldset>}
  {edit&&<><p>{changed(edit)?'Saved values or declarations changed. Discard this draft and inspect again.':'This changes only the saved value; it does not run the behaviour.'}</p><button onClick={()=>setEdit(null)}>Discard memory draft</button></>}
  {reset&&<div><p>Permanently reset {reset.id?'this saved value':'all saved values for this behaviour'}? There is no memory Undo.</p>{changed(reset)&&<p>Values changed. Cancel and inspect again.</p>}<button disabled={blocked||changed(reset)} onClick={()=>void write({kind:'reset',programId:reset.programId,variableId:reset.id,revision:reset.revision,rulesRevision:reset.rulesRevision})}>Confirm memory reset</button><button onClick={()=>setReset(null)}>Cancel reset</button></div>}
  <div className="room-workspace-actions"><button disabled={pending||Boolean(edit)||Boolean(reset)||memory.page===0} onClick={()=>void query(memory.programId,memory.page-1)}>Previous values</button><span>{memory.page+1} / {Math.max(1,Math.ceil(memory.count/4))}</span><button disabled={pending||Boolean(edit)||Boolean(reset)||(memory.page+1)*4>=memory.count} onClick={()=>void query(memory.programId,memory.page+1)}>Next values</button>
   <button disabled={blocked||!memory.programId||!memory.programs.some(p=>p.id===memory.programId)||Boolean(edit)||Boolean(reset)} onClick={()=>setReset({id:'',revision:memory.revision,rulesRevision:rules.revision,programId:memory.programId})}>Reset all saved values</button></div>
 </details>;
}
