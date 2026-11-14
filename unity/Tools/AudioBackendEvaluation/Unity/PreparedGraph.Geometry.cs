// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading;
using SteamAudio;

namespace Maestro.AudioEvaluation
{
    internal sealed partial class PreparedGraph
    {
        sealed class MeshOwner { internal IntPtr Scene, Mesh; }
        sealed class InstanceOwner { internal IntPtr Handle; internal AcousticMeshPose Input; }
        readonly Dictionary<AcousticMeshData, MeshOwner> meshes = new();
        readonly Dictionary<string, InstanceOwner> instances = new(StringComparer.Ordinal);
        AcousticSceneData appliedGeometry;
        internal static int MeshesOpened, MeshesClosed, InstancesOpened, InstancesClosed;
        // Only the preparation/simulation worker mutates geometry. Native DSP
        // consumes source-owned responses and never queries these scenes.
        void ApplyGeometry(AcousticSceneData desired)
        {
            if (ReferenceEquals(appliedGeometry, desired)) return;
            var wanted = new Dictionary<string, AcousticMeshPose>(StringComparer.Ordinal);
            var used = new HashSet<AcousticMeshData>();
            for (int i=0;i<desired.Count;i++) { var input=desired.At(i); wanted.Add(input.Id,input); used.Add(input.Mesh); }
            foreach (var id in new List<string>(instances.Keys))
                if (!wanted.TryGetValue(id,out var input) || input.Mesh != instances[id].Input.Mesh)
                { ReleaseInstance(instances[id]); instances.Remove(id); }
            foreach (var data in new List<AcousticMeshData>(meshes.Keys))
                if (!used.Contains(data)) { ReleaseMesh(meshes[data]); meshes.Remove(data); }
            foreach (var input in wanted.Values)
            {
                if (!meshes.TryGetValue(input.Mesh,out var owner))
                {
                    owner = new MeshOwner(); meshes.Add(input.Mesh,owner);
                    Interlocked.Increment(ref MeshesOpened);
                    var settings = new SceneSettings { type=SceneType.Default };
                    Check(API.iplSceneCreate(context,ref settings,out owner.Scene));
                    owner.Mesh = input.Mesh.Upload(owner.Scene);
                    API.iplStaticMeshAdd(owner.Mesh,owner.Scene);
                    API.iplSceneCommit(owner.Scene);
                }
                if (!instances.TryGetValue(input.Id,out var instance))
                {
                    instance = new InstanceOwner { Input=input }; instances.Add(input.Id,instance);
                    Interlocked.Increment(ref InstancesOpened);
                    var settings = new InstancedMeshSettings { subScene=owner.Scene, transform=input.NativePose };
                    Check(API.iplInstancedMeshCreate(scene,ref settings,out instance.Handle));
                    API.iplInstancedMeshAdd(instance.Handle,scene);
                }
                else if (!instance.Input.NativePose.Equals(input.NativePose))
                    API.iplInstancedMeshUpdateTransform(instance.Handle,scene,input.NativePose);
                instance.Input=input;
            }
            appliedGeometry=desired;
        }
        void ReleaseInstance(InstanceOwner instance)
        {
            if (instance.Handle!=IntPtr.Zero)
            {
                API.iplInstancedMeshRemove(instance.Handle,scene);
                API.iplInstancedMeshRelease(ref instance.Handle);
            }
            Interlocked.Increment(ref InstancesClosed);
        }
        static void ReleaseMesh(MeshOwner owner)
        {
            if (owner.Mesh!=IntPtr.Zero)
            {
                API.iplStaticMeshRemove(owner.Mesh,owner.Scene);
                API.iplStaticMeshRelease(ref owner.Mesh);
            }
            if (owner.Scene!=IntPtr.Zero)
            {
                API.iplSceneCommit(owner.Scene);
                API.iplSceneRelease(ref owner.Scene);
            }
            Interlocked.Increment(ref MeshesClosed);
        }
        void ReleaseGeometry()
        {
            foreach (var instance in instances.Values) ReleaseInstance(instance);
            instances.Clear();
            foreach (var owner in meshes.Values) ReleaseMesh(owner);
            meshes.Clear(); appliedGeometry=null;
        }
    }
}
