// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class InvocationReceiptTests
    {
        string directory;
        sealed class Actions : IRuleActions
        {
            public int Starts;
            public bool CanRun(RuleStep step,out string error) {error=null;return true;}
            public bool Start(string id,RuleStep step,out float seconds,out string error) {Starts++;seconds=1;error=null;return true;}
            public void Stop(string id,bool preserve) {}
        }
        static void Evidence(string name,JObject view)
        {
            string output=Environment.GetEnvironmentVariable("MAESTRO_RECEIPT_EVIDENCE");if(string.IsNullOrEmpty(output))return;
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,name+".json"),view.ToString(Newtonsoft.Json.Formatting.None));
        }
        static JObject Call()=>new() {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=1}};
        [SetUp] public void Before()=>directory=Path.Combine(Path.GetTempPath(),"MaestroReceipts-"+Guid.NewGuid().ToString("N"));
        [TearDown] public void After() {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [Test] public void RestartPreservesCompletedButMarksUnfinishedUncertainWithoutPlayback()
        {
            var receipts=new InvocationReceipts(directory);var actions=new Actions();var scheduler=new RuleScheduler(actions,receipts);
            string completed=receipts.NextId;Assert.That(scheduler.Invoke(Call(),0,out var first,out _,completed),Is.True);scheduler.Tick(2);
            string unfinished=receipts.NextId;Assert.That(scheduler.Invoke(Call(),3,out _,out _,unfinished),Is.True);
            var restarted=new InvocationReceipts(directory);var recoveredActions=new Actions();var recovered=new RuleScheduler(recoveredActions,restarted);
            Assert.That((string)recovered.Invocation(first)["phase"],Is.EqualTo("completed"));
            Assert.That((string)recovered.Invocation(unfinished)["phase"],Is.EqualTo("interrupted"));
            Assert.That((string)recovered.Invocation(unfinished)["status"],Does.Contain("Some effects"));
            Evidence("interrupted",recovered.ObserveInvocations(unfinished));Evidence("completed",recovered.ObserveInvocations(first));
            recovered.Tick(100);Assert.That(recoveredActions.Starts,Is.Zero);Assert.That(recovered.RunningCount,Is.Zero);
            Assert.That(recovered.CancelInvocation(unfinished,out _),Is.True);
            Assert.That((string)recovered.Invocation(unfinished)["phase"],Is.EqualTo("interrupted"));
            var secondRestart=new InvocationReceipts(directory);Assert.That((string)secondRestart.Find(unfinished)["phase"],Is.EqualTo("interrupted"));
        }
        [Test] public void DurableReservationPrecedesEffectsAndEvictedIdsNeverStartAgain()
        {
            var receipts=new InvocationReceipts(directory);string first=receipts.NextId;
            var actions=new Actions();var scheduler=new RuleScheduler(actions,receipts);
            for(int i=0;i<20;i++) {Assert.That(scheduler.Invoke(Call(),i*2,out _,out _,receipts.NextId),Is.True);scheduler.Tick(i*2+1.5f);}
            Assert.That(receipts.Find(first),Is.Null);Assert.That(scheduler.ObserveInvocations(null)["outcomes"].Count(),Is.EqualTo(16));
            Assert.That(scheduler.Invoke(Call(),100,out _,out var error,first),Is.False);Assert.That(error,Does.Contain("expired"));
            Assert.That(actions.Starts,Is.EqualTo(20));
            // A reserved call is already on disk even if execution never began.
            string reserved=receipts.NextId;Assert.That(receipts.Reserve(reserved,Call(),Array.Empty<string>(),out _),Is.True);
            Assert.That((string)new InvocationReceipts(directory).Find(reserved)["phase"],Is.EqualTo("interrupted"));
        }
        [Test] public void FailedWriteBlocksEffectsAndCorruptionNeverFallsBackToAnOlderReceipt()
        {
            Directory.CreateDirectory(directory);Directory.CreateDirectory(Path.Combine(directory,"action-receipts.v1.json.pending"));
            var receipts=new InvocationReceipts(directory);var actions=new Actions();var scheduler=new RuleScheduler(actions,receipts);
            Assert.That(scheduler.Invoke(Call(),0,out _,out _,receipts.NextId),Is.False);Assert.That(actions.Starts,Is.Zero);
            Assert.That(receipts.NextId,Is.Null);Assert.That(receipts.Error,Does.Contain("storage failed"));
            File.WriteAllText(Path.Combine(directory,"action-receipts.v1.json"),"{broken");
            File.WriteAllText(Path.Combine(directory,"action-receipts.v1.json.bak"),"{\"version\":1,\"entries\":[]}");
            var corrupt=new InvocationReceipts(directory);Assert.That(corrupt.NextId,Is.Null);Assert.That(corrupt.Error,Does.Contain("cannot be read"));
            Assert.That(File.ReadAllText(Path.Combine(directory,"action-receipts.v1.json")),Is.EqualTo("{broken"));
        }
        [Test] public void WriteFailureAfterCompletionKeepsLiveEvidenceButRestartIsUncertain()
        {
            var receipts=new InvocationReceipts(directory);var scheduler=new RuleScheduler(new Actions(),receipts);
            Assert.That(scheduler.Invoke(Call(),0,out var id,out _,receipts.NextId),Is.True);
            Directory.CreateDirectory(Path.Combine(directory,"action-receipts.v1.json.pending"));scheduler.Tick(2);
            Assert.That((string)scheduler.Invocation(id)["phase"],Is.EqualTo("completed"));
            Assert.That(scheduler.ObserveInvocations(id)["storageError"].Type,Is.EqualTo(JTokenType.String));
            Evidence("unsaved-completion",scheduler.ObserveInvocations(id));
            Assert.That((string)new InvocationReceipts(directory).Find(id)["phase"],Is.EqualTo("interrupted"));
        }
        [Test] public void FailedReservationStillBoundsTheTerminalObservation()
        {
            var receipts=new InvocationReceipts(directory);var scheduler=new RuleScheduler(new Actions(),receipts);
            for(int i=0;i<16;i++) {Assert.That(scheduler.Invoke(Call(),i*2,out _,out _,receipts.NextId),Is.True);scheduler.Tick(i*2+1.5f);}
            Directory.CreateDirectory(Path.Combine(directory,"action-receipts.v1.json.pending"));
            Assert.That(scheduler.Invoke(Call(),40,out _,out _,receipts.NextId),Is.False);
            Assert.That(scheduler.ObserveInvocations(null)["outcomes"].Count(),Is.EqualTo(16));
            Assert.That(scheduler.ObserveInvocations(null)["running"].Count(),Is.Zero);
        }
        [Test] public void ALongRunningCallRetainsItsNewCompletionAfterOtherCallsFinish()
        {
            var receipts=new InvocationReceipts(directory);string longRun=receipts.NextId;
            Assert.That(receipts.Reserve(longRun,Call(),Array.Empty<string>(),out _),Is.True);
            for(int i=0;i<16;i++) {
                string id=receipts.NextId;Assert.That(receipts.Reserve(id,Call(),Array.Empty<string>(),out _),Is.True);
                var done=receipts.Find(id);done["phase"]="completed";done["status"]="Action completed";receipts.Update(done);
            }
            var result=receipts.Find(longRun);result["phase"]="completed";result["status"]="Action completed";receipts.Update(result);
            Assert.That(receipts.Find(longRun),Is.Not.Null);Assert.That(receipts.Observe(longRun,_=>null)["outcomes"].Last()["id"].Value<string>(),Is.EqualTo(longRun));
            Assert.That((string)new InvocationReceipts(directory).Find(longRun)["phase"],Is.EqualTo("completed"));
        }
        [Test] public void RecoveryKeepsUnfinishedCallsAheadOfOlderCompletedHistory()
        {
            var receipts=new InvocationReceipts(directory);string unfinished=receipts.NextId;
            Assert.That(receipts.Reserve(unfinished,Call(),Array.Empty<string>(),out _),Is.True);
            for(int i=0;i<16;i++) {
                string id=receipts.NextId;Assert.That(receipts.Reserve(id,Call(),Array.Empty<string>(),out _),Is.True);
                var done=receipts.Find(id);done["phase"]="completed";done["status"]="Action completed";receipts.Update(done);
            }
            var recovered=new InvocationReceipts(directory);
            Assert.That((string)recovered.Find(unfinished)["phase"],Is.EqualTo("interrupted"));
            Assert.That(recovered.Observe(unfinished,_=>null)["outcomes"].Count(),Is.EqualTo(16));
        }
        [Test] public void FutureReceiptFilesArePreservedAndDisableNewStarts()
        {
            Directory.CreateDirectory(directory);string future=Path.Combine(directory,"action-receipts.v2.json");File.WriteAllText(future,"future");
            var receipts=new InvocationReceipts(directory);Assert.That(receipts.NextId,Is.Null);Assert.That(File.ReadAllText(future),Is.EqualTo("future"));
            Assert.That(File.Exists(Path.Combine(directory,"action-receipts.v1.json")),Is.False);
        }
    }
}
