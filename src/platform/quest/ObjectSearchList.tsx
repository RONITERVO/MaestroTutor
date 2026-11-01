// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useEffect,useRef,useState,useSyncExternalStore} from 'react';
import {objectSearchKinds,objectSearchRequest,readObjectSearchPage,type ObjectSearchEntry,type ObjectSearchPage} from '../../../shared/objectSearch';
import {roomObjectLabel} from '../../../shared/roomSelection';
import type {RoomAgentClient} from './roomAgentBridge';
type Page={value:ObjectSearchPage;query:string;kind:string;cursor:string;previous:string[]};
/** A bounded projection of the same native discovery fact available to programs and agents. */
export function ObjectSearchList({client,active,disabled,selectedId,selectedIds,onSelect,accepts,exclusionLabel='Not allowed for this field',title='Find saved objects'}:{client:RoomAgentClient;active:boolean;disabled:boolean;selectedId?:string;selectedIds?:readonly string[];onSelect:(id:string,entry:ObjectSearchEntry)=>void;accepts?:(entry:ObjectSearchEntry)=>boolean;exclusionLabel?:string;title?:string}) {
 const {state,pending}=useSyncExternalStore(client.subscribe,client.getSnapshot);
 const [query,setQuery]=useState(''),[kind,setKind]=useState('all'),[page,setPage]=useState<Page|null>(null);
 const [loading,setLoading]=useState(false),[attempted,setAttempted]=useState(false),[error,setError]=useState('');
 const epoch=useRef(0),alive=useRef(true);
 useEffect(()=>{alive.current=true;return()=>{alive.current=false;epoch.current++;};},[]);
 useEffect(()=>{epoch.current++;setPage(null);setAttempted(false);setError('');},[state?.session,state?.temporaryRoom?.id]);
 const search=async(cursor='',previous:string[]=[],searchQuery=query,searchKind=kind)=>{
  const generation=++epoch.current,session=state?.session,temporary=state?.temporaryRoom?.id;
  const current=()=>alive.current&&epoch.current===generation&&client.getSnapshot().state?.session===session&&client.getSnapshot().state?.temporaryRoom?.id===temporary;
  setAttempted(true);setLoading(true);setError('');
  try {
   const request=objectSearchRequest(searchQuery,searchKind,cursor);const result=await client.request([{action:'catalog',catalog:request}]);
   if(!current())return;
   const value=result.ok?readObjectSearchPage(result.catalog,request):null;
   if(!value){setError(result.ok?'Search results changed or are unavailable. Search again to refresh them.':result.status);setPage(null);return;}
   setPage({value,query:searchQuery,kind:searchKind,cursor,previous});
  }catch(e){if(current()){setPage(null);setError(e instanceof Error?e.message:'Objects could not be searched.');}}
  finally{if(alive.current)setLoading(false);}
 };
 useEffect(()=>{if(active&&!pending&&!loading&&!disabled&&!attempted)void search();},[active,pending,loading,disabled,attempted]);
 if(!active)return null;
 const busy=pending||loading||disabled,changed=page&&(page.query!==query||page.kind!==kind);
 const status=error||(loading?'Finding objects…':page?`${page.value.total?`${page.value.offset+1}–${page.value.offset+page.value.entries.length} of ${page.value.total}`:'No matching objects'}${changed?' · Search to apply your filter':''}`:'Search includes objects outside your current view.');
 return <div className="room-object-search">
  <form onSubmit={e=>{e.preventDefault();if(!busy)void search();}}><fieldset disabled={busy}><legend>{title}</legend>
   <label>Object name or ID<input aria-label={title} value={query} maxLength={80} onChange={e=>setQuery(e.target.value)}/></label>
   <label>Kind<select aria-label={title==='Find saved objects'?'Object kind':title+' kind'} value={kind} onChange={e=>setKind(e.target.value)}>{objectSearchKinds().map(value=><option key={value} value={value}>{value==='all'?'All kinds':value==='ImportedModel'?'Imported model':value}</option>)}</select></label>
   <button type="submit">Search objects</button>
  </fieldset></form>
  <p role="status">{status}</p>
  <div className="room-object-list" aria-label="Objects">{page?.value.entries.map(object=><button key={object.id} disabled={busy||accepts?.(object)===false} aria-pressed={selectedIds?selectedIds.includes(object.id):selectedId===object.id} onClick={()=>{if(accepts?.(object)!==false)onSelect(object.id,object);}}><span>{object.kind==='Assembly'?'◇':object.kind==='Maestro'?'♙':object.kind==='Book'?'▤':'○'} {roomObjectLabel(object,state?.objects??page.value.entries)}</span><small>{object.kind}{selectedIds?.includes(object.id)?' · Included':''}{accepts?.(object)===false?' · '+exclusionLabel:''}{state?.objects.find(o=>o.id===object.id)?.animated?' · Playing':''}</small></button>)}</div>
  {page&&<div className="room-workspace-actions"><button disabled={busy||Boolean(changed)||!page.previous.length} onClick={()=>void search(page.previous[page.previous.length-1],page.previous.slice(0,-1),page.query,page.kind)}>Previous objects</button><button disabled={busy||Boolean(changed)||!page.value.next} onClick={()=>void search(page.value.next,[...page.previous,page.cursor],page.query,page.kind)}>Next objects</button></div>}
 </div>;
}
