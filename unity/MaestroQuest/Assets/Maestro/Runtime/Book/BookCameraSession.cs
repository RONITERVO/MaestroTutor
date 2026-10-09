// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Text.RegularExpressions;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Book {
    [Serializable] public sealed class BookCameraRequest {
        public string session,requestId,acknowledged;
        public long pulse;
    }
    /// <summary>One camera lease with one unacknowledged frame. No durable room effects.</summary>
    internal sealed class BookCameraSession {
        readonly Func<RoomEditor> read;
        readonly string host=Guid.NewGuid().ToString("N");
        string session,requestId="",blockedId="";
        long pulse,revision;
        double seen=double.NegativeInfinity,frameAt;
        JObject frame;
        RoomEditor owner;
        internal BookCameraSession(Func<RoomEditor> read){this.read=read;}
        static bool Token(string value)=>value!=null&&Regex.IsMatch(value,"\\A[a-f0-9]{32}\\z");
        internal void Receive(BookCameraRequest value,double now){
            if(value==null||!Token(value.session)||value.pulse<1||value.requestId==null||value.acknowledged==null||
                (value.requestId.Length>0&&!Token(value.requestId))||(value.acknowledged.Length>0&&!Token(value.acknowledged))){Suspend();return;}
            if(session!=value.session){Suspend();session=value.session;pulse=0;blockedId="";}
            if(value.pulse<=pulse)return;
            pulse=value.pulse;seen=now;
            if(requestId!=value.requestId){frame=null;owner=null;requestId=value.requestId;}
            if(frame!=null&&(string)frame["capture"]["captureId"]==value.acknowledged)frame=null;
        }
        internal void Suspend(){if(requestId.Length>0)blockedId=requestId;frame=null;owner=null;seen=double.NegativeInfinity;}
        internal JObject Poll(double now){
            if(session==null)return null;
            if(now-seen>2)Suspend();
            var editor=read();bool available=editor&&editor.ViewSourceAvailable;
            bool requested=requestId.Length>0,failed=requested&&(requestId==blockedId||!available||now-seen>2);
            if(!ReferenceEquals(owner,null)&&owner!=editor)failed=true;
            if(failed){blockedId=requestId;frame=null;owner=null;}
            if(requested&&!failed){
                owner=editor;
                // Retransmit only while fresh. Never queue old images behind a slow page.
                if(frame!=null&&now-frameAt>2)frame=null;
                if(frame==null&&editor.CanCaptureView(out _)){
                    if(editor.CaptureCameraView(out frame,out _))frameAt=now;
                    else {failed=true;blockedId=requestId;}
                }
            }
            return new JObject{["version"]=1,["session"]=session,["host"]=host,["revision"]=++revision,
                ["requestId"]=requestId,["available"]=available,["status"]=failed?"failed":requested?"streaming":"ready",["frame"]=frame?.DeepClone()};
        }
    }
}
