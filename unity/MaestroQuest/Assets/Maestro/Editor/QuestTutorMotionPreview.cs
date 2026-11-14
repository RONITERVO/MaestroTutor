// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using UniGLTF;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Maestro.Quest.Editor
{
    /// <summary>Audition exact packaged clips on the shipped model before curating tutor defaults.</summary>
    public static class QuestTutorMotionPreview
    {
        public static void Render()
        {
            string output=Environment.GetEnvironmentVariable("MAESTRO_ART_EVIDENCE");
            if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Set MAESTRO_ART_EVIDENCE.");
            Directory.CreateDirectory(output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var bundle=BundledMotions.FromApplication();bundle.Verify();var included=BundledAvatar.FromApplication();
            if(bundle.AvatarHash!=included.Hash)throw new InvalidOperationException("Motion package does not match the avatar.");
            var manifest=Resources.Load<TextAsset>(BundledMotions.ResourcePath);
            var entries=(JArray)JObject.Parse(manifest.text)["catalogue"]["entries"];Resources.UnloadAsset(manifest);
            string requested=Environment.GetEnvironmentVariable("MAESTRO_MOTION_PREVIEW_IDS");
            var ids=(requested??"").Split(',',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).ToHashSet();
            if(ids.Count==0)throw new InvalidOperationException("Set MAESTRO_MOTION_PREVIEW_IDS to exact catalogue IDs.");
            var selected=entries.Where(x=>ids.Contains((string)x["id"])).ToArray();
            if(selected.Length!=ids.Count)throw new InvalidOperationException("Unknown motion preview ID.");
            var root=new GameObject("Packaged tutor audition");var model=root.AddComponent<ImportedModel>();
            var cameraRoot=new GameObject("Motion preview camera",typeof(Camera));var camera=cameraRoot.GetComponent<Camera>();
            try {
                model.LoadAsync(included.Read(),new ImmediateCaller()).GetAwaiter().GetResult();model.FitAsMaestro();
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.944f,.929f,.887f,1);
                camera.orthographic=true;camera.orthographicSize=.95f;camera.nearClipPlane=.01f;camera.farClipPlane=20;
                camera.transform.position=new Vector3(-.7f,.95f,3);camera.transform.LookAt(new Vector3(0,.82f,0));
                var report=new JArray();
                foreach(var entry in selected) {
                    var pack=MotionPack.Read(File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,BundledMotions.RelativeDirectory+(string)entry["hash"]+".motion")));
                    if(pack.RigHash!=model.MotionRigHash)throw new InvalidOperationException("Preview rig mismatch.");
                    var clip=MotionClipCompiler.CompileAsync(pack,awaitCaller:new ImmediateCaller()).GetAwaiter().GetResult();
                    try {
                        RenderStrip(camera,model,clip,Path.Combine(output,(string)entry["name"]+"-"+(string)entry["id"]+".png"));
                        report.Add(new JObject {["id"]=entry["id"].DeepClone(),["name"]=entry["name"].DeepClone(),["duration"]=clip.length,["fractions"]=new JArray(0,.125,.25,.375,.5,.625,.75,.875,1)});
                    } finally {UnityEngine.Object.DestroyImmediate(clip);}
                }
                File.WriteAllText(Path.Combine(output,"tutor-motion-preview.json"),new JObject {["avatarHash"]=included.Hash,["manifestHash"]=bundle.Hash,["rigHash"]=model.MotionRigHash,["motions"]=report,["note"]="Native desktop samples; not headset or continuous playback acceptance."}.ToString());
                Debug.Log("MAESTRO_TUTOR_MOTIONS_RENDERED "+selected.Length);
            } finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraRoot);}
        }
        static void RenderStrip(Camera camera,ImportedModel model,AnimationClip clip,string path)
        {
            const int width=240,height=360,count=9;
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=4};
            var pixels=new Texture2D(width*count,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            var content=model.Instance.transform;var position=content.localPosition;var rotation=content.localRotation;var scale=content.localScale;
            try {
                for(int frame=0;frame<count;frame++) {
                    model.Stop();clip.wrapMode=WrapMode.ClampForever;clip.SampleAnimation(content.gameObject,clip.length*frame/(count-1));
                    content.SetLocalPositionAndRotation(position,rotation);content.localScale=scale;
                    var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();var baked=new GameObject[skins.Length];
                    try {
                        for(int i=0;i<skins.Length;i++) {
                            var skin=skins[i];var posed=new GameObject("Sampled "+skin.name,typeof(MeshFilter),typeof(MeshRenderer));baked[i]=posed;posed.transform.SetParent(skin.transform,false);
                            var mesh=new Mesh();skin.BakeMesh(mesh,false);posed.GetComponent<MeshFilter>().sharedMesh=mesh;posed.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
                        }
                        camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,width,height),frame*width,0);
                    } finally {foreach(var pose in baked)if(pose){UnityEngine.Object.DestroyImmediate(pose.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(pose);}foreach(var skin in skins)skin.enabled=true;}
                }
                pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            } finally {camera.targetTexture=null;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
        }
    }
}
