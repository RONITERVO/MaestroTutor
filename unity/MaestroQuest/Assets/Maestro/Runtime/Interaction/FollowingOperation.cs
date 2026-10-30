// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    /// <summary>One leader policy for every native movement medium. Retargeting
    /// preserves the motor, gait time, root owner, capacity slot and deadline.</summary>
    internal abstract class FollowingOperation:CapabilityOperation {
        protected readonly RoomEditor editor;protected readonly RoomItem item;readonly RoomItem leader;protected readonly string leaderId;
        readonly IFollowingMotion movement;protected readonly float distance;readonly float seconds;readonly bool user,spatial;
        bool stopped,keeping,hasGoal;float nextPath;Vector3 lastGoal;string failure;
        protected FollowingOperation(RoomEditor editor,RoomItem item,string leaderId,float distance,float seconds,IFollowingMotion movement,bool spatial=false){
            this.editor=editor;this.item=item;this.leaderId=leaderId;this.distance=distance;this.seconds=seconds;this.movement=movement;this.spatial=spatial;
            user=leaderId==null;leader=user?null:editor.Find(leaderId);
        }
        internal static bool LeaderAvailable(RoomEditor editor,RoomItem item,string leaderId,out string error){
            error="An object cannot follow itself";if(leaderId==editor.Identity(item))return false;
            if(leaderId!=null){
                error="Following needs a loaded leader with accepted collision bounds";
                var value=editor.Find(leaderId);if(!value||!GroundedMovement.LeaderFoot(value,out _))return false;
            }
            error=null;return true;
        }
        protected virtual bool CanStart(out string error)=>LeaderAvailable(editor,item,leaderId,out error);
        internal bool Begin(out string error){
            if(!CanStart(out error)||!movement.Begin(out error))return false;
            Tick();error=failure??(movement.Failed?movement.Reason:null);return error==null;
        }
        bool ReadLeader(out Vector3 point){
            point=default;if(!editor||!editor.ViewerTrackingReady)return false;
            if(user){
                var viewer=editor.Viewer;if(!viewer||!viewer.gameObject.activeInHierarchy)return false;
                point=spatial?viewer.position:new(viewer.position.x,movement.FollowPoint.y,viewer.position.z);
                return RoomRecipe.Finite(point);
            }
            if(!leader||editor.Find(leaderId)!=leader||leader.GetComponent<MaestroAvatar>()?.ModelBusy==true||
                !GroundedMovement.LeaderFoot(leader,out point))return false;
            if(spatial)point=leader.transform.position;
            return RoomRecipe.Finite(point);
        }
        public override float Seconds=>seconds;
        public override bool WaitsThroughGrab(string target)=>leaderId!=null&&target==leaderId;
        public override void Tick(){
            if(stopped||failure!=null)return;
            if(!ReadLeader(out var leaderPoint)){failure="Following stopped because the leader or tracking became unavailable";movement.Fail(failure);return;}
            var point=movement.FollowPoint;var delta=leaderPoint-point;if(!spatial)delta.y=0;
            if(delta.magnitude>distance+(keeping?.08f:.02f)){
                keeping=false;var goal=leaderPoint-delta.normalized*distance;
                if(Time.unscaledTime>=nextPath&&(!hasGoal||(lastGoal-goal).sqrMagnitude>.04f||movement.Blocked||movement.Arrived)){
                    nextPath=Time.unscaledTime+.35f;lastGoal=goal;hasGoal=true;movement.Retarget(editor.Frame.PointToRoom(goal));
                }
            }else if(!keeping){keeping=true;hasGoal=false;nextPath=0;movement.Retarget(editor.Frame.PointToRoom(point));}
            movement.Tick();if(keeping)movement.KeepDistance();
        }
        public override RuleActionState State(out string error){if(stopped){error="Following was stopped";return RuleActionState.Failed;}Tick();error=failure??(movement.Failed?movement.Reason:null);return error==null?RuleActionState.Ready:RuleActionState.Failed;}
        public override bool Complete(out string error){if(State(out error)!=RuleActionState.Ready)return false;return movement.Remember(out error);}
        public override JObject Result {get{var result=movement.Result;result["state"]=failure!=null?"failed":movement.Status;var reason=failure??movement.Reason;result["reason"]=reason.Length>256?reason[..256]:reason;return result;}}
        public override void Stop(bool preservePlacement){if(stopped)return;stopped=true;movement.Stop(preservePlacement);}
    }
}
