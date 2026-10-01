// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        sealed class ActivationPicker:IWorkspaceArchivePicker
        {
            public string CacheRoot {get;}
            readonly string path;
            public bool ReadyToStart=>true;
            internal ActivationPicker(string directory,byte[] bytes)
            {
                CacheRoot=Path.Combine(directory,"picker");var folder=Path.Combine(CacheRoot,Guid.NewGuid().ToString("D"));Directory.CreateDirectory(folder);
                path=Path.Combine(folder,Guid.NewGuid().ToString("D"));File.WriteAllBytes(path,bytes);
            }
            public void Start(string id){}
            public JObject Read(string id)=>new JObject {["id"]=id,["phase"]="selected",["name"]="Imported workspace.zip",["path"]=path,["error"]=""};
            public void Release(string id){}
        }
        JObject activationArgs;Action afterContentBuild;
        IEnumerator ReadyForActivation()
        {
            Open();Assert.That(host.Current,Is.Not.Null,host.Status);host.Current.Rules.Modules.Flush();
            float deadline=Time.realtimeSinceStartup+20;
            while(host.Current.Editor.Find("maestro").GetComponent<MaestroAvatar>().ModelBusy&&Time.realtimeSinceStartup<deadline)yield return null;
            host.Import.InitializeForTests(directory,new ActivationPicker(directory,Archive("Activated robot")));
            var actions=new RoomExecutions(host.Current.Editor,host);var select=MaintenanceStart(actions,"workspace.archive.select",new JObject());Assert.That(actions.Execute(select,out var issue),Is.True,issue);
            string request=(string)actions.Observe()["workspace"]["selected"]["output"]["requestId"];
            do {host.Import.Poll();yield return null;}while((string)host.Import.ReadSelection(request)["phase"]!="prepared"&&Time.realtimeSinceStartup<deadline);
            var preview=host.Import.ReadSelection(request);Assert.That((string)preview["phase"],Is.EqualTo("prepared"),preview.ToString());
            activationArgs=new JObject {["selectionRequestId"]=request,["generationId"]=(string)preview["generationId"],["manifestHash"]=(string)preview["manifestHash"],["expectedRevision"]=host.Selection.Revision};
        }
        (JObject Request,string Id) BeginActivation()
        {
            var actions=new RoomExecutions(host.Current.Editor,host);var request=MaintenanceStart(actions,"workspace.archive.activate",activationArgs);
            Assert.That(actions.Execute(request,out var issue),Is.True,issue);
            string id=(string)actions.Observe()["workspace"]["selected"]["output"]["requestId"];Assert.That(id,Has.Length.EqualTo(32));return(request,id);
        }
        IEnumerator FinishActivation(string id)
        {
            float deadline=Time.realtimeSinceStartup+25;
            while(host.Activation.Busy&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Activation.Busy,Is.False,host.Activation.Read(id)?.ToString());
        }
        RoomObjectData AcceptedEdit(string label)
        {
            var editor=host.Current.Editor;var data=editor.Snapshot().objects.First(x=>!x.IsBuiltIn);data.name=label;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var issue),Is.True,issue);return data;
        }
        void AssertActivated(string id)
        {
            Assert.That(host.Current,Is.Not.Null,host.Status);Assert.That(host.Selection.Active.Generation,Is.EqualTo((string)activationArgs["generationId"]));
            Assert.That((string)host.Activation.Read(id)["phase"],Is.EqualTo("review"));Assert.That(host.Current.Editor.RuntimeGate.Held,Is.True);
            Assert.That(host.Current.Editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Activated robot"));Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator SharedActivationPreservesAcceptedContentAndReconcilesItsReceiptAfterRestart()
        {
            yield return ReadyForActivation();var original=host.Current;var data=AcceptedEdit("Most recent accepted edit");string oldSession=agent.Observe().session;int browserId=browser.GetInstanceID();
            var stale=(JObject)activationArgs.DeepClone();stale["expectedRevision"]=new string('e',32);Assert.That(host.Activation.CanStart(stale,out _),Is.False);Assert.That(original.Editor.WriteGate.Frozen,Is.False);
            var (request,id)=BeginActivation();Assert.That(original.Editor.WriteGate.Frozen,Is.True);Assert.That(host.Import.CanCancel((string)activationArgs["selectionRequestId"],out _),Is.False);
            yield return FinishActivation(id);AssertActivated(id);Assert.That(!original,Is.True);Assert.That(agent.Observe().session,Is.Not.EqualTo(oldSession));Assert.That(browser.GetInstanceID(),Is.EqualTo(browserId));
            var retained=new RoomStorage(store.DataDirectory(host.Selection.Previous)).Load(out var error);Assert.That(error,Is.Null);Assert.That(retained.objects.Single(x=>x.id==data.id).name,Is.EqualTo(data.name));
            var actions=new RoomExecutions(host.Current.Editor,host);Assert.That(actions.Execute(request,out error),Is.True,error);Assert.That((string)actions.Observe()["workspace"]["selected"]["output"]["requestId"],Is.EqualTo(id));
            string path=directory;var selected=host.Selection.Json();UnityEngine.Object.Destroy(root);yield return null;BuildShell(path);Open();yield return ReadyHost();AssertActivated(id);Assert.That(JToken.DeepEquals(selected,host.Selection.Json()),Is.True);
            Assert.That(new RoomExecutions(host.Current.Editor,host).Execute(request,out error),Is.True,error);Assert.That(builds,Is.EqualTo(1));Assert.That(host.Activation.CanCancel(id,out _),Is.False);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_ACTIVATION_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"activated.json"),new JObject {["current"]=host.Activation.Current(),["activation"]=host.Activation.Read(id),["execution"]=new RoomExecutions(host.Current.Editor,host).Observe()}.ToString());}
        }
        [UnityTest] public IEnumerator CancellationKeepsEditsFrozenUntilTheWorkerFinishesEvenWhenTheHostIsDisabled()
        {
            yield return ReadyForActivation();var editor=host.Current.Editor;var data=AcceptedEdit("Before cancellation");
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            host.Activation.Fault=point=>{if(point=="restore.beforeActivation"){entered.Set();if(!release.Wait(TimeSpan.FromSeconds(20)))throw new IOException("Test worker timeout");}};
            try {
                var (_,id)=BeginActivation();float deadline=Time.realtimeSinceStartup+15;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);
                var actions=new RoomExecutions(editor,host);Assert.That(actions.Execute(MaintenanceStart(actions,"workspace.archive.activation.cancel",new JObject {["requestId"]=id}),out var error),Is.True,error);
                root.SetActive(false);yield return null;Assert.That(editor.WriteGate.Frozen,Is.True);Assert.That(host.Activation.WorkerPending,Is.True);
                release.Set();while(host.Activation.WorkerPending&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(host.Activation.WorkerPending,Is.False);Assert.That(editor.WriteGate.Frozen,Is.True);
                root.SetActive(true);yield return FinishActivation(id);Assert.That(host.Current.Editor,Is.SameAs(editor));Assert.That(editor.WriteGate.Frozen,Is.False);Assert.That((string)host.Activation.Read(id)["phase"],Is.EqualTo("cancelled"));
                Assert.That(store.Load().Revision,Is.EqualTo("initial"));Assert.That(editor.Snapshot().objects.Single(x=>x.id==data.id).name,Is.EqualTo(data.name));Assert.That((string)host.Import.ReadSelection((string)activationArgs["selectionRequestId"])["phase"],Is.EqualTo("prepared"));AcceptedEdit("Editable after cancellation");
            }finally{release.Set();}
        }
        [UnityTest] public IEnumerator FailedRetentionLeavesTheLiveOwnersAndAcceptedEditsAvailable()
        {
            yield return ReadyForActivation();var editor=host.Current.Editor;var data=AcceptedEdit("Retention failure keeps this");host.Activation.Fault=point=>{if(point=="restore.beforeRetention")throw new IOException("Simulated full storage");};
            var (_,id)=BeginActivation();yield return FinishActivation(id);Assert.That((string)host.Activation.Read(id)["phase"],Is.EqualTo("failed"));Assert.That(host.Current.Editor,Is.SameAs(editor));Assert.That(editor.WriteGate.Frozen,Is.False);
            Assert.That(store.Load().Revision,Is.EqualTo("initial"));Assert.That(editor.Snapshot().objects.Single(x=>x.id==data.id).name,Is.EqualTo(data.name));Assert.That(Directory.GetFiles(Path.Combine(directory,"workspace-activation.v1","captures")),Is.Empty);
            AcceptedEdit("Still editable");
        }
        [UnityTest] public IEnumerator FailedContentOpeningCanReconcileToAHealthyReviewedWorkspaceOnRestart()
        {
            yield return ReadyForActivation();afterContentBuild=()=>throw new IOException("Simulated content initialization failure");
            var (_,id)=BeginActivation();yield return FinishActivation(id);Assert.That(host.Current,Is.Null);Assert.That((string)host.Activation.Read(id)["phase"],Is.EqualTo("unavailable"));Assert.That((string)host.Activation.Read(id)["committedRevision"],Is.EqualTo(store.Load().Revision));
            string path=directory;UnityEngine.Object.Destroy(root);yield return null;BuildShell(path);Open();yield return ReadyHost();AssertActivated(id);Assert.That(builds,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator SynchronousCaptureFailureLeavesAnHonestTerminalJobAndReleasesEditing()
        {
            yield return ReadyForActivation();host.Activation.Fault=point=>{if(point=="restore.beforeCapture")throw new IOException("Simulated capture startup failure");};
            var actions=new RoomExecutions(host.Current.Editor,host);Assert.That(actions.Execute(MaintenanceStart(actions,"workspace.archive.activate",activationArgs),out _),Is.False);
            string id=(string)host.Activation.Current()["activationRequestId"];Assert.That((string)host.Activation.Read(id)["phase"],Is.EqualTo("failed"));Assert.That(host.Activation.Busy,Is.False);Assert.That(host.Current.Editor.WriteGate.Frozen,Is.False);Assert.That(store.Load().Revision,Is.EqualTo("initial"));
        }
        [UnityTest] public IEnumerator PostCommitFailureIsReconciledAsCommittedInsteadOfReportedAsAnUnchangedRoom()
        {
            yield return ReadyForActivation();AcceptedEdit("Kept before commit");host.Activation.Fault=point=>{if(point=="pointer.afterCommit")throw new IOException("Simulated acknowledgement failure");};
            var (_,id)=BeginActivation();yield return FinishActivation(id);AssertActivated(id);Assert.That((string)host.Activation.Read(id)["committedRevision"],Is.EqualTo(store.Load().Revision));
        }
        [UnityTest] public IEnumerator UnreadableCommitOutcomeDoesNotReleaseOldOwnersForFurtherEditing()
        {
            yield return ReadyForActivation();var editor=host.Current.Editor;host.Activation.Fault=point=>{if(point=="pointer.afterCommit"){File.WriteAllText(Path.Combine(directory,"workspace-generations.v1","current.v1.json"),"{damaged");throw new IOException("Simulated unreadable commit outcome");}};
            var (_,id)=BeginActivation();float deadline=Time.realtimeSinceStartup+15;while(host.Activation.WorkerPending&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(host.Activation.WorkerPending,Is.False);yield return null;
            Assert.That(host.Current.Editor,Is.SameAs(editor));Assert.That(editor.WriteGate.Frozen,Is.True);Assert.That((string)host.Activation.Read(id)["phase"],Is.EqualTo("unavailable"));Assert.That(host.Activation.CanCancel(id,out _),Is.False);
            var data=editor.Snapshot().objects.First(x=>!x.IsBuiltIn);data.name="Must not be accepted into a stale workspace";Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out _),Is.False);
            Assert.That(Directory.GetDirectories(Path.Combine(directory,"workspace-generations.v1","generations")).Length,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator ReservedFailedPairCannotBeRetriedAfterEditingResumes()
        {
            yield return ReadyForActivation();host.Activation.Fault=point=>{if(point=="activation.reserved")throw new IOException("Simulated interruption before pointer commit");};
            var (_,id)=BeginActivation();yield return FinishActivation(id);Assert.That((string)host.Activation.Read(id)["phase"],Is.EqualTo("failed"));Assert.That(store.Load().Revision,Is.EqualTo("initial"));
            Assert.That((string)host.Import.ReadSelection((string)activationArgs["selectionRequestId"])["phase"],Is.EqualTo("retained"));AcceptedEdit("Later edit cannot reuse the older retained snapshot");Assert.That(host.Activation.CanStart(activationArgs,out _),Is.False);Assert.That(host.Import.CanSelect(out var error),Is.True,error);
        }
        [UnityTest] public IEnumerator StartupOpensAnExactlyCommittedActivationWithoutReplayingAnInterruptedReplacement()
        {
            yield return ReadyForActivation();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            host.Activation.Fault=point=>{if(point=="pointer.afterCommit"){entered.Set();if(!release.Wait(TimeSpan.FromSeconds(20)))throw new IOException("Test worker timeout");}};
            try {
                var (_,id)=BeginActivation();float deadline=Time.realtimeSinceStartup+15;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);
                root.SetActive(false);release.Set();while(host.Activation.WorkerPending&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(host.Activation.WorkerPending,Is.False);
                string path=directory;var selected=store.Load().Json();UnityEngine.Object.Destroy(root);yield return null;BuildShell(path);Open();yield return ReadyHost();AssertActivated(id);Assert.That(builds,Is.EqualTo(1));Assert.That(JToken.DeepEquals(selected,store.Load().Json()),Is.True);
            }finally{release.Set();}
        }
        [UnityTest] public IEnumerator TeardownWaitsForRetentionBeforeReleasingItsOwnerThreadHold()
        {
            yield return ReadyForActivation();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            host.Activation.Fault=point=>{if(point=="restore.beforeActivation"){entered.Set();if(!release.Wait(TimeSpan.FromSeconds(20)))throw new IOException("Test worker timeout");}};
            try {
                var (_,id)=BeginActivation();float deadline=Time.realtimeSinceStartup+15;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);
                var operation=host.Activation;string path=directory;UnityEngine.Object.Destroy(root);yield return null;Assert.That(operation.WorkerPending,Is.True);release.Set();while(operation.WorkerPending&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(operation.WorkerPending,Is.False);yield return null;
                BuildShell(path);Open();yield return ReadyHost();Assert.That(store.Load().Revision,Is.EqualTo("initial"));Assert.That(host.Activation.Read(id)["phase"].ToString(),Is.EqualTo("cancelled"));Assert.That(host.Current.Editor.WriteGate.Frozen,Is.False);
            }finally{release.Set();}
        }
        [Test] public void PersistentBrowserRetriesTheLatestLibraryCloseUntilThePageAcknowledgesIt()
        {
            string shown="{\"session\":\"old\",\"revision\":1,\"visible\":true}",closed="{\"session\":\"old\",\"revision\":2,\"visible\":false}";int published=0;string last=null;void Publish(string value){published++;last=value;}
            browser.PublishLibraryState(shown);browser.SetSuspended(true);browser.PublishLibraryState(closed);browser.DeliverLibraryState(true,null,Publish);Assert.That(published,Is.Zero);
            browser.SetSuspended(false);browser.DeliverLibraryState(false,null,Publish);Assert.That(published,Is.Zero);browser.DeliverLibraryState(true,new BookSnapshot {librarySession="old",libraryRevision=1},Publish);Assert.That(last,Is.EqualTo(closed));
            browser.DeliverLibraryState(true,null,Publish);Assert.That(published,Is.EqualTo(2));browser.DeliverLibraryState(true,new BookSnapshot {librarySession="old",libraryRevision=2},Publish);Assert.That(published,Is.EqualTo(2));
            browser.SetSuspended(true);browser.SetSuspended(false);browser.DeliverLibraryState(true,null,Publish);Assert.That(published,Is.EqualTo(3));
        }
    }
}
