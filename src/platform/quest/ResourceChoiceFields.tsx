// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useEffect,useRef,useState} from 'react';
import {argumentValue,type CapabilitySchema,type ResourceChoice} from '../../../shared/capabilities';
import {applyResourceChoice,resourceChoices,resourceChoiceKey,resourceChoiceRequest,readResourceChoicePage,type ResourceChoicePage} from '../../../shared/resourceChoices';
import type {RoomAgentClient} from './roomAgentBridge';
const displayName=(name:string)=>name.trim()||'Unnamed resource';
export function ResourceChoiceFields({schema,value,onChange,client,ready,session}:{schema:CapabilitySchema;value:unknown;onChange:(value:unknown,fields:string[])=>void;client:RoomAgentClient;ready:boolean;session:string}) {
 let choices:ResourceChoice[];try{choices=resourceChoices(schema);}catch{return <p role="alert">Saved resource choices are unavailable for this action.</p>;}
 return <>{choices.map(choice=><ResourceChoiceField key={session+JSON.stringify(choice)} {...{schema,value,onChange,client,ready,session,choice}}/>)}</>;
}
function ResourceChoiceField({schema,value,onChange,client,ready,session,choice}:{schema:CapabilitySchema;value:unknown;onChange:(value:unknown,fields:string[])=>void;client:RoomAgentClient;ready:boolean;session:string;choice:ResourceChoice}) {
 const [page,setPage]=useState<ResourceChoicePage|null>(null),[back,setBack]=useState<number[]>([]),[error,setError]=useState(''),[loading,setLoading]=useState(false);
 const [chosen,setChosen]=useState<{key:string;name:string}|null>(null);
 const generation=useRef(0),alive=useRef(true),context=JSON.stringify([session,value]),latest=useRef(context);latest.current=context;
 useEffect(()=>{alive.current=true;return()=>{alive.current=false;generation.current++;};},[]);
 const load=async(offset=0,previous:number[]=[])=>{
  const token=++generation.current,captured=context;
  const current=()=>alive.current&&generation.current===token&&latest.current===captured&&client.snapshot().session===session;
  setError('');setLoading(true);setPage(null);
  try{
   const result=await client.request([{action:'catalog',catalog:resourceChoiceRequest(schema,choice,offset)}]);
   if(!current())return;
   if(!result.ok||!result.catalog)throw new Error(result.status||'Saved resources are unavailable.');
   setPage(readResourceChoicePage(schema,choice,offset,result.catalog));setBack(previous);
  }catch(e){if(current())setError(e instanceof Error?e.message:'Could not read saved resources.');}
  finally{if(alive.current&&generation.current===token)setLoading(false);}
 };
 const key=resourceChoiceKey(choice,value),id=argumentValue(value,choice.id),revision=choice.revision?argumentValue(value,choice.revision):undefined;
 const selected=page?.entries.find(e=>e.id===id&&(!choice.revision||e.revision===revision));
 const name=(selected?displayName(selected.name):undefined)??(chosen?.key===key?chosen.name:typeof id==='string'&&id?'Saved resource':choice.emptyLabel??'No resource selected');
 const select=(entry:ResourceChoicePage['entries'][number]|null)=>{
  try{const next=applyResourceChoice(schema,choice,value,entry);generation.current++;setChosen({key:resourceChoiceKey(choice,next),name:entry?displayName(entry.name):choice.emptyLabel!});setError('');onChange(next,[choice.id,...(choice.revision?[choice.revision]:[])]);}
  catch(e){setError(e instanceof Error?e.message:'This resource cannot be selected.');}
 };
 return <section aria-label={choice.label+' choice'}>
  <strong>{choice.label}</strong><p>{name}{choice.revision&&id?` · revision ${String(revision)}`:''}</p>
  <button disabled={!ready||loading} onClick={()=>void load()}>Load saved {choice.label.toLowerCase()}</button>
  {!ready&&<small>Load current values first.</small>}
  {/* A command menu starts empty so choosing the already bound resource is still explicit intent. */}
  {page&&<><label>Choose {choice.label.toLowerCase()}<select aria-label={'Choose '+choice.label.toLowerCase()} disabled={!ready||loading} value="" onChange={e=>{if(e.target.value==='empty')select(null);else{const entry=page.entries.find(e2=>JSON.stringify([e2.id,choice.revision?e2.revision:null])===e.target.value);if(entry)select(entry);}}}>
   <option value="" disabled>Choose a saved resource</option>{choice.emptyLabel&&<option value="empty">{choice.emptyLabel}</option>}
   {page.entries.map(e=><option key={e.id} value={JSON.stringify([e.id,choice.revision?e.revision:null])}>{displayName(e.name)}{page.entries.filter(other=>displayName(other.name)===displayName(e.name)).length>1?` · ${e.id}`:''} · revision {e.revision}</option>)}
  </select></label><p>{page.total===0?'No saved resources yet.':!page.entries.length?'This page is empty. Reload the list or go back.':`${page.offset+1}–${page.offset+page.entries.length} of ${page.total}`}</p>
   <button disabled={!ready||loading||!back.length} onClick={()=>void load(back[back.length-1],back.slice(0,-1))}>Previous {choice.label.toLowerCase()}</button>
   <button disabled={!ready||loading||page.next===null} onClick={()=>void load(page.next!,[...back,page.offset])}>Next {choice.label.toLowerCase()}</button>
  </>}
  {loading&&<p role="status">Loading saved resources…</p>}{error&&<p role="alert">{error}</p>}
  {typeof id==='string'&&id&&<details><summary>Selected {choice.label.toLowerCase()} identity</summary><code>{id}</code>{choice.revision?<p>Revision {String(revision)}. Reloading the list never changes this selection; choose a resource explicitly to replace it.</p>:<p>Shared source: future plays use its saved definition when playback begins. Editing it does not replace audio already playing.</p>}</details>}
 </section>;
}
