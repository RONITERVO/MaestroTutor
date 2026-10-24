// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    /// <summary>Finite arrival semantics over the shared owned walking engine.</summary>
    internal sealed class GroundedTravel:CapabilityOperation {
        readonly GroundedMovement movement;
        internal GroundedTravel(RoomEditor editor,RoomItem item,Vector3 destination,float speed){movement=new(editor,item,destination,speed);}
        internal static bool CanBegin(RoomEditor editor,RoomItem item,out string error)=>GroundedMovement.CanBegin(editor,item,out error);
        internal bool Begin(out string error)=>movement.Begin(out error);
        public override float Seconds=>0;
        public override void Tick()=>movement.Tick();
        public override RuleActionState State(out string error){Tick();error=movement.Failed||movement.Blocked?movement.Reason:null;return error!=null?RuleActionState.Failed:movement.Arrived?RuleActionState.Ready:RuleActionState.Preparing;}
        public override bool Complete(out string error){if(State(out error)!=RuleActionState.Ready){error??="The actor has not arrived";return false;}return movement.Remember(out error,requireArrival:true);}
        public override JObject Result=>movement.Result;
        public override void Stop(bool preservePlacement)=>movement.Stop(preservePlacement);
    }
}
