// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed class MotionPlaybackTests
    {
        GameObject root;
        MotionLibrary library;
        string directory;
        [UnitySetUp] public IEnumerator Setup() { root = new GameObject("Motion library test"); directory = Path.Combine(Path.GetTempPath(),"MaestroMotionPlayback-"+Guid.NewGuid().ToString("N")); library = new MotionLibrary(directory); yield return null; }
        [UnityTearDown] public IEnumerator Cleanup() { library.Dispose(); UnityEngine.Object.Destroy(root); yield return null; yield return null; if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        [UnityTest] public IEnumerator CachedMotionsSampleTheExistingMeshAndConvertCubicTangents()
        {
            var model = root.AddComponent<ImportedModel>(); var load = model.LoadAsync(ModelLibrary.Inspect("mesh.glb",ModelFixture.Create()));
            yield return new WaitUntil(() => load.IsCompleted); Assert.That(load.Exception,Is.Null);
            int renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            foreach (string interpolation in new[] { "LINEAR","STEP","CUBICSPLINE" })
            {
                var import = library.ImportAsync(interpolation+".glb",ModelFixture.TranslationMotion(interpolation));
                yield return new WaitUntil(() => import.IsCompleted); Assert.That(import.Exception,Is.Null);
                var request = library.AcquireAsync(import.Result[0].id,model.MotionRigHash);
                yield return new WaitUntil(() => request.IsCompleted); Assert.That(request.Exception,Is.Null);
                using var lease = request.Result;
                Assert.That(model.SampleMotion(lease,.5f,false),Is.True);
                float expected = interpolation == "LINEAR" ? -.5f : interpolation == "STEP" ? 0 : -.75f;
                Assert.That(model.Instance.Nodes[0].localPosition.z,Is.EqualTo(expected).Within(.001f),"Value and tangent axes must both be converted");
                Assert.That(UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,Is.EqualTo(renderers),"Loading motions must not instantiate models");
            }
            var cubic = library.ImportAsync("cubic-rotation.glb",ModelFixture.CubicRotationMotion()); yield return new WaitUntil(() => cubic.IsCompleted); Assert.That(cubic.Exception,Is.Null);
            var rotation = library.AcquireAsync(cubic.Result[0].id,model.MotionRigHash); yield return new WaitUntil(() => rotation.IsCompleted); Assert.That(rotation.Exception,Is.Null);
            using var rotating = rotation.Result; Assert.That(model.SampleMotion(rotating,.5f,false),Is.True);
            var expectedRotation = new Quaternion(0,-(.25f+.5f*Mathf.Sqrt(.5f)),0,.5f+.5f*Mathf.Sqrt(.5f)).normalized;
            Assert.That(Quaternion.Angle(model.Instance.Nodes[0].localRotation,expectedRotation),Is.LessThan(.06f),"Cubic quaternion tangents must retain their magnitude under reflection");
        }
        [UnityTest] public IEnumerator NamedMorphCurvesDeformTheExistingRendererAndCorruptCopiesDoNotPlay()
        {
            var bytes = ModelFixture.MorphMotion(); var model = root.AddComponent<ImportedModel>();
            var load = model.LoadAsync(ModelLibrary.Inspect("morph.glb",bytes)); yield return new WaitUntil(() => load.IsCompleted); Assert.That(load.Exception,Is.Null);
            var add = library.ImportAsync("morph.glb",bytes); yield return new WaitUntil(() => add.IsCompleted); Assert.That(add.Exception,Is.Null); var entry = add.Result.Single();
            var request = library.AcquireAsync(entry.id,model.MotionRigHash); yield return new WaitUntil(() => request.IsCompleted); Assert.That(request.Exception,Is.Null);
            using (var lease = request.Result)
            {
                var skin = model.Instance.SkinnedMeshRenderers.Single(); var mesh = new Mesh();
                try
                {
                    model.SampleClip(0,.5f,false); skin.BakeMesh(mesh,true); var source = mesh.vertices;
                    Assert.That(model.SampleMotion(lease,.5f,false),Is.True); skin.BakeMesh(mesh,true);
                    Assert.That(skin.GetBlendShapeWeight(0),Is.EqualTo(50).Within(.001f));
                    Assert.That(source.Zip(mesh.vertices,Vector3.Distance).Max(),Is.LessThan(.00001f));
                    model.Stop(); Assert.That(skin.GetBlendShapeWeight(0),Is.Zero);
                }
                finally { UnityEngine.Object.Destroy(mesh); }
            }
            library.Dispose(); string payload = Path.Combine(directory,entry.hash+".motion.glb"); File.WriteAllBytes(payload,new byte[30]); library = new MotionLibrary(directory);
            request = library.AcquireAsync(entry.id,model.MotionRigHash); yield return new WaitUntil(() => request.IsCompleted); Assert.That(request.IsFaulted,Is.True); Assert.That(library.ResidentClipCount,Is.Zero);
            File.Delete(payload); request = library.AcquireAsync(entry.id,model.MotionRigHash); yield return new WaitUntil(() => request.IsCompleted); Assert.That(request.IsFaulted,Is.True); Assert.That(library.ResidentClipCount,Is.Zero);
        }
        // The user's collection stays outside the project and the build. One
        // deterministic representative per category exercises the actual exporter.
        [UnityTest] public IEnumerator SelectedCollectionMotionsMatchSourceTransformsAndDeformedMeshes()
        {
            string source = Environment.GetEnvironmentVariable("MAESTRO_MOTION_DIRECTORY");
            if (string.IsNullOrEmpty(source)) Assert.Ignore("No private motion collection selected for verification");
            var representatives = Directory.GetFiles(source,"*.glb",SearchOption.AllDirectories).GroupBy(Path.GetDirectoryName).OrderBy(x => x.Key,StringComparer.Ordinal).Select(group => group.OrderBy(x => x,StringComparer.Ordinal).First()).ToArray();
            Assert.That(representatives,Is.Not.Empty);
            foreach (string path in representatives)
            {
                byte[] bytes = ModelLibrary.ReadBounded(path); string hash = ModelLibrary.Hash(bytes);
                var go = new GameObject("Original collection model"); go.transform.SetParent(root.transform,false); var model = go.AddComponent<ImportedModel>();
                var load = model.LoadAsync(ModelLibrary.Inspect(Path.GetFileName(path),bytes)); yield return new WaitUntil(() => load.IsCompleted); Assert.That(load.Exception,Is.Null);
                var add = library.ImportAsync(Path.GetFileName(path),bytes); yield return new WaitUntil(() => add.IsCompleted); Assert.That(add.Exception,Is.Null);
                var skins = model.Instance.SkinnedMeshRenderers; var baked = new Mesh();
                Vector3[] Vertices()
                {
                    var result = new List<Vector3>(); foreach (var skin in skins) { skin.BakeMesh(baked,true); result.AddRange(baked.vertices.Select(skin.transform.TransformPoint)); } return result.ToArray();
                }
                int renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
                float maximumVertexError = 0;
                try
                {
                    foreach (var entry in add.Result)
                    {
                        int clip = entry.origins.First(x => x.sourceHash == hash).clipIndex;
                        var request = library.AcquireAsync(entry.id,model.MotionRigHash); yield return new WaitUntil(() => request.IsCompleted); Assert.That(request.Exception,Is.Null);
                        using var motion = request.Result;
                        foreach (float fraction in new[] { 0f,.17f,.5f,.83f,1f })
                        {
                            float time = fraction*entry.duration;
                            Assert.That(model.SampleClip(clip,time,false),Is.True);
                            var positions = model.Instance.Nodes.Select(x => x.localPosition).ToArray(); var rotations = model.Instance.Nodes.Select(x => x.localRotation).ToArray(); var scales = model.Instance.Nodes.Select(x => x.localScale).ToArray(); var vertices = Vertices();
                            Assert.That(model.SampleMotion(motion,time,false),Is.True);
                            for (int i=0;i<positions.Length;i++)
                            {
                                Assert.That(Vector3.Distance(positions[i],model.Instance.Nodes[i].localPosition),Is.LessThan(.0001f),path+" position "+i);
                                Assert.That(Quaternion.Angle(rotations[i],model.Instance.Nodes[i].localRotation),Is.LessThan(.06f),path+" clip "+clip+" fraction "+fraction+" rotation "+i);
                                Assert.That(Vector3.Distance(scales[i],model.Instance.Nodes[i].localScale),Is.LessThan(.0001f),path+" scale "+i);
                            }
                            maximumVertexError = Mathf.Max(maximumVertexError,vertices.Zip(Vertices(),Vector3.Distance).DefaultIfEmpty().Max());
                        }
                    }
                    Assert.That(maximumVertexError,Is.LessThan(.0001f),"Extracted motions must deform the same visible mesh as the source clips");
                    Assert.That(UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,Is.EqualTo(renderers));
                    Assert.That(ModelLibrary.Hash(ModelLibrary.ReadBounded(path)),Is.EqualTo(hash));
                    Debug.Log("MAESTRO_MOTION_EQUIVALENCE file="+Path.GetFileName(path)+" clips="+add.Result.Length+" maximumVertexError="+maximumVertexError);
                }
                finally { UnityEngine.Object.Destroy(baked); UnityEngine.Object.Destroy(go); }
                yield return null; yield return null;
            }
        }
        [UnityTest] public IEnumerator CachePinsActiveClipsRejectsMismatchAndEvictsOnlyReleasedLeases()
        {
            var entries = new List<MotionEntry>();
            for (int i=0;i<9;i++)
            {
                var import = library.ImportAsync("motion-"+i+".glb",ModelFixture.TranslationMotion("LINEAR",i+1)); yield return new WaitUntil(() => import.IsCompleted);
                Assert.That(import.Exception,Is.Null); entries.Add(import.Result[0]);
            }
            var wrong = library.AcquireAsync(entries[0].id,new string('f',64)); yield return new WaitUntil(() => wrong.IsCompleted); Assert.That(wrong.IsFaulted,Is.True); Assert.That(library.ResidentClipCount,Is.Zero);
            var leases = new List<MotionLibrary.Lease>();
            try
            {
                foreach (var entry in entries.Take(8)) { var task = library.AcquireAsync(entry.id,entry.rigHash); yield return new WaitUntil(() => task.IsCompleted); Assert.That(task.Exception,Is.Null); leases.Add(task.Result); }
                var rejected = library.AcquireAsync(entries[8].id,entries[8].rigHash); yield return new WaitUntil(() => rejected.IsCompleted); Assert.That(rejected.IsFaulted,Is.True);
                Assert.That(library.ResidentClipCount,Is.EqualTo(8)); Assert.That(leases.All(x => x.Clip),Is.True);
                leases[0].Dispose(); var ninth = library.AcquireAsync(entries[8].id,entries[8].rigHash); yield return new WaitUntil(() => ninth.IsCompleted); Assert.That(ninth.Exception,Is.Null); leases.Add(ninth.Result);
                Assert.That(library.ResidentClipCount,Is.EqualTo(8)); Assert.That(leases.Skip(1).All(x => x.Clip),Is.True);
            }
            finally { foreach (var lease in leases) lease.Dispose(); }
            library.Dispose(); Assert.That(library.ResidentClipCount,Is.Zero);
        }
    }
}
