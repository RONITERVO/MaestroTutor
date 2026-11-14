// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    /// <summary>One owned capsule and deadline across supported and buoyant travel.
    /// Route proposals never publish a pose; every real step proves current inputs.</summary>
    internal sealed class ShoreMovement:CapabilityOperation,IFollowingMotion {
        static readonly List<ShoreMovement> active=new();
        readonly RoomEditor editor;readonly RoomItem item;readonly string habitat;
        readonly RoomLocomotionOwner owner;Vector3 destination;readonly float speed,immersion,followBand;
        bool surface;readonly bool facing,fullPose,following;readonly JObject gaitSelection,arguments;
        RoomShoreClearance clearance;RoomShoreRoute planner;Vector3[] route;int corner,lastFrame=-1,generation=-1;
        RoomUprightBody body;Vector3 localFoot,plannedGoal;MaestroAvatar avatar;
        ImportedObjectWalk walk;ImportedLocomotionPose swim;
        bool owned,stopped,arrived,classified,floating,poseKnown,poseFloating;string failure,blocked;float distance;
        public string Status {get;private set;}="planning";
        public Vector3 FollowPoint=>Foot;
        public bool Failed=>failure!=null;
        public bool Blocked=>blocked!=null;
        public bool Arrived=>arrived;
        public string Reason=>failure??blocked??"";
        Vector3 Foot=>item.transform.TransformPoint(localFoot);
        internal ShoreMovement(RoomEditor editor,RoomItem item,JObject args,bool following=false,float followDistance=0){
            this.following=following;followBand=followDistance*Mathf.Tan(RoomGroundQuery.MaximumSlope*Mathf.Deg2Rad)+RoomGroundQuery.MaximumStep;arguments=(JObject)args.DeepClone();
            this.editor=editor;this.item=item;habitat=(string)args["habitat"];destination=NavigateObjectCapability.Destination(args);
            speed=(float)args["speed"];immersion=(float)args["immersion"];surface=ShoreNavigationSchema.Surface(args);facing=(string)args["facing"]=="travel";
            gaitSelection=args["gait"] is JObject gait?(JObject)gait.DeepClone():null;fullPose=ImportedLocomotionPose.Selected(gaitSelection);
            owner=new(editor,item,fullPose);
        }
        internal static bool CanBegin(RoomEditor editor,RoomItem item,JObject args,out string error){
            if(!GroundedMovement.CanBegin(editor,item,out error)||!ImportedLocomotionPose.Valid(editor,item,args["gait"] as JObject,out error))return false;
            error="Choose a different, existing liquid habitat";
            if((string)args["habitat"]==editor.Identity(item)||!editor.Find((string)args["habitat"]))return false;
            error=null;return true;
        }
        public bool Begin(out string error)=>Begin(arguments,out error);
        internal bool Begin(JObject args,out string error){
            error="This shore action has already started or stopped";if(owned||stopped)return false;
            if(!CanBegin(editor,item,args,out error)||!GroundedMovement.Body(item,out float radius,out float height,out var foot,out error))return false;
            body=new(radius,height);localFoot=item.transform.InverseTransformPoint(foot);avatar=item.GetComponent<MaestroAvatar>();
            clearance=new(editor,item,habitat,body,immersion,()=>owned&&!stopped);
            if(!owner.Attach(this,out error,()=>Stop(false))){clearance.Dispose();clearance=null;return false;}
            owned=true;active.Add(this);return true;
        }
        public void Fail(string error){failure=error??"Shore movement became unavailable";owner.Invalidate(failure);Status="failed";}
        void Block(string error){
            if(!following){Fail(error);return;}
            blocked=error??"The shore following destination is blocked";Status="blocked";planner=null;route=null;arrived=false;
        }
        public void Retarget(Vector3 point){
            if(!following||stopped||failure!=null)return;
            if(!RoomRecipe.Finite(point)||!RoomViewpoint.ValidPosition(point)){Block("Following destination is outside the authored world bounds");return;}
            destination=point;planner=null;route=null;arrived=false;blocked=null;Status="planning";
        }
        public void KeepDistance(){if(!stopped&&!Failed&&!Blocked&&arrived)Status="keepingDistance";}
        public bool Remember(out string error){
            if(!owner.Validate(this,out error)){Fail(error);return false;}
            var foot=Foot;
            if(!clearance.Prepare(foot,foot,out error)||!clearance.Classify(foot,out var wet,out error)||
                !clearance.Step(foot,Vector3.zero,.001f,wet,out _,out error)){
                error??="The shore body is still preparing";return false;
            }
            return owner.TryRemember(this,out error);
        }
        public override float Seconds=>0;
        public override RuleActionState State(out string error){Tick();error=failure;return failure!=null?RuleActionState.Failed:arrived?RuleActionState.Ready:RuleActionState.Preparing;}
        bool PrepareGoal(out Vector3 goal,out string error){
            goal=editor.Frame.PointToWorld(destination);if(surface&&!following)goal.y=classified?clearance.FloatFoot:Foot.y;
            if(!clearance.Prepare(Foot,goal,out error,following?followBand:0)){
                if(clearance.Pending){Status="preparingWorld";return false;}Fail(error);return false;
            }
            if(following){
                if(!classified){if(!clearance.Classify(Foot,out floating,out error)){Fail(error);return false;}classified=true;}
                if(!clearance.Step(Foot,Vector3.zero,.001f,floating,out _,out error)){Fail(error);return false;}
                if(!clearance.FollowGoal(goal,followBand,out goal,out surface,out error)){Block(error);return false;}
            }
            if(surface){
                goal.y=clearance.FloatFoot;
                if(!clearance.Prepare(Foot,goal,out error)){
                    if(clearance.Pending){Status="preparingWorld";return false;}Fail(error);return false;
                }
            }
            error=null;return true;
        }
        bool PoseReady(bool afloat,out string error){
            if(!poseKnown||poseFloating!=afloat){
                // Restore the preceding source before capturing the successor.
                // Loading a gait holds root ownership and never replays travel.
                walk?.Dispose();walk=null;swim?.Dispose();swim=null;
                if(avatar){avatar.SpatialWalk(0);avatar.SetEditing(true,preserveUpperBody:!fullPose);}
                if(afloat)swim=new(editor,item,gaitSelection);
                else if(!avatar)walk=new(editor,item);
                poseKnown=true;poseFloating=afloat;
            }
            var state=afloat?swim.State(out error):walk!=null?walk.State(out error):Ready(out error);
            if(state==RuleActionState.Failed){Fail(error);return false;}
            if(state==RuleActionState.Preparing){Status="preparingAnimation";return false;}
            return true;
        }
        static RuleActionState Ready(out string error){error=null;return RuleActionState.Ready;}
        public override void Tick(){
            if(stopped||failure!=null||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            if(!owned){Fail("The shore movement owner became unavailable");return;}
            if(!owner.Validate(this,out var ownershipError)){Fail(ownershipError);return;}
            arrived=false;
            if(!PrepareGoal(out var goal,out var error))return;
            if(generation!=clearance.Generation||Vector3.Distance(plannedGoal,goal)>.0001f){
                planner=null;route=null;generation=clearance.Generation;plannedGoal=goal;
            }
            var from=Foot;
            if(!classified){
                if(!clearance.Classify(from,out floating,out error)){Fail(error);return;}
                classified=true;
            }
            if(!PoseReady(floating,out error))return;
            float dt=Mathf.Clamp(Time.deltaTime,0,.05f);if(dt<=0)return;
            // Re-prove even a cached arrival, and rise/fall with a live surface
            // before spending movement on horizontal travel.
            if(!clearance.Step(from,Vector3.zero,speed*dt,floating,out var standing,out error)){Fail(error);return;}
            if(Vector3.Distance(from,standing.Foot)>.0001f||standing.Floating!=floating){
                planner=null;route=null;Status="surfaceAdjusting";ApplyStep(from,standing,dt);return;
            }
            if(blocked!=null)return;
            if(Vector3.Distance(from,goal)<.005f&&floating==surface){arrived=true;Status="arrived";return;}
            if(route==null){
                Status="planning";planner??=new(from,floating,goal,surface,clearance.Step);planner.Tick();
                if(!planner.Done)return;
                if(planner.Path==null){Block(planner.Error);return;}
                route=planner.Path;corner=1;
            }
            while(corner<route.Length&&Vector3.Distance(from,route[corner])<.005f)corner++;
            if(corner>=route.Length){Fail("The shore route ended before its typed destination");return;}
            var delta=route[corner]-from;delta.y=0;
            if(!clearance.Step(from,Vector3.ClampMagnitude(delta,speed*dt),speed*dt,floating,out var next,out error)){Block(error);return;}
            Status="moving";ApplyStep(from,next,dt);
        }
        void ApplyStep(Vector3 from,RoomShoreMotor.Step next,float dt){
            if(!PoseReady(next.Floating,out var error))return;
            var direction=next.Foot-from;direction.y=0;
            var rotation=facing&&direction.sqrMagnitude>1e-8f?Quaternion.RotateTowards(item.transform.rotation,Quaternion.LookRotation(direction),120*dt):item.transform.rotation;
            var position=next.Foot-rotation*Vector3.Scale(item.transform.lossyScale,localFoot);
            if(!RoomRecipe.Finite(position)||!RoomViewpoint.ValidPosition(editor.Frame.PointToRoom(position))){Fail("Shore movement would leave the authored world");return;}
            item.transform.SetPositionAndRotation(position,rotation);floating=next.Floating;distance+=next.Distance;owner.AcceptPose();
            if(floating){if(!swim.Apply(dt,out error)){Fail(error);return;}}
            else if(avatar)avatar.SpatialWalk(next.Distance/dt);
            else if(walk?.Apply(next.Distance/dt,dt)==false){Fail("The assigned walking animation became unavailable");return;}
            owner.Checkpoint();
        }
        public override bool Complete(out string error){
            if(State(out error)!=RuleActionState.Ready){error??="The actor has not arrived";return false;}
            if(!owner.Validate(this,out error)){Fail(error);return false;}
            if(!PrepareGoal(out var goal,out error)){arrived=false;error??="The shore world is still preparing";return false;}
            var foot=Foot;
            if(!clearance.Step(foot,Vector3.zero,.001f,floating,out var stance,out error)){Fail(error);return false;}
            if(Vector3.Distance(foot,goal)>=.005f||stance.Floating!=surface||Vector3.Distance(stance.Foot,foot)>.0001f){
                arrived=false;Status="planning";error="The actor has not arrived at the current supported or buoyant destination";return false;
            }
            if(owner.TryRemember(this,out error))return true;
            Fail(error);return false;
        }
        public override JObject Result {get{var p=editor&&item?editor.Frame.PointToRoom(item.transform.position):Vector3.zero;return new(){["position"]=new JObject{["x"]=p.x,["y"]=p.y,["z"]=p.z},["distance"]=distance};}}
        internal static JObject Observe(RoomEditor editor,RoomItem item){
            foreach(var value in active)if(value.editor==editor&&value.item==item){
                var result=value.Result;result["active"]=true;result["mode"]=value.following?"follow":"shore";result["movement"]="shore";result["state"]=value.Status;
                var reason=value.Reason;result["reason"]=reason.Length>256?reason[..256]:reason;return result;
            }return null;
        }
        public override void Stop(bool preservePlacement){
            if(stopped)return;stopped=true;if(!arrived)failure??="Shore movement was stopped";
            clearance?.Dispose();clearance=null;walk?.Dispose();walk=null;swim?.Dispose();swim=null;
            if(owned){owned=false;active.Remove(this);if(avatar)avatar.SpatialWalk(0);owner.Detach(this);}
            owner.Dispose();
        }
    }
}
