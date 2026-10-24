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
    internal sealed class GroundedMovement:IFollowingMotion {
        internal const int MaximumActive=RoomLocomotionBudget.MaximumActive;
        static readonly List<GroundedMovement> active=new();
        readonly RoomEditor editor;readonly RoomItem item;readonly string target;
        readonly RoomLocomotionOwner owner;readonly bool ownsLifetime;
        Vector3 destination;readonly float speed;readonly string mode;readonly Func<Collider,bool> obstacle;
        RoomNavigation navigation;ImportedObjectWalk gait;MaestroAvatar avatar;
        float radius,height,distance;Vector3 localFoot;
        Vector3[] route=Array.Empty<Vector3>();int corner,lastFrame=-1;uint surfaceRevision;
        bool owned,stopped,arrived;string failure,blocked,waitingReason;
        // A transient actor is not a permanent destination failure. This is a
        // bounded retry, not a reservation, priority override or predicted path.
        internal const float ActorWaitSeconds=3,ActorRetrySeconds=.35f;
        float actorWaitStarted=-1,actorRetryAt,actorWaitDistance;
        public string Status {get;private set;}="planning";
        public string Reason=>failure??blocked??waitingReason??navigation?.FollowRouteStatus??"";
        public bool Failed=>failure!=null;
        public bool Blocked=>blocked!=null;
        public bool Arrived=>arrived;
        public Vector3 FollowPoint=>Foot;
        internal Vector3 Foot=>item?item.transform.TransformPoint(localFoot):Vector3.zero;
        internal GroundedMovement(RoomEditor editor,RoomItem item,Vector3 destination,float speed,string mode="walk",RoomLocomotionOwner owner=null){this.mode=mode;this.editor=editor;this.item=item;target=editor.Identity(item);this.owner=owner??new(editor,item);ownsLifetime=owner==null;this.destination=destination;this.speed=speed;obstacle=Obstacle;}
        internal static bool CanBegin(RoomEditor editor,RoomItem item,out string error,RoomLocomotionOwner owner=null){
            if(!RoomLocomotionOwner.CanBegin(editor,item,owner,false,out error))return false;
            if(item.TryGetComponent<RoomEnvironmentBinding>(out var environment)&&environment.RestorationHeld){error=environment.RestorationReason;return false;}
            error="Start physics with accepted ground before walking";
            if(editor.PhysicsWorld.IncludesRealRoom(item)&&!editor.PhysicsWorld.EnvironmentReady(item))return false;
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
        public bool Begin(out string error){
            error="This walking operation has already started or stopped";if(owned||stopped)return false;
            if(!CanBegin(editor,item,out error,owner)||!Body(item,out radius,out height,out var foot,out error))return false;
            avatar=item.GetComponent<MaestroAvatar>();
            localFoot=item.transform.InverseTransformPoint(foot);
            var host=new GameObject("Owned walking query");host.transform.SetParent(editor.transform,false);navigation=host.AddComponent<RoomNavigation>();navigation.Initialize(editor.PhysicsWorld);navigation.SetVirtualFrame(editor.transform);
            if(!navigation.Prepare(radius,height,out error,item)){navigation.enabled=false;ArtResources.Release(host);navigation=null;return false;}
            if(!owner.Attach(this,out error,()=>Stop(false))){navigation.enabled=false;ArtResources.Release(host);navigation=null;return false;}
            owned=true;active.Add(this);
            if(avatar)avatar.SpatialWalk(0);
            if(!avatar)gait=new ImportedObjectWalk(editor,item);
            return true;
        }
        bool Obstacle(Collider c)=>RoomWalkingObstacle.Blocks(c,item.transform,target);
        internal static bool HasWalkingOwner(RoomItem target){
            if(!target||!target.isActiveAndEnabled)return false;
            foreach(var value in active)
                if(value.item==target&&value.owned&&!value.stopped&&!value.Failed&&!value.Blocked&&!value.Arrived)return true;
            var direct=target.GetComponent<AvatarSpatialMotion>();
            return direct&&direct.Active&&direct.Mode!=AvatarSpatialMode.Look;
        }
        bool WaitForWalkingActor(){
            if(mode!="walk"||!navigation.HasWalkingActorBlocker)return false;
            float now=Time.realtimeSinceStartup;
            if(actorWaitStarted<0){actorWaitStarted=now;actorWaitDistance=distance;}
            if(now-actorWaitStarted>=ActorWaitSeconds){
                Block("Walking stopped after waiting three seconds for another walking actor to clear the route");
                return true;
            }
            waitingReason="Waiting briefly for another walking actor to clear the route";
            Status="waitingForActor";actorRetryAt=now+ActorRetrySeconds;IdlePose();
            route=Array.Empty<Vector3>();corner=0;navigation.CancelFollowRoute();return true;
        }
        void ClearActorWait(){actorWaitStarted=-1;actorRetryAt=0;waitingReason=null;}
        void IdlePose(){if(avatar)avatar.SpatialWalk(0);gait?.Apply(0,0);}
        public void Fail(string message){failure=message??"Walking became unavailable";owner.Invalidate(failure);Status="failed";IdlePose();}
        void Block(string message){blocked=message??"No accepted walking route is available";Status="blocked";waitingReason=null;IdlePose();}
        public void Retarget(Vector3 point){
            if(stopped||failure!=null)return;
            if(!RoomRecipe.Finite(point)||point.sqrMagnitude>625){Block("Following destination is outside the authored world bounds");return;}
            ClearActorWait();destination=point;route=Array.Empty<Vector3>();corner=0;arrived=false;blocked=null;Status="planning";navigation?.CancelFollowRoute();
        }
        public void KeepDistance(){if(!stopped&&!Failed&&!Blocked&&arrived)Status="keepingDistance";}
        internal static bool LeaderFoot(RoomItem value,out Vector3 foot){
            foot=default;if(!value||!value.isActiveAndEnabled)return false;
            if(value.GetComponent<MaestroAvatar>()){foot=value.transform.position;return RoomRecipe.Finite(foot);}
            Bounds bounds=default;bool found=false;
            foreach(var c in value.Grab.colliders){if(!c||!c.enabled||c.isTrigger||!c.gameObject.activeInHierarchy||c.gameObject.layer==RoomPhysicsLayers.Pending)continue;if(!found){bounds=c.bounds;found=true;}else bounds.Encapsulate(c.bounds);}
            foot=new(bounds.center.x,bounds.min.y,bounds.center.z);return found&&RoomRecipe.Finite(foot);
        }
        internal static JObject Observe(RoomEditor editor,RoomItem item){
            foreach(var value in active)if(value.editor==editor&&value.item==item){var result=value.Result;result["active"]=true;result["mode"]=value.mode;result["movement"]="walk";result["state"]=value.Status;result["reason"]=value.Reason.Length>256?value.Reason[..256]:value.Reason;return result;}
            var p=editor.Frame.PointToRoom(item.transform.position);return new(){["active"]=false,["mode"]="none",["movement"]="none",["state"]="inactive",["reason"]="",["position"]=new JObject{["x"]=p.x,["y"]=p.y,["z"]=p.z},["distance"]=0};
        }
        public void Tick(){
            if(stopped||failure!=null||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            if(!owned||!navigation){Fail("The walking actor or physics became unavailable");return;}
            if(!owner.Validate(this,out var ownershipError)){Fail(ownershipError);return;}
            if(!navigation.PrepareStartingInputs(out var startingError)){
                if(navigation.StartingInputsPending){arrived=false;Status="preparingGround";IdlePose();return;}
                Fail(startingError);return;
            }
            if(!navigation.Ready){Fail("The selected ground or physics is unavailable");return;}
            if(gait!=null){var readiness=gait.State(out var gaitError);if(readiness==RuleActionState.Failed){Fail(gaitError);return;}if(readiness==RuleActionState.Preparing){arrived=false;Status="preparingAnimation";IdlePose();return;}}
            if(blocked!=null)return;
            if(waitingReason!=null){
                if(Time.realtimeSinceStartup<actorRetryAt){IdlePose();return;}
                waitingReason=null;
            }
            var from=Foot;var goal=editor.Frame.PointToWorld(destination);
            if(arrived||Vector3.Distance(from,goal)<.005f){
                arrived=false;Status="planning";IdlePose();
                if(navigation.Traverse(from,from,obstacle,out _,out var stationaryError)){arrived=true;Status="arrived";IdlePose();}
                else if(!navigation.MovementInputsPending)Block(stationaryError);return;
            }
            if(route.Length==0||surfaceRevision!=navigation.SurfaceRevision){
                if(!navigation.FollowRoute(from,goal,obstacle,out route,avoidSolids:true)){
                    IdlePose();
                    Status="planning";
                    if(!navigation.FollowRoutePending&&!navigation.PathsPending&&!WaitForWalkingActor())Block(navigation.FollowRouteStatus);
                    return;
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
            owner.AcceptPose();
            // Retry/detour work alone cannot replenish the wait. Only meaningful
            // accepted movement earns a new allowance for a later obstruction.
            if(actorWaitStarted>=0&&distance-actorWaitDistance>=.1f)ClearActorWait();
            if(avatar)avatar.SpatialWalk(moved/dt);else if(gait?.Apply(moved/dt,dt)==false){Fail("The assigned walking animation became unavailable");return;}
            owner.Checkpoint();
        }
        public bool Remember(out string error)=>Remember(out error,false);
        internal bool Remember(out string error,bool requireArrival){
            if(!owner.Validate(this,out error)){Fail(error);return false;}
            if(requireArrival){
                // Re-prove the accepted stance without taking a second movement
                // step. The floor or water may have changed since State().
                if(!navigation.PrepareStartingInputs(out error)||!navigation.Ready){
                    error??="The selected ground is unavailable";arrived=false;return false;
                }
                Physics.SyncTransforms();
                var foot=Foot;
                if(!navigation.Traverse(foot,foot,obstacle,out var floor,out error)){
                    arrived=false;if(!navigation.MovementInputsPending)Block(error);
                    return false;
                }
                if(Vector3.Distance(foot,floor)>=.005f){
                    error="The ground beneath the arrival position changed";arrived=false;Block(error);return false;
                }
            }
            if(owner.TryRemember(this,out error))return true;
            Fail(error);return false;
        }
        public JObject Result {get {var p=editor&&item?editor.Frame.PointToRoom(item.transform.position):Vector3.zero;return new(){["position"]=new JObject{["x"]=p.x,["y"]=p.y,["z"]=p.z},["distance"]=distance};}}
        public void Stop(bool preservePlacement){
            if(stopped)return;stopped=true;if(!arrived)failure??="Walking was stopped";
            if(navigation){navigation.enabled=false;ArtResources.Release(navigation.gameObject);navigation=null;}
            gait?.Dispose();gait=null;if(owned){owned=false;active.Remove(this);if(avatar)avatar.SpatialWalk(0);owner.Detach(this);}if(ownsLifetime)owner.Dispose();
        }
    }
}
