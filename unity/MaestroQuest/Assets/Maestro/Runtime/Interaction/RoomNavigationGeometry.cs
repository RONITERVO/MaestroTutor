// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Maestro.Quest.Interaction
{
    /// <summary>Reusable collision-source snapshots scoped to one room owner.
    /// The signature includes accepted mesh revisions and transforms, so a
    /// mutable mesh or world relocation cannot keep an old navigation bake.</summary>
    internal sealed class RoomNavigationGeometry
    {
        internal readonly struct Entry
        {
            readonly Collider collider;
            readonly uint revision;
            internal readonly NavMeshBuildSource Source;
            internal Entry(Collider value, uint version, NavMeshBuildSource source)
            { collider=value; revision=version; Source=source; }
            internal bool Same(Entry other) => collider==other.collider && revision==other.revision &&
                SameMatrix(Source.transform,other.Source.transform) && Source.shape==other.Source.shape &&
                Source.size.Equals(other.Source.size) && Source.sourceObject==other.Source.sourceObject;
            static bool SameMatrix(Matrix4x4 a,Matrix4x4 b) {
                for(int i=0;i<16;i++)if(Mathf.Abs(a[i]-b[i])>.00001f)return false;return true;
            }
        }
        readonly List<(Creation.RoomEditor owner,string target)> dependencies=new();
        readonly List<Collider> colliders=new();
        readonly List<Entry> entries=new();
        readonly List<NavMeshBuildSource> sources=new();
        internal RoomGroundQuery Ground {get;}=new();
        internal Bounds Bounds { get; private set; }
        internal Vector3 Position { get; private set; }
        internal Quaternion Rotation { get; private set; }
        internal void Clear()
        {
            dependencies.Clear();colliders.Clear();entries.Clear();sources.Clear();Ground.Clear();Bounds=default;Position=default;Rotation=Quaternion.identity;
        }
        internal bool Capture(Transform owner,bool includeScan=true,Transform coordinates=null)
        {
            Clear();
            if(!owner)return false;
            if(!coordinates)coordinates=owner;
            var frame=new Creation.RoomFrame(coordinates);
            // Scaling terrain is supported through its collider transform. Changing
            // the world's metre or gravity convention needs a separate explicit policy.
            if(!frame.Valid||Mathf.Abs(frame.MetresPerUnit-1)>.00001f||Vector3.Dot(coordinates.up,Vector3.up)<.99999f)return false;
            Position=coordinates.position;Rotation=coordinates.rotation;
            owner.GetComponentsInChildren(false,colliders);
            foreach(var collider in colliders) {
                if(!RoomGroundQuery.Accepted(collider,includeScan,out var surface))continue;
                bool authored=surface;
                var source=new NavMeshBuildSource { transform=coordinates.worldToLocalMatrix*collider.transform.localToWorldMatrix,component=collider,area=0 };
                Bounds localBounds;
                if(collider is MeshCollider mesh&&mesh.sharedMesh) { source.shape=NavMeshBuildSourceShape.Mesh;source.sourceObject=mesh.sharedMesh;localBounds=mesh.sharedMesh.bounds; }
                else if(collider is BoxCollider box) { source.shape=NavMeshBuildSourceShape.Box;source.size=box.size;source.transform*=Matrix4x4.Translate(box.center);localBounds=new Bounds(Vector3.zero,box.size); }
                else continue;
                var x=source.transform.MultiplyVector(Vector3.right*localBounds.extents.x);
                var y=source.transform.MultiplyVector(Vector3.up*localBounds.extents.y);
                var z=source.transform.MultiplyVector(Vector3.forward*localBounds.extents.z);
                var boundsInFrame=new Bounds(source.transform.MultiplyPoint3x4(localBounds.center),2*new Vector3(
                    Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z)));
                if(sources.Count==0)Bounds=boundsInFrame;else {var bounds=Bounds;bounds.Encapsulate(boundsInFrame);Bounds=bounds;}
                Ground.Add(collider);sources.Add(source);entries.Add(new Entry(collider,authored?surface.Revision:0,source));
                // Capture identity with the geometry. A destroyed/replaced collider
                // cannot silently release an existing map's canonical dependency.
                var item=collider.GetComponentInParent<RoomItem>(true);
                var authority=item?item.GetComponentInParent<Creation.RoomEditor>():null;
                var id=authority?authority.Identity(item):null;
                if(id!=null)dependencies.Add((authority,id));
            }
            return entries.Count>0;
        }
        internal bool Same(RoomNavigationGeometry other)
        {
            if(entries.Count!=other.entries.Count||dependencies.Count!=other.dependencies.Count)return false;
            for(int i=0;i<dependencies.Count;i++)if(dependencies[i]!=other.dependencies[i])return false;
            for(int i=0;i<entries.Count;i++)if(!entries[i].Same(other.entries[i]))return false;
            return true;
        }
        internal void Retain(Creation.RoomEditor editor,Creation.RoomRetentionGraph graph)
        {
            foreach(var dependency in dependencies)if(dependency.owner==editor)
                graph.Retain(dependency.target,Creation.RoomRetentionReason.Navigation);
        }
        internal List<NavMeshBuildSource> BuildSources => sources;
    }
}
