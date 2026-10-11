// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>A rotation-independent enclosing volume, shared by throws and catches.</summary>
    public readonly struct RoomCollisionVolume
    {
        public readonly Vector3 LocalCentre; public readonly float Radius;
        RoomCollisionVolume(Vector3 centre,float radius){LocalCentre=centre;Radius=radius;}
        public Vector3 Centre(RoomItem item)=>item.transform.TransformPoint(LocalCentre);
        public Vector3 Centre(RoomItem item,Rigidbody body)=>body.position+body.rotation*Vector3.Scale(LocalCentre,item.transform.lossyScale);
        public static bool Read(RoomItem item,float maximumRadius,out RoomCollisionVolume value,out string error){
            value=default;error="The object has no ready collision volume";if(!item||!item.Grab)return false;
            Physics.SyncTransforms();bool found=false;Bounds bounds=default;SphereCollider sphere=null;int count=0;
            foreach(var collider in item.Grab.colliders)if(collider&&collider.enabled&&collider.gameObject.activeInHierarchy&&!collider.isTrigger){if(!found)bounds=collider.bounds;else bounds.Encapsulate(collider.bounds);found=true;count++;sphere=collider as SphereCollider;}
            if(!found)return false;
            float radius=count==1&&sphere?sphere.radius*Mathf.Max(Mathf.Abs(sphere.transform.lossyScale.x),Mathf.Abs(sphere.transform.lossyScale.y),Mathf.Abs(sphere.transform.lossyScale.z)):bounds.extents.magnitude;
            if(!float.IsFinite(bounds.center.sqrMagnitude)||!float.IsFinite(radius)||radius<=0||radius>maximumRadius){error="The collision volume exceeds this action's supported size";return false;}
            value=new RoomCollisionVolume(item.transform.InverseTransformPoint(bounds.center),radius);error=null;return true;
        }
    }
}
