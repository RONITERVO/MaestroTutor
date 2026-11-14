// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
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
        JObject historyInspection,historyReset,historyInspectState,historyResetState;
        void DamageHistory(string target){string parent=Path.Combine(directory,"workspace-"+target+".v1");Directory.CreateDirectory(parent);File.WriteAllText(Path.Combine(parent,"latest.json"),"{ damaged "+target);}
        IEnumerator HistoryCall(string action,JObject args)
        {
            var actions=new RoomExecutions(host.Current?host.Current.Editor:null,host);var request=MaintenanceStart(actions,action,args);Assert.That(actions.Execute(request,out var error),Is.True,error);
            float deadline=Time.realtimeSinceStartup+15;while(host.History.Busy&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;
            var state=actions.Observe();Assert.That((string)state["workspace"]["selected"]["phase"],Is.EqualTo("completed"),state.ToString());
            var output=(JObject)state["workspace"]["selected"]["output"];Assert.That(host.Runtime.TryRead("workspace.history",1,new JObject(),out var fact),Is.True);Assert.That(JToken.DeepEquals(JObject.FromObject(fact.Value),host.History.Status()),Is.True);
            if(action.EndsWith("inspect",StringComparison.Ordinal)){historyInspection=output;historyInspectState=state;}else{historyReset=output;historyResetState=state;}
            Assert.That(actions.Execute(request,out error),Is.True,error); // Existing receipt cannot dispatch twice.
        }
        IEnumerator RepairHistory(string target)
        {
            yield return HistoryCall("workspace.history.inspect",new JObject {["target"]=target});
            Assert.That(host.History.CanReset(Guid.NewGuid().ToString("N"),(string)historyInspection["fingerprint"],out _),Is.False);
            yield return HistoryCall("workspace.history.reset",new JObject {["inspectionId"]=historyInspection["requestId"].DeepClone(),["fingerprint"]=historyInspection["fingerprint"].DeepClone()});
            Assert.That((string)host.History.Status()["phase"],Is.EqualTo("reset"));Assert.That(host.History.CanInspect(target,out _),Is.False);
            string evidence=Path.Combine(directory,"workspace-history.v1","evidence",(string)historyReset["evidenceId"]);Assert.That(File.ReadAllText(Path.Combine(evidence,"original")),Is.EqualTo("{ damaged "+target));
        }
        IEnumerator RepairsOnlyTracking(string target)
        {
            DamageHistory(target);Open();yield return ReadyHost();var content=host.Current;var selection=host.Selection.Json();string session=agent.Observe().session;var actions=new RoomExecutions(content.Editor,host);var roomNext=actions.Observe()["nextRunId"].DeepClone();
            yield return RepairHistory(target);Assert.That(host.Current,Is.SameAs(content));Assert.That(JToken.DeepEquals(selection,host.Selection.Json()),Is.True);Assert.That(agent.Observe().session,Is.EqualTo(session));Assert.That(JToken.DeepEquals(roomNext,actions.Observe()["nextRunId"]),Is.True);Assert.That(content.Editor.RuntimeGate.Held,Is.False);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_HISTORY_EVIDENCE");if(target=="recovery"&&!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"history.json"),new JObject {["inspection"]=historyInspection,["reset"]=historyReset,["inspectExecution"]=historyInspectState,["resetExecution"]=historyResetState,["status"]=host.History.Status()}.ToString());}
        }
        [UnityTest] public IEnumerator SharedHistoryRepairsActivationWithoutChangingLiveWorkspace(){yield return RepairsOnlyTracking("activation");}
        [UnityTest] public IEnumerator SharedHistoryRepairsReviewWithoutChangingLiveWorkspace(){yield return RepairsOnlyTracking("review");}
        [UnityTest] public IEnumerator SharedHistoryRepairsRecoveryWithoutChangingLiveWorkspace(){yield return RepairsOnlyTracking("recovery");}
        [UnityTest] public IEnumerator HistoryResetNeverApprovesContentAndNewReviewStillCompletes()
        {
            var incoming=Prepare(Archive("Incoming"));var retained=Prepare(Archive("Previous"));store.Activate(incoming.Id,incoming.Receipt.ManifestHash,"initial",retained.Id,retained.Receipt.ManifestHash);DamageHistory("review");Open();yield return ReadyHost();yield return ReadyReviewOwners();
            var selection=host.Selection.Json();yield return RepairHistory("review");Assert.That(host.ReviewRequired,Is.True);Assert.That(host.Current.Editor.RuntimeGate.Held,Is.True);Assert.That(physics.Running,Is.False);Assert.That(JToken.DeepEquals(selection,host.Selection.Json()),Is.True);
            string id=BeginReview();yield return FinishReview(id);Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("prepared"));ApproveReview(id);yield return FinishReview(id);Assert.That(host.ReviewRequired,Is.False);Assert.That(physics.Running,Is.False);
        }
        [UnityTest] public IEnumerator HistoryRepairWorksWithoutCurrentOwnersThenFreshRecoveryStillNeedsReview()
        {
            DamageHistory("recovery");MissingSelection();yield return RepairHistory("recovery");Assert.That(host.Current,Is.Null);yield return ChooseFreshRecovery();RecoveryCall("workspace.recovery.commit",recoveryArgs);yield return FinishRecovery("review");Assert.That(host.ReviewRequired,Is.True);Assert.That(host.Current.Editor.RuntimeGate.Held,Is.True);
        }
        [UnityTest] public IEnumerator ChangedHistoryFailsResetAndConsumesInspection()
        {
            DamageHistory("activation");Open();yield return ReadyHost();var inspected=host.History.Inspect("activation");while(inspected.Pending)yield return null;
            string path=Path.Combine(directory,"workspace-activation.v1","latest.json");File.WriteAllText(path,"changed after inspection");var reset=host.History.Reset(inspected.Id,inspected.Snapshot.Fingerprint);while(reset.Pending)yield return null;
            Assert.That(reset.Error,Is.Not.Null);Assert.That(host.Activation.HistoryUnavailable,Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo("changed after inspection"));Assert.That(host.History.CanReset(inspected.Id,inspected.Snapshot.Fingerprint,out _),Is.False);
        }
        [UnityTest] public IEnumerator PausedHistoryWorkerKeepsExclusiveOwnershipUntilItDrains()
        {
            DamageHistory("recovery");Open();yield return ReadyHost();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.History.Fault=point=>{if(point=="history.worker"){entered.Set();if(!release.Wait(15000))throw new TimeoutException();}};
            var job=host.History.Inspect("recovery");try{
                float deadline=Time.realtimeSinceStartup+10;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);host.History.Pause(true);
                Assert.That(host.History.Busy,Is.True);Assert.That(host.Recovery.CanInspect(out _),Is.False);Assert.That(host.Import.CanSelect(out _),Is.False);Assert.That(host.History.CanInspect("recovery",out _),Is.False);
            }finally{release.Set();}while(job.Pending)yield return null;Assert.That(job.Phase,Is.EqualTo("cancelled"));Assert.That(host.Recovery.HistoryUnavailable,Is.True);host.History.Pause(false);Assert.That(host.History.CanInspect("recovery",out _),Is.True);
        }
        [UnityTest] public IEnumerator PauseAfterRenameStillReportsCommittedResetAndRestartNeverReplaysIt()
        {
            DamageHistory("review");Open();yield return ReadyHost();var inspected=host.History.Inspect("review");while(inspected.Pending)yield return null;
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.History.Fault=point=>{if(point=="history.afterMove"){entered.Set();if(!release.Wait(15000))throw new TimeoutException();}};
            var reset=host.History.Reset(inspected.Id,inspected.Snapshot.Fingerprint);try{
                float deadline=Time.realtimeSinceStartup+10;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);host.History.Pause(true);Assert.That(host.History.Busy,Is.True);
            }finally{release.Set();}while(reset.Pending)yield return null;Assert.That(reset.Phase,Is.EqualTo("reset"));Assert.That(host.Review.HistoryUnavailable,Is.False);
            string evidence=Path.Combine(directory,"workspace-history.v1","evidence");int count=Directory.GetDirectories(evidence).Length;UnityEngine.Object.Destroy(root);yield return null;BuildShell(directory);Open();yield return ReadyHost();Assert.That(host.Review.HistoryUnavailable,Is.False);Assert.That((string)host.History.Status()["phase"],Is.EqualTo("idle"));Assert.That(Directory.GetDirectories(evidence).Length,Is.EqualTo(count));
        }
        [UnityTest] public IEnumerator RetiringHistoryWorkerBlocksNewHostUntilOriginalOwnerDrains()
        {
            DamageHistory("recovery");Open();yield return ReadyHost();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.History.Fault=point=>{if(point=="history.worker"){entered.Set();if(!release.Wait(15000))throw new TimeoutException();}};
            host.History.Inspect("recovery");var old=host;try{
                float deadline=Time.realtimeSinceStartup+10;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);UnityEngine.Object.Destroy(root);yield return null;BuildShell(directory);Open();Assert.That(host.Ready,Is.False);Assert.That(old.Retirement.IsCompleted,Is.False);
            }finally{release.Set();}yield return ReadyHost();Assert.That(old.Retirement.IsCompleted,Is.True);Assert.That(host.Recovery.HistoryUnavailable,Is.True);yield return RepairHistory("recovery");
        }
    }
}
