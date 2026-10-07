// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>Bounded, fail-closed queries. The catching pair is excluded; scanned surfaces and hands are not.</summary>
    public sealed class CatchClearance
    {
        readonly Collider[] overlaps=new Collider[48]; readonly RaycastHit[] hits=new RaycastHit[48];
        const int Mask=(1<<RoomPhysicsLayers.Scanned)|(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment)|(1<<RoomPhysicsLayers.Controller);
        bool Obstacle(Collider c,RoomItem ball,RoomItem holder)=>c&&c.enabled&&!c.isTrigger&&!c.transform.IsChildOf(ball.transform)&&!c.transform.IsChildOf(holder.transform);
        public bool Segment(RoomItem ball,RoomItem holder,Transform viewer,Vector3 from,Vector3 to,float radius,RoomPhysicsWorld world=null){
            int mask=world?world.CollisionMask(Mask):Mask;
            if(!viewer||!viewer.gameObject.activeInHierarchy||!float.IsFinite(viewer.position.sqrMagnitude)||!float.IsFinite(from.sqrMagnitude)||!float.IsFinite(to.sqrMagnitude)||CatchGeometry.SegmentDistance(viewer.position,from,to)<.35f+radius)return false;
            Physics.SyncTransforms();var delta=to-from;
            if(delta.sqrMagnitude>1e-8f){int n=Physics.SphereCastNonAlloc(from,radius,delta.normalized,hits,delta.magnitude,mask,QueryTriggerInteraction.Ignore);if(n==hits.Length)return false;for(int i=0;i<n;i++)if(Obstacle(hits[i].collider,ball,holder))return false;}
            int found=Physics.OverlapCapsuleNonAlloc(from,to,radius,overlaps,mask,QueryTriggerInteraction.Ignore);if(found==overlaps.Length)return false;
            // Conservative for non-convex room meshes; no unsupported ClosestPoint calls.
            for(int i=0;i<found;i++)if(Obstacle(overlaps[i],ball,holder))return false;return true;
        }
    }
}
