// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        JObject recoveryArgs;string recoveryId;JObject recoveryInspection,recoveryCandidate,recoveryPreview,recoveryPrepared,recoveryOpening;
        JObject RecoveryCall(string action,JObject args)
        {
            var actions=new RoomExecutions(host.Current?host.Current.Editor:null,host);var request=MaintenanceStart(actions,action,args);Assert.That(actions.Execute(request,out var error),Is.True,error);
            recoveryOpening=actions.Observe();return request;
        }
        IEnumerator FinishRecovery(string expected)
        {
            float deadline=Time.realtimeSinceStartup+25;while(host.Recovery.Busy&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Recovery.Busy,Is.False,host.Recovery.Status().ToString());Assert.That((string)host.Recovery.Status()["phase"],Is.EqualTo(expected),host.Recovery.Status().ToString());
        }
        IEnumerator InspectAndSelectRecovery(string sourceId)
        {
            RecoveryCall("workspace.recovery.inspect",new JObject());recoveryId=(string)recoveryOpening["workspace"]["selected"]["output"]["requestId"];yield return FinishRecovery("inspected");recoveryInspection=host.Recovery.Status();
            Assert.That(host.Runtime.TryRead("workspace.recovery",1,new JObject(),out var fact),Is.True);Assert.That(JToken.DeepEquals(JObject.FromObject(fact.Value),recoveryInspection),Is.True);
            recoveryCandidate=null;for(int i=0;i<(int)recoveryInspection["candidateCount"];i++){Assert.That(host.Runtime.TryRead("workspace.recovery.candidate",1,new JObject {["requestId"]=recoveryId,["index"]=i},out fact),Is.True);var candidate=JObject.FromObject(fact.Value);if((string)candidate["generationId"]==sourceId)recoveryCandidate=candidate;}
            Assert.That(recoveryCandidate,Is.Not.Null);recoveryArgs=new JObject {["requestId"]=recoveryId,["originHash"]=recoveryInspection["originHash"].DeepClone(),["generationId"]=recoveryCandidate["generationId"].DeepClone(),["manifestHash"]=recoveryCandidate["manifestHash"].DeepClone()};
            RecoveryCall("workspace.recovery.select",new JObject {["requestId"]=recoveryId,["originHash"]=recoveryArgs["originHash"].DeepClone(),["source"]=new JObject {["kind"]="retained",["generationId"]=recoveryArgs["generationId"].DeepClone(),["manifestHash"]=recoveryArgs["manifestHash"].DeepClone()}});yield return FinishRecovery("prepared");recoveryPrepared=host.Recovery.Status();Assert.That(host.Runtime.TryRead("workspace.recovery.preview",1,new JObject {["requestId"]=recoveryId},out fact),Is.True);recoveryPreview=JObject.FromObject(fact.Value);
            recoveryArgs["generationId"]=recoveryPreview["generationId"].DeepClone();recoveryArgs["manifestHash"]=recoveryPreview["manifestHash"].DeepClone();
        }
        [UnityTest] public IEnumerator SharedRecoveryPreservesDamagedStoresAndUnsavedEditsAndKeepsBookAndAgentConnected()
        {
            yield return ReadyForDamagedPreservation(true);var old=host.Current;int bookIdentity=browser.GetInstanceID();string raw=old.Editor.SaveDirectory;byte[] original=File.ReadAllBytes(Path.Combine(raw,"room.v8.json"));string source=host.Selection.Previous.Generation;
            yield return InspectAndSelectRecovery(source);AcceptedEdit("Accepted before damaged recovery");string session=agent.Observe().session;
            var command=RecoveryCall("workspace.recovery.commit",recoveryArgs);Assert.That(old.Editor.WriteGate.Frozen,Is.True);yield return FinishRecovery("review");
            Assert.That(browser.GetInstanceID(),Is.EqualTo(bookIdentity));Assert.That(agent.Observe().session,Is.Not.EqualTo(session));Assert.That(host.Current,Is.Not.SameAs(old));Assert.That(host.Current.Rules.ReadOnly,Is.False);Assert.That(host.ReviewRequired,Is.True);Assert.That(physics.Running,Is.False);Assert.That(host.Current.Rules.Runtime.Scheduler.RunningCount,Is.Zero);
            Assert.That(File.ReadAllBytes(Path.Combine(raw,"room.v8.json")),Is.EqualTo(original));Assert.That(File.ReadAllText(Path.Combine(raw,"behaviours.v2.json")),Is.EqualTo("{unreadable original"));
            string selected=host.Selection.Revision;var actions=new RoomExecutions(host.Current.Editor,host);Assert.That(actions.Execute(command,out var error),Is.True,error);Assert.That(host.Selection.Revision,Is.EqualTo(selected));Assert.That(host.Recovery.CanCommit(recoveryArgs,out _),Is.False);
            string generation=Path.Combine(directory,"workspace-generations.v1","generations",host.Selection.Active.Generation);var preserved=Directory.GetFiles(Path.Combine(generation,"preservation"),"*.zip").Single();Assert.That(RecoveryText(preserved,"accepted.json"),Does.Contain("Accepted before damaged recovery"));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_RECOVERY_FLOW_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"recovery.json"),new JObject {["inspected"]=recoveryInspection,["candidate"]=recoveryCandidate,["prepared"]=recoveryPrepared,["preview"]=recoveryPreview,["completed"]=host.Recovery.Status(),["opening"]=recoveryOpening}.ToString());File.Copy(preserved,Path.Combine(evidence,"preserved.zip"),true);}
            yield return ReadyReviewOwners();string review=BeginReview();yield return FinishReview(review);ApproveReview(review);yield return FinishReview(review);Assert.That(host.ReviewRequired,Is.False);Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator MissingCurrentOwnersRecoverThroughTheSameMaintenanceActionsWithoutFallback()
        {
            var active=Prepare(Archive("Unavailable current"));var saved=Prepare(Archive("Recovered without old owners"));store.Activate(active.Id,active.Receipt.ManifestHash,"initial",saved.Id,saved.Receipt.ManifestHash);
            string pointer=Path.Combine(directory,"workspace-generations.v1","current.v1.json");File.WriteAllText(pointer,"{unreadable selection");Open();Assert.That(host.Current,Is.Null);Assert.That(builds,Is.Zero);
            yield return InspectAndSelectRecovery(saved.Id);Assert.That(File.ReadAllText(pointer),Is.EqualTo("{unreadable selection"));RecoveryCall("workspace.recovery.commit",recoveryArgs);yield return FinishRecovery("review");
            Assert.That(builds,Is.EqualTo(1));Assert.That(host.Current.Editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Recovered without old owners"));Assert.That(host.Current.Editor.RuntimeGate.Held,Is.True);
            string selected=host.Selection.Revision,path=directory;UnityEngine.Object.Destroy(root);yield return null;BuildShell(path);Open();yield return ReadyHost();while(host.Recovery.Busy)yield return null;Assert.That(host.Selection.Revision,Is.EqualTo(selected));Assert.That((string)host.Recovery.Status()["phase"],Is.EqualTo("review"));Assert.That(builds,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator CancelledRecoveryPreparationKeepsCurrentOwnersAndOriginalCandidates()
        {
            yield return ReadyForDamagedPreservation(true);string source=host.Selection.Previous.Generation;var current=host.Current;string revision=host.Selection.Revision;
            RecoveryCall("workspace.recovery.inspect",new JObject());recoveryId=(string)host.Recovery.Status()["requestId"];yield return FinishRecovery("inspected");var metadata=store.InspectRecovery();var candidate=metadata["candidates"].Single(x=>(string)x["generationId"]==source);
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.Recovery.Fault=point=>{if(point=="damage.copied"){entered.Set();release.Wait(TimeSpan.FromSeconds(20));}};
            try{RecoveryCall("workspace.recovery.select",new JObject {["requestId"]=recoveryId,["originHash"]=metadata["originHash"].DeepClone(),["source"]=new JObject {["kind"]="retained",["generationId"]=source,["manifestHash"]=candidate["manifestHash"].DeepClone()}});float deadline=Time.realtimeSinceStartup+15;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);RecoveryCall("workspace.recovery.cancel",new JObject {["requestId"]=recoveryId});Assert.That(host.Recovery.Busy,Is.True);}
            finally{release.Set();}yield return FinishRecovery("cancelled");Assert.That(host.Current,Is.SameAs(current));Assert.That(host.Selection.Revision,Is.EqualTo(revision));Assert.That(current.Editor.WriteGate.Frozen,Is.False);
        }
        [UnityTest] public IEnumerator CancelledPreservationDrainsWorkersAndRetainsEvidenceWithoutChangingSelection()
        {
            yield return ReadyForDamagedPreservation(true);yield return InspectAndSelectRecovery(host.Selection.Previous.Generation);string revision=host.Selection.Revision;var editor=host.Current.Editor;
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.Recovery.Fault=point=>{if(point=="recovery.preserved"){entered.Set();release.Wait(TimeSpan.FromSeconds(20));}};
            try{RecoveryCall("workspace.recovery.commit",recoveryArgs);float deadline=Time.realtimeSinceStartup+15;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);RecoveryCall("workspace.recovery.cancel",new JObject {["requestId"]=recoveryId});Assert.That(editor.WriteGate.Frozen,Is.True);}
            finally{release.Set();}yield return FinishRecovery("cancelled");Assert.That(host.Selection.Revision,Is.EqualTo(revision));Assert.That(editor.WriteGate.Frozen,Is.False);Assert.That((string)host.Recovery.Status()["evidenceHash"],Has.Length.EqualTo(64));
        }
        [UnityTest] public IEnumerator FaultBeforeRecoveryCommitKeepsOldOwnersAndSavedEvidence()=>RecoveryCommitFault("pointer.beforeCommit",false);
        [UnityTest] public IEnumerator FaultAfterRecoveryCommitReconcilesWithoutRetryingTheEffect()=>RecoveryCommitFault("pointer.afterCommit",true);
        IEnumerator RecoveryCommitFault(string point,bool switched)
        {
            yield return ReadyForDamagedPreservation(true);yield return InspectAndSelectRecovery(host.Selection.Previous.Generation);var old=host.Current;string before=host.Selection.Revision;host.Recovery.Fault=phase=>{if(phase==point)throw new IOException("Injected commit fault");};RecoveryCall("workspace.recovery.commit",recoveryArgs);yield return FinishRecovery(switched?"review":"failed");
            Assert.That(host.Selection.Revision==before,Is.EqualTo(!switched));Assert.That(host.Current==old,Is.EqualTo(!switched));Assert.That(host.Recovery.Status()["evidenceHash"].ToString(),Has.Length.EqualTo(64));Assert.That(host.Current.Editor.WriteGate.Frozen,Is.False);
        }
        [UnityTest] public IEnumerator RefusedOwnerReplacementRemainsHeldEvenIfStorageLaterBecomesReadable()
        {
            yield return ReadyForDamagedPreservation(true);yield return InspectAndSelectRecovery(host.Selection.Previous.Generation);var old=host.Current;
            string pointer=Path.Combine(directory,"workspace-generations.v1","current.v1.json");byte[] selected=null;
            host.Recovery.Fault=phase=>{if(phase=="pointer.afterCommit"){selected=File.ReadAllBytes(pointer);File.WriteAllText(pointer,"{unavailable after commit");}};
            RecoveryCall("workspace.recovery.commit",recoveryArgs);float deadline=Time.realtimeSinceStartup+25;
            while((string)host.Recovery.Status()["phase"]!="unavailable"&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That((string)host.Recovery.Status()["phase"],Is.EqualTo("unavailable"));Assert.That(selected,Is.Not.Null);Assert.That(host.Recovery.Busy,Is.True);
            File.WriteAllBytes(pointer,selected);for(int i=0;i<4;i++){host.Recovery.Poll();yield return null;}
            Assert.That(host.Current,Is.SameAs(old));Assert.That(old.Editor.WriteGate.Frozen,Is.True);Assert.That(host.Switching,Is.False);
            Assert.That(host.Recovery.CanInspect(out _),Is.False);Assert.That(host.Recovery.CanCancel(recoveryId,out _),Is.False);
        }
        [UnityTest] public IEnumerator RestartWaitsForRecoveryWorkerThenReconcilesInsteadOfReplaying()
        {
            yield return ReadyForDamagedPreservation(true);yield return InspectAndSelectRecovery(host.Selection.Previous.Generation);string before=host.Selection.Revision,path=directory;var oldHost=host;var oldRoot=root;
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.Recovery.Fault=phase=>{if(phase=="recovery.preserved"){entered.Set();release.Wait(TimeSpan.FromSeconds(20));}};
            try {
                RecoveryCall("workspace.recovery.commit",recoveryArgs);float deadline=Time.realtimeSinceStartup+15;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);
                UnityEngine.Object.Destroy(oldRoot);yield return null;BuildShell(path);Open();yield return null;Assert.That(host.Ready,Is.False);Assert.That(host.Current,Is.Null);Assert.That(oldHost.Retirement.IsCompleted,Is.False);
            }finally{release.Set();if(oldRoot)UnityEngine.Object.Destroy(oldRoot);}
            yield return ReadyHost();Assert.That(host.Selection.Revision,Is.EqualTo(before));Assert.That((string)host.Recovery.Status()["phase"],Is.EqualTo("cancelled"));Assert.That(host.Current.Rules.ReadOnly,Is.True);Assert.That(host.Current.Editor.WriteGate.Frozen,Is.False);
        }
        [UnityTest] public IEnumerator StaleRecoveryRequestsCannotSelectCancelOrCommitAnotherOperation()
        {
            yield return ReadyForDamagedPreservation(true);RecoveryCall("workspace.recovery.inspect",new JObject());string old=(string)host.Recovery.Status()["requestId"];yield return FinishRecovery("inspected");RecoveryCall("workspace.recovery.inspect",new JObject());yield return FinishRecovery("inspected");Assert.That(host.Recovery.CanCancel(old,out _),Is.False);Assert.That(host.Recovery.Candidate(old,0),Is.Null);
            Assert.That(host.Recovery.CanCommit(new JObject {["requestId"]=old},out _),Is.False);Assert.That(host.Recovery.CanSelect(new JObject {["requestId"]=old},out _),Is.False);
        }
    }
}
