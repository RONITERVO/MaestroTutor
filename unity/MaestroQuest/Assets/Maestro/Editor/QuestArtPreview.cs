// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Book;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Maestro.Quest.Editor
{
    /// <summary>Renders the actual Unity materials and rig, without a headset or live account.</summary>
    public static class QuestArtPreview
    {
        public static void Render()
        {
            var output = Environment.GetEnvironmentVariable("MAESTRO_ART_EVIDENCE");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set MAESTRO_ART_EVIDENCE to a local output directory.");
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Art verification camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.944f,.929f,.887f,1);
            camera.orthographic = true;
            camera.nearClipPlane = .01f; camera.farClipPlane = 20;
            camera.allowHDR = false; camera.allowMSAA = true;
            var bookObject = new GameObject("Book render", typeof(IllustratedBook));
            var book = bookObject.GetComponent<IllustratedBook>();
            book.Build(); book.SetBookmark(true, PageSide.Left);
            bookObject.AddComponent<PhysicalBookControls>().Build(null, book);
            var pagePath = Environment.GetEnvironmentVariable("MAESTRO_BOOK_PREVIEW_TEXTURE");
            if (!string.IsNullOrEmpty(pagePath))
            {
                var pages = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!pages.LoadImage(File.ReadAllBytes(pagePath))) throw new InvalidOperationException("Invalid page capture.");
                pages.filterMode = FilterMode.Trilinear; pages.anisoLevel = 4;
                book.SetSurface(pages);
            }
            camera.orthographicSize = .33f;
            camera.transform.position = new Vector3(.11f,.20f,-1.4f);
            camera.transform.LookAt(new Vector3(.045f,-.015f,0));
            Capture(camera, Path.Combine(output,"book-unity.png"), 1440, 1080);
            bookObject.SetActive(false);

            const string modelPath = "Assets/Maestro/Resources/Avatars/DefaultMaestro.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (!prefab) throw new InvalidOperationException("Included Maestro model was not imported.");
            var avatar = UnityEngine.Object.Instantiate(prefab);
            avatar.AddComponent<PencilModelStyle>().Apply();
            var renderers = avatar.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__preview__")).ToArray();
            camera.orthographicSize = bounds.size.y * .57f;
            foreach (var view in new[] { ("front", new Vector3(0,.12f,3)), ("quarter", new Vector3(-1.4f,.12f,3)), ("back", new Vector3(0,.12f,-3)) })
            {
                camera.transform.position = bounds.center + view.Item2;
                camera.transform.LookAt(bounds.center);
                Capture(camera, Path.Combine(output,"maestro-unity-"+view.Item1+".png"), 800, 1000);
            }
            foreach (var clip in clips)
            {
                clip.SampleAnimation(avatar, clip.length * .5f);
                // Synchronous editor captures do not run a player skinning frame.
                // Bake the sampled bones into temporary renderers for each still.
                var poses = new System.Collections.Generic.List<GameObject>();
                foreach (var skin in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var posed = new GameObject("Sampled " + skin.name, typeof(MeshFilter), typeof(MeshRenderer));
                    posed.transform.SetParent(skin.transform, false);
                    var mesh = new Mesh(); skin.BakeMesh(mesh, true);
                    posed.GetComponent<MeshFilter>().sharedMesh = mesh;
                    posed.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false; poses.Add(posed);
                }
                camera.transform.position = bounds.center + new Vector3(-1.2f,.12f,3);
                camera.transform.LookAt(bounds.center);
                Capture(camera, Path.Combine(output,"maestro-motion-"+clip.name.Split('|').Last()+".png"), 800, 1000);
                foreach (var pose in poses) { UnityEngine.Object.DestroyImmediate(pose.GetComponent<MeshFilter>().sharedMesh); UnityEngine.Object.DestroyImmediate(pose); }
                foreach (var skin in avatar.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.enabled = true;
            }
            var shader = Shader.Find("Maestro/Watercolor");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Watercolor shader has compilation errors.");
            File.WriteAllText(Path.Combine(output,"unity-art-evidence.txt"),
                "Editor: " + Application.unityVersion + "\nGraphics: " + SystemInfo.graphicsDeviceType +
                "\nAnimations: " + string.Join(", ", clips.Select(clip => clip.name)) +
                "\nDesktop Unity renders. Not headset performance, passthrough, or store-readiness evidence.\n");
            Debug.Log("MAESTRO_ART_RENDERED " + output);
        }

        static void Capture(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var pixels = new Texture2D(width,height,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
