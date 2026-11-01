// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Book {
    internal sealed class ScreenShareBookCamera:IBookCameraFeed,IBookCameraContextFeed {
        readonly NativeBookBrowser browser;
        string context;
        double readyAt;
        internal ScreenShareBookCamera(NativeBookBrowser browser){this.browser=browser;}
        public string SourceId=>BookCameraSession.MixedSource;
        public bool Available=>Application.platform==RuntimePlatform.Android&&browser&&browser.IsReady;
        public void Context(string value){if(context==value)return;context=value;browser.ScreenShareCommand("SetScreenShareContext",value);}
        public bool StartCapture(out string error){
            error=null;if(!Available){error="screen-share-unavailable";return false;}
            readyAt=Time.realtimeSinceStartupAsDouble+.25;browser.ScreenShareCommand("StartScreenShare");return true;
        }
        public bool Frame(out JObject image,out string error){
            image=null;error=null;if(Time.realtimeSinceStartupAsDouble<readyAt)return false;
            try{var state=JObject.Parse(browser.ReadScreenShare());error=(string)state["error"];image=state["frame"] as JObject;return image!=null;}
            catch(System.Exception){error="screen-share-unavailable";return false;}
        }
        public void Stop(){browser.ScreenShareCommand("StopScreenShare");}
    }
}
