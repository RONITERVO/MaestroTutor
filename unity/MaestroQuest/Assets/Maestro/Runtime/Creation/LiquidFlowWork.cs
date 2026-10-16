// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class LiquidPouring
    {
        uint flowEpoch;
        // Like contact sampling, an explicit internal tick can step a disabled
        // automatic loop in desktop tests. An inactive workspace cannot step.
        internal bool FlowWorkEligible=>gameObject.activeInHierarchy&&!contactsPaused&&contactsFocused&&editor&&editor.isActiveAndEnabled&&world&&world.Running&&!blocked&&!editor.RuntimeGate.Held&&!editor.Ownership.Suspended&&!editor.WriteGate.Frozen&&editor.CanSaveRoom&&editor.Frame.Valid&&vessels.Count>0&&float.IsFinite(Physics.gravity.sqrMagnitude)&&Physics.gravity.sqrMagnitude>=.01f;
        internal int FlowWorkVessels=>vessels.Count;
        internal RoomResourceOwner FlowWorkOwner=>new(editor.WorldIdentity,null,"liquid");
        internal bool FlowTarget(string id)=>vessels.ContainsKey(id);
        string FlowWorkIssue=>FlowWorkEligible&&!FlowWorkResources.Ready(this,out var issue)?issue:null;
        string FlowPhase(string id)=>blocked?"failed":(FlowWorkIssue??FlowInputIssue(id))!=null?"waiting":Owns(id)?"flowing":"idle";
        void InvalidateRain(string reason){foreach(var v in vessels.Values){v.Exposure=0;v.RainReady=false;v.RainReason=reason;}}
        void WaitForFlowWork(string reason){
            // Only the active flow tick closes accepted work. Admission checks in
            // physics preparation must not mutate the journal or re-enter a save.
            InvalidateRain(reason);HideStreams();
            if(FlowWorkResources.Queued(this))return; // A missed opportunity is not a new episode.
            ReleaseFlowInputs();Finish(out _);
        }
    }
    public sealed partial class RoomEditor
    {
        bool PhysicsNeedsFlow(PhysicsIslandQuery query){
            if(!Liquids)return false;
            foreach(var id in query.Members){var body=Find(id)?.GetComponent<RigidRoomItem>();if(body&&body.PhysicsWorkEligible&&Liquids.FlowTarget(id))return true;}
            return false;
        }
        internal bool PrepareFlowWork(PhysicsIslandQuery query,out string error){
            error=null;return !PhysicsNeedsFlow(query)||FlowWorkResources.TryAdmit(Liquids,out error);
        }
        internal bool FlowWorkDeferred(PhysicsIslandQuery query)=>PhysicsNeedsFlow(query)&&FlowWorkResources.Queued(Liquids)&&!FlowWorkResources.Ready(Liquids,out _);
        internal bool FlowWorkReady(PhysicsIslandQuery query,out string error){
            error=null;return !PhysicsNeedsFlow(query)||FlowWorkResources.Ready(Liquids,out error);
        }
    }
}
