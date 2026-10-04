// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>A bounded free-flight estimate, followed by ordinary room physics. Never a homing controller.</summary>
    public sealed class RoomBallistics
    {
        public Vector3 Origin {get;private set;}
        public Vector3 Destination {get;private set;}
        public Vector3 Velocity {get;private set;}
        public float Seconds {get;private set;}
        public float Radius {get;private set;}
        public float Speed=>Velocity.magnitude;
        readonly Collider[] overlaps=new Collider[48];
        readonly RaycastHit[] hits=new RaycastHit[48];
        const int Mask=(1<<RoomPhysicsLayers.Scanned)|(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment)|(1<<RoomPhysicsLayers.Controller);
        static bool Finite(Vector3 v)=>float.IsFinite(v.sqrMagnitude);
        static bool Obstacle(Collider c,RoomItem item)=>c&&c.enabled&&!c.isTrigger&&!c.transform.IsChildOf(item.transform);
        static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b) {var d=b-a;return Vector3.Distance(p,a+d*Mathf.Clamp01(d.sqrMagnitude>0?Vector3.Dot(p-a,d)/d.sqrMagnitude:0));}
        public bool Prepare(RoomItem item,RoomPhysicsWorld world,Transform viewer,Vector3 destination,float seconds,float maxSpeed,out string error)
        {
            error=null;
            if(!item||!world||!viewer||!viewer.gameObject.activeInHierarchy||!Finite(viewer.position)){error="Return to the active room view before aiming a throw";return false;}
            var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();
            if(!item.isActiveAndEnabled||!rigid||!rigid.isActiveAndEnabled||!body){error="This object has no rigid-body physics";return false;}
            if(!rigid.CanReceivePhysicsAction(out error))return false;
            float dt=Time.fixedDeltaTime;
            if(!Finite(destination)||!Finite(Physics.gravity)||!float.IsFinite(seconds)||seconds<.2f||seconds>2||!float.IsFinite(maxSpeed)||maxSpeed<.1f||maxSpeed>8||dt<.005f||dt>.05f){error="The throw or physics time step is outside supported limits";return false;}
            if(!RoomCollisionVolume.Read(item,1,out var volume,out error))return false;
            Origin=volume.Centre(item);Destination=destination;Radius=volume.Radius;
            if(!world.CanSimulate(Origin)){error="The planned throw starts outside the aligned room";return false;}
            int steps=Mathf.CeilToInt(seconds/dt);Seconds=steps*dt;
            // Semi-implicit fixed-step estimate with the existing room body's small linear damping.
            // Contacts and moving targets are deliberately not predicted.
            float damping=Mathf.Clamp01(1-body.linearDamping*dt),coefficient=0,velocityCoefficient=1;
            Vector3 gravityPosition=Vector3.zero,gravityVelocity=Vector3.zero;
            for(int i=0;i<steps;i++){velocityCoefficient*=damping;coefficient+=velocityCoefficient*dt;gravityVelocity=(gravityVelocity+Physics.gravity*dt)*damping;gravityPosition+=gravityVelocity*dt;}
            Velocity=(destination-Origin-gravityPosition)/coefficient;
            if(!Finite(Velocity)||Speed>maxSpeed){error="The aimed throw exceeds its speed limit; move the target or change flight time";return false;}
            var point=Origin;var velocity=Velocity;var previous=Origin;int stride=Mathf.Max(1,Mathf.CeilToInt(steps/64f));
            if(!ClearPoint(item,point,Radius,out error))return false;
            for(int i=1;i<=steps;i++){
                velocity=(velocity+Physics.gravity*dt)*damping;point+=velocity*dt;
                if(!world.CanSimulate(point)){error="The planned throw leaves the aligned room";return false;}
                if(SegmentDistance(viewer.position,previous,point)<Radius+.35f){error="The planned throw passes too close to the user's head";return false;}
                if(i%stride!=0&&i!=steps)continue;
                var delta=point-previous;
                // A small curve envelope covers the gap between bounded sweep samples.
                float pad=Physics.gravity.magnitude*Mathf.Pow(stride*dt,2)/8;
                int n=delta.sqrMagnitude<.00000001f?0:Physics.SphereCastNonAlloc(previous,Radius+pad,delta.normalized,hits,delta.magnitude,Mask,QueryTriggerInteraction.Ignore);
                if(n==hits.Length){error="Too many nearby colliders to verify the planned throw";return false;}
                for(int j=0;j<n;j++)if(Obstacle(hits[j].collider,item)){error="The planned throw is blocked by a room surface or object";return false;}
                if(!ClearPoint(item,point,Radius,out error))return false;
                previous=point;
            }
            return true;
        }
        bool ClearPoint(RoomItem item,Vector3 point,float radius,out string error){
            error=null;int count=Physics.OverlapSphereNonAlloc(point,radius,overlaps,Mask,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length){error="Too many nearby colliders to verify the planned throw";return false;}
            for(int i=0;i<count;i++)if(Obstacle(overlaps[i],item)&&(overlaps[i] is MeshCollider mesh&&!mesh.convex||Vector3.Distance(overlaps[i].ClosestPoint(point),point)<radius-.001f)){error="The planned throw is blocked by a room surface or object";return false;}
            return true;
        }
    }
}
