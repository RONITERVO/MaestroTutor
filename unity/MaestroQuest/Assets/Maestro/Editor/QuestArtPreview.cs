// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Maestro.Quest.Editor
{
    /// <summary>Renders the actual Unity materials and rig, without a headset or live account.</summary>
    public static class QuestArtPreview
    {
        public static void RenderPhysics()
        {
            var output = Environment.GetEnvironmentVariable("MAESTRO_ART_EVIDENCE");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set MAESTRO_ART_EVIDENCE.");
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string directory = Path.Combine(Path.GetTempPath(),"MaestroPhysicsPreview-"+Guid.NewGuid().ToString("N"));
            var root = new GameObject("Physics tools verification");
            try
            {
                var room = root.AddComponent<RoomInteraction>(); var physics = root.AddComponent<RoomPhysicsWorld>();
                RoomItem Included(string name)
                {
                    var value = new GameObject(name); value.transform.SetParent(root.transform,false);
                    var collider = value.AddComponent<BoxCollider>(); var item = value.AddComponent<RoomItem>(); item.Configure(new Collider[] { collider }); return item;
                }
                var book = Included("book"); var maestro = Included("maestro");
                var editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,maestro,directory,physics);
                editor.Create(RoomObjectKind.Ball);
                foreach (var item in root.GetComponentsInChildren<RoomItem>()) item.gameObject.SetActive(false);
                var scan = root.AddComponent<ScannedRoom>(); scan.Initialize(physics);
                var board = new GameObject("Solid physics tools"); board.transform.SetParent(root.transform,false); board.AddComponent<PhysicsTools>().Build(editor,physics,scan,room);
                var camera = new GameObject("Verification camera",typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform,false);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.93f,.91f,.87f,1);
                camera.orthographic = true; camera.orthographicSize = .38f; camera.nearClipPlane = .01f;
                camera.transform.position = new Vector3(0,0,-1); camera.transform.LookAt(Vector3.zero);
                Capture(camera,Path.Combine(output,"physics-tools-unity.png"),1600,1300);
                Debug.Log("MAESTRO_PHYSICS_TOOLS_RENDERED");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        }
        public static void RenderRules()
        {
            var output = Environment.GetEnvironmentVariable("MAESTRO_ART_EVIDENCE");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set MAESTRO_ART_EVIDENCE.");
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string directory = Path.Combine(Path.GetTempPath(),"MaestroRulePreview-"+Guid.NewGuid().ToString("N"));
            var root = new GameObject("Rule tools verification");
            try
            {
                var room = root.AddComponent<RoomInteraction>();
                RoomItem Included(string name)
                {
                    var value = new GameObject(name); value.transform.SetParent(root.transform,false);
                    var collider = value.AddComponent<BoxCollider>(); var item = value.AddComponent<RoomItem>();
                    item.Configure(new Collider[] { collider }); room.Register(item); return item;
                }
                var book = Included("book"); var maestro = Included("maestro");
                var editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,maestro,directory);
                foreach (var item in root.GetComponentsInChildren<RoomItem>()) item.gameObject.SetActive(false);
                var workshop = root.AddComponent<RuleWorkshop>(); workshop.Initialize(editor,directory); workshop.NewSequence(); workshop.AddBinding(); workshop.AddButton(ButtonMount.LeftController);
                var board = new GameObject("Solid rule tools"); board.transform.SetParent(root.transform,false); board.AddComponent<RuleTools>().Build(workshop,room);
                var camera = new GameObject("Verification camera",typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform,false);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.93f,.91f,.87f,1);
                camera.orthographic = true; camera.orthographicSize = .52f; camera.nearClipPlane = .01f;
                camera.transform.position = new Vector3(0,0,-1); camera.transform.LookAt(Vector3.zero);
                Capture(camera,Path.Combine(output,"rule-tools-unity.png"),1600,1450);
                Debug.Log("MAESTRO_RULE_TOOLS_RENDERED");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (Directory.Exists(directory)) Directory.Delete(directory,true);
            }
        }

        public static void RenderToolTray()
        {
            var output = Environment.GetEnvironmentVariable("MAESTRO_ART_EVIDENCE");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set MAESTRO_ART_EVIDENCE.");
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root = new GameObject("Tool tray verification");
            var room = root.AddComponent<RoomInteraction>(); var editor = root.AddComponent<RoomEditor>();
            var tray = new GameObject("Solid creation tools"); tray.AddComponent<RoomToolTray>().Build(editor,room);
            var camera = new GameObject("Verification camera",typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.93f,.91f,.87f,1);
            camera.orthographic = true; camera.orthographicSize = .25f; camera.nearClipPlane = .01f;
            camera.transform.position = new Vector3(.03f,.04f,-1); camera.transform.LookAt(Vector3.zero);
            Capture(camera,Path.Combine(output,"creation-tools-unity.png"),1500,1050);
            Debug.Log("MAESTRO_CREATION_TOOLS_RENDERED");
        }

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
            var previewBrowser = bookObject.AddComponent<NativeBookBrowser>();
            var bookControls = bookObject.AddComponent<PhysicalBookControls>(); bookControls.Build(previewBrowser, book);
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
            // Sample the real notice token with a native-owned message. No Android
            // dialog or permission state is represented by this desktop render.
            typeof(NativeBookBrowser).GetProperty(nameof(NativeBookBrowser.Error)).SetValue(previewBrowser,"Microphone allowed. Resume audio with the bell, then try speaking again.");
            bookControls.SendMessage("Update"); camera.orthographicSize = .46f;
            camera.transform.position = new Vector3(-.10f,.04f,-1.4f); camera.transform.LookAt(new Vector3(-.10f,0,0));
            Capture(camera,Path.Combine(output,"book-permission-notice.png"),1800,1100);
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
            avatar.SetActive(false);
            var spatialPreview = new GameObject("Maestro movement tools preview");
            var spatialRoom = spatialPreview.AddComponent<RoomInteraction>();
            var spatial = spatialPreview.AddComponent<AvatarSpatialMotion>();
            var spatialBoard = new GameObject("Movement controls"); spatialBoard.transform.SetParent(spatialPreview.transform,false);
            spatialBoard.AddComponent<AvatarSpatialTools>().Build(spatial,null,null,null,spatialRoom);
            camera.orthographicSize = .35f; camera.transform.position = new Vector3(0,0,-1); camera.transform.LookAt(Vector3.zero);
            Capture(camera,Path.Combine(output,"maestro-movement-tools.png"),1600,1100);
            UnityEngine.Object.DestroyImmediate(spatialPreview);
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
