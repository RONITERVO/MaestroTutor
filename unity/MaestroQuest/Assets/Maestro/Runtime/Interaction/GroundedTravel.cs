// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    /// <summary>One owned, finite journey over the shared navigation and spatial-input boundary.</summary>
    internal sealed class GroundedTravel:CapabilityOperation {
        internal const int MaximumActive=4;
        static int active;
        readonly RoomEditor editor;readonly RoomItem item;readonly string target,session;
        readonly Vector3 destination;readonly float speed;readonly Func<Collider,bool> obstacle;
        RoomNavigation navigation;MaestroAvatar avatar;RigidRoomItem rigid;
        float radius,height,started,rememberAt,distance;Vector3 localFoot,acceptedPosition,acceptedScale;Quaternion acceptedRotation;
        Vector3[] route=Array.Empty<Vector3>();int corner,lastFrame=-1,revision;uint surfaceRevision;
        bool owned,stopped,arrived;string failure;
        internal GroundedTravel(RoomEditor editor,RoomItem item,Vector3 destination,float speed){this.editor=editor;this.item=item;target=editor.Identity(item);session=editor.TemporarySessionId;this.destination=destination;this.speed=speed;obstacle=Obstacle;}
        public override float Seconds=>0;
        internal static bool CanBegin(RoomEditor editor,RoomItem item,out string error){
            error="Grounded movement needs an active target and room";
            if(!editor||!item||!item.isActiveAndEnabled||!editor.isActiveAndEnabled||editor.RuntimeGate.Held)return false;
            error="Start physics with accepted ground before walking";if(!editor.PhysicsWorld||!editor.PhysicsWorld.SimulationActive||!editor.PhysicsWorld.EnvironmentReady(item))return false;
            error="Wait for another walking action to finish (four active walks maximum)";if(active>=MaximumActive)return false;
            error="Walking needs a tracked viewer and an upright metre-scale world";
            var viewer=editor.Viewer;if(!viewer||!viewer.gameObject.activeInHierarchy||!editor.ViewerTrackingReady||!editor.Frame.Valid||Mathf.Abs(editor.Frame.MetresPerUnit-1)>.0001f||Vector3.Dot(editor.transform.up,Vector3.up)<.99999f)return false;
            error="Release this object and stop authoring before walking";
            if(item.Grab.isSelected||item.PoseLocked||editor.GetComponent<AnimationWorkshop>()?.ControlsTarget(editor.Identity(item))==true)return false;
            error="A physical anchor, terrain owner or connected assembly cannot walk as one actor";
            if(item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true||item.GetComponentsInChildren<RoomWalkableSurface>(true).Length>0||editor.PhysicsIslandMembers(item).Length>1)return false;
            error="Stop the object's current root animation or attachment before walking";
            if(item.GetComponent<RigidRoomItem>()?.AnimationOwned==true||item.GetComponent<HeldRoomProp>()?.Holding==true)return false;
            return Body(item,out _,out _,out _,out error);
        }
        static bool Body(RoomItem item,out float radius,out float height,out Vector3 foot,out string error){
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
            owned=true;active++;started=Time.realtimeSinceStartup;rememberAt=Time.unscaledTime+1;revision=editor.ObjectRevision(target);
            acceptedPosition=item.transform.localPosition;acceptedRotation=item.transform.localRotation;acceptedScale=item.transform.localScale;
            if(rigid){rigid.StopVelocity();rigid.SetAnimationOwner(this,true);}if(avatar){avatar.SetEditing(true,preserveUpperBody:true);avatar.SpatialWalk(0);}
            return true;
        }
        bool Obstacle(Collider c){
            if(c.transform.IsChildOf(item.transform))return false;
            var held=c.GetComponentInParent<HeldRoomProp>();return !held||!held.Holding||held.HolderId!=target;
        }
        void Fail(string message){failure=message??"No accepted walking route is available";if(avatar)avatar.SpatialWalk(0);}
        public override void Tick(){
            if(stopped||arrived||failure!=null||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            if(!owned||!editor||!editor.isActiveAndEnabled||editor.TemporarySessionId!=session||editor.RuntimeGate.Held||!editor.ViewerTrackingReady||editor.Find(target)!=item||!item||!item.isActiveAndEnabled||item.Grab.isSelected||!navigation||!navigation.Ready||!editor.Viewer||!editor.Viewer.gameObject.activeInHierarchy){Fail("The walking actor, tracking or physics became unavailable");return;}
            if(editor.ObjectRevision(target)!=revision||Vector3.Distance(item.transform.localPosition,acceptedPosition)>.001f||Quaternion.Angle(item.transform.localRotation,acceptedRotation)>.1f||Vector3.Distance(item.transform.localScale,acceptedScale)>.0001f||avatar&&avatar.ModelBusy){Fail("The walking actor changed; start a new journey from its current position");return;}
            if(Time.realtimeSinceStartup-started>300){Fail("Walking exceeded its five-minute lifetime");return;}
            var from=item.transform.TransformPoint(localFoot);var goal=editor.Frame.PointToWorld(destination);
            if(Vector3.Distance(from,goal)<.005f){
                if(navigation.Traverse(from,from,obstacle,out _,out var stationaryError)){arrived=true;if(avatar)avatar.SpatialWalk(0);}
                else if(!navigation.MovementInputsPending)Fail(stationaryError);return;
            }
            if(route.Length==0||surfaceRevision!=navigation.SurfaceRevision){
                if(!navigation.FollowRoute(from,goal,obstacle,out route)){
                    if(avatar)avatar.SpatialWalk(0);
                    if(!navigation.FollowRoutePending&&!navigation.PathsPending)Fail(navigation.FollowRouteStatus);return;
                }
                corner=1;surfaceRevision=navigation.SurfaceRevision;
                if(route.Length<2){Fail("The route has no accepted destination");return;}
            }
            while(corner<route.Length&&Vector3.Distance(from,route[corner])<.005f)corner++;
            if(corner==route.Length){arrived=true;if(avatar)avatar.SpatialWalk(0);return;}
            float dt=Mathf.Clamp(Time.deltaTime,0,.05f);if(dt==0)return;
            var next=Vector3.MoveTowards(from,route[corner],speed*dt);
            if(!navigation.Traverse(from,next,obstacle,out var floor,out var error)){
                if(avatar)avatar.SpatialWalk(0);
                if(!navigation.MovementInputsPending)Fail(error);return;
            }
            var user=editor.Viewer.position;var a=Vector3.ProjectOnPlane(from-user,Vector3.up);var b=Vector3.ProjectOnPlane(floor-user,Vector3.up);var step=b-a;
            float closest=(a+step*(step.sqrMagnitude<1e-8f?0:Mathf.Clamp01(-Vector3.Dot(a,step)/step.sqrMagnitude))).magnitude;
            if(closest<radius+.35f&&!(b.magnitude>=a.magnitude&&closest>=a.magnitude-.001f)){Fail("Walking stopped to keep clear of the user");return;}
            var direction=floor-from;direction.y=0;var rotation=direction.sqrMagnitude>1e-8f?Quaternion.RotateTowards(item.transform.rotation,Quaternion.LookRotation(direction),120*dt):item.transform.rotation;
            var position=floor-rotation*Vector3.Scale(item.transform.lossyScale,localFoot);
            if(!RoomRecipe.Finite(position)||editor.Frame.PointToRoom(position).sqrMagnitude>625){Fail("Walking would leave the authored world bounds");return;}
            float moved=Vector3.Distance(from,floor);item.transform.SetPositionAndRotation(position,rotation);distance+=moved;
            acceptedPosition=item.transform.localPosition;acceptedRotation=item.transform.localRotation;
            if(avatar)avatar.SpatialWalk(moved/dt);
            if(Time.unscaledTime>=rememberAt){rememberAt=Time.unscaledTime+1;Remember();}
        }
        void Remember(){if(editor&&item&&editor.Find(target)==item&&editor.TemporarySessionId==session){editor.RememberPlacement(target);revision=editor.ObjectRevision(target);}}
        public override RuleActionState State(out string error){Tick();error=failure;return error!=null?RuleActionState.Failed:arrived?RuleActionState.Ready:RuleActionState.Preparing;}
        public override bool Complete(out string error){if(State(out error)!=RuleActionState.Ready){error??="The actor has not arrived";return false;}Remember();return true;}
        public override JObject Result {get {var p=editor&&item?editor.Frame.PointToRoom(item.transform.position):Vector3.zero;return new(){["position"]=new JObject{["x"]=p.x,["y"]=p.y,["z"]=p.z},["distance"]=distance};}}
        public override void Stop(bool preservePlacement){
            if(stopped)return;stopped=true;if(!arrived)failure??="Walking was stopped";
            if(navigation){navigation.enabled=false;ArtResources.Release(navigation.gameObject);navigation=null;}
            if(!owned)return;owned=false;active--;if(avatar){avatar.SpatialWalk(0);avatar.SetEditing(false,preserveUpperBody:true);}Remember();if(rigid){rigid.StopVelocity();rigid.SetAnimationOwner(this,false);}
        }
    }
}
