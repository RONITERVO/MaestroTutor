// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Avatar;
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
        IEnumerator ReadyForReview()
        {
            yield return ReadyForActivation();var (_,id)=BeginActivation();yield return FinishActivation(id);AssertActivated(id);
            yield return ReadyReviewOwners();
        }
        IEnumerator ReadyReviewOwners()
        {
            host.Current.Rules.Modules.Flush();float deadline=Time.realtimeSinceStartup+20;
            while(host.Current.Editor.Find("maestro").GetComponent<MaestroAvatar>().ModelBusy&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Current.Editor.Find("maestro").GetComponent<MaestroAvatar>().ModelBusy,Is.False);
        }
        string BeginReview()
        {
            var actions=new RoomExecutions(host.Current.Editor,host);var command=MaintenanceStart(actions,"workspace.review.prepare",new JObject {["expectedRevision"]=host.Selection.Revision});
            Assert.That(actions.Execute(command,out var error),Is.True,error);return (string)actions.Observe()["workspace"]["selected"]["output"]["requestId"];
        }
        IEnumerator FinishReview(string id)
        {
            float deadline=Time.realtimeSinceStartup+20;while(host.Review.Busy&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Review.Busy,Is.False,host.Review.Read(id)?.ToString());
        }
        JObject ReviewArguments(string id)
        {
            var value=host.Review.Read(id);return new JObject {["requestId"]=id,["manifestHash"]=(string)value["manifestHash"],["expectedRevision"]=(string)value["workspace"]["revision"]};
        }
        JObject ApproveReview(string id)
        {
            var actions=new RoomExecutions(host.Current.Editor,host);var command=MaintenanceStart(actions,"workspace.review.complete",ReviewArguments(id));Assert.That(actions.Execute(command,out var error),Is.True,error);return command;
        }
        [UnityTest] public IEnumerator ExactReviewApprovalPreservesLiveOwnersUndoAndAgentSessionAndKeepsEffectsStopped()
        {
            yield return ReadyForReview();var editor=host.Current.Editor;var content=host.Current;string session=agent.Observe().session;var changed=AcceptedEdit("Inspected accepted contents");
            string id=BeginReview();yield return FinishReview(id);Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("prepared"));string hash=(string)host.Review.Read(id)["manifestHash"];
            string same=BeginReview();yield return FinishReview(same);Assert.That((string)host.Review.Read(same)["manifestHash"],Is.EqualTo(hash),"Unchanged accepted content must have stable review identity");Assert.That(host.Review.Read(id),Is.Null,"Older request identities are unavailable, not rebound");
            var prepared=host.Review.Read(same);var request=ApproveReview(same);Assert.That(editor.WriteGate.Frozen,Is.True);yield return FinishReview(same);
            Assert.That((string)host.Review.Read(same)["phase"],Is.EqualTo("completed"));Assert.That(host.ReviewRequired,Is.False);Assert.That(editor.RuntimeGate.Held,Is.False);Assert.That(editor.WriteGate.Frozen,Is.False);
            Assert.That(host.Current,Is.SameAs(content));Assert.That(host.Current.Editor,Is.SameAs(editor));Assert.That(agent.Observe().session,Is.EqualTo(session));Assert.That(editor.CanUndo,Is.True);
            Assert.That(physics.Running,Is.False);Assert.That(host.Current.Rules.Runtime.Scheduler.RunningCount,Is.Zero);
            var recipe=editor.Find(changed.id).GetComponent<RecipeObject>();Assert.That(recipe.IsPlaying,Is.False);yield return null;Assert.That(recipe.IsPlaying,Is.False);
            var durable=new RoomStorage(editor.SaveDirectory).Load(out var error);Assert.That(error,Is.Null);Assert.That(durable.objects.Single(x=>x.id==changed.id).name,Is.EqualTo(changed.name));
            var executions=new RoomExecutions(editor,host);Assert.That(executions.Execute(request,out error),Is.True,error);Assert.That(host.Current,Is.SameAs(content));Assert.That(host.Review.CanCancel(same,out _),Is.False);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_REVIEW_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"reviewed.json"),new JObject {["prepared"]=prepared,["completed"]=host.Review.Read(same),["current"]=host.Activation.Current(),["execution"]=executions.Observe()}.ToString());}
            string path=directory,approvedRevision=host.Selection.Revision;UnityEngine.Object.Destroy(root);yield return null;BuildShell(path);Open();yield return ReadyHost();
            Assert.That(host.Selection.Revision,Is.EqualTo(approvedRevision));Assert.That(host.ReviewRequired,Is.False);Assert.That((string)host.Review.Read(same)["phase"],Is.EqualTo("completed"));
            Assert.That((string)host.Activation.Read((string)host.Activation.Current()["activationRequestId"])["phase"],Is.EqualTo("review"),"A later review does not turn an already completed activation into an interruption");
        }
        [UnityTest] public IEnumerator EditsAfterInspectionCannotInheritEarlierApproval()
        {
            yield return ReadyForReview();string id=BeginReview();yield return FinishReview(id);string revision=host.Selection.Revision;AcceptedEdit("Changed after inspection");
            ApproveReview(id);yield return FinishReview(id);Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("stale"));Assert.That(host.Selection.Revision,Is.EqualTo(revision));Assert.That(host.ReviewRequired,Is.True);Assert.That(host.Current.Editor.RuntimeGate.Held,Is.True);Assert.That(host.Current.Editor.WriteGate.Frozen,Is.False);
            string fresh=BeginReview();yield return FinishReview(fresh);ApproveReview(fresh);yield return FinishReview(fresh);Assert.That(host.ReviewRequired,Is.False);
        }
        [UnityTest] public IEnumerator PreparedReviewSurvivesRestartButCompletionStillChecksReloadedAcceptedData()
        {
            yield return ReadyForReview();AcceptedEdit("Prepared before restart");string id=BeginReview();yield return FinishReview(id);var args=ReviewArguments(id);string path=directory;
            UnityEngine.Object.Destroy(root);yield return null;BuildShell(path);Open();yield return ReadyHost();yield return ReadyReviewOwners();Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("prepared"));Assert.That(host.ReviewRequired,Is.True);
            Assert.That(JToken.DeepEquals(ReviewArguments(id),args),Is.True);ApproveReview(id);yield return FinishReview(id);Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("completed"));
        }
        [UnityTest] public IEnumerator CancelledReviewCompletionDrainsBeforeReleasingEditingAndDoesNotApprove()
        {
            yield return ReadyForReview();string id=BeginReview();yield return FinishReview(id);var editor=host.Current.Editor;
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();host.Review.Fault=point=>{if(point=="review.beforeCommit"){entered.Set();if(!release.Wait(TimeSpan.FromSeconds(20)))throw new IOException("Test timeout");}};
            try {
                ApproveReview(id);float deadline=Time.realtimeSinceStartup+15;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);
                var actions=new RoomExecutions(editor,host);Assert.That(actions.Execute(MaintenanceStart(actions,"workspace.review.cancel",new JObject {["requestId"]=id}),out var error),Is.True,error);
                root.SetActive(false);Assert.That(editor.WriteGate.Frozen,Is.True);release.Set();while(host.Review.WorkerPending&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(host.Review.WorkerPending,Is.False);Assert.That(editor.WriteGate.Frozen,Is.True);
                root.SetActive(true);yield return FinishReview(id);Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("cancelled"));Assert.That(editor.WriteGate.Frozen,Is.False);Assert.That(editor.RuntimeGate.Held,Is.True);Assert.That(store.Load().Active.ReviewRequired,Is.True);
            }finally{release.Set();}
        }
        [UnityTest] public IEnumerator FailedAcceptedSavePreventsReviewApprovalAndPreservesTheLiveEdit()
        {
            yield return ReadyForReview();string id=BeginReview();yield return FinishReview(id);var editor=host.Current.Editor;Assert.That(editor.TryFlush(out var beforeError),Is.True,beforeError);var data=AcceptedEdit("Save failure cannot approve older disk contents");
            string target=Path.Combine(editor.SaveDirectory,"room.v7.json"),backup=target+".test-retained";
            Assert.That(File.Exists(target),Is.True);File.Move(target,backup);Directory.CreateDirectory(target);
            try {
                ApproveReview(id);yield return FinishReview(id);
                Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("failed"));Assert.That(store.Load().Active.ReviewRequired,Is.True);Assert.That(editor.WriteGate.Frozen,Is.False);Assert.That(editor.Snapshot().objects.Single(x=>x.id==data.id).name,Is.EqualTo(data.name));Assert.That(editor.CanUndo,Is.True);
            }finally{Directory.Delete(target);File.Move(backup,target);}
            Assert.That(editor.TryFlush(out var error),Is.True,error);
        }
        [UnityTest] public IEnumerator ReviewFailureAfterCommitReconcilesTheSavedApproval()
        {
            yield return ReadyForReview();string id=BeginReview();yield return FinishReview(id);host.Review.Fault=point=>{if(point=="pointer.afterCommit")throw new IOException("Simulated lost approval acknowledgement");};
            ApproveReview(id);yield return FinishReview(id);Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("completed"));Assert.That((string)host.Review.Read(id)["committedRevision"],Is.EqualTo(store.Load().Revision));Assert.That(host.Current.Editor.RuntimeGate.Held,Is.False);
        }
        [UnityTest] public IEnumerator UnreadableReviewOutcomeKeepsOldOwnersFrozen()
        {
            yield return ReadyForReview();string id=BeginReview();yield return FinishReview(id);var editor=host.Current.Editor;host.Review.Fault=point=>{if(point=="pointer.afterCommit"){File.WriteAllText(Path.Combine(directory,"workspace-generations.v1","current.v1.json"),"{damaged");throw new IOException("Simulated unreadable outcome");}};
            ApproveReview(id);float deadline=Time.realtimeSinceStartup+15;while(host.Review.WorkerPending&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(host.Review.WorkerPending,Is.False);yield return null;
            Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("unavailable"));Assert.That(editor.WriteGate.Frozen,Is.True);Assert.That(editor.RuntimeGate.Held,Is.True);Assert.That(host.Review.CanCancel(id,out _),Is.False);
        }
        [UnityTest] public IEnumerator CompletingReviewCannotReleaseAnotherNativeHold()
        {
            yield return ReadyForReview();using var other=host.Current.Editor.RuntimeGate.Hold("Another native operation still owns activity");string id=BeginReview();yield return FinishReview(id);ApproveReview(id);yield return FinishReview(id);
            Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("completed"));Assert.That(host.ReviewRequired,Is.False);Assert.That((bool)host.Review.Read(id)["activityHeld"],Is.True);Assert.That(physics.Running,Is.False);
            other.Dispose();Assert.That(host.Current.Editor.RuntimeGate.Held,Is.False);yield return null;Assert.That(physics.Running,Is.False);Assert.That(host.Current.Rules.Runtime.Scheduler.RunningCount,Is.Zero);
        }
    }
}
