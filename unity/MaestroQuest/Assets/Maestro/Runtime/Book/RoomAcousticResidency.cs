// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;

namespace Maestro.Quest.Book
{
    public sealed partial class RoomAcoustics
    {
        // Separate bounded CPU ownership survives an audio-device reset, which
        // releases native handles. These are derived mesh inputs, not authored data.
        int retainedSurfaces, retainedVertices, retainedTriangles;
        internal int RetainedGeometryCount => retainedSurfaces;
        internal int ResidencyEpoch {get;private set;}
        internal sealed class RetainedInput : IAcousticGeometry
        {
            internal Transform Frame;
            internal Matrix4x4 Placement, InitialPlacement, InitialFrame, InitialWorld, Local;
            internal float[] Vertices; internal int[] Indices;
            internal bool Current = true, Ended;
            public bool Active => !Ended && Frame && Frame.gameObject.activeInHierarchy;
            public bool HasGeometry => true;
            public bool Scanned => false;
            public bool ReflectionBoundary { get; internal set; }
            public int Revision { get; internal set; }
            public Matrix4x4 Pose => Placement.Equals(InitialPlacement) && Frame.localToWorldMatrix.Equals(InitialFrame)
                ? InitialWorld : Frame.localToWorldMatrix * Placement * Local;
            public string Issue { get; set; }
            public bool Read(out float[] vertices, out int[] indices, out string issue)
            {
                vertices = Vertices; indices = Indices;
                issue = Current ? null : "Acoustic definition changed; native geometry preparation is required";
                return Current && vertices != null && indices != null;
            }
        }
        internal sealed class AcousticResidency : IDisposable
        {
            internal RoomAcoustics Owner;
            internal AcousticSurface[] Surfaces;
            internal AcousticGeometryBinding[] SourceBindings;
            internal GameObject SourceRoot; internal Transform SourceFrame;
            internal Matrix4x4 RootPose, FramePose;
            internal RetainedInput[] Inputs;
            internal Entry[] Entries;
            internal bool Committed, Ended, DefinitionCurrent = true;
            internal bool Current => !Ended && DefinitionCurrent;
            // Definition readiness belongs to the shared CPU publication. An
            // SDK may be unavailable, over capacity or finishing old work while
            // another renderer can already consume these registered inputs.
            internal bool Published
            {
                get
                {
                    if (!Current || !Committed || !Owner) return false;
                    for (int i = 0; i < Inputs.Length; i++)
                    {
                        var input = Inputs[i];
                        if (!input.Active || !input.Current || input.Vertices == null || input.Indices == null
                            || !Owner.entries.TryGetValue(input, out var entry) || !ReferenceEquals(entry, Entries[i])) return false;
                    }
                    return true;
                }
            }
            // Renderer-specific readiness is intentionally stricter. CPU
            // publication never implies that Meta can use or map its geometry.
            internal bool Admitted => Published && Owner.ContextOpen && Inputs.All(input => input.Active && Owner.entries.TryGetValue(input,out var entry) && entry.Handle != IntPtr.Zero && !entry.Retired && entry.Revision == input.Revision && entry.Pose.Equals(input.Pose));
            internal int Vertices, Triangles;
            internal bool Commit()
            {
                if (Ended || Committed || !Owner) return false;
                if(Surfaces==null){
                    for(int i=0;i<Inputs.Length;i++){Owner.Register(Inputs[i]);Entries[i]=Owner.entries[Inputs[i]];}
                    Committed=true;return true;
                }
                if (SourceRoot && (!SourceFrame || !RootPose.Equals(SourceRoot.transform.localToWorldMatrix)
                    || !FramePose.Equals(SourceFrame.localToWorldMatrix)
                    || !SourceRoot.GetComponentsInChildren<AcousticSurface>(true)
                        .Where(s => s.isActiveAndEnabled && s.Available && s.Mesh).SequenceEqual(Surfaces))) return false;
                if (!ReferenceEquals(SourceRoot, null) && !SourceRoot) return false;
                for (int i = 0; i < Surfaces.Length; i++)
                    if (!Surfaces[i] || !Owner.entries.TryGetValue(Surfaces[i], out var current)
                        || !ReferenceEquals(current, Entries[i])
                        || !SourceBindings[i].Matches(current.GeometryKey, Surfaces[i])) return false;
                // No SDK destroy/upload and no invalidation: the same native
                // input remains owned while its renderer is being destroyed.
                for (int i = 0; i < Surfaces.Length; i++)
                { Owner.entries.Remove(Surfaces[i]); Owner.entries.Add(Inputs[i], Entries[i]); }
                // Do not keep destroyed component wrappers (and their importer
                // graphs) alive through the cache. Rollback resolves the live root.
                Owner.geometryCatalog=null; Surfaces = null; SourceBindings = null; SourceRoot = null; SourceFrame = null; Committed = true; return true;
            }
            internal void UpdateDefinition(bool current, Matrix4x4 placement)
            {
                if (Ended) return;
                DefinitionCurrent=current;
                foreach (var input in Inputs)
                {
                    input.Placement = placement;
                    if (input.Current == current) continue;
                    input.Current = current; input.Revision++;
                    if (Committed && Owner) Owner.Invalidate(input);
                }
            }
            internal void Restore(GameObject root)
            {
                if (Ended) return;
                var surfaces = root ? root.GetComponentsInChildren<AcousticSurface>(true)
                    .Where(s => s.Available && s.Mesh).ToArray() : Array.Empty<AcousticSurface>();
                bool same = Committed && Owner && surfaces.Length == Inputs.Length;
                for (int i = 0; same && i < Inputs.Length; i++)
                {
                    var input = Inputs[i]; var surface = surfaces[i];
                    same = input.Current && Owner.entries.TryGetValue(input, out var current) && ReferenceEquals(current, Entries[i]) && !surface.Scanned
                        && surface.ReflectionBoundary == input.ReflectionBoundary
                        && ReadMesh(surface.Mesh, out var vertices, out var indices, out _)
                        && vertices.SequenceEqual(input.Vertices) && indices.SequenceEqual(input.Indices);
                }
                if (same)
                {
                    for (int i = 0; i < Inputs.Length; i++)
                    {
                        // A newly enabled view may already have an empty entry.
                        // Retire it before returning this exact geometry owner.
                        Owner.Unregister(surfaces[i]); Owner.entries.Remove(Inputs[i]);
                        var entry = Entries[i]; var input = Inputs[i];
                        // Keep a busy worker's old metadata intact. Only relabel
                        // an actually matching native payload as the new view.
                        if (!entry.Retired && entry.Handle != IntPtr.Zero && entry.Revision == input.Revision
                            && entry.Pose.Equals(input.Pose)) entry.Revision = surfaces[i].Revision;
                        if (entry.Handle == IntPtr.Zero) { entry.Revision = -1; entry.AttemptEpoch = -1; }
                        Owner.entries.Add(surfaces[i], entry); Owner.geometryCatalog=null;
                    }
                }
                Dispose();
            }
            public void Dispose()
            {
                if (Ended) return; Ended = true;
                if (Owner)
                {
                    if (Committed) foreach (var input in Inputs) Owner.Unregister(input);
                    Owner.retainedSurfaces -= Inputs.Length; Owner.retainedVertices -= Vertices; Owner.retainedTriangles -= Triangles; Owner.ResidencyEpoch++;
                }
                foreach (var input in Inputs) { if (input == null) continue; input.Ended = true; input.Vertices = null; input.Indices = null; input.Frame = null; }
                Owner = null; Surfaces = null; SourceBindings = null; SourceRoot = null; SourceFrame = null; Inputs = null; Entries = null;
            }
        }
        internal bool TryPrepareInputs(AcousticMeshInput[] meshes,Transform frame,Matrix4x4 placement,bool boundary,out AcousticResidency residency,out string error)
        {
            residency=null;error="Invalid prepared acoustic input";
            if(meshes==null||!frame||!ValidPose(frame.localToWorldMatrix)||!ValidPose(placement))return false;
            int vertices=0,triangles=0;
            foreach(var mesh in meshes){
                if(mesh==null||!ValidPose(frame.localToWorldMatrix*placement*mesh.Local)||!ValidateMeshData(mesh.Vertices,mesh.Indices,out error))return false;
                vertices+=mesh.Vertices.Length/3;triangles+=mesh.Indices.Length/3;
            }
            if(retainedSurfaces+meshes.Length>MaximumVirtualSurfaces||retainedVertices+vertices>98304||retainedTriangles+triangles>MaximumVirtualTriangles)
            {error="Retained acoustic geometry budget reached";return false;}
            var inputs=new RetainedInput[meshes.Length];
            for(int i=0;i<inputs.Length;i++)inputs[i]=new RetainedInput{Frame=frame,Placement=placement,InitialPlacement=placement,InitialFrame=frame.localToWorldMatrix,
                InitialWorld=frame.localToWorldMatrix*placement*meshes[i].Local,Local=meshes[i].Local,Vertices=meshes[i].Vertices,Indices=meshes[i].Indices,ReflectionBoundary=boundary};
            retainedSurfaces+=inputs.Length;retainedVertices+=vertices;retainedTriangles+=triangles;ResidencyEpoch++;
            residency=new AcousticResidency{Owner=this,Inputs=inputs,Entries=new Entry[inputs.Length],Vertices=vertices,Triangles=triangles};error=null;return true;
        }
        internal bool TryPrepareResidency(GameObject root, Transform frame, out AcousticResidency residency, out string error)
        {
            residency = null; error = "The area's acoustic geometry is still preparing or unavailable";
            if (!root || !frame || !ValidPose(frame.localToWorldMatrix)) return false;
            var surfaces = root.GetComponentsInChildren<AcousticSurface>(true)
                .Where(s => s.isActiveAndEnabled && s.Available && s.Mesh).ToArray();
            if (surfaces.Length == 0) { error = null; return true; }
            Synchronize();
            if (!opened) return false;
            var inputs = new RetainedInput[surfaces.Length]; var owned = new Entry[surfaces.Length];
            var bindings = new AcousticGeometryBinding[surfaces.Length];
            int vertices = 0, triangles = 0;
            var placement = frame.worldToLocalMatrix * root.transform.localToWorldMatrix;
            for (int i = 0; i < surfaces.Length; i++)
            {
                var surface = surfaces[i];
                // Physical scan input remains with its tracking owner. Only
                // already admitted virtual geometry can cross this boundary.
                if (surface.Scanned || !entries.TryGetValue(surface, out var entry) || entry.Retired
                    || entry.Handle == IntPtr.Zero || entry.VertexData == null || entry.IndexData == null
                    || entry.Revision != surface.Revision || entry.Boundary != surface.ReflectionBoundary
                    || !entry.Pose.Equals(surface.transform.localToWorldMatrix)) return false;
                owned[i] = entry; bindings[i] = new AcousticGeometryBinding(entry.GeometryKey, surface); vertices += entry.Vertices; triangles += entry.Triangles;
                inputs[i] = new RetainedInput { Frame = frame, Placement = placement, InitialPlacement = placement,
                    InitialFrame = frame.localToWorldMatrix, InitialWorld = entry.Pose,
                    Local = root.transform.worldToLocalMatrix * entry.Pose, Vertices = entry.VertexData, Indices = entry.IndexData,
                    ReflectionBoundary = entry.Boundary, Revision = entry.Revision };
            }
            if (retainedSurfaces + inputs.Length > MaximumVirtualSurfaces || retainedVertices + vertices > 98304
                || retainedTriangles + triangles > MaximumVirtualTriangles)
            { error = "Retained acoustic geometry budget reached"; return false; }
            retainedSurfaces += inputs.Length; retainedVertices += vertices; retainedTriangles += triangles;
            residency = new AcousticResidency { Owner = this, Surfaces = surfaces, SourceBindings = bindings, SourceRoot = root, SourceFrame = frame, RootPose = root.transform.localToWorldMatrix, FramePose = frame.localToWorldMatrix, Inputs = inputs, Entries = owned, Vertices = vertices, Triangles = triangles };
            error = null; return true;
        }
    }
}
