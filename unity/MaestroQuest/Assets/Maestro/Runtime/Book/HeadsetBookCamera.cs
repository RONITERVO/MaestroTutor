// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Security.Cryptography;
using Meta.XR;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
namespace Maestro.Quest.Book {
    /// <summary>Explicit camera selection owns the sensor. Permission results never start it.</summary>
    internal sealed class HeadsetBookCamera:MonoBehaviour,IBookCameraFeed {
        const string PermissionName="horizonos.permission.HEADSET_CAMERA";
        ARCameraManager passthrough;
        PassthroughCameraAccess sensor;
        GameObject sensorNode;
        Texture2D pixels;
        bool active,inFlight,available;
        int epoch;
        double started,nextCapture,supportCheckedAt=double.NegativeInfinity;
        DateTime lastImage;
        JObject pending;
        string failure;
        internal void Initialize(ARCameraManager value){passthrough=value;}
        public string SourceId=>BookCameraSession.HeadsetSource;
        public bool Available {
            get {
#if UNITY_ANDROID && !UNITY_EDITOR
                // Awake may run before XR identifies the headset. A negative result
                // must not hide the camera for the rest of that app session.
                double now=Time.realtimeSinceStartupAsDouble;
                if(now-supportCheckedAt<5)return available;
                supportCheckedAt=now;
                try{available=SystemInfo.supportsAsyncGPUReadback&&PassthroughCameraAccess.IsSupported;}
                catch(Exception exception) when(CaptureFailure(exception)){available=false;}
                return available;
#else
                return false;
#endif
            }
        }
        static bool CaptureFailure(Exception exception)=>exception is ArgumentException||exception is UnityException||exception is InvalidOperationException||exception is AndroidJavaException;
        public bool StartCapture(out string error){
            Stop();error=null;
            try{return StartCamera(out error);}
            catch(Exception exception) when(CaptureFailure(exception)){Stop();error="camera-unavailable";return false;}
        }
        bool StartCamera(out string error){
            error=null;
            if(!Available){error="camera-unavailable";return false;}
            if(!passthrough||!passthrough.enabled){error="passthrough-required";return false;}
            if(!Permission.HasUserAuthorizedPermission(PermissionName)){
                // A grant (even one received while backgrounded) changes Android state only.
                // The old lease fails; only another explicit source selection may start capture.
                Permission.RequestUserPermission(PermissionName);error="permission-required";return false;
            }
            sensorNode=new GameObject("Selected headset camera");var node=sensorNode;node.SetActive(false);node.transform.SetParent(transform,false);
            sensor=node.AddComponent<PassthroughCameraAccess>();sensor.enabled=false;
            sensor.CameraPosition=PassthroughCameraAccess.CameraPositionType.Left;sensor.RequestedResolution=new Vector2Int(1280,960);sensor.MaxFramerate=15;
            active=true;started=Time.realtimeSinceStartupAsDouble;lastImage=default;node.SetActive(true);sensor.enabled=true;return true;
        }
        public bool Frame(out JObject image,out string error){
            image=null;error=null;
            try{return ReadFrame(out image,out error);}
            catch(Exception exception) when(CaptureFailure(exception)){Stop();error="camera-unavailable";return false;}
        }
        bool ReadFrame(out JObject image,out string error){
            image=null;error=null;if(!active)return false;
            if(!Permission.HasUserAuthorizedPermission(PermissionName)||!passthrough||!passthrough.enabled){error="camera-unavailable";return false;}
            if(failure!=null){error=failure;return false;}
            if(!sensor||!sensor.enabled){error="camera-unavailable";return false;}
            double now=Time.realtimeSinceStartupAsDouble;
            if(!sensor.IsPlaying){if(now-started>8)error="camera-stale";return false;}
            var age=(DateTime.UtcNow-sensor.Timestamp).TotalSeconds;
            if(age< -1||age>2){error="camera-stale";return false;}
            if(pending!=null){
                var ready=pending;pending=null;
                if((DateTime.UtcNow-(DateTime)ready["capture"]["capturedAt"]).TotalSeconds<=2){image=ready;return true;}
            }
            if(inFlight||now<nextCapture||sensor.Timestamp==lastImage)return false;
            var texture=sensor.GetTexture();if(!texture)return false;
            lastImage=sensor.Timestamp;nextCapture=now+1;
            QueueImage(texture,lastImage);
            return false;
        }
        void QueueImage(Texture texture,DateTime capturedAt){
            var size=OutputSize(texture.width,texture.height);var target=RenderTexture.GetTemporary(size.x,size.y,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            int owner=epoch;inFlight=true;
            try{
                Graphics.Blit(texture,target);
                AsyncGPUReadback.Request(target,0,TextureFormat.RGBA32,request=>{
                    try{
                        if(owner!=epoch||!active)return;
                        if(request.hasError){failure="camera-unavailable";return;}
                        if((DateTime.UtcNow-capturedAt).TotalSeconds>2)return;
                        if(!pixels||pixels.width!=size.x||pixels.height!=size.y){if(pixels)Destroy(pixels);pixels=new Texture2D(size.x,size.y,TextureFormat.RGBA32,false);}
                        pixels.LoadRawTextureData(request.GetData<byte>());pixels.Apply();
                        pending=Encode(pixels,capturedAt);
                        if(pending==null)failure="camera-unavailable";
                    }catch(Exception exception) when(CaptureFailure(exception)){failure="camera-unavailable";}
                    finally{RenderTexture.ReleaseTemporary(target);inFlight=false;}
                });
            }catch{RenderTexture.ReleaseTemporary(target);inFlight=false;throw;}
        }
        internal static Vector2Int OutputSize(int width,int height){float scale=Mathf.Min(1,512f/Mathf.Max(width,height));return new Vector2Int(Mathf.Max(1,Mathf.RoundToInt(width*scale)),Mathf.Max(1,Mathf.RoundToInt(height*scale)));}
        internal static JObject Encode(Texture2D image,DateTime capturedAt){
            byte[] bytes=null;foreach(int quality in new[]{75,50,30}){bytes=image.EncodeToJPG(quality);if(bytes.Length<=Creation.RoomEditor.ViewByteLimit)break;}
            if(bytes==null||bytes.Length==0||bytes.Length>Creation.RoomEditor.ViewByteLimit)return null;
            string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            return new JObject{["sourceId"]=BookCameraSession.HeadsetSource,["capture"]=new JObject{
                ["captureId"]=Guid.NewGuid().ToString("N"),["sha256"]=hash,["mimeType"]="image/jpeg",["width"]=image.width,["height"]=image.height,["capturedAt"]=capturedAt.ToUniversalTime().ToString("O")},["data"]=Convert.ToBase64String(bytes)};
        }
        public void Stop(){
            active=false;epoch++;pending=null;failure=null;nextCapture=0;
            if(sensorNode){sensorNode.SetActive(false);Destroy(sensorNode);sensorNode=null;}sensor=null;
            if(pixels){Destroy(pixels);pixels=null;}
        }
        void OnDisable()=>Stop();
        void OnApplicationPause(bool value){if(value)Stop();}
        void OnApplicationFocus(bool value){if(!value)Stop();}
    }
}
