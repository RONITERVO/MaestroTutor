// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Maestro.Quest.Book
{
    [Serializable] public sealed class QuestIntegrityRequest { public string session, nonce; public int revision; }
    [Serializable] public sealed class QuestIntegrityResult { public int version=1; public string session, token, error; public int revision; }

    /// <summary>A proof belongs to one request in one live top-level document.</summary>
    public sealed class QuestIntegrityExchange
    {
        readonly Action<string,Action<string,string>> acquire;
        QuestIntegrityRequest current;
        string reply;
        int epoch;
        double deadline;
        public QuestIntegrityExchange(Action<string,Action<string,string>> acquire) { this.acquire=acquire; }
        public void Clear() { epoch++; current=null; reply=null; }
        public void Poll(QuestIntegrityRequest request,double now,Action<string> publish)
        {
            if (!Valid(request)) { Clear(); return; }
            if (current!=null && current.session==request.session && current.revision==request.revision)
            {
                // Reusing an ID for a different nonce is never a new operation.
                if (current.nonce!=request.nonce) { epoch++; reply=null; Complete(epoch,null,"unavailable"); publish(reply); return; }
                if (reply==null && now>=deadline) Complete(epoch,null,"unavailable");
                if (reply!=null) publish(reply);
                return;
            }
            Clear();
            current=new QuestIntegrityRequest {session=request.session,revision=request.revision,nonce=request.nonce};
            deadline=now+15; int owner=epoch;
            try { acquire(request.nonce,(token,error)=>Complete(owner,token,error)); }
            catch(Exception) { Complete(owner,null,"unavailable"); }
            if(reply!=null)publish(reply);
        }
        void Complete(int owner,string token,string error)
        {
            if(owner!=epoch||current==null||reply!=null)return;
            bool valid=string.IsNullOrEmpty(error)&&!string.IsNullOrEmpty(token)&&token.Length<=32768&&Regex.IsMatch(token,@"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$");
            reply=JsonUtility.ToJson(new QuestIntegrityResult {session=current.session,revision=current.revision,token=valid?token:null,error=valid?null:error=="not-configured"?error:"unavailable"});
        }
        public static bool Valid(QuestIntegrityRequest request)=>request!=null&&request.revision>0&&request.session!=null&&Regex.IsMatch(request.session,"^[a-f0-9]{32}$")&&request.nonce!=null&&Regex.IsMatch(request.nonce,"^[A-Za-z0-9_-]{43}$");
    }
}
