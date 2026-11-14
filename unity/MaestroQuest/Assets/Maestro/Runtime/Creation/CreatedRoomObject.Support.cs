// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using UnityEngine;
using UnityEngine.AI;
namespace Maestro.Quest.Creation {
    public sealed partial class CreatedRoomObject {
        RoomSupportPiece[] supportPrimary=Array.Empty<RoomSupportPiece>();
        readonly HashSet<RoomWalkableSurface> supportMarkers=new();
        bool supportEnabled;
        string supportPublicationIssue;
        ulong supportGeneration;
        internal string SupportWorld=>collisionOwner?.World??"";
        internal string SupportTarget=>collisionOwner?.Target??"";
        internal ulong SupportGeneration=>supportGeneration;
        internal bool StaticSupportReady=>StaticSupportIssue==null;
        internal string StaticSupportIssue {
            get {
                if(!supportEnabled)return "Static support is disabled";
                if(nativeResourcesReleased||!isActiveAndEnabled)return "Static support is unavailable";
                if(supportPublicationIssue!=null)return supportPublicationIssue;
                if(pendingCollider||supportPrimary.Length==0||supportMarkers.Count!=supportPrimary.Length)return "Static support collision is not ready";
                var item=acousticItem;var body=acousticBody;
                if(!item||!body||!body.StaticSupportEligible)return "Static support is held, moving, restoring or unavailable";
                if(item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true)return "Static support needs the authored world frame";
                if(!RoomEnvironmentBinding.RestorationReady(item))return "Static support placement needs recovery";
                foreach(var source in supportPrimary){
                    var collision=source.Collision;
                    if(!RoomWalkableSurface.ValidGeometry(collision)||collision.attachedRigidbody!=item.GetComponent<Rigidbody>())return "Static support collision is unavailable";
                    if(!item.HasSolid(collision))return "Static support source registration changed";
                }
                return null;
            }
        }
        internal void ApplyStaticSupport(RoomObjectData data) {
            bool enabled=data?.staticSupport?.enabled==true&&RoomStaticSupport.Validate(data,out _);
            if(enabled==supportEnabled)return;
            supportEnabled=enabled;PublishStaticSupport();
        }
        // Called at the primary-selection seam, before height-field colliders are appended.
        void SetSupportPrimary(RoomSupportPiece[] sources) {
            supportPrimary=sources??Array.Empty<RoomSupportPiece>();PublishStaticSupport();
        }
        void PublishStaticSupport() {
            RetireStaticSupport();supportGeneration++;supportPublicationIssue=null;
            if(!supportEnabled||nativeResourcesReleased||supportPrimary.Length==0)return;
            // Never publish a partial floor from a mixed or unrecognized source.
            foreach(var source in supportPrimary)if(!RoomWalkableSurface.CompatibleGeometry(source.Collision)){
                supportPublicationIssue="Every selected static support collider must be a box or compatible mesh";return;
            }
            if(!AdmitStaticSupport(out supportPublicationIssue))return;
            foreach(var source in supportPrimary){
                var marker=source.Collision.gameObject.AddComponent<RoomWalkableSurface>();
                marker.Publish(source.Collision,this,source.Source,source.Piece,supportGeneration);supportMarkers.Add(marker);
            }
        }
        bool AdmitStaticSupport(out string reason) {
            var sources=new List<NavMeshBuildSource>(supportPrimary.Length);var bounds=RoomSpatialExtent.Empty;
            foreach(var piece in supportPrimary){
                var collision=piece.Collision;
                var source=new NavMeshBuildSource{transform=transform.worldToLocalMatrix*collision.transform.localToWorldMatrix,component=collision,area=0};
                Bounds local;
                if(collision is BoxCollider box){source.shape=NavMeshBuildSourceShape.Box;source.size=box.size;source.transform*=Matrix4x4.Translate(box.center);local=new Bounds(Vector3.zero,box.size);}
                else if(collision is MeshCollider mesh&&mesh.sharedMesh){source.shape=NavMeshBuildSourceShape.Mesh;source.sourceObject=mesh.sharedMesh;local=mesh.sharedMesh.bounds;}
                else{reason="Static support collision is unavailable";return false;}
                bounds.Include(local,source.transform);sources.Add(source);
            }
            reason="Static support source bounds are unavailable";
            if(!bounds.Known||!bounds.HasBounds)return false;
            // Original geometry has already been reserved by CollisionResources.
            // This gate checks source payload only. Each actor's aggregate bake
            // separately reserves its copies, work and retained map lifetime.
            return NavigationBuildInput.TryCapture(sources,bounds.Bounds,.2f,1.7f,out var input,out reason)&&
                NavigationResources.TryCheckSourceSet(input.Cost,out reason);
        }
        void RetireStaticSupport() {
            foreach(var marker in supportMarkers)if(marker){marker.Retire();Art.ArtResources.Release(marker);}
            supportMarkers.Clear();
        }
        // Every ownership transition invalidates existing captures, including an
        // acquire/release pair inside one frame. Returning to ready is not a Start.
        internal void InvalidateStaticSupport() {
            foreach(var marker in supportMarkers)if(marker)marker.Invalidate();
        }
        internal bool OwnsStaticSupport(RoomWalkableSurface marker,bool complete=true) {
            if(!marker||marker.SourceGeneration!=supportGeneration||!supportMarkers.Contains(marker))return false;
            if(complete)return StaticSupportReady;
            return supportEnabled&&!nativeResourcesReleased&&isActiveAndEnabled&&!pendingCollider&&supportPublicationIssue==null&&
                supportPrimary.Length>0&&supportMarkers.Count==supportPrimary.Length&&acousticItem&&acousticBody&&acousticBody.StaticSupportEligible&&
                acousticItem.HasSolid(marker.Collision)&&marker.Collision.attachedRigidbody==acousticItem.GetComponent<Rigidbody>()&&acousticItem.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame!=true&&
                RoomEnvironmentBinding.RestorationReady(acousticItem);
        }
        void OnDisable()=>InvalidateStaticSupport();
        void OnEnable()=>InvalidateStaticSupport();
    }
}
