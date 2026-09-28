// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useState,useSyncExternalStore} from 'react';
import {roomSessionCall} from '../../../shared/roomSession';
import type {RoomAgentClient} from './roomAgentBridge';
/** A projection of the same catalog action and durable receipt path used by the agent. */
export function TemporaryRoomControls({client,disabled=false}:{client:RoomAgentClient;disabled?:boolean}) {
 const {state,pending}=useSyncExternalStore(client.subscribe,client.getSnapshot);
 const [error,setError]=useState('');
 const view=state?.temporaryRoom;if(!view||!state.capabilities?.includes('temporaryRoom.v1'))return null;
 const run=async(operation:'begin'|'keep'|'discard')=>{
  setError('');try{const result=await client.request([{action:'execution',execution:{operation:'start',call:roomSessionCall(operation,view)}}],state);if(!result.ok)setError(result.status);}
  catch(e){setError(e instanceof Error?e.message:'The room did not confirm this request. Inspect its state before retrying.');}
 };
 return <section aria-label="Temporary room" className="room-message">
  <strong>{view.active?'Temporary room':'Saved room'}</strong>
  <p>{view.active?`Object edits stay temporary until you save a snapshot. ${view.savedRevision===0?'Discard returns to the starting room.':'Later edits stay temporary; Discard returns to the last saved snapshot.'}`:'Try changes together in a temporary room. Ordinary creations are saved normally.'}</p>
  <p>Behaviour definitions, imported files and chat are saved separately.</p>
  <div role="status">{error||view.error||(view.phase==='starting'?'Saving the starting room. New edits are already temporary; this write can finish after Stop.':view.pending?'Saving snapshot. A dispatched save can finish after Stop.':view.phase==='ready'?'Starting room saved. New edits remain temporary.':view.phase==='saved'?`Snapshot ${view.savedRevision} saved. Later edits are still temporary.`:'')}</div>
  <div className="room-workspace-actions">
   {!view.active?<button disabled={disabled||pending} onClick={()=>void run('begin')}>Begin temporary room</button>:<>
    <button disabled={disabled||pending||view.pending} onClick={()=>void run('keep')}>Save snapshot</button>
    <button disabled={disabled||pending||view.pending} onClick={()=>void run('discard')}>Discard unsaved & end</button>
   </>}
  </div>
 </section>;
}
