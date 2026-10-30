// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class QuestIntegrityTests
    {
        sealed class Api:IQuestPlatformApi
        {
            public Action<bool> Initialized,Entitled; public Action<string> Proof;
            public int Initializations,Entitlements,Proofs;
            public void Initialize(string id,Action<bool> done){Initializations++;Initialized=done;}
            public void Entitlement(Action<bool> done){Entitlements++;Entitled=done;}
            public void Integrity(string nonce,Action<string> done){Proofs++;Proof=done;}
        }
        [Test] public void DevelopmentWithoutIdentityDoesNotPretendToBeVerifiedOrTerminate()
        {
            var api=new Api();var session=new QuestPlatformSession(api,()=>0,false);session.Begin(false,"");
            Assert.That(session.Status,Is.EqualTo(QuestPlatformStatus.Unavailable));Assert.That(session.MustExit,Is.False);Assert.That(api.Initializations,Is.Zero);
            string error=null;session.Integrity(new string('a',43),(token,e)=>error=e);Assert.That(error,Is.EqualTo("not-configured"));Assert.That(api.Proofs,Is.Zero);
        }
        [TestCase(false,"")] [TestCase(true,"invalid")] public void ReleaseCannotRunWithMissingOrInvalidPlatformConfiguration(bool enabled,string appId)
        {var session=new QuestPlatformSession(new Api(),()=>0,true);session.Begin(enabled,appId);Assert.That(session.MustExit,Is.True);}
        [Test] public void InitializationAndEntitlementMustBothSucceedBeforeAnyProof()
        {
            var api=new Api();var session=new QuestPlatformSession(api,()=>0,true);session.Begin(true,"123456");
            session.Integrity(new string('a',43),(t,e)=>Assert.That(e,Is.EqualTo("unavailable")));Assert.That(api.Proofs,Is.Zero);
            api.Initialized(true);Assert.That(api.Entitlements,Is.EqualTo(1));Assert.That(session.Status,Is.EqualTo(QuestPlatformStatus.Starting));
            api.Entitled(true);Assert.That(session.Status,Is.EqualTo(QuestPlatformStatus.Ready));Assert.That(session.MustExit,Is.False);
            string proof=null;session.Integrity(new string('a',43),(t,e)=>proof=t);api.Proof("header.payload.signature");Assert.That(proof,Is.EqualTo("header.payload.signature"));
        }
        [Test] public void FailedAndLateEntitlementsCannotOpenARelease()
        {
            foreach(bool failure in new[]{false,true}){
                double now=0;var api=new Api();var session=new QuestPlatformSession(api,()=>now,true);session.Begin(true,"123456");api.Initialized(true);
                if(failure)api.Entitled(false);else {now=8;session.Tick();api.Entitled(true);}
                Assert.That(session.MustExit,Is.True);Assert.That(session.Status,Is.EqualTo(QuestPlatformStatus.Failed));
            }
        }
        [Test] public void FailedInitializationAndLateCallbacksStayClosed()
        {
            double now=0;var api=new Api();var session=new QuestPlatformSession(api,()=>now,true);session.Begin(true,"123456");var first=api.Initialized;
            api.Initialized(false);Assert.That(session.MustExit,Is.True);first(true);Assert.That(api.Entitlements,Is.Zero);
            session.Begin(true,"123456");first(true);Assert.That(api.Entitlements,Is.Zero);
            now=9;api.Initialized(true);Assert.That(session.MustExit,Is.True);
        }
        [Test] public void ConcurrentProofsAreBoundedAndLateTimedOutProofsAreIgnored()
        {
            double now=0;var api=new Api();var session=new QuestPlatformSession(api,()=>now,true);session.Begin(true,"123456");api.Initialized(true);api.Entitled(true);
            int completed=0;session.Integrity(new string('a',43),(t,e)=>completed++);var old=api.Proof;
            session.Integrity(new string('b',43),(t,e)=>Assert.That(e,Is.EqualTo("unavailable")));Assert.That(api.Proofs,Is.EqualTo(1));
            now=16;session.Integrity(new string('c',43),(t,e)=>completed++);old("old");Assert.That(completed,Is.Zero);
            api.Proof("new");Assert.That(completed,Is.EqualTo(1));session.Dispose();api.Proof("late");Assert.That(completed,Is.EqualTo(1));
        }
        static QuestIntegrityRequest Request(int revision=1,string session=null)=>new QuestIntegrityRequest {revision=revision,session=session??new string('a',32),nonce=new string('a',43)};
        [Test] public void RepeatedPollingPublishesTheSameReplyWithoutAskingForAnotherProof()
        {
            Action<string,string> done=null;int calls=0,published=0;string reply=null;
            var exchange=new QuestIntegrityExchange((nonce,callback)=>{calls++;done=callback;});var request=Request();
            exchange.Poll(request,0,json=>published++);exchange.Poll(request,1,json=>published++);Assert.That(calls,Is.EqualTo(1));Assert.That(published,Is.Zero);
            done("header.body.signature",null);exchange.Poll(request,2,json=>reply=json);exchange.Poll(request,3,json=>Assert.That(json,Is.EqualTo(reply)));
            Assert.That(JsonUtility.FromJson<QuestIntegrityResult>(reply).token,Is.EqualTo("header.body.signature"));Assert.That(calls,Is.EqualTo(1));
        }
        [Test] public void ReplacedDocumentAndSuspensionDiscardOldProofs()
        {
            Action<string,string> done=null;var exchange=new QuestIntegrityExchange((n,c)=>done=c);string reply=null;
            exchange.Poll(Request(),0,j=>reply=j);var old=done;exchange.Clear();old("old.proof.value",null);Assert.That(reply,Is.Null);
            exchange.Poll(Request(1,new string('b',32)),1,j=>reply=j);old("old.proof.value",null);exchange.Poll(Request(1,new string('b',32)),2,j=>reply=j);Assert.That(reply,Is.Null);
            done("new.proof.value",null);exchange.Poll(Request(1,new string('b',32)),3,j=>reply=j);Assert.That(JsonUtility.FromJson<QuestIntegrityResult>(reply).token,Is.EqualTo("new.proof.value"));
        }
        [Test] public void TimeoutsAndChangedNonceAreTerminalForTheCurrentRequest()
        {
            int calls=0;Action<string,string> done=null;string reply=null;var exchange=new QuestIntegrityExchange((n,c)=>{calls++;done=c;});var request=Request();
            exchange.Poll(request,0,j=>reply=j);exchange.Poll(request,15,j=>reply=j);done("late.proof.value",null);
            Assert.That(JsonUtility.FromJson<QuestIntegrityResult>(reply).error,Is.EqualTo("unavailable"));
            request.nonce=new string('b',43);exchange.Poll(request,16,j=>reply=j);exchange.Poll(request,17,j=>reply=j);Assert.That(calls,Is.EqualTo(1));
        }
        [TestCase(null)] [TestCase("too short")] [TestCase("header.body.signature-with-!")]
        public void InvalidTokensNeverCrossTheBookBoundary(string token)
        {
            string reply=null;var exchange=new QuestIntegrityExchange((n,c)=>c(token,null));exchange.Poll(Request(),0,j=>reply=j);
            var result=JsonUtility.FromJson<QuestIntegrityResult>(reply);Assert.That(result.token,Is.Null.Or.Empty);Assert.That(result.error,Is.EqualTo("unavailable"));
        }
    }
}
