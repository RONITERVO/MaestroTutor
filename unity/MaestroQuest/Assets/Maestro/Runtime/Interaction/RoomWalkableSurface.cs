// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Interaction {
    /// <summary>One exact accepted provider handle. Terrain/import adapters keep
    /// their legacy eligibility; ordinary primary support has an explicit owner.</summary>
    public sealed class RoomWalkableSurface:MonoBehaviour {
        internal Collider Collision {get;private set;}
        internal uint Revision {get;private set;}
        internal static ulong ChangeRevision {get;private set;}
        internal bool Ordinary {get;private set;}
        internal Creation.CreatedRoomObject OrdinaryOwner {get;private set;}
        internal string SourceId {get;private set;}
        internal int Piece {get;private set;}
        internal ulong SourceGeneration {get;private set;}
        internal void Publish(Collider collision){Collision=collision;Ordinary=false;OrdinaryOwner=null;SourceId="";Piece=0;SourceGeneration=0;Invalidate();}
        internal void Publish(Collider collision,Creation.CreatedRoomObject owner,string source,int piece,ulong generation){
            Collision=collision;Ordinary=true;OrdinaryOwner=owner;SourceId=source;Piece=piece;SourceGeneration=generation;Invalidate();
        }
        internal void Invalidate(){Revision++;ChangeRevision++;}
        internal void Retire(){Collision=null;Invalidate();enabled=false;}
        void OnEnable()=>Invalidate();
        void OnDisable()=>Invalidate();
        internal static bool CompatibleGeometry(Collider collision)=>collision&&
            (collision is BoxCollider box&&box.size.x>0&&box.size.y>0&&box.size.z>0||
             collision is MeshCollider mesh&&mesh.sharedMesh&&mesh.sharedMesh.vertexCount>0);
        internal static bool ValidGeometry(Collider collision)=>CompatibleGeometry(collision)&&!RoomItem.IsPointerOnly(collision)&&
            collision.enabled&&!collision.isTrigger&&collision.gameObject.activeInHierarchy;
        internal bool Available=>isActiveAndEnabled&&ValidGeometry(Collision)&&
            (!Collision.attachedRigidbody||Collision.attachedRigidbody.isKinematic)&&
            GetComponentInParent<RoomItem>(true)?.Grab?.isSelected!=true&&
            (!Ordinary||OrdinaryOwner&&OrdinaryOwner.OwnsStaticSupport(this));
        internal bool CapturedAvailable=>isActiveAndEnabled&&
            (Ordinary?OrdinaryOwner&&OrdinaryOwner.OwnsStaticSupport(this,false):Available);
        internal static RoomWalkableSurface Find(Collider collision){
            if(!collision)return null;
            var first=collision.GetComponent<RoomWalkableSurface>();
            if(!first)return null;
            if(first.Collision==collision&&first.Available)return first;
            foreach(var surface in collision.GetComponents<RoomWalkableSurface>())
                if(surface.Collision==collision&&surface.Available)return surface;
            return null;
        }
    }
    // A capture never substitutes a newer registration on the same collider.
    internal readonly struct RoomGroundHandle {
        internal readonly Collider Collision;
        readonly RoomWalkableSurface provider;
        readonly uint revision;
        readonly bool scan;
        internal RoomGroundHandle(Collider collision,RoomWalkableSurface surface,bool scanned=false){
            Collision=collision;provider=surface;revision=surface?surface.Revision:0;scan=scanned;
        }
        internal bool Valid=>RoomWalkableSurface.ValidGeometry(Collision)&&
            (!Collision.attachedRigidbody||Collision.attachedRigidbody.isKinematic)&&
            (provider?provider.Collision==Collision&&provider.Revision==revision&&provider.CapturedAvailable:
                scan&&Collision.gameObject.layer==RoomPhysicsLayers.Scanned);
    }
}
