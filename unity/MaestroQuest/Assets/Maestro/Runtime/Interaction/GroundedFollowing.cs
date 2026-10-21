// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    /// <summary>A moving destination policy; the shared walking engine owns every accepted step.</summary>
    internal sealed class GroundedFollowing:CapabilityOperation {
        readonly RoomEditor editor;readonly RoomItem item,leader;readonly string leaderId;
        readonly float distance,seconds;readonly bool user;readonly GroundedMovement movement;
        bool stopped,keeping,hasGoal;float nextPath;Vector3 lastGoal;string failure;
        internal GroundedFollowing(RoomEditor editor,RoomItem item,string leaderId,float distance,float speed,float seconds){
            this.editor=editor;this.item=item;this.leaderId=leaderId;this.distance=distance;this.seconds=seconds;user=leaderId==null;leader=user?null:editor.Find(leaderId);
            movement=new(editor,item,editor.Frame.PointToRoom(item.transform.position),speed,"follow");
        }
        internal static bool CanBegin(RoomEditor editor,RoomItem item,string leaderId,float distance,out string error){
            if(!GroundedMovement.CanBegin(editor,item,out error))return false;
            error="An object cannot follow itself";if(leaderId==editor.Identity(item))return false;
            if(leaderId!=null){error="Following needs a loaded leader with accepted collision bounds";var value=editor.Find(leaderId);if(!value||!GroundedMovement.LeaderFoot(value,out _))return false;}
            if(leaderId==null&&GroundedMovement.Body(item,out var radius,out _,out _,out error)&&distance<radius+.35f){error="The following distance is too small for this body's clearance from the user";return false;}
            error=null;return true;
        }
        internal bool Begin(out string error){
            if(!CanBegin(editor,item,leaderId,distance,out error)||!movement.Begin(out error))return false;
            Tick();error=failure??(movement.Failed?movement.Reason:null);return error==null;
        }
        bool ReadLeader(out Vector3 foot){
            foot=default;if(!editor||!editor.ViewerTrackingReady)return false;
            if(user){var viewer=editor.Viewer;if(!viewer||!viewer.gameObject.activeInHierarchy)return false;foot=new(viewer.position.x,movement.Foot.y,viewer.position.z);return RoomRecipe.Finite(foot);}
            return leader&&editor.Find(leaderId)==leader&&!(leader.GetComponent<MaestroAvatar>()?.ModelBusy==true)&&GroundedMovement.LeaderFoot(leader,out foot);
        }
        public override float Seconds=>seconds;
        public override bool WaitsThroughGrab(string target)=>leaderId!=null&&target==leaderId;
        public override void Tick(){
            if(stopped||failure!=null)return;
            if(!ReadLeader(out var leaderFoot)){failure="Following stopped because the leader or tracking became unavailable";movement.Fail(failure);return;}
            var foot=movement.Foot;var delta=Vector3.ProjectOnPlane(leaderFoot-foot,Vector3.up);
            if(delta.magnitude>distance+(keeping?.08f:.02f)){
                keeping=false;var goal=leaderFoot-delta.normalized*distance;
                if(Time.unscaledTime>=nextPath&&(!hasGoal||(lastGoal-goal).sqrMagnitude>.04f||movement.Blocked||movement.Arrived)){
                    nextPath=Time.unscaledTime+.35f;lastGoal=goal;hasGoal=true;movement.Retarget(editor.Frame.PointToRoom(goal));
                }
            }else if(!keeping){keeping=true;hasGoal=false;nextPath=0;movement.Retarget(editor.Frame.PointToRoom(foot));}
            movement.Tick();if(keeping)movement.KeepDistance();
        }
        public override RuleActionState State(out string error){Tick();error=failure??(movement.Failed?movement.Reason:null);return error==null?RuleActionState.Ready:RuleActionState.Failed;}
        public override bool Complete(out string error){if(State(out error)!=RuleActionState.Ready)return false;return movement.Remember(out error);}
        public override JObject Result {get {var result=movement.Result;result["state"]=movement.Status;result["reason"]=movement.Reason.Length>256?movement.Reason[..256]:movement.Reason;return result;}}
        public override void Stop(bool preservePlacement){if(stopped)return;stopped=true;movement.Stop(preservePlacement);}
    }
}
