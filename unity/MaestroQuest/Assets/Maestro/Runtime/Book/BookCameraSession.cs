// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Text.RegularExpressions;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Book {
    [Serializable] public sealed class BookCameraRequest {
        public string session,requestId,acknowledged,sourceId=BookCameraSession.VirtualSource;
        public long pulse;
    }
    internal interface IBookCameraFeed {
        string SourceId {get;}
        bool Available {get;}
        bool Start(out string error);
        bool Frame(out JObject image,out string error);
        void Stop();
    }
    /// <summary>One selected source, one expiring lease and one unacknowledged image.</summary>
    internal sealed class BookCameraSession {
        internal const string VirtualSource="maestro-camera:virtual-scene",HeadsetSource="maestro-camera:headset-camera";
        readonly Func<RoomEditor> read;
        readonly IBookCameraFeed device;
        readonly string host=Guid.NewGuid().ToString("N");
        string session,requestId="",sourceId="",blockedId="",error;
        long pulse,revision;
        double seen=double.NegativeInfinity,frameAt;
        JObject frame;
        RoomEditor owner;
        bool started;
        internal BookCameraSession(Func<RoomEditor> read,IBookCameraFeed device=null){this.read=read;this.device=device;}
        static bool Token(string value)=>value!=null&&Regex.IsMatch(value,"\\A[a-f0-9]{32}\\z");
        void Release(){device?.Stop();started=false;frame=null;owner=null;}
        internal void Receive(BookCameraRequest value,double now){
            if(value==null||!Token(value.session)||value.pulse<1||value.requestId==null||value.acknowledged==null||
                (value.requestId.Length>0&&(!Token(value.requestId)||(value.sourceId!=VirtualSource&&(device==null||value.sourceId!=device.SourceId))))||
                (value.acknowledged.Length>0&&!Token(value.acknowledged))){Suspend();return;}
            if(session!=value.session){Suspend();session=value.session;pulse=0;blockedId="";}
            if(value.pulse<=pulse)return;
            pulse=value.pulse;seen=now;
            if(requestId!=value.requestId){Release();error=null;requestId=value.requestId;sourceId=value.sourceId;}
            else if(requestId.Length>0&&sourceId!=value.sourceId){Suspend();return;}
            if(frame!=null&&(string)frame["capture"]["captureId"]==value.acknowledged)frame=null;
        }
        internal void Suspend(){if(requestId.Length>0)blockedId=requestId;Release();seen=double.NegativeInfinity;}
        internal JObject Poll(double now){
            if(session==null)return null;
            if(now-seen>2)Suspend();
            var editor=read();bool available=editor&&editor.ViewSourceAvailable;
            var sources=new JArray();if(available){sources.Add(VirtualSource);if(device?.Available==true)sources.Add(device.SourceId);}
            bool requested=requestId.Length>0,physical=device!=null&&sourceId==device.SourceId;
            bool failed=requested&&(requestId==blockedId||!available||now-seen>2||(physical&&device.Available!=true));
            if(!ReferenceEquals(owner,null)&&owner!=editor)failed=true;
            if(requested&&!failed){
                owner=editor;
                if(!started){started=true;if(physical&&!device.Start(out error))failed=true;}
                if(!failed){
                    if(frame!=null&&now-frameAt>2)frame=null;
                    if(frame==null){
                        if(physical){if(device.Frame(out frame,out var captureError))frameAt=now;else if(captureError!=null){error=captureError;failed=true;}}
                        else if(editor.CanCaptureView(out _)){
                            if(editor.CaptureCameraView(out frame,out _)){frame["sourceId"]=VirtualSource;frameAt=now;}
                            else failed=true;
                        }
                    }
                }
            }
            if(failed){blockedId=requestId;Release();}
            return new JObject{["version"]=1,["session"]=session,["host"]=host,["revision"]=++revision,
                ["requestId"]=requestId,["sourceId"]=sourceId,["sources"]=sources,["status"]=failed?"failed":requested?"streaming":"ready",
                ["error"]=failed?error:null,["frame"]=frame?.DeepClone()};
        }
    }
}
