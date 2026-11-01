// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Interaction
{
    public sealed partial class RoomPhysicsWorld
    {
        int startIntentVersion;bool preparingStart;
        // A pending explicit Start may survive its own native readiness changes,
        // never another control, scan, environment or lifecycle transition.
        internal bool CanPrepareSimulation(string expected,out string error)
        {
            error="Room physics changed; read physics.simulation again before changing it";
            if(expected!=stateId||CollisionAdmissionIssue!=observedCollisionIssue)return false;
            error=RuntimeHeld?RuntimeHoldReason:!Active?"Return to the active room before starting physics":null;
            return error==null;
        }
        internal bool BeginPreparedSimulation(string expected,out int intent,out string error)
        {
            intent=0;if(!CanPrepareSimulation(expected,out error))return false;
            intent=++startIntentVersion;preparingStart=true;Status="Preparing objects for physics";Notify(readinessOnly:true);return true;
        }
        internal bool PreparedSimulationCurrent(int intent)=>this&&preparingStart&&intent==startIntentVersion&&!RuntimeHeld&&Active;
        internal bool CompletePreparedSimulation(int intent,out JObject result,out string error)
        {
            result=null;error="Physics start was superseded; inspect the room and request Start again";
            if(!PreparedSimulationCurrent(intent))return false;
            if(!SetRunning(true,out error))return false;
            result=ObserveSimulation();return true;
        }
        internal void CancelPreparedSimulation(int intent)
        {
            if(!this||!preparingStart||intent!=startIntentVersion)return;
            Status=IdleStatus;Notify();
        }
    }
}
