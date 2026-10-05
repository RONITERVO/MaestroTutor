// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {useCallback,useEffect,useState} from 'react';
import type {RoomAgentClient} from './roomAgentBridge';
import {sessionActivity} from '../browser/sessionActivity';

type Intent={visible:boolean;attemptedSession?:string};
/** Only presentation intent may cross the startup handoff. Room edits still use
 * the client's original session lease and are never queued or replayed here. */
export function useWorkspaceNavigation(room:RoomAgentClient,view:ReturnType<RoomAgentClient['getSnapshot']>) {
  const [intent,setIntent]=useState<Intent|null>(null);
  const request=useCallback((visible:boolean)=>setIntent(current=>
    visible?{visible}:current||room.getSnapshot().state?.visible||room.snapshot().request?.commands.some(command=>command.action==='workspace'&&command.visible)?{visible:false}:null),[room]);
  useEffect(()=>sessionActivity.onSuspend(()=>setIntent(null)),[]);
  useEffect(()=>{
    if(!intent||sessionActivity.status().suspended)return;
    // The workshop's own Back button uses the same client. Respect it while an
    // acknowledged opening is still waiting for the native content to bind.
    if(intent.visible&&room.snapshot().request?.commands.some(command=>command.action==='workspace'&&!command.visible)){
      setIntent(null);return;
    }
    const state=view.state;if(!state||view.pending)return;
    if(Boolean(state.visible)===intent.visible){
      // Before content exists, a visible maintenance view may still be replaced
      // by the initial bind. Settle an open only when the book itself is present.
      if(!intent.visible||state.objects.some(object=>object.id==='book'))setIntent(null);
      return;
    }
    if(intent.attemptedSession===state.session)return;
    const attempt={...intent,attemptedSession:state.session};setIntent(attempt);
    void room.request([{action:'workspace',visible:intent.visible}],state).then(result=>{
      if(!result.ok)setIntent(current=>current===attempt?null:current);
    }).catch(()=>{
      const currentSession=room.getSnapshot().state?.session;
      // A live replacement may accept a fresh presentation request. A timeout,
      // disconnection or refusal is not an instruction to keep retrying.
      if(!currentSession||currentSession===state.session)setIntent(current=>current===attempt?null:current);
    });
  },[intent,room,view]);
  return {request,suppressOpen:intent?.visible===false};
}
