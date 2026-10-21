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
    /// <summary>One finite, owned, wholly submerged journey. The accepted body, habitat
    /// and solid sweeps authorize every root change; elapsed time never authorizes travel.</summary>
    internal sealed class AquaticMovement:CapabilityOperation {
        static readonly List<AquaticMovement> active=new();
        readonly RoomEditor editor;readonly RoomItem item;readonly string target,session,habitat;
        readonly Vector3 destination;readonly float speed,immersion;readonly bool facing,fullPose,surface;readonly JObject gaitSelection;
        RoomAquaticClearance clearance;RoomAquaticRoute planner;Pose[] route;int corner,lastFrame=-1,revision,inputGeneration=-1;
        ImportedLocomotionPose gait;MaestroAvatar avatar;RigidRoomItem rigid;
        bool owned,stopped,arrived,surfaceGoalKnown;string failure;float started,rememberAt,distance,surfaceHintY;
        Vector3 acceptedPosition,acceptedScale,framePosition,plannedGoal;Quaternion acceptedRotation,frameRotation;
        internal string Status {get;private set;}="planning";
        internal AquaticMovement(RoomEditor editor,RoomItem item,JObject args){
            this.editor=editor;this.item=item;target=editor.Identity(item);session=editor.TemporarySessionId;
            habitat=(string)args["habitat"];destination=NavigateObjectCapability.Destination(args);speed=(float)args["speed"];surface=AquaticNavigationSchema.Surface(args);immersion=surface?(float)args["immersion"]:1;
            facing=(string)args["facing"]=="travel";gaitSelection=args["gait"] is JObject g?(JObject)g.DeepClone():null;
            fullPose=ImportedLocomotionPose.Selected(gaitSelection);
        }
        internal static bool HasOwner(RoomItem item)=>active.Exists(x=>x.item==item&&x.owned&&!x.stopped);
        internal static bool CanBegin(RoomEditor editor,RoomItem item,JObject args,out string error){
            error="Swimming needs an active actor and room";
            if(!editor||!editor.isActiveAndEnabled||!item||!item.isActiveAndEnabled||editor.RuntimeGate.Held)return false;
            error="Start physics before swimming";
            if(!editor.PhysicsWorld||!editor.PhysicsWorld.SimulationActive)return false;
            if(!RoomLocomotionBudget.Available(out error))return false;
            error="Swimming needs a tracked viewer and an upright metre-scale world";
            if(!editor.Viewer||!editor.Viewer.gameObject.activeInHierarchy||!editor.ViewerTrackingReady||!editor.Frame.Valid||
                Mathf.Abs(editor.Frame.MetresPerUnit-1)>.0001f||Vector3.Dot(editor.transform.up,Vector3.up)<.99999f)return false;
            error="Release this object and stop authoring before swimming";
            if(item.Grab.isSelected||item.PoseLocked||editor.GetComponent<AnimationWorkshop>()?.ControlsTarget(editor.Identity(item))==true)return false;
            error="Physical anchors, terrain owners and connected assemblies cannot swim as one actor";
            if(item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true||item.GetComponentsInChildren<RoomWalkableSurface>(true).Length>0||editor.PhysicsIslandMembers(item).Length>1)return false;
            error="Stop the object's root animation or attachment before swimming";
            if(item.GetComponent<RigidRoomItem>()?.AnimationOwned==true||item.GetComponent<HeldRoomProp>()?.Holding==true)return false;
            error="Choose a different, existing liquid habitat";
            string id=(string)args["habitat"];
            if(id==editor.Identity(item)||!editor.Find(id))return false;
            return ImportedLocomotionPose.Valid(editor,item,args["gait"] as JObject,out error);
        }
        internal bool Begin(JObject args,out string error){
            error="This swimming action has already started or stopped";
            if(owned||stopped)return false;
            if(!CanBegin(editor,item,args,out error))return false;
            clearance=new(editor,item,habitat,()=>owned&&!stopped,surface);
            if(!clearance.Body(out _,out error)){clearance.Dispose();clearance=null;return false;}
            if(!RoomLocomotionBudget.Acquire(this,out error)){clearance.Dispose();clearance=null;return false;}
            owned=true;active.Add(this);started=Time.realtimeSinceStartup;rememberAt=Time.unscaledTime+1;
            revision=editor.ObjectRevision(target);AcceptPose();
            framePosition=editor.transform.position;frameRotation=editor.transform.rotation;
            rigid=item.GetComponent<RigidRoomItem>();avatar=item.GetComponent<MaestroAvatar>();
            if(rigid){rigid.StopVelocity();rigid.SetAnimationOwner(this,true);}
            if(avatar)avatar.SetEditing(true,preserveUpperBody:!fullPose);
            gait=new(editor,item,gaitSelection);
            return true;
        }
        void AcceptPose(){acceptedPosition=item.transform.localPosition;acceptedRotation=item.transform.localRotation;acceptedScale=item.transform.localScale;}
        void Fail(string error){failure=error??"Swimming became unavailable";Status="failed";}
        public override float Seconds=>0;
        public override RuleActionState State(out string error){Tick();error=failure;return failure!=null?RuleActionState.Failed:arrived?RuleActionState.Ready:RuleActionState.Preparing;}
        public override void Tick(){
            if(stopped||failure!=null||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            if(!owned||!editor||!editor.isActiveAndEnabled||editor.TemporarySessionId!=session||editor.RuntimeGate.Held||
                !editor.ViewerTrackingReady||!editor.Viewer||!editor.Viewer.gameObject.activeInHierarchy||
                !item||!item.isActiveAndEnabled||editor.Find(target)!=item||item.Grab.isSelected||item.PoseLocked||
                editor.GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){Fail("The swimming actor, tracking or workspace became unavailable");return;}
            if(editor.ObjectRevision(target)!=revision||Vector3.Distance(item.transform.localPosition,acceptedPosition)>.001f||
                Quaternion.Angle(item.transform.localRotation,acceptedRotation)>.1f||Vector3.Distance(item.transform.localScale,acceptedScale)>.0001f||
                avatar&&avatar.ModelBusy){Fail("The swimming actor changed; start a new journey from its current position");return;}
            if(!editor.PhysicsWorld||!editor.PhysicsWorld.SimulationActive||!editor.Frame.Valid||
                Mathf.Abs(editor.Frame.MetresPerUnit-1)>.0001f||Vector3.Dot(editor.transform.up,Vector3.up)<.99999f){Fail("The aquatic physics or world frame became unavailable");return;}
            if(Time.realtimeSinceStartup-started>300){Fail("Swimming exceeded its five-minute lifetime");return;}
            var goal=editor.Frame.PointToWorld(destination);if(surface)goal.y=surfaceGoalKnown?surfaceHintY:item.transform.position.y;
            if(!clearance.Prepare(goal,out var error)){
                if(clearance.Pending){Status="preparingWorld";return;}
                Fail(error);return;
            }
            if(surface){
                if(!clearance.SurfaceDestination(goal,immersion,out goal,out error)){Fail(error);return;}
                surfaceGoalKnown=true;surfaceHintY=goal.y;
                // The surface may be above/below the actor's initial corridor. Its
                // resolved height participates in the same native input admission.
                if(!clearance.Prepare(goal,out error)){
                    if(clearance.Pending){Status="preparingWorld";return;}Fail(error);return;
                }
            }
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
            if(Vector3.Distance(current.position,goal)<.005f){arrived=true;Status="arrived";return;}
            if(route==null){
                Status="planning";planner??=new(current,goal,facing,clearance.Clear,surface?goal.y:null);planner.Tick();
                if(!planner.Done)return;
                if(planner.Path==null){Fail(planner.Error);return;}
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
        void ApplyStep(Pose current,Pose next,float dt){
            if(!clearance.Clear(current,next,out var error)){Fail(error);return;}
            item.transform.SetPositionAndRotation(next.position,next.rotation);
            distance+=Vector3.Distance(current.position,next.position);AcceptPose();
            if(!gait.Apply(dt,out error)){Fail(error);return;}
            if(Time.unscaledTime>=rememberAt){rememberAt=Time.unscaledTime+1;Remember();}
        }
        void Remember(){if(editor&&item&&editor.Find(target)==item&&editor.TemporarySessionId==session){editor.RememberPlacement(target);revision=editor.ObjectRevision(target);}}
        public override bool Complete(out string error){if(State(out error)!=RuleActionState.Ready){error??="The actor has not arrived";return false;}Remember();return true;}
        public override JObject Result {get{var p=editor&&item?editor.Frame.PointToRoom(item.transform.position):Vector3.zero;return new(){["position"]=new JObject{["x"]=p.x,["y"]=p.y,["z"]=p.z},["distance"]=distance};}}
        internal static JObject Observe(RoomEditor editor,RoomItem item){
            foreach(var value in active)if(value.editor==editor&&value.item==item){
                var result=value.Result;result["active"]=true;result["mode"]=value.surface?"surface":"swim";result["state"]=value.Status;
                string reason=value.failure??"";result["reason"]=reason.Length>256?reason[..256]:reason;return result;
            }
            return null;
        }
        public override void Stop(bool preservePlacement){
            if(stopped)return;stopped=true;if(!arrived)failure??="Swimming was stopped";
            clearance?.Dispose();clearance=null;gait?.Dispose();gait=null;
            if(!owned)return;owned=false;active.Remove(this);RoomLocomotionBudget.Release(this);
            if(avatar)avatar.SetEditing(false,preserveUpperBody:!fullPose);
            Remember();if(rigid){rigid.StopVelocity();rigid.SetAnimationOwner(this,false);}
        }
    }
}
