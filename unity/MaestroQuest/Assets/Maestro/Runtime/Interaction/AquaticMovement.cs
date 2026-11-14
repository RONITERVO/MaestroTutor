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
    /// <summary>One finite, owned submerged or surface journey. The accepted body, habitat
    /// and solid sweeps authorize every root change; elapsed time never authorizes travel.</summary>
    internal sealed class AquaticMovement:CapabilityOperation,IFollowingMotion {
        static readonly List<AquaticMovement> active=new();
        readonly RoomEditor editor;readonly RoomItem item;readonly string target,habitat;
        readonly RoomLocomotionOwner owner;readonly bool ownsLifetime;
        Vector3 destination;readonly float speed,immersion;readonly bool facing,fullPose,surface,following;readonly JObject gaitSelection,arguments;
        RoomAquaticClearance clearance;RoomAquaticRoute planner;Pose[] route;int corner,lastFrame=-1,inputGeneration=-1;
        ImportedLocomotionPose gait;
        bool owned,stopped,arrived,surfaceGoalKnown;string failure,blocked;float distance,surfaceHintY;
        Vector3 framePosition,plannedGoal;Quaternion frameRotation;
        public string Status {get;private set;}="planning";
        public Vector3 FollowPoint=>item.transform.position;
        public bool Failed=>failure!=null;
        public bool Blocked=>blocked!=null;
        public bool Arrived=>arrived;
        public string Reason=>failure??blocked??"";
        internal AquaticMovement(RoomEditor editor,RoomItem item,JObject args,RoomLocomotionOwner owner=null,bool following=false){
            this.editor=editor;this.item=item;this.following=following;arguments=(JObject)args.DeepClone();target=editor.Identity(item);
            habitat=(string)args["habitat"];destination=NavigateObjectCapability.Destination(args);speed=(float)args["speed"];surface=AquaticNavigationSchema.Surface(args);immersion=surface?(float)args["immersion"]:1;
            facing=(string)args["facing"]=="travel";gaitSelection=args["gait"] is JObject g?(JObject)g.DeepClone():null;
            fullPose=ImportedLocomotionPose.Selected(gaitSelection);
            this.owner=owner??new(editor,item,fullPose);ownsLifetime=owner==null;
        }
        internal static bool HasOwner(RoomItem item)=>active.Exists(x=>x.item==item&&x.owned&&!x.stopped);
        internal static bool CanBegin(RoomEditor editor,RoomItem item,JObject args,out string error,RoomLocomotionOwner owner=null){
            if(!RoomLocomotionOwner.CanBegin(editor,item,owner,ImportedLocomotionPose.Selected(args["gait"] as JObject),out error))return false;
            error="Choose a different, existing liquid habitat";
            string id=(string)args["habitat"];
            if(id==editor.Identity(item)||!editor.Find(id))return false;
            return ImportedLocomotionPose.Valid(editor,item,args["gait"] as JObject,out error);
        }
        public bool Begin(out string error)=>Begin(arguments,out error);
        internal bool Begin(JObject args,out string error){
            error="This swimming action has already started or stopped";
            if(owned||stopped)return false;
            if(!CanBegin(editor,item,args,out error,owner))return false;
            clearance=new(editor,item,habitat,()=>owned&&!stopped,surface);
            if(!clearance.Body(out _,out error)){clearance.Dispose();clearance=null;return false;}
            if(!owner.Attach(this,out error,()=>Stop(false))){clearance.Dispose();clearance=null;return false;}
            owned=true;active.Add(this);
            framePosition=editor.transform.position;frameRotation=editor.transform.rotation;
            gait=new(editor,item,gaitSelection);
            return true;
        }
        public void Fail(string error){failure=error??"Swimming became unavailable";owner.Invalidate(failure);Status="failed";}
        void Block(string error){
            if(!following){Fail(error);return;}
            blocked=error??"The aquatic following destination is blocked";Status="blocked";planner=null;route=null;arrived=false;
        }
        public void Retarget(Vector3 point){
            if(!following||stopped||failure!=null)return;
            if(!RoomRecipe.Finite(point)||!RoomViewpoint.ValidPosition(point)){Block("Following destination is outside the authored world bounds");return;}
            destination=point;planner=null;route=null;arrived=false;blocked=null;Status="planning";
        }
        public void KeepDistance(){if(!stopped&&!Failed&&!Blocked&&arrived)Status="keepingDistance";}
        public bool Remember(out string error){
            if(!owner.Validate(this,out error)){Fail(error);return false;}
            var current=new Pose(item.transform.position,item.transform.rotation);
            if(!clearance.Prepare(current.position,out error)||!clearance.Clear(current,current,out error)){
                error??="The aquatic body is still preparing";return false;
            }
            return owner.TryRemember(this,out error);
        }
        public override float Seconds=>0;
        public override RuleActionState State(out string error){Tick();error=failure;return failure!=null?RuleActionState.Failed:arrived?RuleActionState.Ready:RuleActionState.Preparing;}
        public override void Tick(){
            if(stopped||failure!=null||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            if(!owned){Fail("The swimming owner became unavailable");return;}
            if(!owner.Validate(this,out var ownershipError)){Fail(ownershipError);return;}
            arrived=false;
            if(!PrepareGoal(out var goal,out var error))return;
            if(inputGeneration!=clearance.Generation||Vector3.Distance(plannedGoal,goal)>.0001f){planner=null;route=null;inputGeneration=clearance.Generation;plannedGoal=goal;}
            var readiness=gait.State(out error);
            if(readiness==RuleActionState.Failed){Fail(error);return;}
            if(readiness==RuleActionState.Preparing){Status="preparingAnimation";return;}
            Physics.SyncTransforms();
            var current=new Pose(item.transform.position,item.transform.rotation);
            if(!clearance.Clear(current,current,out error)){Fail(error);return;}
            if(Vector3.Distance(framePosition,editor.transform.position)>.0001f||Quaternion.Angle(frameRotation,editor.transform.rotation)>.01f){
                planner=null;route=null;framePosition=editor.transform.position;frameRotation=editor.transform.rotation;
            }
            float dt=Mathf.Clamp(Time.deltaTime,0,.05f);if(dt<=0)return;
            if(surface&&Mathf.Abs(current.position.y-goal.y)>=.005f){
                Status="surfaceAdjusting";var adjusted=current;
                adjusted.position.y=Mathf.MoveTowards(current.position.y,goal.y,speed*dt);
                ApplyStep(current,adjusted,dt);return;
            }
            if(blocked!=null)return;
            if(Vector3.Distance(current.position,goal)<.005f){arrived=true;Status="arrived";return;}
            if(route==null){
                Status="planning";planner??=new(current,goal,facing,clearance.Clear,surface?goal.y:null);planner.Tick();
                if(!planner.Done)return;
                if(planner.Path==null){Block(planner.Error);return;}
                route=planner.Path;corner=1;
            }
            while(corner<route.Length&&Vector3.Distance(current.position,route[corner].position)<.0001f&&Quaternion.Angle(current.rotation,route[corner].rotation)<.01f)corner++;
            if(corner>=route.Length){Fail("The aquatic route ended before the destination");return;}
            Pose next=current;
            if(Quaternion.Angle(current.rotation,route[corner].rotation)>.01f){
                next.rotation=Quaternion.RotateTowards(current.rotation,route[corner].rotation,120*dt);Status="turning";
            }else{next.position=Vector3.MoveTowards(current.position,route[corner].position,speed*dt);Status="moving";}
            ApplyStep(current,next,dt);
        }
        bool PrepareGoal(out Vector3 goal,out string error){
            goal=editor.Frame.PointToWorld(destination);if(surface)goal.y=surfaceGoalKnown?surfaceHintY:item.transform.position.y;
            if(!clearance.Prepare(goal,out error)){
                if(clearance.Pending){Status="preparingWorld";return false;}
                Fail(error);return false;
            }
            if(following){
                Physics.SyncTransforms();var current=new Pose(item.transform.position,item.transform.rotation);
                if(!clearance.Clear(current,current,out error)){Fail(error);return false;}
            }
            if(surface){
                if(!clearance.SurfaceDestination(goal,immersion,out goal,out error)){Block(error);return false;}
                surfaceGoalKnown=true;surfaceHintY=goal.y;
                // The surface may be above/below the actor's initial corridor. Its
                // resolved height participates in the same native input admission.
                if(!clearance.Prepare(goal,out error)){
                    if(clearance.Pending){Status="preparingWorld";return false;}Fail(error);return false;
                }
            }
            error=null;return true;
        }
        void ApplyStep(Pose current,Pose next,float dt){
            if(!clearance.Clear(current,next,out var error)){Block(error);return;}
            item.transform.SetPositionAndRotation(next.position,next.rotation);
            distance+=Vector3.Distance(current.position,next.position);owner.AcceptPose();
            if(!gait.Apply(dt,out error)){Fail(error);return;}
            owner.Checkpoint();
        }
        public override bool Complete(out string error){
            if(State(out error)!=RuleActionState.Ready){error??="The actor has not arrived";return false;}
            if(!owner.Validate(this,out error)){Fail(error);return false;}
            // Completion checks the live habitat and current body, without
            // advancing root motion or consuming another animation step.
            if(!PrepareGoal(out var goal,out error)){arrived=false;error??="The swimming world is still preparing";return false;}
            Physics.SyncTransforms();var current=new Pose(item.transform.position,item.transform.rotation);
            if(!clearance.Clear(current,current,out error)){Fail(error);return false;}
            if(Vector3.Distance(current.position,goal)>=.005f){
                arrived=false;Status="planning";error="The actor has not arrived at the current destination";return false;
            }
            if(owner.TryRemember(this,out error))return true;
            Fail(error);return false;
        }
        public override JObject Result {get{var p=editor&&item?editor.Frame.PointToRoom(item.transform.position):Vector3.zero;return new(){["position"]=new JObject{["x"]=p.x,["y"]=p.y,["z"]=p.z},["distance"]=distance};}}
        internal static JObject Observe(RoomEditor editor,RoomItem item){
            foreach(var value in active)if(value.editor==editor&&value.item==item){
                var result=value.Result;result["active"]=true;result["mode"]=value.following?"follow":value.surface?"surface":"swim";result["movement"]=value.surface?"surface":"swim";result["state"]=value.Status;
                string reason=value.Reason;result["reason"]=reason.Length>256?reason[..256]:reason;return result;
            }
            return null;
        }
        public override void Stop(bool preservePlacement){
            if(stopped)return;stopped=true;if(!arrived)failure??="Swimming was stopped";
            clearance?.Dispose();clearance=null;gait?.Dispose();gait=null;
            if(owned){owned=false;active.Remove(this);owner.Detach(this);}
            if(ownsLifetime)owner.Dispose();
        }
    }
}
