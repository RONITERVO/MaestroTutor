// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Meta.XR.Acoustics;
using UnityEngine;

namespace Maestro.Quest.Book
{
    /// <summary>Owns native acoustic geometry independently of physics, render
    /// visibility and workspace persistence. No reflection into SDK components.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class RoomAcoustics : MonoBehaviour
    {
        internal const int MaximumMeshVertices = 16384, MaximumMeshTriangles = 8192;
        // Separate reservations prevent a large creation from evicting the scan.
        internal const int MaximumScannedSurfaces = 128, MaximumVirtualSurfaces = 384;
        const int MaximumScannedTriangles = 16384, MaximumVirtualTriangles = 49152;
        const int MaximumUploadsPerFrame = 4;
        sealed class Entry
        {
            internal IntPtr Handle;
            internal int Triangles, Vertices, Revision = -1, AttemptEpoch = -1;
            internal bool Scanned;
            internal bool Retired;
            internal Matrix4x4 Pose;
        }
        readonly Dictionary<AcousticSurface, Entry> entries = new();
        readonly List<Entry> retiredEntries = new();
        static readonly object leaseLock = new();
        static object contextLease;
        object lease;
        RoomAcousticMapJob mapJob;
        IntPtr map;
        bool opened;
        internal Action FirstMapProgress;
        MetaXRAcousticNativeInterface.INativeInterface native;
        IntPtr material;
        AcousticModel previousModel;
        int scannedCount, virtualCount, scannedTriangles, virtualTriangles, scannedVertices, virtualVertices, epoch;
        bool paused, focused = true, ownsModel;
        public bool Ready => opened && mapJob == null;
        internal bool MapComputing => mapJob != null;
        internal bool MapReady => Ready && map != IntPtr.Zero;
        internal string MapIssue { get; private set; }
        public string Issue { get; private set; }
        public int GeometryCount => scannedCount + virtualCount;
        public int TriangleCount => scannedTriangles + virtualTriangles;
        public int OmittedCount { get; private set; }

        internal void Register(AcousticSurface surface)
        {
            if (!entries.ContainsKey(surface)) { InvalidateMap(); entries.Add(surface, new Entry()); }
        }
        internal void Unregister(AcousticSurface surface)
        {
            if (entries.Remove(surface, out var entry)) Release(entry);
        }
        internal void Invalidate(AcousticSurface surface)
        {
            InvalidateMap();
            // Destroy old topology immediately: no invisible wall while queued
            // replacement geometry is being validated or uploaded.
            if (entries.TryGetValue(surface, out var entry)) { Release(entry); entry.Revision = -1; }
        }
        void OnEnable() { AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged; Open(); }
        void Open()
        {
            if (opened || !isActiveAndEnabled || paused || !focused) return;
            Issue = null;
            lock (leaseLock)
            {
                if (contextLease != null) { Issue = "Another room owns acoustic geometry or is finishing its calculation"; return; }
                contextLease = lease = new object();
            }
            try
            {
                if (AudioSettings.GetSpatializerPluginName() != SpeechSpatializer.PluginName)
                    throw new InvalidOperationException("The spatial audio renderer is unavailable");
                Check(MetaXRAcousticNativeInterface.UnityNativeInterface.ovrAudio_GetPluginContext(out var context));
                if (context == IntPtr.Zero) throw new InvalidOperationException("The spatial audio context is unavailable");
                native = MetaXRAcousticNativeInterface.Interface;
                if (native is not MetaXRAcousticNativeInterface.UnityNativeInterface)
                    throw new InvalidOperationException("The Unity acoustic renderer is unavailable");
                previousModel = MetaXRAcousticSettings.Instance.AcousticModel;
                Check(native.SetAcousticModel(AcousticModel.AcousticRayTracing)); ownsModel = true;
                Check(native.CreateAudioMaterial(out material));
                // Neutral hard-surface approximation, not an inference about the
                // real wall's construction. Measured/material authoring is future work.
                foreach (float frequency in new[] { 125f, 500f, 2000f, 8000f })
                {
                    Check(native.AudioMaterialSetFrequency(material, MaterialProperty.ABSORPTION, frequency, .2f));
                    Check(native.AudioMaterialSetFrequency(material, MaterialProperty.TRANSMISSION, frequency, 0));
                    Check(native.AudioMaterialSetFrequency(material, MaterialProperty.SCATTERING, frequency, .5f));
                }
                opened = true; epoch++;
            }
            catch (Exception) { Close(); Issue = "Room acoustics could not start; directional speech remains available"; }
        }
        static void Check(int result)
        {
            if (result != 0) throw new InvalidOperationException("Native acoustic operation failed: " + result);
        }
        internal void Synchronize()
        {
            if (!opened) return;
            if (mapJob != null)
            {
                if (!MapInputsUnchanged()) mapJob.Cancel();
                if (!mapJob.Completed) return;
                var completed = mapJob; mapJob = null;
                MapIssue = completed.Issue;
                map = completed.Take();
                completed.Retire();
                foreach (var retired in retiredEntries) { retired.Retired = false; Release(retired); }
                retiredEntries.Clear();
                if (map != IntPtr.Zero)
                {
                    try { Check(native.AudioSceneIRSetEnabled(map, true)); }
                    catch (Exception) { InvalidateMap(); MapIssue = "Acoustic map could not be activated"; }
                }
            }
            int uploads = 0, omitted = 0;
            foreach (var pair in entries)
            {
                var surface = pair.Key; var entry = pair.Value;
                if (!surface || !surface.isActiveAndEnabled || !surface.Available || !surface.Mesh)
                { Release(entry); continue; }
                var pose = surface.transform.localToWorldMatrix;
                if (!ValidPose(pose)) { Release(entry); surface.Issue = "Invalid acoustic transform"; omitted++; continue; }
                if (entry.Handle != IntPtr.Zero && entry.Revision != surface.Revision) Release(entry);
                if (entry.Handle == IntPtr.Zero)
                {
                    if (entry.Revision == surface.Revision && entry.AttemptEpoch == epoch) { omitted++; continue; }
                    if (uploads >= MaximumUploadsPerFrame) { omitted++; continue; }
                    uploads++;
                    entry.Revision = surface.Revision; entry.AttemptEpoch = epoch;
                    if (!TryUpload(surface, entry, pose)) { omitted++; continue; }
                }
                else if (!entry.Pose.Equals(pose))
                {
                    InvalidateMap();
                    try { Check(native.AudioGeometrySetTransform(entry.Handle, in pose)); entry.Pose = pose; }
                    catch (Exception) { Release(entry); surface.Issue = "Acoustic movement could not be applied"; omitted++; }
                }
            }
            OmittedCount = omitted;
        }
        // Called by the reflection owner, not by diagnostics or provider code.
        // Input geometry stays frozen while the SDK worker reads it. Direct HRTF
        // remains available; source acoustics must respect Ready throughout.
        internal bool RequestMap(Vector3[] points)
        {
            Synchronize();
            if (!Ready || GeometryCount == 0 || OmittedCount != 0 || points == null || points.Length == 0 || points.Length > RoomAcousticMapJob.MaximumPoints)
                return false;
            var packed = new float[points.Length * 3];
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                if (!float.IsFinite(point.sqrMagnitude) || Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y), Mathf.Abs(point.z)) > 10000) return false;
                // Map points are native world positions; unlike geometry's
                // SetTransform wrapper, ComputeCustomPoints does not flip Z.
                packed[3 * i] = point.x; packed[3 * i + 1] = point.y; packed[3 * i + 2] = -point.z;
            }
            InvalidateMap(); MapIssue = null;
            try { mapJob = new RoomAcousticMapJob(native, packed, FirstMapProgress); return true; }
            catch (Exception) { MapIssue = "Acoustic map could not start"; return false; }
        }
        bool MapInputsUnchanged()
        {
            foreach (var pair in entries)
            {
                var surface = pair.Key; var entry = pair.Value;
                if (entry.Handle == IntPtr.Zero) continue;
                if (!surface || !surface.isActiveAndEnabled || !surface.Available || !surface.Mesh
                    || surface.Revision != entry.Revision || !surface.transform.localToWorldMatrix.Equals(entry.Pose)) return false;
            }
            return true;
        }
        void InvalidateMap()
        {
            mapJob?.Cancel();
            if (map == IntPtr.Zero) return;
            native.AudioSceneIRSetEnabled(map, false); native.DestroyAudioSceneIR(map); map = IntPtr.Zero;
        }
        bool TryUpload(AcousticSurface surface, Entry entry, Matrix4x4 pose)
        {
            IntPtr candidate = IntPtr.Zero;
            try
            {
                if (!ReadMesh(surface.Mesh, out var vertices, out var indices, out var issue)) { surface.Issue = issue; return false; }
                int triangles = indices.Length / 3;
                bool scan = surface.Scanned;
                if ((scan ? scannedCount >= MaximumScannedSurfaces : virtualCount >= MaximumVirtualSurfaces) ||
                    triangles + (scan ? scannedTriangles : virtualTriangles) > (scan ? MaximumScannedTriangles : MaximumVirtualTriangles) ||
                    vertices.Length / 3 + (scan ? scannedVertices : virtualVertices) > (scan ? 32768 : 98304))
                { surface.Issue = "Acoustic scene budget reached"; return false; }
                Check(native.CreateAudioGeometry(out candidate));
                Check(native.AudioGeometrySetObjectFlag(candidate, ObjectFlags.ENABLED, false));
                Check(native.AudioGeometrySetObjectFlag(candidate, ObjectFlags.STATIC, false));
                var groups = new[] { new MeshGroup { indexOffset = UIntPtr.Zero, faceCount = (UIntPtr)triangles, faceType = FaceType.TRIANGLES, material = material } };
                Check(native.AudioGeometryUploadMeshArrays(candidate, vertices, vertices.Length / 3, indices, indices.Length, groups, 1));
                // SDK performs its own Z conversion. Vertices and transform here
                // are Unity coordinates, including the actual parent transforms.
                Check(native.AudioGeometrySetTransform(candidate, in pose));
                Check(native.AudioGeometrySetObjectFlag(candidate, ObjectFlags.ENABLED, true));
                entry.Handle = candidate; candidate = IntPtr.Zero; entry.Pose = pose; entry.Scanned = scan; entry.Triangles = triangles; entry.Vertices = vertices.Length / 3;
                if (scan) { scannedCount++; scannedTriangles += triangles; scannedVertices += entry.Vertices; }
                else { virtualCount++; virtualTriangles += triangles; virtualVertices += entry.Vertices; }
                surface.Issue = null; return true;
            }
            catch (Exception) { surface.Issue = "Acoustic mesh could not be uploaded"; return false; }
            finally { if (candidate != IntPtr.Zero) native.DestroyAudioGeometry(candidate); }
        }
        internal static bool ReadMesh(Mesh mesh, out float[] vertices, out int[] indices, out string issue)
        {
            vertices = null; indices = null; issue = "Acoustic mesh is unreadable or exceeds its budget";
            if (!mesh || !mesh.isReadable || mesh.vertexCount < 3 || mesh.vertexCount > MaximumMeshVertices || mesh.subMeshCount < 1 || mesh.subMeshCount > 64) return false;
            ulong count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) != MeshTopology.Triangles || mesh.GetIndexCount(i) % 3 != 0) return false;
                count += mesh.GetIndexCount(i);
            }
            if (count < 3 || count > MaximumMeshTriangles * 3) return false;
            var points = mesh.vertices; var triangles = mesh.triangles;
            foreach (var p in points) if (!float.IsFinite(p.sqrMagnitude) || Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z)) > 10000) return false;
            foreach (int i in triangles) if (i < 0 || i >= points.Length) return false;
            for (int i = 0; i < triangles.Length; i += 3)
                if (Vector3.Cross(points[triangles[i + 1]] - points[triangles[i]], points[triangles[i + 2]] - points[triangles[i]]).sqrMagnitude < 1e-16f)
                { issue = "Acoustic mesh has degenerate triangles"; return false; }
            vertices = new float[points.Length * 3]; indices = triangles;
            for (int i = 0; i < points.Length; i++) { vertices[i * 3] = points[i].x; vertices[i * 3 + 1] = points[i].y; vertices[i * 3 + 2] = points[i].z; }
            issue = null; return true;
        }
        internal static bool ValidPose(Matrix4x4 pose)
        {
            for (int i = 0; i < 16; i++) if (!float.IsFinite(pose[i]) || Mathf.Abs(pose[i]) > 10000) return false;
            return pose.determinant > 1e-10f && pose.m30 == 0 && pose.m31 == 0 && pose.m32 == 0 && pose.m33 == 1;
        }
        void Release(Entry entry)
        {
            if (entry.Handle == IntPtr.Zero) return;
            InvalidateMap();
            if (mapJob != null)
            {
                // The SDK may still be reading this object. Cancellation is
                // cooperative, so retain its handle until the worker returns.
                if (!entry.Retired) { entry.Retired = true; retiredEntries.Add(entry); }
                return;
            }
            // Disable first so a failed destroy cannot leave a ghost obstacle.
            native.AudioGeometrySetObjectFlag(entry.Handle, ObjectFlags.ENABLED, false);
            native.DestroyAudioGeometry(entry.Handle); entry.Handle = IntPtr.Zero;
            if (entry.Scanned) { scannedCount--; scannedTriangles -= entry.Triangles; scannedVertices -= entry.Vertices; }
            else { virtualCount--; virtualTriangles -= entry.Triangles; virtualVertices -= entry.Vertices; }
            entry.Triangles = entry.Vertices = 0; epoch++;
        }
        void Close()
        {
            opened = false; InvalidateMap();
            if (mapJob != null)
            {
                var handles = new HashSet<IntPtr>();
                foreach (var entry in entries.Values) { if (entry.Handle != IntPtr.Zero) handles.Add(entry.Handle); entry.Handle = IntPtr.Zero; entry.Revision = -1; entry.Retired = false; }
                foreach (var entry in retiredEntries) { if (entry.Handle != IntPtr.Zero) handles.Add(entry.Handle); entry.Handle = IntPtr.Zero; entry.Retired = false; }
                retiredEntries.Clear();
                var closing = mapJob; mapJob = null;
                var api = native; var ownedMaterial = material; var ownedLease = lease; var restore = ownsModel; var model = previousModel;
                material = IntPtr.Zero; lease = null; ownsModel = false;
                scannedCount = virtualCount = scannedTriangles = virtualTriangles = scannedVertices = virtualVertices = OmittedCount = 0;
                // No Unity objects in the closure. The previous native scene
                // remains exclusively leased until every input has been freed.
                closing.Retire(() =>
                {
                    try
                    {
                        foreach (var handle in handles) { api.AudioGeometrySetObjectFlag(handle, ObjectFlags.ENABLED, false); api.DestroyAudioGeometry(handle); }
                        if (ownedMaterial != IntPtr.Zero) api.DestroyAudioMaterial(ownedMaterial);
                        if (restore) api.SetAcousticModel(model);
                    }
                    finally { ReleaseLease(ownedLease); }
                });
                return;
            }
            foreach (var entry in entries.Values) { Release(entry); entry.Revision = -1; }
            if (material != IntPtr.Zero) { native.DestroyAudioMaterial(material); material = IntPtr.Zero; }
            if (ownsModel) { native.SetAcousticModel(previousModel); ownsModel = false; }
            ReleaseLease(lease); lease = null;
            OmittedCount = 0;
        }
        static void ReleaseLease(object owner) { lock (leaseLock) { if (ReferenceEquals(contextLease, owner)) contextLease = null; } }
        void LateUpdate() { if (!opened) Open(); Synchronize(); }
        void OnDisable() { AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged; Close(); }
        void AudioConfigurationChanged(bool _) { Close(); Open(); }
        void OnDestroy() { Close(); entries.Clear(); }
        void OnApplicationPause(bool value) { paused = value; if (value) Close(); else Open(); }
        void OnApplicationFocus(bool value) { focused = value; if (!value) Close(); else Open(); }
    }
}
