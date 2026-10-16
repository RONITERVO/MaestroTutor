// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class LiquidPouring
    {
        uint flowEpoch;
        // Actual visits in the last quantity pass, not discovery/PhysX time or
        // a scheduling allowance. A value copy cannot alter the running sample.
        internal struct FlowExecutionWork {
            internal int Groups,Members,PourSegments,PourCandidates,ImmersionChecks,ImmersionCandidates,ScoopRecipients,ScoopCandidates,RainSamples;
        }
        FlowExecutionWork flowExecutionWork;
        internal FlowExecutionWork LastFlowExecution=>flowExecutionWork;
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
        internal LiquidPouring PhysicsFlowFor(IEnumerable<RigidRoomItem> members)=>Liquids&&members.Any(body=>body&&body.PhysicsWorkEligible&&Liquids.FlowTarget(body.PhysicsWorkOwner.Target))?Liquids:null;
        internal bool ExecutionWorkReady(PhysicsIslandQuery query,out string error){
            error=null;
            foreach(var id in query.Members){var body=Find(id)?.GetComponent<RigidRoomItem>();if(body&&body.PhysicsWorkEligible&&!PhysicsWorkResources.Ready(body,out error))return false;}
            return FlowWorkReady(query,out error);
        }
        internal bool ExecutionWorkDeferred(PhysicsIslandQuery query){
            var members=query.Members.Select(id=>Find(id)?.GetComponent<RigidRoomItem>()).Where(body=>body&&body.PhysicsWorkEligible).ToArray();
            return members.All(PhysicsWorkResources.Queued)&&!ExecutionWorkReady(query,out _);
        }
        bool PhysicsNeedsFlow(PhysicsIslandQuery query){
            if(!Liquids)return false;
            foreach(var id in query.Members){var body=Find(id)?.GetComponent<RigidRoomItem>();if(body&&body.PhysicsWorkEligible&&Liquids.FlowTarget(id))return true;}
            return false;
        }
        internal bool PrepareFlowWork(PhysicsIslandQuery query,out string error){
            error=null;return !PhysicsNeedsFlow(query)||FlowWorkResources.TryAdmit(Liquids,out error);
        }
        internal bool FlowWorkReady(PhysicsIslandQuery query,out string error){
            error=null;return !PhysicsNeedsFlow(query)||FlowWorkResources.Ready(Liquids,out error);
        }
    }
}
