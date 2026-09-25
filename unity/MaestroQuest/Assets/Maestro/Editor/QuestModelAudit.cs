// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using Maestro.Quest.Imports;
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
                Debug.Log("MAESTRO_LOCAL_AVATAR_IMPORTED " + Path.GetFileName(path) + " renderers=" + model.Instance.Renderers.Count + " clips=" + model.ClipCount);
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(go); if (texture) UnityEngine.Object.DestroyImmediate(texture); if (pixels) UnityEngine.Object.DestroyImmediate(pixels); }
        }
    }
}
