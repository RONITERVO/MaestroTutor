// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>Queries accepted collision geometry, never sculpt previews or an
    /// invented level plane. Navigation and direct traversal share this policy.</summary>
    internal sealed class RoomGroundQuery
    {
        internal const float MaximumSlope=25,MaximumStep=.10f,Skin=.04f;
        static readonly float MinimumNormal=Mathf.Cos(MaximumSlope*Mathf.Deg2Rad);
        static readonly float Gradient=Mathf.Tan(MaximumSlope*Mathf.Deg2Rad);
        readonly List<Collider> discovered=new(),surfaces=new();
        internal void Clear()=>surfaces.Clear();
        internal void Add(Collider collision)=>surfaces.Add(collision);
        internal bool Contains(Collider collision)=>surfaces.Contains(collision);
        internal static bool Accepted(Collider collision,bool includeScan,out RoomWalkableSurface surface)
        {
            surface=null;
            if(!collision||!collision.enabled||collision.isTrigger||!collision.gameObject.activeInHierarchy||
                collision.attachedRigidbody&&!collision.attachedRigidbody.isKinematic)return false;
            if(collision is BoxCollider box ? box.size.x<=0||box.size.y<=0||box.size.z<=0 :
                collision is not MeshCollider mesh||!mesh.sharedMesh||mesh.sharedMesh.vertexCount==0)return false;
            surface=collision.GetComponent<RoomWalkableSurface>();
            bool authored=surface&&surface.Available&&surface.Collision==collision;
            if(authored&&collision.attachedRigidbody?.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true)return false;
            if(!authored)surface=null;
            return authored||includeScan&&collision.gameObject.layer==RoomPhysicsLayers.Scanned;
        }
        internal void Capture(Transform owner,bool includeScan=false)
        {
            surfaces.Clear();discovered.Clear();if(!owner)return;
            owner.GetComponentsInChildren(false,discovered);
            foreach(var collision in discovered)if(Accepted(collision,includeScan,out _))surfaces.Add(collision);
        }
        internal bool Sample(Vector3 expected,float above,float below,out RaycastHit result)
        {
            result=default;
            if(!Creation.RoomRecipe.Finite(expected)||!float.IsFinite(above)||!float.IsFinite(below)||above<0||below<0)return false;
            var ray=new Ray(expected+Vector3.up*(above+.002f),Vector3.down);float distance=above+below+.004f;bool found=false;
            foreach(var surface in surfaces){
                if(!surface||!surface.enabled||!surface.gameObject.activeInHierarchy)continue;
                if(surface.Raycast(ray,out var hit,distance)&&(!found||hit.distance<result.distance)){result=hit;found=true;}
            }
            // A steep surface above a floor is an obstruction, not permission to
            // select the lower floor and walk through the visible geometry.
            return found&&result.normal.y>=MinimumNormal;
        }
        internal bool Support(Vector3 expected,float radius,float above,float below,out Vector3 point,out Vector3 normal,out string error)
        {
            point=normal=default;error="Movement needs accepted ground within the supported slope, step and footprint";
            if(!float.IsFinite(radius)||radius<=0||!Sample(expected,above,below,out var centre))return false;
            float band=radius*Gradient+MaximumStep+.005f;
            for(int sample=0;sample<8;sample++){
                float angle=sample*Mathf.PI/4;
                var offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                if(!Sample(centre.point+offset,band,band,out var edge))return false;
                if(Mathf.Abs(edge.point.y-centre.point.y)>band)return false;
            }
            point=centre.point;normal=centre.normal;error=null;return true;
        }
    }
}
