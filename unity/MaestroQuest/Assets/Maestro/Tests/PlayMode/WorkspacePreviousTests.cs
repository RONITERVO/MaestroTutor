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
        IEnumerator ReadyForPrevious()
        {
            var current=Prepare(Archive("Current robot"));var previous=Prepare(Archive("Previous robot"));store.Activate(current.Id,current.Receipt.ManifestHash,"initial",previous.Id,previous.Receipt.ManifestHash);
            Open();yield return ReadyReviewOwners();Assert.That(host.Import.Available,Is.False,"Previous selection must work without an Android chooser");
        }
        JObject PreviousArguments(JObject value)=>new JObject {["expectedRevision"]=value["revision"].DeepClone(),["generationId"]=value["generationId"].DeepClone(),["manifestHash"]=value["manifestHash"].DeepClone()};
        (string Id,JObject Execution) BeginPrevious()
        {
            var actions=new RoomExecutions(host.Current.Editor,host);var command=MaintenanceStart(actions,"workspace.previous.select",PreviousArguments(host.Import.ReadPrevious()));Assert.That(actions.Execute(command,out var error),Is.True,error);
            var execution=actions.Observe();return((string)execution["workspace"]["selected"]["output"]["requestId"],execution);
        }
        IEnumerator FinishPrevious(string id,string expected="prepared")
        {
            float deadline=Time.realtimeSinceStartup+20;while(Time.realtimeSinceStartup<deadline){host.Import.Poll();string phase=(string)host.Import.ReadSelection(id)["phase"];if(phase is "prepared" or "cancelled" or "failed")break;yield return null;}
            Assert.That((string)host.Import.ReadSelection(id)["phase"],Is.EqualTo(expected),host.Import.ReadSelection(id).ToString());
        }
        [UnityTest] public IEnumerator PreviousRecoveryUsesSharedPreviewActivationAndReviewWhileRetainingTodaysAcceptedEdits()
        {
            yield return ReadyForPrevious();var editor=host.Current.Editor;var content=host.Current;int bookId=browser.GetInstanceID();var previous=host.Import.ReadPrevious();string origin=host.Selection.Revision;
            Assert.That(host.Runtime.TryRead("workspace.previous",1,new JObject(),out var fact),Is.True);Assert.That(JToken.DeepEquals(JObject.FromObject(fact.Value),previous),Is.True);
            var (id,opening)=BeginPrevious();yield return FinishPrevious(id);var preview=host.Import.ReadSelection(id);Assert.That((string)preview["generationId"],Is.Not.EqualTo((string)previous["generationId"]));Assert.That(host.Current,Is.SameAs(content));Assert.That(host.Selection.Revision,Is.EqualTo(origin));
            AcceptedEdit("Accepted today before recovery");activationArgs=new JObject {["selectionRequestId"]=id,["generationId"]=preview["generationId"].DeepClone(),["manifestHash"]=preview["manifestHash"].DeepClone(),["expectedRevision"]=origin};
            var (_,activation)=BeginActivation();yield return FinishActivation(activation);Assert.That(host.Current,Is.Not.SameAs(content));Assert.That(browser.GetInstanceID(),Is.EqualTo(bookId));
            Assert.That(host.Current.Editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Previous robot"));Assert.That(host.Current.Editor.RuntimeGate.Held,Is.True);Assert.That(physics.Running,Is.False);Assert.That(host.Current.Rules.Runtime.Scheduler.RunningCount,Is.Zero);
            var retained=new RoomStorage(store.DataDirectory(host.Selection.Previous)).Load(out var error);Assert.That(error,Is.Null);Assert.That(retained.objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Accepted today before recovery"));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_PREVIOUS_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"previous.json"),new JObject {["previous"]=previous,["selection"]=preview,["selectionExecution"]=opening,["activation"]=host.Activation.Read(activation),["current"]=host.Activation.Current(),["execution"]=new RoomExecutions(host.Current.Editor,host).Observe()}.ToString());}
            yield return ReadyReviewOwners();string review=BeginReview();yield return FinishReview(review);ApproveReview(review);yield return FinishReview(review);Assert.That(host.ReviewRequired,Is.False);Assert.That(physics.Running,Is.False);
            string path=directory,revision=host.Selection.Revision;UnityEngine.Object.Destroy(root);yield return null;BuildShell(path);Open();yield return ReadyHost();Assert.That(host.Selection.Revision,Is.EqualTo(revision));Assert.That(host.Current.Editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Previous robot"));
        }
        [UnityTest] public IEnumerator ReviewChangesInvalidatePreviousPreviewBeforeActivationEvenWithUpdatedArguments()
        {
            yield return ReadyForPrevious();var (id,_)=BeginPrevious();yield return FinishPrevious(id);var preview=host.Import.ReadSelection(id);string revision=host.Selection.Revision;
            string review=BeginReview();yield return FinishReview(review);ApproveReview(review);yield return FinishReview(review);
            activationArgs=new JObject {["selectionRequestId"]=id,["generationId"]=preview["generationId"].DeepClone(),["manifestHash"]=preview["manifestHash"].DeepClone(),["expectedRevision"]=host.Selection.Revision};
            Assert.That(host.Selection.Revision,Is.Not.EqualTo(revision));Assert.That(host.Activation.CanStart(activationArgs,out _),Is.False);host.Import.Cancel(id);yield return FinishPrevious(id,"cancelled");Assert.That(host.Current.Editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Current robot"));
        }
        [UnityTest] public IEnumerator CancellationWaitsForPreviousCopyAndNeverDeletesTheRecoverySource()
        {
            yield return ReadyForPrevious();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.Import.Fault=point=>{if(point=="previous.copied"){entered.Set();release.Wait(TimeSpan.FromSeconds(20));}};
            string revision=host.Selection.Revision,source=store.DataDirectory(host.Selection.Previous);var (id,_)=BeginPrevious();
            try {float deadline=Time.realtimeSinceStartup+10;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);host.Import.Cancel(id);host.Import.Poll();Assert.That((string)host.Import.ReadSelection(id)["phase"],Is.EqualTo("cancelling"));}
            finally{release.Set();}
            yield return FinishPrevious(id,"cancelled");Assert.That(host.Selection.Revision,Is.EqualTo(revision));Assert.That(Directory.Exists(source),Is.True);Assert.That(host.Current.Editor.WriteGate.Frozen,Is.False);
        }
        [UnityTest] public IEnumerator ChangedPreviousDataReportsFailureAndPreservesTheCurrentOwners()
        {
            yield return ReadyForPrevious();var content=host.Current;string revision=host.Selection.Revision;string path=Path.Combine(store.DataDirectory(host.Selection.Previous),"room.v5.json");File.WriteAllText(path,"{broken");
            Assert.That((bool)host.Import.ReadPrevious()["available"],Is.True,"Metadata is not asset verification");var (id,_)=BeginPrevious();yield return FinishPrevious(id,"failed");
            Assert.That(host.Current,Is.SameAs(content));Assert.That(host.Selection.Revision,Is.EqualTo(revision));Assert.That(File.ReadAllText(path),Is.EqualTo("{broken"));Assert.That(host.Current.Editor.WriteGate.Frozen,Is.False);
        }
    }
}
