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
        JObject retentionOutput,retentionState,retentionEntry;string retentionInspection,retentionRun;
        IEnumerator RetentionCall(string mode,JObject args)
        {
            var actions=new RoomExecutions(host.Current?host.Current.Editor:null,host);var request=MaintenanceStart(actions,"workspace.retention."+mode,args);retentionRun=(string)request["runId"];Assert.That(actions.Execute(request,out var error),Is.True,error);
            float deadline=Time.realtimeSinceStartup+20;while(host.Retention.Busy&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;
            retentionState=actions.Observe();Assert.That((string)host.Retention.Status()["requestId"],Is.EqualTo(retentionRun));Assert.That((string)retentionState["workspace"]["selected"]["phase"],Is.EqualTo("completed"),retentionState.ToString());retentionOutput=(JObject)retentionState["workspace"]["selected"]["output"];
            Assert.That(host.Runtime.TryRead("workspace.retention",1,new JObject(),out var fact),Is.True);Assert.That(JToken.DeepEquals(JObject.FromObject(fact.Value),host.Retention.Status()),Is.True);Assert.That(actions.Execute(request,out error),Is.True,error);
        }
        IEnumerator RetentionInspect(string generation)
        {
            yield return RetentionCall("inspect",new JObject());retentionInspection=(string)retentionOutput["inspectionId"];retentionEntry=null;
            for(int i=0;i<(int)retentionOutput["count"];i++){Assert.That(host.Runtime.TryRead("workspace.retention.entry",1,new JObject {["inspectionId"]=retentionInspection,["index"]=i},out var fact),Is.True);var item=JObject.FromObject(fact.Value);if((string)item["generationId"]==generation)retentionEntry=item;}Assert.That(retentionEntry,Is.Not.Null);
        }
        JObject RetentionArgs()=>new() {["inspectionId"]=retentionInspection,["generationId"]=retentionEntry["generationId"].DeepClone(),["originalManifestHash"]=retentionEntry["originalManifestHash"].DeepClone()};
        [UnityTest] public IEnumerator SharedRetentionExportPublishesNewerPortableContentWithoutChangingLiveOwners()
        {
            evidencePublications=0;var saved=Prepare(Archive("Imported robot"));string data=Path.Combine(directory,"workspace-generations.v1","generations",saved.Id,"data"),roomPath=Path.Combine(data,"room.v3.json");var roomData=JObject.Parse(File.ReadAllText(roomPath));roomData["objects"][2]["name"]="Edited after import";File.WriteAllText(roomPath,roomData.ToString());File.WriteAllText(Path.Combine(data,"private-receipts.json"),"must remain private");Open();yield return ReadyHost();EvidencePublisher();
            var content=host.Current;int browserId=browser.GetInstanceID();var pointer=host.Selection.Json();string session=agent.Observe().session;
            yield return RetentionInspect(saved.Id);var inspected=retentionOutput.DeepClone();var inspectionState=retentionState.DeepClone();
            yield return RetentionCall("export",RetentionArgs());Assert.That(evidencePublications,Is.EqualTo(1));Assert.That((string)retentionOutput["manifestHash"],Is.Not.EqualTo(saved.Receipt.ManifestHash));Assert.That((int)retentionOutput["excludedFiles"],Is.EqualTo(1));Assert.That(host.Current,Is.SameAs(content));Assert.That(browser.GetInstanceID(),Is.EqualTo(browserId));Assert.That(JToken.DeepEquals(pointer,host.Selection.Json()),Is.True);Assert.That(agent.Observe().session,Is.EqualTo(session));Assert.That(File.ReadAllText(Path.Combine(data,"private-receipts.json")),Is.EqualTo("must remain private"));
            Directory.CreateDirectory(Path.Combine(directory,"export-check"));
            using(var input=File.OpenRead(publishedEvidence))using(var staged=WorkspaceArchive.Stage(input,Path.Combine(directory,"export-check"))){Assert.That(staged.Receipt.ManifestHash,Is.EqualTo((string)retentionOutput["manifestHash"]));Assert.That(File.ReadAllText(Path.Combine(staged.DirectoryPath,"room.v3.json")),Does.Contain("Edited after import"));Assert.That(File.Exists(Path.Combine(staged.DirectoryPath,"private-receipts.json")),Is.False);}
            string proof=Environment.GetEnvironmentVariable("MAESTRO_RETENTION_FLOW");if(!string.IsNullOrEmpty(proof)){Directory.CreateDirectory(proof);File.Copy(publishedEvidence,Path.Combine(proof,"workspace.zip"),true);File.WriteAllText(Path.Combine(proof,"retention.json"),new JObject {["inspection"]=inspected,["entry"]=retentionEntry,["export"]=retentionOutput,["inspectExecution"]=inspectionState,["exportExecution"]=retentionState,["status"]=host.Retention.Status()}.ToString());}
        }
        [UnityTest] public IEnumerator RetentionInspectionCannotExportAnActiveOrSupersededChoice()
        {
            var saved=Prepare(Archive("Current"));var previous=Prepare(Archive("Previous"));store.Activate(saved.Id,saved.Receipt.ManifestHash,"initial",previous.Id,previous.Receipt.ManifestHash);Open();yield return ReadyHost();EvidencePublisher();
            yield return RetentionInspect(saved.Id);Assert.That(host.Retention.CanStart("export",RetentionArgs(),out _),Is.False);yield return RetentionInspect(previous.Id);var stale=RetentionArgs();yield return RetentionInspect(previous.Id);Assert.That(host.Retention.CanStart("export",stale,out _),Is.False);
            EvidencePublisher(path=>throw new IOException("No publication"));var job=host.Retention.Start("export",RetentionArgs());while(job.Pending)yield return null;Assert.That(job.Phase,Is.EqualTo("failed"));Assert.That(host.Runtime.TryRead("workspace.retention",1,new JObject(),out var failure),Is.True);Assert.That((string)JObject.FromObject(failure.Value)["phase"],Is.EqualTo("failed"));Assert.That(Directory.GetFiles(Path.Combine(directory,"evidence-cache")),Is.Empty);Assert.That(Directory.Exists(store.DataDirectory(host.Selection.Previous)),Is.True);
        }
        [UnityTest] public IEnumerator RetainedPublisherCancellationKeepsOwnershipAndCancelledReceiptUntilWorkerDrains()
        {
            var saved=Prepare(Archive("Previous"));Open();yield return ReadyHost();yield return RetentionInspect(saved.Id);using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();EvidencePublisher(path=>{entered.Set();if(!release.Wait(15000))throw new TimeoutException();return "Downloads/Maestro/"+Path.GetFileName(path);});
            var actions=new RoomExecutions(host.Current.Editor,host);var request=MaintenanceStart(actions,"workspace.retention.export",RetentionArgs());Assert.That(actions.Execute(request,out var error),Is.True,error);
            try{float deadline=Time.realtimeSinceStartup+10;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);host.Runtime.Scheduler.StopAll();Assert.That(host.Retention.Busy,Is.True);Assert.That(host.Evidence.CanStart("inspect",new JObject(),out _),Is.False);Assert.That(host.Recovery.CanInspect(out _),Is.False);Assert.That(host.Export.CanStart(out _),Is.False);Assert.That((bool)host.Activation.Current()["changing"],Is.True);}finally{release.Set();}
            while(host.Retention.Busy)yield return null;yield return null;Assert.That((string)host.Retention.Status()["phase"],Is.EqualTo("exported"));Assert.That((string)host.Runtime.Scheduler.Receipts.Find((string)request["runId"])["phase"],Is.EqualTo("cancelled"));
        }
        [UnityTest] public IEnumerator RetentionWorkerKeepsPathLeaseAcrossDestroyedHostAndDoesNotResumeOldJob()
        {
            var saved=Prepare(Archive("Previous"));Open();yield return ReadyHost();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.Retention.Fault=point=>{if(point=="retention.worker"){entered.Set();if(!release.Wait(15000))throw new TimeoutException();}};host.Retention.Start("inspect",new JObject());var old=host;
            try{float deadline=Time.realtimeSinceStartup+10;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);UnityEngine.Object.Destroy(root);yield return null;BuildShell(directory);Open();Assert.That(host.Ready,Is.False);Assert.That(old.Retirement.IsCompleted,Is.False);}finally{release.Set();}
            yield return ReadyHost();Assert.That(old.Retirement.IsCompleted,Is.True);Assert.That((string)host.Retention.Status()["phase"],Is.EqualTo("idle"));yield return RetentionInspect(saved.Id);
        }
    }
}
