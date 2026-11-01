// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class FilePublicationTests
    {
        string directory,staged,target,backup;
        static IOException Error(int code)=>new IOException("Publication test failure",unchecked((int)0x80070000)|code);
        [SetUp] public void Before(){directory=Path.Combine(Path.GetTempPath(),"MaestroFilePublication-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);staged=Path.Combine(directory,"next");target=Path.Combine(directory,"current");backup=Path.Combine(directory,"backup");File.WriteAllText(staged,"new");File.WriteAllText(target,"old");}
        [TearDown] public void After(){Assert.That(Path.GetFullPath(directory),Does.StartWith(Path.GetFullPath(Path.GetTempPath())));if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [Test] public void SuccessfulPublicationDoesNotWaitAndKeepsTheRequestedBackup()
        {
            int waits=0;FilePublication.Replace(staged,target,backup,true,File.Replace,_=>waits++);
            Assert.That(waits,Is.Zero);Assert.That(File.ReadAllText(target),Is.EqualTo("new"));Assert.That(File.ReadAllText(backup),Is.EqualTo("old"));Assert.That(File.Exists(staged),Is.False);
        }
        [TestCase(32)] [TestCase(33)] [TestCase(1175)]
        public void TransientWindowsFailurePublishesTheSameStagedBytesOnce(int code)
        {
            var waits=new List<int>();int calls=0;
            FilePublication.Replace(staged,target,backup,true,(s,t,b)=>{calls++;Assert.That(File.ReadAllText(s),Is.EqualTo("new"));Assert.That(File.ReadAllText(t),Is.EqualTo("old"));if(calls<3)throw Error(code);File.Replace(s,t,b);},waits.Add);
            Assert.That(calls,Is.EqualTo(3));Assert.That(waits,Is.EqualTo(new[]{20,40}));Assert.That(File.ReadAllText(target),Is.EqualTo("new"));Assert.That(File.ReadAllText(backup),Is.EqualTo("old"));Assert.That(File.Exists(staged),Is.False);
        }
        [Test] public void PersistentSharingFailureIsBoundedAndRetainsBothFiles()
        {
            var waits=new List<int>();int calls=0;var expected=Error(1175);
            Assert.That(Assert.Throws<IOException>(()=>FilePublication.Replace(staged,target,backup,true,(_,_,_)=>{calls++;throw expected;},waits.Add)),Is.SameAs(expected));
            Assert.That(calls,Is.EqualTo(4));Assert.That(waits,Is.EqualTo(new[]{20,40,80}));Assert.That(File.ReadAllText(staged),Is.EqualTo("new"));Assert.That(File.ReadAllText(target),Is.EqualTo("old"));Assert.That(File.Exists(backup),Is.False);
        }
        [TestCase(5)] [TestCase(112)] [TestCase(1176)] [TestCase(1177)] [TestCase(0)]
        public void PermissionsDiskFullPartialRenamesAndUnknownFailuresAreNeverRetried(int code)
        {
            int calls=0,waits=0;var expected=Error(code);
            Assert.That(Assert.Throws<IOException>(()=>FilePublication.Replace(staged,target,backup,true,(_,_,_)=>{calls++;throw expected;},_=>waits++)),Is.SameAs(expected));Assert.That(calls,Is.EqualTo(1));Assert.That(waits,Is.Zero);
        }
        [Test] public void NonWindowsFailureHasNoRetryOrDelayEvenWithTheSameErrorNumber()
        {
            int calls=0,waits=0;Assert.Throws<IOException>(()=>FilePublication.Replace(staged,target,backup,false,(_,_,_)=>{calls++;throw Error(1175);},_=>waits++));Assert.That(calls,Is.EqualTo(1));Assert.That(waits,Is.Zero);
        }
        [TestCase(true)] [TestCase(false)]
        public void AChangedPublicationStateCannotBeRetried(bool removeStaged)
        {
            int calls=0,waits=0;Assert.Throws<IOException>(()=>FilePublication.Replace(staged,target,backup,true,(_,_,_)=>{calls++;File.Delete(removeStaged?staged:target);throw Error(1175);},_=>waits++));Assert.That(calls,Is.EqualTo(1));Assert.That(waits,Is.Zero);
        }
        [Test] public void AnExceptionAfterPublicationIsNotTurnedIntoSuccessOrASecondPublication()
        {
            int calls=0,waits=0;Assert.Throws<IOException>(()=>FilePublication.Replace(staged,target,backup,true,(s,t,b)=>{calls++;File.Replace(s,t,b);throw Error(1176);},_=>waits++));Assert.That(calls,Is.EqualTo(1));Assert.That(waits,Is.Zero);Assert.That(File.ReadAllText(target),Is.EqualTo("new"));Assert.That(File.ReadAllText(backup),Is.EqualTo("old"));
        }
#if UNITY_EDITOR_WIN
        [TestCase(true)] [TestCase(false)]
        public void RealWindowsReadLocksRecoverOnlyAfterTheReaderReleasesItsHandle(bool lockStaged)
        {
            FileStream reader=new FileStream(lockStaged?staged:target,FileMode.Open,FileAccess.Read,FileShare.Read);int calls=0;var waits=new List<int>();
            try {FilePublication.Replace(staged,target,backup,true,(s,t,b)=>{calls++;File.Replace(s,t,b);},ms=>{waits.Add(ms);Assert.That(File.ReadAllText(staged),Is.EqualTo("new"));Assert.That(File.ReadAllText(target),Is.EqualTo("old"));reader.Dispose();reader=null;});}
            finally{reader?.Dispose();}
            Assert.That(calls,Is.EqualTo(2));Assert.That(waits,Is.EqualTo(new[]{20}));Assert.That(File.ReadAllText(target),Is.EqualTo("new"));Assert.That(File.ReadAllText(backup),Is.EqualTo("old"));
        }
#endif
        sealed class Actions:IRuleActions
        {
            internal int Starts;
            public bool CanRun(CapabilityCall call,out string error){error=null;return true;}
            public bool Start(string id,CapabilityCall call,out float seconds,out string error){Starts++;seconds=1;error=null;return true;}
            public void Stop(string id,bool preserve){}
        }
        static JObject Call()=>new(){["id"]="time.wait",["version"]=1,["arguments"]=new JObject{["seconds"]=1}};
        [Test] public void ReceiptPublicationRetriesBeforeStartingExactlyOneEffect()
        {
            var seed=new InvocationReceipts(directory);Assert.That(seed.Reserve(seed.NextId,Call(),Array.Empty<string>(),out _),Is.True);
            int failures=0;var waits=new List<int>();bool inject=false;var actions=new Actions();
            var receipts=new InvocationReceipts(directory,(s,t,b)=>FilePublication.Replace(s,t,b,true,(a,c,d)=>{if(inject&&failures++<2){Assert.That(actions.Starts,Is.Zero);throw Error(1175);}File.Replace(a,c,d);},waits.Add));
            inject=true;var scheduler=new RuleScheduler(actions,receipts);string id=receipts.NextId;Assert.That(scheduler.Invoke(Call(),0,out _,out var error,id),Is.True,error);scheduler.Tick(2);
            Assert.That(actions.Starts,Is.EqualTo(1));Assert.That(waits,Is.EqualTo(new[]{20,40}));Assert.That(receipts.Error,Is.Null);Assert.That((string)new InvocationReceipts(directory).Find(id)["phase"],Is.EqualTo("completed"));
            Assert.That(scheduler.Invoke(Call(),3,out _,out error,id),Is.False);Assert.That(error,Does.Contain("expired"));Assert.That(actions.Starts,Is.EqualTo(1));
        }
        [Test] public void ExhaustedReceiptPublicationNeverStartsAnEffectOrReplaysOnReopen()
        {
            var seed=new InvocationReceipts(directory);Assert.That(seed.Reserve(seed.NextId,Call(),Array.Empty<string>(),out _),Is.True);bool inject=false;int calls=0;
            var receipts=new InvocationReceipts(directory,(s,t,b)=>FilePublication.Replace(s,t,b,true,(a,c,d)=>{if(inject){calls++;throw Error(1175);}File.Replace(a,c,d);},_=>{}));
            var before=File.ReadAllBytes(Path.Combine(directory,"action-receipts.v1.json"));inject=true;var actions=new Actions();var scheduler=new RuleScheduler(actions,receipts);string id=receipts.NextId;
            Assert.That(scheduler.Invoke(Call(),0,out _,out _,id),Is.False);Assert.That(actions.Starts,Is.Zero);Assert.That(calls,Is.EqualTo(4));Assert.That(receipts.NextId,Is.Null);Assert.That(File.ReadAllBytes(Path.Combine(directory,"action-receipts.v1.json")),Is.EqualTo(before));
            var reopened=new InvocationReceipts(directory);var other=new Actions();Assert.That(new RuleScheduler(other,reopened).Invoke(Call(),0,out _,out _,id),Is.False);Assert.That(other.Starts,Is.Zero);
        }
    }
}
