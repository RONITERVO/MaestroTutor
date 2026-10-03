// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Rules;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class InvocationReceiptTests
    {
        string directory;
        sealed class Actions : IRuleActions
        {
            public int Starts,Stops;
            public bool CanRun(CapabilityCall step,out string error) {error=null;return true;}
            public bool Start(string id,CapabilityCall invocation,out float seconds,out string error) { invocation.TryStep(out var step,out _);Starts++;seconds=1;error=null;return true;}
            public void Stop(string id,bool preserve) {Stops++;}
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
        [TestCase("action-receipts.v1.json")] [TestCase("action-receipts.v7.json")] [TestCase("action-receipts.v7.json.pending")]
        public void ExplicitRecoveryArchivesUnknownEvidenceAndNeverReusesOldStartIds(string name)
        {
            Directory.CreateDirectory(directory);string path=Path.Combine(directory,name);
            byte[] original={0x7b,0xff,0x00,0x21};File.WriteAllBytes(path,original);
            var receipts=new InvocationReceipts(directory);Assert.That(receipts.Error,Is.Not.Null);
            string recovery=(string)receipts.RecoveryView["id"];var actions=new Actions();var scheduler=new RuleScheduler(actions,receipts);
            Assert.That(scheduler.RecoverInvocations(recovery,out var error),Is.True,error);
            Assert.That(receipts.Error,Is.Null);Assert.That(receipts.NextId,Is.Not.Null);Assert.That(actions.Starts,Is.Zero);
            string archive=Directory.GetDirectories(Path.Combine(directory,"action-receipt-archives")).Single();
            Assert.That(File.ReadAllBytes(Path.Combine(archive,name)),Is.EqualTo(original));
            Assert.That(File.Exists(Path.Combine(directory,"action-recovery.pending.json")),Is.False);
            string fresh=receipts.NextId;Assert.That(scheduler.Invoke(Call(),0,out var run,out error,fresh),Is.True,error);
            Assert.That(scheduler.RecoverInvocations(recovery,out error),Is.True,error,"Duplicate recovery is a no-op");
            Assert.That(scheduler.RunningCount,Is.EqualTo(1));Assert.That(actions.Stops,Is.Zero);
            Assert.That(scheduler.Invoke(Call(),1,out _,out _,Guid.NewGuid().ToString("N")),Is.False);
            Assert.That(new InvocationReceipts(directory).Find(run)["phase"].Value<string>(),Is.EqualTo("interrupted"));
            Evidence("history-recovered",receipts.Observe(run,_=>null));
        }
        [Test] public void RecoveryStopsOnlyOneOffActionsAndCanRetryAfterARealDiskFailure()
        {
            var receipts=new InvocationReceipts(directory);var actions=new Actions();var scheduler=new RuleScheduler(actions,receipts);
            var saved=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Saved wait",program=Maestro.Quest.Programs.BehaviourProgram.FromSteps(new RuleStep {action=RuleActionKind.Wait,seconds=30})};
            scheduler.Configure(new RuleDocument {sequences=new[]{saved}});Assert.That(scheduler.Trigger(saved.id,0),Is.True);
            Assert.That(scheduler.Invoke(Call(),0,out var running,out _,receipts.NextId),Is.True);
            string pending=Path.Combine(directory,"action-receipts.v1.json.pending");Directory.CreateDirectory(pending);
            Assert.That(scheduler.Invoke(Call(),0,out _,out _,receipts.NextId),Is.False);
            string token=(string)receipts.RecoveryView["id"];
            Assert.That(scheduler.RecoverInvocations(Guid.NewGuid().ToString("N"),out _),Is.False);Assert.That(actions.Stops,Is.Zero);
            Evidence("history-error",scheduler.ObserveInvocations(running));
            Assert.That(scheduler.RecoverInvocations(token,out _),Is.False);Assert.That(actions.Stops,Is.EqualTo(1));
            Assert.That(scheduler.ObserveRuns().Single().sequenceId,Is.EqualTo(saved.id));Assert.That(receipts.NextId,Is.Null);
            Directory.Delete(pending);
            Assert.That(scheduler.RecoverInvocations(token,out var error),Is.True,error);
            Assert.That(scheduler.ObserveRuns().Single().sequenceId,Is.EqualTo(saved.id));
            string archive=Directory.GetDirectories(Path.Combine(directory,"action-receipt-archives")).Single();
            var session=JArray.Parse(File.ReadAllText(Path.Combine(archive,"session.json")));
            Assert.That(session.Single(x=>(string)x["id"]==running)["phase"].Value<string>(),Is.EqualTo("cancelled"));
            Assert.That(scheduler.Invocation(running),Is.Null);
            Assert.That(scheduler.Invoke(Call(),0,out _,out _,running),Is.False);
            Evidence("history-recovered-empty",scheduler.ObserveInvocations(null));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void RestartCanResumeEachArchiveCommitBoundaryWithoutDiscardingEvidence(int phase)
        {
            Directory.CreateDirectory(directory);string primary=Path.Combine(directory,"action-receipts.v1.json"),future=Path.Combine(directory,"action-receipts.v2.json");
            File.WriteAllText(primary,"old damaged primary");File.WriteAllText(future,"future evidence");
            string id=Guid.NewGuid().ToString("N"),archive=Path.Combine(directory,"action-receipt-archives",id);Directory.CreateDirectory(archive);
            File.WriteAllText(Path.Combine(directory,"action-recovery.pending.json"),new JObject {["version"]=1,["id"]=id,["files"]=new JArray(Path.GetFileName(primary),Path.GetFileName(future))}.ToString());
            if(phase>=1){File.Copy(primary,Path.Combine(archive,Path.GetFileName(primary)));File.Copy(future,Path.Combine(archive,Path.GetFileName(future)));}
            if(phase>=2)File.Delete(future);
            if(phase>=3)File.WriteAllText(primary,"{\"version\":1,\"entries\":[]}");
            var receipts=new InvocationReceipts(directory);Assert.That(receipts.NextId,Is.Null);
            Assert.That(receipts.Recover((string)receipts.RecoveryView["id"],out var error),Is.True,error);
            Assert.That(File.ReadAllText(Path.Combine(archive,Path.GetFileName(primary))),Is.EqualTo("old damaged primary"));
            Assert.That(File.ReadAllText(Path.Combine(archive,Path.GetFileName(future))),Is.EqualTo("future evidence"));
            Assert.That(new InvocationReceipts(directory).Error,Is.Null);
        }
        [Test] public void LockedEvidenceCannotBeSkippedAndRetryUsesTheSameRecoveryArchive()
        {
            Directory.CreateDirectory(directory);string primary=Path.Combine(directory,"action-receipts.v1.json"),future=Path.Combine(directory,"action-receipts.v2.json");
            File.WriteAllText(primary,"broken primary");File.WriteAllText(future,"future evidence");
            var receipts=new InvocationReceipts(directory);string id=(string)receipts.RecoveryView["id"];
            using(var locked=new FileStream(future,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
                Assert.That(receipts.Recover(id,out _),Is.False);Assert.That(File.ReadAllText(primary),Is.EqualTo("broken primary"));Assert.That(receipts.NextId,Is.Null);
            }
            Assert.That(receipts.Recover(id,out var error),Is.True,error);
            Assert.That(Directory.GetDirectories(Path.Combine(directory,"action-receipt-archives")).Length,Is.EqualTo(1));
        }
        [Test] public void CorruptRecoveryMetadataIsPreservedAndCannotNameFilesOutsideTheJournal()
        {
            Directory.CreateDirectory(directory);string marker=Path.Combine(directory,"action-recovery.pending.json");
            string raw=new JObject {["version"]=1,["id"]="../outside",["files"]=new JArray("../room.v5.json")}.ToString();
            File.WriteAllText(marker,raw);string room=Path.Combine(directory,"room.v5.json");File.WriteAllText(room,"keep this room");
            var receipts=new InvocationReceipts(directory);Assert.That(receipts.Recover((string)receipts.RecoveryView["id"],out var error),Is.True,error);
            string archive=Directory.GetDirectories(Path.Combine(directory,"action-receipt-archives")).Single();
            Assert.That(File.ReadAllText(Path.Combine(archive,"previous-marker.json")),Is.EqualTo(raw));Assert.That(File.ReadAllText(room),Is.EqualTo("keep this room"));
        }

        [TestCase("markerBytes")] [TestCase("archiveFiles")] [TestCase("archiveCount")]
        public void RecoveryCapacityIncludesMalformedMetadataAndEveryNewArchiveFile(string limit)
        {
            Directory.CreateDirectory(directory);string primary=Path.Combine(directory,"action-receipts.v1.json");File.WriteAllText(primary,"preserve primary");
            string archives=Path.Combine(directory,"action-receipt-archives");
            if(limit=="markerBytes"){
                using var large=new FileStream(Path.Combine(directory,"action-recovery.pending.json"),FileMode.CreateNew,FileAccess.Write);large.SetLength(16L*1024*1024+1);
            }else{
                Directory.CreateDirectory(archives);
                if(limit=="archiveFiles"){string archive=Path.Combine(archives,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(archive);for(int i=0;i<511;i++)File.WriteAllText(Path.Combine(archive,"evidence-"+i),"x");}
                else for(int i=0;i<128;i++)Directory.CreateDirectory(Path.Combine(archives,Guid.NewGuid().ToString("N")));
            }
            var receipts=new InvocationReceipts(directory);Assert.That(receipts.Error,Is.Not.Null);
            int before=Directory.Exists(archives)?Directory.GetDirectories(archives).Length:0;
            Assert.That(receipts.Recover((string)receipts.RecoveryView["id"],out _),Is.False);
            Assert.That(receipts.NextId,Is.Null);Assert.That(File.ReadAllText(primary),Is.EqualTo("preserve primary"));
            Assert.That(Directory.Exists(archives)?Directory.GetDirectories(archives).Length:0,Is.EqualTo(before),"Capacity rejection must not allocate another archive");
        }

        [Test] public void FutureReceiptFilesArePreservedAndDisableNewStarts()
        {
            Directory.CreateDirectory(directory);string future=Path.Combine(directory,"action-receipts.v2.json");File.WriteAllText(future,"future");
            var receipts=new InvocationReceipts(directory);Assert.That(receipts.NextId,Is.Null);Assert.That(File.ReadAllText(future),Is.EqualTo("future"));
            Assert.That(File.Exists(Path.Combine(directory,"action-receipts.v1.json")),Is.False);
        }
    }
}
