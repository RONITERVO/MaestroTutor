// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // A fixed 4x4x4 solid-occupancy approximation, cached until accepted geometry/
    // ownership changes. Union membership avoids counting overlapping colliders twice.
    internal sealed class MediumDisplacement {
        internal const int Samples=64,MaximumColliders=64;
        internal readonly Vector3[] Points=new Vector3[Samples];
        internal int Count;internal float CellVolume;internal string Reason="";
        internal void Capture(RoomItem item){
            Count=0;CellVolume=0;Reason="No supported solid collider displacement";
            var colliders=item.Grab.colliders;if(colliders.Count==0||colliders.Count>MaximumColliders)return;
            Bounds bounds=default;bool first=true;
            foreach(var c in colliders){
                if(!Solid(c,item))continue;
                if(!LocalBounds(c,out var local)){Reason="Displacement requires box, sphere, capsule or convex mesh colliders";return;}
                var matrix=item.transform.worldToLocalMatrix*c.transform.localToWorldMatrix;
                for(int i=0;i<8;i++){var p=matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
            }
            if(first)return;var size=bounds.size;CellVolume=size.x*size.y*size.z/Samples;
            if(!float.IsFinite(CellVolume)||CellVolume<=1e-12f){CellVolume=0;return;}
            for(int i=0;i<Samples;i++){
                var p=bounds.min+Vector3.Scale(size,new Vector3(((i%4)+.5f)/4,(((i/4)%4)+.5f)/4,((i/16)+.5f)/4));var world=item.transform.TransformPoint(p);
                foreach(var c in colliders)if(Solid(c,item)&&(c.ClosestPoint(world)-world).sqrMagnitude<1e-12f){Points[Count++]=p;break;}
            }
            if(Count>0)Reason="";
        }
        static bool Solid(Collider c,RoomItem item)=>c&&c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&c.attachedRigidbody==item.GetComponent<Rigidbody>();
        static bool LocalBounds(Collider c,out Bounds b){
            b=default;
            if(c is BoxCollider box)b=new Bounds(box.center,box.size);
            else if(c is SphereCollider sphere)b=new Bounds(sphere.center,Vector3.one*(sphere.radius*2));
            else if(c is CapsuleCollider capsule){var size=Vector3.one*(capsule.radius*2);size[capsule.direction]=Mathf.Max(capsule.height,capsule.radius*2);b=new Bounds(capsule.center,size);}
            else if(c is MeshCollider mesh&&mesh.convex&&mesh.sharedMesh)b=mesh.sharedMesh.bounds;
            else return false;
            return true;
        }
    }
}
