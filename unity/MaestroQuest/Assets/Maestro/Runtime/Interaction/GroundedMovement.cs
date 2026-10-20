// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    /// <summary>One owned walking body and planner, shared by fixed journeys and following.</summary>
    internal sealed class GroundedMovement {
        internal const int MaximumActive=4;
        static readonly List<GroundedMovement> active=new();
        readonly RoomEditor editor;readonly RoomItem item;readonly string target,session;
        Vector3 destination;readonly float speed;readonly string mode;readonly Func<Collider,bool> obstacle;
        RoomNavigation navigation;ImportedObjectWalk gait;MaestroAvatar avatar;RigidRoomItem rigid;
        float radius,height,started,rememberAt,distance;Vector3 localFoot,acceptedPosition,acceptedScale;Quaternion acceptedRotation;
        Vector3[] route=Array.Empty<Vector3>();int corner,lastFrame=-1,revision;uint surfaceRevision;
        bool owned,stopped,arrived;string failure,blocked;
        internal string Status {get;private set;}="planning";
        internal string Reason=>failure??blocked??navigation?.FollowRouteStatus??"";
        internal bool Failed=>failure!=null;
        internal bool Blocked=>blocked!=null;
        internal bool Arrived=>arrived;
        internal Vector3 Foot=>item?item.transform.TransformPoint(localFoot):Vector3.zero;
        internal GroundedMovement(RoomEditor editor,RoomItem item,Vector3 destination,float speed,string mode="walk"){this.mode=mode;this.editor=editor;this.item=item;target=editor.Identity(item);session=editor.TemporarySessionId;this.destination=destination;this.speed=speed;obstacle=Obstacle;}
        internal static bool CanBegin(RoomEditor editor,RoomItem item,out string error){
            error="Grounded movement needs an active target and room";
            if(!editor||!item||!item.isActiveAndEnabled||!editor.isActiveAndEnabled||editor.RuntimeGate.Held)return false;
            error="Start physics with accepted ground before walking";if(!editor.PhysicsWorld||!editor.PhysicsWorld.SimulationActive||editor.PhysicsWorld.IncludesRealRoom(item)&&!editor.PhysicsWorld.EnvironmentReady(item))return false;
            error="Wait for another walking/following action to finish (four active actions maximum)";if(active.Count>=MaximumActive)return false;
            error="Walking needs a tracked viewer and an upright metre-scale world";
            var viewer=editor.Viewer;if(!viewer||!viewer.gameObject.activeInHierarchy||!editor.ViewerTrackingReady||!editor.Frame.Valid||Mathf.Abs(editor.Frame.MetresPerUnit-1)>.0001f||Vector3.Dot(editor.transform.up,Vector3.up)<.99999f)return false;
            error="Release this object and stop authoring before walking";
            if(item.Grab.isSelected||item.PoseLocked||editor.GetComponent<AnimationWorkshop>()?.ControlsTarget(editor.Identity(item))==true)return false;
            error="A physical anchor, terrain owner or connected assembly cannot walk as one actor";
            if(item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true||item.GetComponentsInChildren<RoomWalkableSurface>(true).Length>0||editor.PhysicsIslandMembers(item).Length>1)return false;
            error="Stop the object's current root animation or attachment before walking";
            if(item.GetComponent<RigidRoomItem>()?.AnimationOwned==true||item.GetComponent<HeldRoomProp>()?.Holding==true)return false;
            var walk=AvatarWalkSelection.Observe(editor,editor.Identity(item));
            if(item.GetComponent<CreatedRoomObject>()&&walk!=null&&walk.source!="none"&&!walk.available){error=walk.status;return false;}
            return Body(item,out _,out _,out _,out error);
        }
        internal static bool Body(RoomItem item,out float radius,out float height,out Vector3 foot,out string error){
            radius=height=0;foot=default;error="Walking needs an upright object with accepted root colliders";
            if(Vector3.Dot(item.transform.up,Vector3.up)<.99999f)return false;
            if(item.GetComponent<MaestroAvatar>()) {float scale=item.transform.lossyScale.y;radius=.25f*scale;height=1.7f*scale;foot=item.transform.position;}
            else {
                Bounds bounds=default;bool found=false;
                foreach(var c in item.Grab.colliders){if(!c||!c.enabled||c.isTrigger||!c.gameObject.activeInHierarchy||c.gameObject.layer==RoomPhysicsLayers.Pending)continue;if(!found){bounds=c.bounds;found=true;}else bounds.Encapsulate(c.bounds);}
                if(!found||!RoomRecipe.Finite(bounds.center)||!RoomRecipe.Finite(bounds.extents))return false;
                radius=new Vector2(bounds.extents.x,bounds.extents.z).magnitude;
                height=Mathf.Max(bounds.size.y,2*radius+RoomGroundQuery.Skin+.01f);
                foot=new(bounds.center.x,bounds.min.y,bounds.center.z);
            }
            error="The walking body must have radius 0.02–1.5 m and height at most 5 m";
            if(!float.IsFinite(radius)||!float.IsFinite(height)||radius<.02f||radius>1.5f||height>5)return false;
            error=null;return true;
        }
        internal bool Begin(out string error){
            error="This walking operation has already started or stopped";if(owned||stopped)return false;
            if(!CanBegin(editor,item,out error)||!Body(item,out radius,out height,out var foot,out error))return false;
            avatar=item.GetComponent<MaestroAvatar>();rigid=item.GetComponent<RigidRoomItem>();
            localFoot=item.transform.InverseTransformPoint(foot);
            var host=new GameObject("Owned walking query");host.transform.SetParent(editor.transform,false);navigation=host.AddComponent<RoomNavigation>();navigation.Initialize(editor.PhysicsWorld);navigation.SetVirtualFrame(editor.transform);
            if(!navigation.Prepare(radius,height,out error,item)){navigation.enabled=false;ArtResources.Release(host);navigation=null;return false;}
            owned=true;active.Add(this);started=Time.realtimeSinceStartup;rememberAt=Time.unscaledTime+1;revision=editor.ObjectRevision(target);
            acceptedPosition=item.transform.localPosition;acceptedRotation=item.transform.localRotation;acceptedScale=item.transform.localScale;
            if(rigid){rigid.StopVelocity();rigid.SetAnimationOwner(this,true);}if(avatar){avatar.SetEditing(true,preserveUpperBody:true);avatar.SpatialWalk(0);}
            if(!avatar)gait=new ImportedObjectWalk(editor,item);
            return true;
        }
        bool Obstacle(Collider c)=>RoomWalkingObstacle.Blocks(c,item.transform,target);
        void IdlePose(){if(avatar)avatar.SpatialWalk(0);gait?.Apply(0,0);}
        internal void Fail(string message){failure=message??"Walking became unavailable";Status="failed";IdlePose();}
        void Block(string message){blocked=message??"No accepted walking route is available";Status="blocked";IdlePose();}
        internal void Retarget(Vector3 point){
            if(stopped||failure!=null)return;
            if(!RoomRecipe.Finite(point)||point.sqrMagnitude>625){Block("Following destination is outside the authored world bounds");return;}
            destination=point;route=Array.Empty<Vector3>();corner=0;arrived=false;blocked=null;Status="planning";navigation?.CancelFollowRoute();
        }
        internal void KeepDistance(){if(!stopped&&!Failed&&!Blocked&&arrived)Status="keepingDistance";}
        internal static bool LeaderFoot(RoomItem value,out Vector3 foot){
            foot=default;if(!value||!value.isActiveAndEnabled)return false;
            if(value.GetComponent<MaestroAvatar>()){foot=value.transform.position;return RoomRecipe.Finite(foot);}
            Bounds bounds=default;bool found=false;
            foreach(var c in value.Grab.colliders){if(!c||!c.enabled||c.isTrigger||!c.gameObject.activeInHierarchy||c.gameObject.layer==RoomPhysicsLayers.Pending)continue;if(!found){bounds=c.bounds;found=true;}else bounds.Encapsulate(c.bounds);}
            foot=new(bounds.center.x,bounds.min.y,bounds.center.z);return found&&RoomRecipe.Finite(foot);
        }
        internal static JObject Observe(RoomEditor editor,RoomItem item){
            foreach(var value in active)if(value.editor==editor&&value.item==item){var result=value.Result;result["active"]=true;result["mode"]=value.mode;result["state"]=value.Status;result["reason"]=value.Reason.Length>256?value.Reason[..256]:value.Reason;return result;}
            var p=editor.Frame.PointToRoom(item.transform.position);return new(){["active"]=false,["mode"]="none",["state"]="inactive",["reason"]="",["position"]=new JObject{["x"]=p.x,["y"]=p.y,["z"]=p.z},["distance"]=0};
        }
        internal void Tick(){
            if(stopped||failure!=null||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            if(!owned||!editor||!editor.isActiveAndEnabled||editor.TemporarySessionId!=session||editor.RuntimeGate.Held||!editor.ViewerTrackingReady||editor.Find(target)!=item||!item||!item.isActiveAndEnabled||item.Grab.isSelected||!navigation||!editor.Viewer||!editor.Viewer.gameObject.activeInHierarchy){Fail("The walking actor, tracking or physics became unavailable");return;}
            if(editor.ObjectRevision(target)!=revision||Vector3.Distance(item.transform.localPosition,acceptedPosition)>.001f||Quaternion.Angle(item.transform.localRotation,acceptedRotation)>.1f||Vector3.Distance(item.transform.localScale,acceptedScale)>.0001f||avatar&&avatar.ModelBusy){Fail("The walking actor changed; start a new journey from its current position");return;}
            if(Time.realtimeSinceStartup-started>300){Fail("Walking exceeded its five-minute lifetime");return;}
            if(!navigation.PrepareStartingInputs(out var startingError)){
                if(navigation.StartingInputsPending){Status="preparingGround";IdlePose();return;}
                Fail(startingError);return;
            }
            if(!navigation.Ready){Fail("The selected ground or physics is unavailable");return;}
            if(gait!=null){var readiness=gait.State(out var gaitError);if(readiness==RuleActionState.Failed){Fail(gaitError);return;}if(readiness==RuleActionState.Preparing){Status="preparingAnimation";IdlePose();return;}}
            if(blocked!=null)return;
            var from=Foot;var goal=editor.Frame.PointToWorld(destination);
            if(arrived||Vector3.Distance(from,goal)<.005f){
                arrived=false;Status="planning";IdlePose();
                if(navigation.Traverse(from,from,obstacle,out _,out var stationaryError)){arrived=true;Status="arrived";IdlePose();}
                else if(!navigation.MovementInputsPending)Block(stationaryError);return;
            }
            if(route.Length==0||surfaceRevision!=navigation.SurfaceRevision){
                if(!navigation.FollowRoute(from,goal,obstacle,out route,avoidSolids:true)){
                    IdlePose();
                    Status="planning";if(!navigation.FollowRoutePending&&!navigation.PathsPending)Block(navigation.FollowRouteStatus);return;
                }
                corner=1;surfaceRevision=navigation.SurfaceRevision;
                if(route.Length<2){Block("The route has no accepted destination");return;}
            }
            corner=navigation.AdvanceCorner(from,route,corner,.005f);
            if(corner==route.Length){arrived=true;Status="arrived";IdlePose();return;}
            float dt=Mathf.Clamp(Time.deltaTime,0,.05f);if(dt==0)return;
            var next=Vector3.MoveTowards(from,route[corner],speed*dt);
            if(!navigation.Traverse(from,next,obstacle,out var floor,out var error)){
                IdlePose();
                if(!navigation.MovementInputsPending){
                    if(navigation.CanDetourTraversalBlocker){route=Array.Empty<Vector3>();navigation.CancelFollowRoute();}
                    else Block(error);
                }return;
            }
            var user=editor.Viewer.position;var a=Vector3.ProjectOnPlane(from-user,Vector3.up);var b=Vector3.ProjectOnPlane(floor-user,Vector3.up);var step=b-a;
            float closest=(a+step*(step.sqrMagnitude<1e-8f?0:Mathf.Clamp01(-Vector3.Dot(a,step)/step.sqrMagnitude))).magnitude;
            if(closest<radius+.35f&&!(b.magnitude>=a.magnitude&&closest>=a.magnitude-.001f)){Block("Walking stopped to keep clear of the user");return;}
            var direction=floor-from;direction.y=0;var rotation=direction.sqrMagnitude>1e-8f?Quaternion.RotateTowards(item.transform.rotation,Quaternion.LookRotation(direction),120*dt):item.transform.rotation;
            var position=floor-rotation*Vector3.Scale(item.transform.lossyScale,localFoot);
            if(!RoomRecipe.Finite(position)||editor.Frame.PointToRoom(position).sqrMagnitude>625){Block("Walking would leave the authored world bounds");return;}
            Status="moving";float moved=Vector3.Distance(from,floor);item.transform.SetPositionAndRotation(position,rotation);distance+=moved;
            acceptedPosition=item.transform.localPosition;acceptedRotation=item.transform.localRotation;
            if(avatar)avatar.SpatialWalk(moved/dt);else if(gait?.Apply(moved/dt,dt)==false){Fail("The assigned walking animation became unavailable");return;}
            if(Time.unscaledTime>=rememberAt){rememberAt=Time.unscaledTime+1;Remember();}
        }
        internal void Remember(){if(editor&&item&&editor.Find(target)==item&&editor.TemporarySessionId==session){editor.RememberPlacement(target);revision=editor.ObjectRevision(target);}}
        internal JObject Result {get {var p=editor&&item?editor.Frame.PointToRoom(item.transform.position):Vector3.zero;return new(){["position"]=new JObject{["x"]=p.x,["y"]=p.y,["z"]=p.z},["distance"]=distance};}}
        internal void Stop(bool preservePlacement){
            if(stopped)return;stopped=true;if(!arrived)failure??="Walking was stopped";
            if(navigation){navigation.enabled=false;ArtResources.Release(navigation.gameObject);navigation=null;}
            gait?.Dispose();gait=null;if(!owned)return;owned=false;active.Remove(this);if(avatar){avatar.SpatialWalk(0);avatar.SetEditing(false,preserveUpperBody:true);}Remember();if(rigid){rigid.StopVelocity();rigid.SetAnimationOwner(this,false);}
        }
    }
}
