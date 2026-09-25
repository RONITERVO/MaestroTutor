// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using Maestro.Quest.Imports;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using System.Linq;
using UnityEngine;
using UniGLTF;

namespace Maestro.Quest.Editor
{
    public static class QuestModelAudit
    {
        [Serializable] sealed class Result { public string file, result; public int vertices, triangles, texturePixels; }
        [Serializable] sealed class Report { public List<Result> models = new(); }
        public static void Inspect()
        {
            string directory = Environment.GetEnvironmentVariable("MAESTRO_MODEL_DIRECTORY");
            string output = Environment.GetEnvironmentVariable("MAESTRO_MODEL_AUDIT");
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set the model directory and audit output.");
            var report = new Report();
            foreach (var path in Directory.GetFiles(directory, "*.vrm"))
            {
                var result = new Result { file = Path.GetFileName(path) };
                try
                {
                    var info = ModelInspection.Inspect(ModelLibrary.ReadBounded(path)); result.result = "Preflight passed; runtime/device check still required";
                    result.vertices = info.Vertices; result.triangles = info.Triangles; result.texturePixels = info.TexturePixels;
                }
                catch (Exception error) { result.result = error is ModelImportException ? error.Message : "Could not inspect this model"; }
                report.models.Add(result);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("MAESTRO_MODEL_AUDIT_WRITTEN");
        }

        public static void Preview()
        {
            string path = Environment.GetEnvironmentVariable("MAESTRO_MODEL_PREVIEW");
            string output = Environment.GetEnvironmentVariable("MAESTRO_MODEL_AUDIT");
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set preview model and evidence path.");
            var asset = ModelLibrary.Inspect(Path.GetFileName(path), ModelLibrary.ReadBounded(path));
            var root = new GameObject("Local avatar verification"); var go = new GameObject("Verification camera", typeof(Camera));
            RenderTexture texture = null; Texture2D pixels = null; var previous = RenderTexture.active;
            try
            {
                var model = root.AddComponent<ImportedModel>(); model.LoadAsync(asset, new ImmediateCaller()).GetAwaiter().GetResult();
                if (!model.Ready) throw new InvalidOperationException("Model was not loaded");
                var camera = go.GetComponent<Camera>(); camera.transform.position = new Vector3(0,0,-1); camera.transform.LookAt(Vector3.zero);
                camera.orthographic = true; camera.orthographicSize = .21f; camera.nearClipPlane = .01f; camera.backgroundColor = new Color(.93f,.91f,.87f,1); camera.clearFlags = CameraClearFlags.SolidColor;
                texture = new RenderTexture(1400, 1600, 24); pixels = new Texture2D(1400,1600,TextureFormat.RGB24,false);
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture; pixels.ReadPixels(new Rect(0,0,1400,1600),0,0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output), "local-avatar-preview.png"), pixels.EncodeToPNG());
                if (model.IsHumanoid)
                {
                    model.FitAsMaestro();
                    var driver = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Avatars/DefaultMaestro"),root.transform);
                    if (!driver.TryGetComponent<Animator>(out var animator)) animator = driver.AddComponent<Animator>();
                    animator.enabled = false;
                    var clips = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Maestro/Resources/Avatars/DefaultMaestro.fbx").OfType<AnimationClip>();
                    foreach (var renderer in driver.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    var rig = root.AddComponent<AvatarPoseRig>(); rig.Initialize(animator);
                    var retargeter = root.AddComponent<HumanoidRetargeter>(); retargeter.Initialize(rig,model.Humanoid); rig.SetDisplayRig(retargeter);
                    camera.orthographicSize = 1.05f; camera.transform.position = new Vector3(0,.9f,3); camera.transform.LookAt(new Vector3(0,.9f,0));
                    foreach (var gesture in new[] { "Idle","Greeting","Pointing" })
                    {
                        clips.First(clip => clip.name.Split('|').Last() == gesture).SampleAnimation(driver,.55f); retargeter.ApplyPose();
                        // A synchronous editor still has no player skinning frame.
                        // Bake the actual retargeted bones, as the included-avatar preview does.
                        var poses = new List<GameObject>();
                        foreach (var skin in model.Instance.SkinnedMeshRenderers)
                        {
                            var posed = new GameObject("Retargeted " + skin.name,typeof(MeshFilter),typeof(MeshRenderer)); posed.transform.SetParent(skin.transform,false);
                            var mesh = new Mesh(); skin.BakeMesh(mesh,true); posed.GetComponent<MeshFilter>().sharedMesh = mesh;
                            posed.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials; skin.enabled = false; poses.Add(posed);
                        }
                        camera.Render(); RenderTexture.active = texture; pixels.ReadPixels(new Rect(0,0,1400,1600),0,0); pixels.Apply();
                        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output),"custom-maestro-"+gesture.ToLowerInvariant()+".png"),pixels.EncodeToPNG());
                        foreach (var posed in poses) { UnityEngine.Object.DestroyImmediate(posed.GetComponent<MeshFilter>().sharedMesh); UnityEngine.Object.DestroyImmediate(posed); }
                        foreach (var skin in model.Instance.SkinnedMeshRenderers) skin.enabled = true;
                    }
                    var from = rig.CanonicalBone(PoseJoint.LeftHand).position - rig.CanonicalBone(PoseJoint.LeftLowerArm).position;
                    var to = rig.Bone(PoseJoint.LeftHand).position - rig.Bone(PoseJoint.LeftLowerArm).position;
                    Debug.Log("MAESTRO_RETARGET_VERIFIED bones="+rig.Capture().Length+" forearmAngle="+Vector3.Angle(from,to));
                }
                Debug.Log("MAESTRO_LOCAL_AVATAR_IMPORTED " + Path.GetFileName(path) + " renderers=" + model.Instance.Renderers.Count + " clips=" + model.ClipCount);
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(go); if (texture) UnityEngine.Object.DestroyImmediate(texture); if (pixels) UnityEngine.Object.DestroyImmediate(pixels); }
        }
    }
}
