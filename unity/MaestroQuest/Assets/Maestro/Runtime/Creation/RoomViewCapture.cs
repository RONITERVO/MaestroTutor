// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>One on-demand virtual-only image. Pixels travel separately from recurring observations.</summary>
    public sealed partial class RoomEditor {
        public const string ViewCaptureFeature="roomViewCapture.v1";
        internal const int ViewWidth=512,ViewHeight=384,ViewByteLimit=98304;
        const int CaptureLayer=31;
        JObject viewMetadata;
        string viewData;
        float nextViewCapture,viewExpires;
        bool capturingView;
        internal JObject ViewCaptureMetadata=>viewMetadata!=null&&Time.unscaledTime<viewExpires?(JObject)viewMetadata.DeepClone():null;
        internal void ClearViewCapture(){viewMetadata=null;viewData=null;}
        internal JObject ViewCapturePayload(string session,string acknowledged){
            var metadata=ViewCaptureMetadata;if(metadata==null||(string)metadata["captureId"]==acknowledged)return null;
            return new JObject{["version"]=1,["revision"]=1,["session"]=session,["capture"]=metadata,["data"]=viewData};
        }
        internal bool CanCaptureView(out string error){
            error="The virtual room view is unavailable";
            if(!isActiveAndEnabled||!Viewer||Ownership.Suspended||RuntimeGate.Held||WriteGate.Frozen)return false;
            if(!RoomRecipe.Finite(Viewer.position)||!RoomRecipe.Finite(Viewer.forward)){error="The viewer pose is unavailable";return false;}
            if(capturingView||Time.unscaledTime<nextViewCapture){error="Wait one second between virtual room snapshots";return false;}
            error=null;return true;
        }
        internal bool CaptureView(out JObject result,out string error){
            result=null;if(!CanCaptureView(out error))return false;
            capturingView=true;nextViewCapture=Time.unscaledTime+1;
            GameObject cameraObject=null;RenderTexture target=null;Texture2D pixels=null;var previous=RenderTexture.active;
            var changed=new Dictionary<GameObject,int>();
            try{
                var book=Find("book");var selected=new HashSet<Renderer>();
                foreach(var item in objects.Where(pair=>pair.Key!="book").Select(pair=>pair.Value).Where(item=>item))
                    foreach(var renderer in item.GetComponentsInChildren<Renderer>())
                        if(renderer&&renderer.enabled&&renderer.gameObject.activeInHierarchy&&(!book||!renderer.transform.IsChildOf(book.transform)))selected.Add(renderer);
                var rules=GetComponent<Maestro.Quest.Rules.RoomRules>();if(rules)foreach(var renderer in rules.ViewButtonRenderers)selected.Add(renderer);
                if(selected.Count>1024)throw new InvalidOperationException("This view has too many renderers to capture");
                // The temporary layer is private to this synchronous render. Never include an unrelated renderer.
                if(UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Any(r=>r.enabled&&r.gameObject.layer==CaptureLayer&&!selected.Contains(r)))
                    throw new InvalidOperationException("The snapshot render layer is in use");
                foreach(var renderer in selected)if(!changed.ContainsKey(renderer.gameObject)){changed.Add(renderer.gameObject,renderer.gameObject.layer);renderer.gameObject.layer=CaptureLayer;}
                cameraObject=new GameObject("Virtual room snapshot",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;
                camera.transform.SetPositionAndRotation(Viewer.position,Viewer.rotation);camera.stereoTargetEye=StereoTargetEyeMask.None;
                camera.cullingMask=1<<CaptureLayer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.91f,.93f,.94f,1);
                camera.nearClipPlane=.03f;camera.farClipPlane=30;camera.fieldOfView=60;camera.aspect=(float)ViewWidth/ViewHeight;camera.allowHDR=false;camera.allowMSAA=false;camera.useOcclusionCulling=false;
                target=RenderTexture.GetTemporary(ViewWidth,ViewHeight,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);camera.targetTexture=target;using(new Maestro.Quest.Interaction.RoomDepthOcclusion.VirtualCapture())camera.Render();
                RenderTexture.active=target;pixels=new Texture2D(ViewWidth,ViewHeight,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,ViewWidth,ViewHeight),0,0);pixels.Apply();
                byte[] bytes=null;foreach(int quality in new[]{75,50,30}){bytes=pixels.EncodeToJPG(quality);if(bytes.Length<=ViewByteLimit)break;}
                if(bytes==null||bytes.Length==0||bytes.Length>ViewByteLimit)throw new InvalidOperationException("This snapshot exceeds its image budget");
                string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                var position=transform.InverseTransformPoint(camera.transform.position);var rotation=(Quaternion.Inverse(transform.rotation)*camera.transform.rotation).normalized;
                var metadata=new JObject{["captureId"]=Guid.NewGuid().ToString("N"),["sha256"]=hash,["mimeType"]="image/jpeg",["width"]=ViewWidth,["height"]=ViewHeight,
                    ["capturedAt"]=DateTime.UtcNow.ToString("O"),["sceneRevision"]=Revision,["verticalFov"]=60,
                    ["position"]=new JObject{["x"]=position.x,["y"]=position.y,["z"]=position.z},["rotation"]=new JObject{["x"]=rotation.x,["y"]=rotation.y,["z"]=rotation.z,["w"]=rotation.w}};
                viewData=Convert.ToBase64String(bytes);viewMetadata=metadata;viewExpires=Time.unscaledTime+120;result=(JObject)metadata.DeepClone();return true;
            }catch(Exception ex) when(ex is InvalidOperationException||ex is ArgumentException||ex is UnityException){error="Virtual room snapshot failed: "+ex.Message;return false;}
            finally{
                RenderTexture.active=previous;if(cameraObject){cameraObject.GetComponent<Camera>().targetTexture=null;Destroy(cameraObject);}if(target)RenderTexture.ReleaseTemporary(target);if(pixels)Destroy(pixels);
                foreach(var pair in changed)if(pair.Key)pair.Key.layer=pair.Value;capturingView=false;
            }
        }
    }
}
