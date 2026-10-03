// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState,useSyncExternalStore} from 'react';
import {constructionCaptureCall,constructionSelectionCall,roomObjectLabel} from '../../../shared/roomSelection';
import type {CapabilityInvocation} from '../../../shared/capabilities';
import type {RoomAgentClient} from './roomAgentBridge';
export function ConstructionSelectionControls({client,disabled,onCapture}:{client:RoomAgentClient;disabled:boolean;onCapture:(call:CapabilityInvocation)=>void}) {
 const {state,pending}=useSyncExternalStore(client.subscribe,client.getSnapshot),[error,setError]=useState('');
 const selection=state?.constructionSelection;if(!state?.capabilities?.includes('constructionSelection.v1')||!selection)return null;
 const locked=disabled||pending;
 const change=async(members:string[],collecting=selection.collecting)=>{setError('');try{const result=await client.request([{action:'execution',execution:{operation:'start',call:constructionSelectionCall(selection,members,collecting)}}],state);if(!result.ok)setError(result.status);else if(result.execution?.selected?.phase!=='completed')setError(result.execution?.selected?.status??'Selection change has not completed.');}catch(e){setError(e instanceof Error?e.message:'Selection could not be changed.');}};
 const locate=async(target:string)=>{setError('');try{const result=await client.request([{action:'inspect',target}]);if(!result.ok)setError(result.status);}catch(e){setError(e instanceof Error?e.message:'Object could not be located.');}};
 const label=(id:string)=>roomObjectLabel(state.objects.find(o=>o.id===id)!,state.objects);
 return <details className="construction-selection" open={selection.collecting||undefined}>
  <summary>Construction pieces · {selection.members.length} selected</summary>
  <p>Collect pieces in the room or choose them below. Locate highlights one piece. The first selected piece becomes the origin when you save a reusable construction.</p>
  {selection.collecting&&<p role="status">Collecting: point and tap to add or remove pieces. Item-tapped behaviours are paused for these taps. Finish keeps your selection.</p>}
  {error&&<p role="alert">{error}</p>}
  <div className="room-workspace-actions"><button disabled={locked} onClick={()=>void change(selection.members,!selection.collecting)}>{selection.collecting?'Finish collecting':'Collect in room'}</button><button disabled={locked||!selection.members.length} onClick={()=>void change([],false)}>Clear pieces</button></div>
  <fieldset disabled={locked}><legend>Choose up to 16 creations</legend>{state.objects.filter(o=>!['book','maestro'].includes(o.id)).map(object=><div className="room-selection-choice" key={object.id}><label><input type="checkbox" aria-label={'Include '+roomObjectLabel(object,state.objects)} checked={selection.members.includes(object.id)} disabled={!selection.members.includes(object.id)&&selection.members.length>=16} onChange={e=>void change(e.target.checked?[...selection.members,object.id]:selection.members.filter(id=>id!==object.id))}/>{roomObjectLabel(object,state.objects)}</label><button onClick={()=>void locate(object.id)} aria-label={'Locate '+roomObjectLabel(object,state.objects)}>Locate</button></div>)}</fieldset>
  {selection.members.length>0&&<ol aria-label="Construction piece order">{selection.members.map((id,i)=><li key={id}>{label(id)}{i===0?' · origin':''}<button disabled={locked||i===0} aria-label={'Move '+label(id)+' earlier'} onClick={()=>{const members=[...selection.members];[members[i-1],members[i]]=[members[i],members[i-1]];void change(members);}}>Earlier</button></li>)}</ol>}
  <button disabled={locked||!selection.members.length||!state.capabilities.includes('constructionCapture.v1')} onClick={()=>{setError('');try{onCapture(constructionCaptureCall(selection,state.objects));}catch(e){setError(e instanceof Error?e.message:'Capture inputs are unavailable.');}}}>Review reusable construction</button>
  <p>Selection is temporary and does not change the saved room. Capture runs only after you review and start its existing action.</p>
 </details>;
}
