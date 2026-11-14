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
        JObject evidenceOutput,evidenceState,evidenceEntry;string evidenceInspection,evidenceRun,publishedEvidence;int evidencePublications;
        void EvidencePublisher(Func<string,string> custom=null)
        {
            host.Export.InitializeForTests(host.Current?host.Current.Editor:null,host.Current?host.Current.Rules:null,host.Current?host.Current.Controls:null,Path.Combine(directory,"evidence-cache"),custom??(path=>{
                string output=Path.Combine(directory,"published");Directory.CreateDirectory(output);publishedEvidence=Path.Combine(output,Path.GetFileName(path));File.Copy(path,publishedEvidence);Interlocked.Increment(ref evidencePublications);return "Downloads/Maestro/"+Path.GetFileName(path);
            }));
        }
        IEnumerator EvidenceReady()
        {
            evidencePublications=0;DamageHistory("recovery");Open();yield return ReadyHost();yield return RepairHistory("recovery");EvidencePublisher();
        }
        IEnumerator EvidenceCall(string mode,JObject args)
        {
            var actions=new RoomExecutions(host.Current?host.Current.Editor:null,host);var request=MaintenanceStart(actions,"workspace.evidence."+mode,args);evidenceRun=(string)request["runId"];Assert.That(actions.Execute(request,out var error),Is.True,error);
            float deadline=Time.realtimeSinceStartup+20;while(host.Evidence.Busy&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;
            evidenceState=actions.Observe();Assert.That((string)host.Evidence.Status()["requestId"],Is.EqualTo(evidenceRun));Assert.That((string)evidenceState["workspace"]["selected"]["phase"],Is.EqualTo("completed"),evidenceState.ToString());evidenceOutput=(JObject)evidenceState["workspace"]["selected"]["output"];
            Assert.That(host.Runtime.TryRead("workspace.evidence",1,new JObject(),out var fact),Is.True);Assert.That(JToken.DeepEquals(JObject.FromObject(fact.Value),host.Evidence.Status()),Is.True);
            Assert.That(actions.Execute(request,out error),Is.True,error);
        }
        IEnumerator EvidenceInspect()
        {
            yield return EvidenceCall("inspect",new JObject());evidenceInspection=(string)evidenceOutput["inspectionId"];Assert.That((int)evidenceOutput["count"],Is.EqualTo(1));
            Assert.That(host.Runtime.TryRead("workspace.evidence.entry",1,new JObject {["inspectionId"]=evidenceInspection,["index"]=0},out var fact),Is.True);evidenceEntry=JObject.FromObject(fact.Value);
        }
        JObject EvidenceArgs(string run=null)
        {
            var value=new JObject {["inspectionId"]=evidenceInspection,["evidenceId"]=evidenceEntry["evidenceId"].DeepClone(),["fingerprint"]=evidenceEntry["fingerprint"].DeepClone()};if(run!=null)value["exportRunId"]=run;return value;
        }
        [UnityTest] public IEnumerator SharedEvidenceExportAndRemovalPreserveWorkspaceAndUseExactDurableReceipt()
        {
            yield return EvidenceReady();var current=host.Current;var selection=host.Selection.Json();string session=agent.Observe().session;var actions=new RoomExecutions(current.Editor,host);var roomRun=actions.Observe()["nextRunId"].DeepClone();
            yield return EvidenceInspect();var inspection=evidenceOutput.DeepClone();var inspectionState=evidenceState.DeepClone();
            Assert.That(host.Evidence.CanStart("remove",EvidenceArgs(Guid.NewGuid().ToString("N")),out _),Is.False);
            yield return EvidenceCall("export",EvidenceArgs());string exportedRun=evidenceRun;var exported=evidenceOutput.DeepClone();var exportState=evidenceState.DeepClone();Assert.That(evidencePublications,Is.EqualTo(1));Assert.That(File.Exists(publishedEvidence),Is.True);string archive=publishedEvidence;
            yield return EvidenceCall("remove",EvidenceArgs(exportedRun));Assert.That(evidencePublications,Is.EqualTo(1));Assert.That(File.Exists(archive),Is.True);Assert.That(host.Current,Is.SameAs(current));Assert.That(JToken.DeepEquals(selection,host.Selection.Json()),Is.True);Assert.That(agent.Observe().session,Is.EqualTo(session));Assert.That(JToken.DeepEquals(roomRun,actions.Observe()["nextRunId"]),Is.True);
            Assert.That(Directory.GetDirectories(Path.Combine(directory,"workspace-history.v1","evidence")),Is.Empty);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_EVIDENCE_FLOW");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.Copy(archive,Path.Combine(evidence,"evidence.zip"),true);File.WriteAllText(Path.Combine(evidence,"maintenance.json"),new JObject {["inspection"]=inspection,["entry"]=evidenceEntry,["export"]=exported,["removed"]=evidenceOutput,["inspectExecution"]=inspectionState,["exportExecution"]=exportState,["removeExecution"]=evidenceState,["status"]=host.Evidence.Status()}.ToString());}
        }
        [UnityTest] public IEnumerator RetainedExportReceiptWorksAfterRestartWithANewInspection()
        {
            yield return EvidenceReady();yield return EvidenceInspect();yield return EvidenceCall("export",EvidenceArgs());string run=evidenceRun,oldInspection=evidenceInspection,archive=publishedEvidence;
            UnityEngine.Object.Destroy(root);yield return null;BuildShell(directory);Open();yield return ReadyHost();EvidencePublisher();yield return EvidenceInspect();Assert.That(evidenceInspection,Is.Not.EqualTo(oldInspection));Assert.That(host.Evidence.CanStart("remove",EvidenceArgs(run),out var error),Is.True,error);
            yield return EvidenceCall("remove",EvidenceArgs(run));Assert.That(File.Exists(archive),Is.True);Assert.That(host.Current,Is.Not.Null);
        }
        [UnityTest] public IEnumerator ChangedEvidenceAndAnotherExportReceiptCannotAuthorizeRemoval()
        {
            yield return EvidenceReady();yield return EvidenceInspect();yield return EvidenceCall("export",EvidenceArgs());string run=evidenceRun,path=Path.Combine(directory,"workspace-history.v1","evidence",(string)evidenceEntry["evidenceId"],"accepted.json");
            File.AppendAllText(path,"new bytes");var stale=host.Evidence.Start("remove",EvidenceArgs(run));while(stale.Pending)yield return null;Assert.That(stale.Phase,Is.EqualTo("failed"));Assert.That(File.ReadAllText(path),Does.EndWith("new bytes"));
            yield return EvidenceInspect();Assert.That(host.Evidence.CanStart("remove",EvidenceArgs(run),out _),Is.False);Assert.That(host.Evidence.CanStart("remove",EvidenceArgs((string)historyResetState["workspace"]["selected"]["id"]),out _),Is.False);
        }
        [UnityTest] public IEnumerator PublicationFailureNeverAuthorizesRemovalAndCleansItsPrivateCapture()
        {
            yield return EvidenceReady();yield return EvidenceInspect();EvidencePublisher(path=>throw new IOException("Publication failed"));var actions=new RoomExecutions(host.Current.Editor,host);var request=MaintenanceStart(actions,"workspace.evidence.export",EvidenceArgs());Assert.That(actions.Execute(request,out var error),Is.True,error);
            while(host.Evidence.Busy)yield return null;yield return null;Assert.That((string)actions.Observe()["workspace"]["selected"]["phase"],Is.EqualTo("failed"));Assert.That(host.Evidence.CanStart("remove",EvidenceArgs((string)request["runId"]),out _),Is.False);Assert.That(Directory.GetFiles(Path.Combine(directory,"evidence-cache")),Is.Empty);Assert.That(Directory.GetDirectories(Path.Combine(directory,"workspace-history.v1","evidence")),Has.Length.EqualTo(1));
        }
        [UnityTest] public IEnumerator StopWhilePublisherOwnsFileDrainsBeforeAnotherOperationAndCannotClaimAnExportReceipt()
        {
            yield return EvidenceReady();yield return EvidenceInspect();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();EvidencePublisher(path=>{entered.Set();if(!release.Wait(15000))throw new TimeoutException();return "Downloads/Maestro/"+Path.GetFileName(path);});
            var actions=new RoomExecutions(host.Current.Editor,host);var request=MaintenanceStart(actions,"workspace.evidence.export",EvidenceArgs());Assert.That(actions.Execute(request,out var error),Is.True,error);
            try{
                float deadline=Time.realtimeSinceStartup+10;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);host.Runtime.Scheduler.StopAll();Assert.That(host.Evidence.Busy,Is.True);Assert.That(host.History.CanInspect("recovery",out _),Is.False);Assert.That(host.Recovery.CanInspect(out _),Is.False);
            }finally{release.Set();}while(host.Evidence.Busy)yield return null;yield return null;
            Assert.That((string)host.Evidence.Status()["phase"],Is.EqualTo("exported"));Assert.That((string)host.Runtime.Scheduler.Receipts.Find((string)request["runId"])["phase"],Is.EqualTo("cancelled"));Assert.That(host.Evidence.CanStart("remove",EvidenceArgs((string)request["runId"]),out _),Is.False);
        }
        [UnityTest] public IEnumerator EvidenceMaintenanceWithoutRoomDoesNotRecoverOrApproveContent()
        {
            DamageHistory("recovery");MissingSelection();yield return RepairHistory("recovery");EvidencePublisher();yield return EvidenceInspect();yield return EvidenceCall("export",EvidenceArgs());string run=evidenceRun;yield return EvidenceCall("remove",EvidenceArgs(run));Assert.That(host.Current,Is.Null);Assert.That(host.Selection,Is.Null);
        }
        [UnityTest] public IEnumerator OldEvidenceWorkerRetainsPathOwnershipAcrossHostReplacement()
        {
            yield return EvidenceReady();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.Evidence.Fault=point=>{if(point=="evidence.worker"){entered.Set();if(!release.Wait(15000))throw new TimeoutException();}};host.Evidence.Start("inspect",new JObject());var old=host;
            try{
                float deadline=Time.realtimeSinceStartup+10;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);UnityEngine.Object.Destroy(root);yield return null;BuildShell(directory);Open();Assert.That(host.Ready,Is.False);Assert.That(old.Retirement.IsCompleted,Is.False);
            }finally{release.Set();}yield return ReadyHost();Assert.That(old.Retirement.IsCompleted,Is.True);yield return EvidenceInspect();
        }
    }
}
