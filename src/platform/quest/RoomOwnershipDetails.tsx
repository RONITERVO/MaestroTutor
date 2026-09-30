// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {RoomAgentState} from '../../core-sdk/room/roomAgent';
const channels:Record<string,string>={wholeTarget:'whole object',locomotion:'movement',upperBody:'arms and torso',gaze:'look',prop:'held object'};
const roles={ambient:'Tutor activity',program:'Behaviour',reflex:'Reaction',control:'Direct control',grab:'Your hands'};
/** The same native ownership evidence supplied to the agent, visible on request. */
export function RoomOwnershipDetails({state}:{state:RoomAgentState}) {
 const view=state.ownership;if(!view||!state.capabilities?.includes('roomOwnership.v1'))return null;
 return <details className="room-ownership"><summary>In control now · {view.owners.length}</summary>
  {view.error?<p role="status">{view.error}</p>:view.suspended?<p>Room actions are paused.</p>:view.owners.length===0?<p>No actions are controlling objects.</p>:null}
  {view.owners.map(owner=><div key={owner.id} className="room-owner"><strong>{owner.label}</strong><small>{roles[owner.role]}{owner.allowsGrab?' · You can grip and move this object':''}</small>
   <ul>{owner.claims.map(claim=><li key={JSON.stringify([claim.target,claim.channel])}>{state.objects.find(o=>o.id===claim.target)?.name??claim.target} · {channels[claim.channel]??claim.channel}</li>)}</ul>
  </div>)}
  <p>Interrupted behaviours stay stopped. Use their Run control when you want to start again.</p>
 </details>;
}
