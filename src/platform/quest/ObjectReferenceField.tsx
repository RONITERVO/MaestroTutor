// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {createContext,useContext,useEffect,useRef,useState,useSyncExternalStore,type ReactNode} from 'react';
import {validateCapabilityValue,type CapabilitySchema} from '../../../shared/capabilities';
import {objectSearchFeature,type ObjectSearchEntry} from '../../../shared/objectSearch';
import {objectReferenceRequest,readObjectReference,type ObjectReferenceSnapshot} from '../../../shared/objectReference';
import {roomObjectLabel} from '../../../shared/roomSelection';
import type {RoomAgentClient} from './roomAgentBridge';
import {ObjectSearchList} from './ObjectSearchList';

type Scope={client:RoomAgentClient;session:string;temporary?:string;visible:boolean;pending:boolean;supported:boolean};
const ObjectReferenceContext=createContext<Scope|null>(null);
/** Provides the existing native client to every schema-generated action/fact/program field. */
export function ObjectReferenceProvider({client,children}:{client:RoomAgentClient;children:ReactNode}) {
 const {state,pending}=useSyncExternalStore(client.subscribe,client.getSnapshot);
 return <ObjectReferenceContext.Provider value={{client,session:state?.session??'',temporary:state?.temporaryRoom?.id,visible:state?.visible!==false,pending,supported:state?.capabilities?.includes(objectSearchFeature)===true}}>{children}</ObjectReferenceContext.Provider>;
}
type Props={schema:CapabilitySchema;value:unknown;onChange:(value:unknown)=>void;label:string;objects:readonly {id:string;name?:string}[]};
export function ObjectReferenceField(props:Props) {
 const scope=useContext(ObjectReferenceContext);
 if(scope?.supported)return <SavedObjectReference key={JSON.stringify([scope.session,scope.temporary])} {...props} scope={scope}/>;
 const selected=typeof props.value==='string'?props.value:'',options=props.objects.filter(o=>validateCapabilityValue(o.id,props.schema)===null);
 return <label>{props.label}<select aria-label={props.label} value={selected} onChange={e=>props.onChange(e.target.value)}>
  {!options.some(o=>o.id===selected)&&<option value={selected}>{selected||'Choose an object'}</option>}
  {options.map(o=><option key={o.id} value={o.id}>{roomObjectLabel(o,props.objects)}</option>)}
 </select></label>;
}
function SavedObjectReference({schema,value,onChange,label,objects,scope}:{scope:Scope}&Props) {
 const id=typeof value==='string'?value:'',valid=validateCapabilityValue(id,schema)===null;
 const [open,setOpen]=useState(false),[loading,setLoading]=useState(false),[error,setError]=useState('');
 const [observed,setObserved]=useState<ObjectReferenceSnapshot|ObjectSearchEntry|null>(null);
 const generation=useRef(0),alive=useRef(true),context=JSON.stringify([scope.session,scope.temporary,scope.visible,id,schema]),latest=useRef(context);latest.current=context;
 useEffect(()=>{alive.current=true;return()=>{alive.current=false;generation.current++;};},[]);
 useEffect(()=>{generation.current++;setOpen(false);setLoading(false);setError('');},[context]);
 const read=async()=>{
  const token=++generation.current,captured=context;
  const current=()=>alive.current&&generation.current===token&&latest.current===captured&&scope.client.getSnapshot().state?.session===scope.session&&scope.client.getSnapshot().state?.temporaryRoom?.id===scope.temporary;
  setError('');setLoading(true);
  try {
   const result=await scope.client.request([{action:'catalog',catalog:objectReferenceRequest(id)}]);
   if(!current())return;
   const snapshot=result.ok?readObjectReference(result.catalog,id):null;
   if(!snapshot)throw new Error(result.ok?'This object cannot be read now. Its saved ID has been kept.':result.status);
   setObserved(snapshot);
  }catch(e){if(current())setError(e instanceof Error?e.message:'This object cannot be read now. Its saved ID has been kept.');}
  finally{if(alive.current&&generation.current===token)setLoading(false);}
 };
 const chosen=observed?.id===id?observed:null,name=chosen?.name||chosen?.kind||objects.find(o=>o.id===id)?.name;
 const busy=scope.pending||loading||!scope.visible;
 return <section className="object-reference-field" aria-label={label+' object choice'}>
  <strong>{label}</strong><p>{id?(name||'Selected object'):'Choose an object'}{chosen&&'revision' in chosen?` · read at revision ${chosen.revision}`:''}</p>
  {id&&<details><summary>Selected object ID</summary><code>{id}</code><p>The exact ID is retained even outside these search results. Reading its name does not reserve an edit revision.</p></details>}
  <div className="room-workspace-actions"><button type="button" disabled={busy} aria-expanded={open} onClick={()=>setOpen(!open)}>{open?'Close search for ':'Find '}{label}</button><button type="button" disabled={busy||!valid||!id} onClick={()=>void read()}>Read {label}</button></div>
  {open&&<ObjectSearchList client={scope.client} active={scope.visible} disabled={loading} selectedId={id} title={'Find '+label+' objects'} accepts={entry=>validateCapabilityValue(entry.id,schema)===null} onSelect={(next,entry)=>{
   if(validateCapabilityValue(next,schema)!==null)return;
   generation.current++;setObserved(entry);setError('');setOpen(false);onChange(next);
  }}/>}
  {error&&<p role="alert">{error}</p>}
 </section>;
}
