// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Text.RegularExpressions;

namespace Maestro.Quest.Book
{
    public interface IQuestPlatformApi
    {
        void Initialize(string appId,Action<bool> complete);
        void Entitlement(Action<bool> complete);
        void Integrity(string nonce,Action<string> complete);
    }
    public enum QuestPlatformStatus { Unavailable, Starting, Ready, Failed }
    public sealed class QuestPlatformSession
    {
        readonly IQuestPlatformApi api;
        readonly Func<double> now;
        readonly bool requireEntitlement;
        double deadline;
        int epoch,proofGeneration;
        bool proofPending;
        double proofDeadline;
        public QuestPlatformStatus Status {get;private set;}
        public bool MustExit=>requireEntitlement&&(Status==QuestPlatformStatus.Failed||Status==QuestPlatformStatus.Unavailable);
        public QuestPlatformSession(IQuestPlatformApi api,Func<double> now,bool requireEntitlement) {this.api=api;this.now=now;this.requireEntitlement=requireEntitlement;}
        public void Begin(bool enabled,string appId)
        {
            epoch++;proofGeneration++;proofPending=false;int owner=epoch;
            if(!enabled||appId==null||!Regex.IsMatch(appId,"^[1-9][0-9]{0,29}$")){Status=QuestPlatformStatus.Unavailable;return;}
            Status=QuestPlatformStatus.Starting;deadline=now()+8;
            try {api.Initialize(appId,ok=>{
                if(owner!=epoch||Status!=QuestPlatformStatus.Starting)return;
                if(!ok||now()>=deadline){Status=QuestPlatformStatus.Failed;return;}
                try {api.Entitlement(entitled=>{
                    if(owner!=epoch||Status!=QuestPlatformStatus.Starting)return;
                    Status=entitled&&now()<deadline?QuestPlatformStatus.Ready:QuestPlatformStatus.Failed;
                });}catch(Exception){Status=QuestPlatformStatus.Failed;}
            });}catch(Exception){Status=QuestPlatformStatus.Failed;}
        }
        public void Tick(){if(Status==QuestPlatformStatus.Starting&&now()>=deadline){epoch++;Status=QuestPlatformStatus.Failed;}}
        public void Integrity(string nonce,Action<string,string> complete)
        {
            Tick();
            if(Status!=QuestPlatformStatus.Ready){complete(null,Status==QuestPlatformStatus.Unavailable?"not-configured":"unavailable");return;}
            if(nonce==null||!Regex.IsMatch(nonce,"^[A-Za-z0-9_-]{43}$")){complete(null,"unavailable");return;}
            if(proofPending&&now()<proofDeadline){complete(null,"unavailable");return;}
            int owner=epoch,generation=++proofGeneration;proofPending=true;proofDeadline=now()+15;
            try{api.Integrity(nonce,token=>{if(owner==epoch&&generation==proofGeneration&&Status==QuestPlatformStatus.Ready){proofPending=false;complete(token,string.IsNullOrEmpty(token)?"unavailable":null);}});}
            catch(Exception){proofPending=false;complete(null,"unavailable");}
        }
        public void Dispose(){epoch++;proofGeneration++;proofPending=false;Status=QuestPlatformStatus.Unavailable;}
    }
}
