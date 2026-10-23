// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    internal sealed class RoomShoreClearance:IDisposable {
        readonly RoomEditor editor;readonly RoomItem actor;readonly string id,habitat;readonly Func<bool> owned;
        readonly RoomUprightBody body;readonly float immersion;
        readonly RoomShoreMotor motor=new();readonly RoomGroundMotor stance=new();
        RoomSpatialInputs inputs;RoomNavigationGeometry accepted=new(),candidate=new();
        readonly Func<Collider,bool> obstacle;readonly Func<Vector3,bool> permitted;
        Matrix4x4 frame;bool frameKnown;
        internal bool Pending=>inputs.Pending;
        internal int Generation {get;private set;}
        internal float FloatFoot {get;private set;}
        internal RoomShoreClearance(RoomEditor editor,RoomItem actor,string habitat,RoomUprightBody body,float immersion,Func<bool> owned){
            this.editor=editor;this.actor=actor;this.habitat=habitat;this.body=body;this.immersion=immersion;this.owned=owned;
            id=editor.Identity(actor);inputs=NewInputs();obstacle=c=>RoomWalkingObstacle.Blocks(c,actor.transform,id);permitted=Permitted;
        }
        RoomSpatialInputs NewInputs()=>new(editor,id,owned,false,purpose:"Shore traversal",followPhysicsPlacements:true);
        internal bool Prepare(Vector3 foot,Vector3 goal,out string error,float verticalBand=0){
            if(inputs.SourceChanged){inputs.Dispose();inputs=NewInputs();Generation++;}
            var extent=body.SweepBounds(foot,goal);extent.Expand(new Vector3(4,.4f,4));
            var ground=extent;ground.Expand(new Vector3(body.Radius*2,.5f+2*verticalBand,body.Radius*2));
            var local=editor.SpatialInputRoomBounds(ground);
            var targets=editor.SpatialMovementTargets(editor.SpatialInputRoomBounds(extent),local)
                .Concat(editor.Liquids.NavigationCavities(extent)).Append(habitat).Where(x=>x!=id).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            if(!targets.SequenceEqual(inputs.Targets))Generation++;
            if(!inputs.Require(targets,(string[] ids,out string issue)=>editor.SpatialMovementReady(ids,local,out issue),out error))return false;
            if(!editor.Liquids.ShoreSurface(actor,habitat,foot,body,immersion,out float height,out error))return false;
            FloatFoot=height;
            var world=editor.PhysicsWorld;
            error="Shore traversal needs the actor's accepted ground and active physics";
            if(!owned()||!world||!world.SimulationActive||!world.EnvironmentReady(actor))return false;
            Physics.SyncTransforms();
            if(!candidate.Capture(world.transform,world.IncludesRealRoom(actor),editor.transform,obstacle))return false;
            var current=editor.transform.localToWorldMatrix;
            if(!frameKnown||!frame.Equals(current)||!candidate.Same(accepted)){Generation++;frame=current;frameKnown=true;}
            (accepted,candidate)=(candidate,accepted);candidate.Clear();error=null;return true;
        }
        int Mask=>editor.PhysicsWorld.CollisionMask((1<<RoomPhysicsLayers.Scanned)|(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment),actor);
        bool Permitted(Vector3 foot){
            if(!owned()||!editor||!editor.Frame.Valid||!RoomViewpoint.ValidPosition(editor.Frame.PointToRoom(foot)))return false;
            var world=editor.PhysicsWorld;if(!world||!world.SimulationActive||!world.EnvironmentReady(actor))return false;
            if(!world.IncludesRealRoom(actor))return world.ContainsSimulation(foot+Vector3.up*.1f,actor);
            var bounds=body.Bounds(foot);
            for(int i=0;i<8;i++){
                var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                if(!world.ContainsSimulation(point,actor))return false;
            }
            return true;
        }
        bool Admit(Vector3 from,Vector3 to,bool floating,out string error)=>editor.Liquids.ShoreAdmission(actor,habitat,from,to,body,floating,out error);
        internal bool FollowGoal(Vector3 hint,float band,out Vector3 goal,out bool surface,out string error){
            goal=hint;surface=false;
            bool supported=accepted.Ground.Support(hint,body.Radius,band,band,out var floor,out _,out _);
            if(supported&&floor.y>=FloatFoot-.001f){goal=floor;error=null;return true;}
            var afloat=new Vector3(hint.x,FloatFoot,hint.z);
            if(Admit(afloat,afloat,true,out _)){goal=afloat;surface=true;error=null;return true;}
            if(supported&&Admit(floor,floor,false,out _)){goal=floor;error=null;return true;}
            error="The following position has no supported shore or water in the selected habitat";return false;
        }
        internal bool Classify(Vector3 foot,out bool floating,out string error){
            floating=false;error="The starting capsule is outside the actor's accepted world";
            if(!Permitted(foot))return false;
            if(stance.Travel(accepted.Ground,foot,Vector3.zero,body.Radius,body.Height,Mask,obstacle,permitted,out _,out _)&&Admit(foot,foot,false,out error))return true;
            floating=true;
            if(!Admit(foot,foot,true,out error))return false;
            error="The starting shore capsule is blocked";
            if(!stance.ClearExact(foot,foot,body.Radius,body.Height,Mask,obstacle))return false;
            error=null;return true;
        }
        internal bool Step(Vector3 from,Vector3 planar,float distance,bool floating,out RoomShoreMotor.Step next,out string error){
            next=default;error="The shore capsule is outside the actor's current world";
            if(!Permitted(from))return false;
            if(!motor.Travel(accepted.Ground,from,planar,distance,body,FloatFoot,floating,Mask,obstacle,permitted,Admit,out next,out error))return false;
            var user=editor.Viewer;
            error="Shore traversal needs the tracked user";
            if(!user||!user.gameObject.activeInHierarchy||!editor.ViewerTrackingReady)return false;
            var a=Vector3.ProjectOnPlane(from-user.position,Vector3.up);var b=Vector3.ProjectOnPlane(next.Foot-user.position,Vector3.up);var change=b-a;
            float closest=(a+change*(change.sqrMagnitude<1e-8f?0:Mathf.Clamp01(-Vector3.Dot(a,change)/change.sqrMagnitude))).magnitude;
            error="Shore traversal stopped to keep clear of the user";
            if(closest<body.Radius+.35f&&!(b.magnitude>=a.magnitude&&closest>=a.magnitude-.001f))return false;
            error=null;return true;
        }
        public void Dispose(){inputs.Dispose();accepted.Clear();candidate.Clear();motor.Clear();stance.Clear();}
    }
}
