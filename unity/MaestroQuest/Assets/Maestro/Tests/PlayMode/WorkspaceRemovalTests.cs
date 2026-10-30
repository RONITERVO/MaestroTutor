// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Threading;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        JObject RemovalReviewArgs()=>new() {["inspectionId"]=retentionInspection,["generationId"]=retentionEntry["generationId"].DeepClone()};
        JObject RemovalArgs(JObject preview)=>new() {["previewId"]=preview["previewId"].DeepClone(),["generationId"]=preview["generationId"].DeepClone(),["fingerprint"]=preview["fingerprint"].DeepClone(),["confirmation"]="delete "+(string)preview["generationId"]};
        [UnityTest] public IEnumerator ConfirmedDisposalSharesCatalogAndDurableReceiptsAndNeverNeedsAnExport()
        {
            var saved=Prepare(Archive("Discard me"));Open();yield return ReadyHost();var content=host.Current;var selection=host.Selection.Json();yield return RetentionInspect(saved.Id);yield return RetentionCall("reviewRemoval",RemovalReviewArgs());var preview=(JObject)retentionOutput.DeepClone();Assert.That((bool)preview["eligible"],Is.True);
            Assert.That(host.Runtime.TryRead("workspace.retention.removal",1,new JObject(),out var fact),Is.True);Assert.That(JToken.DeepEquals(JObject.FromObject(fact.Value),preview),Is.True);var args=RemovalArgs(preview);args["confirmation"]="delete "+new string('f',32);Assert.That(host.Retention.CanStart("remove",args,out _),Is.False);
            var previewExecution=retentionState.DeepClone();yield return RetentionCall("remove",RemovalArgs(preview));Assert.That((bool)retentionOutput["removed"],Is.True);Assert.That(host.Current,Is.SameAs(content));Assert.That(JToken.DeepEquals(host.Selection.Json(),selection),Is.True);Assert.That(Directory.Exists(Path.Combine(directory,"workspace-generations.v1","generations",saved.Id)),Is.False);Assert.That(host.Runtime.TryRead("workspace.retention.removal",1,new JObject(),out _),Is.False);
            string run=retentionRun;var completed=retentionState.DeepClone();var result=retentionOutput.DeepClone();var request=new JObject {["operation"]="start",["runId"]=run,["call"]=completed["workspace"]["selected"]["call"].DeepClone()};
            UnityEngine.Object.Destroy(root);yield return null;BuildShell(directory);Open();yield return ReadyHost();Assert.That(host.Retention.Removal(),Is.Null);var actions=new RoomExecutions(host.Current.Editor,host);Assert.That(actions.Execute(request,out var error),Is.True,error);Assert.That(host.Retention.Busy,Is.False);Assert.That((string)actions.Observe()["workspace"]["selected"]["phase"],Is.EqualTo("completed"));
            string proof=Environment.GetEnvironmentVariable("MAESTRO_REMOVAL_FLOW");if(!string.IsNullOrEmpty(proof)){Directory.CreateDirectory(proof);File.WriteAllText(Path.Combine(proof,"removal.json"),new JObject {["preview"]=preview,["previewExecution"]=previewExecution,["remove"]=result,["removeExecution"]=completed,["restartedExecution"]=actions.Observe()}.ToString());}
        }
        [UnityTest] public IEnumerator DisposalPreviewIsSessionLocalAndCannotSurviveReinspectionOrRestart()
        {
            var saved=Prepare(Archive("Keep me"));Open();yield return ReadyHost();yield return RetentionInspect(saved.Id);yield return RetentionCall("reviewRemoval",RemovalReviewArgs());var args=RemovalArgs(retentionOutput);yield return RetentionInspect(saved.Id);Assert.That(host.Retention.CanStart("remove",args,out _),Is.False);
            yield return RetentionCall("reviewRemoval",RemovalReviewArgs());args=RemovalArgs(retentionOutput);UnityEngine.Object.Destroy(root);yield return null;BuildShell(directory);Open();yield return ReadyHost();Assert.That(host.Retention.CanStart("remove",args,out _),Is.False);Assert.That(Directory.Exists(Path.Combine(directory,"workspace-generations.v1","generations",saved.Id)),Is.True);
        }
        [UnityTest] public IEnumerator DisposalOfSelectedGenerationIsAnIneligiblePreviewWithoutExecution()
        {
            var saved=Prepare(Archive("Current"));var previous=Prepare(Archive("Previous"));store.Activate(saved.Id,saved.Receipt.ManifestHash,"initial",previous.Id,previous.Receipt.ManifestHash);Open();yield return ReadyHost();yield return RetentionInspect(saved.Id);yield return RetentionCall("reviewRemoval",RemovalReviewArgs());Assert.That((bool)retentionOutput["eligible"],Is.False);Assert.That((string)retentionOutput["fingerprint"],Is.Empty);Assert.That(host.Retention.CanStart("remove",RemovalArgs(retentionOutput),out _),Is.False);
        }
        [UnityTest] public IEnumerator DestroyingHostDuringDisposalHoldsPathUntilWorkerDrainsAndRestartDoesNotReplay()
        {
            var saved=Prepare(Archive("Discard"));Open();yield return ReadyHost();yield return RetentionInspect(saved.Id);yield return RetentionCall("reviewRemoval",RemovalReviewArgs());var args=RemovalArgs(retentionOutput);using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.Retention.Fault=point=>{if(point=="retention.removing"&&!entered.IsSet){entered.Set();if(!release.Wait(15000))throw new TimeoutException();}};
            var actions=new RoomExecutions(host.Current.Editor,host);var request=MaintenanceStart(actions,"workspace.retention.remove",args);Assert.That(actions.Execute(request,out var error),Is.True,error);var old=host;
            try{float deadline=Time.realtimeSinceStartup+10;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);UnityEngine.Object.Destroy(root);yield return null;BuildShell(directory);Open();Assert.That(host.Ready,Is.False);Assert.That(old.Retirement.IsCompleted,Is.False);}finally{release.Set();}
            yield return ReadyHost();Assert.That(old.Retirement.IsCompleted,Is.True);Assert.That(Directory.Exists(Path.Combine(directory,"workspace-generations.v1","generations",saved.Id)),Is.False);Assert.That((string)host.Retention.Status()["phase"],Is.EqualTo("idle"));actions=new RoomExecutions(host.Current.Editor,host);Assert.That(actions.Execute(request,out error),Is.True,error);Assert.That(host.Retention.Busy,Is.False);Assert.That((string)actions.Observe()["workspace"]["selected"]["phase"],Is.EqualTo("interrupted").Or.EqualTo("cancelled"));
        }
        [UnityTest] public IEnumerator DisposalStopAfterFirstDeleteKeepsOwnershipUntilWorkerFinishesAndNeverReplays()
        {
            var saved=Prepare(Archive("Discard"));Open();yield return ReadyHost();yield return RetentionInspect(saved.Id);yield return RetentionCall("reviewRemoval",RemovalReviewArgs());var args=RemovalArgs(retentionOutput);using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.Retention.Fault=point=>{if(point=="retention.removing"&&!entered.IsSet){entered.Set();if(!release.Wait(15000))throw new TimeoutException();}};
            var actions=new RoomExecutions(host.Current.Editor,host);var request=MaintenanceStart(actions,"workspace.retention.remove",args);Assert.That(actions.Execute(request,out var error),Is.True,error);
            try{float deadline=Time.realtimeSinceStartup+10;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);host.Runtime.Scheduler.StopAll();Assert.That(host.Retention.Busy,Is.True);Assert.That(host.Evidence.CanStart("inspect",new JObject(),out _),Is.False);Assert.That(host.Recovery.CanInspect(out _),Is.False);}finally{release.Set();}
            while(host.Retention.Busy)yield return null;yield return null;Assert.That((string)host.Retention.Status()["phase"],Is.EqualTo("removed"));Assert.That(Directory.Exists(Path.Combine(directory,"workspace-generations.v1","generations",saved.Id)),Is.False);Assert.That((string)host.Runtime.Scheduler.Receipts.Find((string)request["runId"])["phase"],Is.EqualTo("cancelled"));Assert.That(actions.Execute(request,out error),Is.True,error);Assert.That(host.Retention.Busy,Is.False);
        }
    }
}
