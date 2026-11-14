// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Avatar;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        IEnumerator PoseSettled(bool success=true)
        {
            float end=Time.realtimeSinceStartup+20;
            while(workshop.IsSavingPose){Assert.Less(Time.realtimeSinceStartup,end,"Pose publication timed out");yield return null;}
            Assert.AreEqual(!success,workshop.HasUnsavedPose,workshop.Status);
            yield return RecordingDrained();
        }
        [UnityTest] public IEnumerator PoseQueueKeepsLaterEditsAndExactReceiptsAcrossObserverCancellation()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));var firstPose=EditHead(20);int firstVersion=workshop.PoseVersion;
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                var args=PoseArgs("save");
                Assert.IsTrue(BehaviourCatalog.Action("animation.pose").Module.Start(new CapabilityContext(editor,workshop),"first",args,out var op,out error),error);
                Assert.IsTrue(workshop.BeginPoseSave(workshop.PoseSessionId,firstVersion,false,out var first,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,op.State(out error),error);op.Stop(false);
                Assert.IsTrue(workshop.IsPosing);Assert.IsTrue((bool)PoseFact()["editing"]);Assert.AreEqual("saving",(string)PoseFact()["phase"]);
                var laterPose=EditHead(-35);int laterVersion=workshop.PoseVersion;
                Assert.IsTrue(workshop.BeginPoseSave(workshop.PoseSessionId,laterVersion,true,out var last,out error),error);
                Assert.AreNotSame(first,last);Assert.IsFalse(workshop.IsPosing);Assert.IsNull(first.Result);
                blocker.Dispose();yield return PoseSettled();
                SamePose(laterPose,editor.Read("maestro").joints);
                SamePose(laterPose,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").joints);
                Assert.AreEqual(firstVersion,(int)first.Result["version"]);Assert.AreEqual(laterVersion,(int)last.Result["version"]);
                Assert.Less((int)first.Result["revision"],(int)last.Result["revision"]);
                var receipt=first.Result;receipt["revision"]=1;Assert.AreNotEqual(1,(int)first.Result["revision"]);
                Assert.AreEqual(RuleActionState.Failed,op.State(out _));Assert.IsFalse(op.Complete(out _));
                editor.Undo();SamePose(firstPose,editor.Read("maestro").joints);editor.Undo();Assert.IsNull(editor.Read("maestro").joints);
            }finally{blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PosePublicationKeepsNewLivePreviewAndCannotReportItAsSaved()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));var firstPose=EditHead(25);int version=workshop.PoseVersion;
            using var pause=new RecordingPublicationPause();int ownerThread=Thread.CurrentThread.ManagedThreadId;
            Assert.IsTrue(workshop.BeginPoseSave(workshop.PoseSessionId,version,false,out var first,out var error,checkpoint:pause.Check),error);
            try{
                float end=Time.realtimeSinceStartup+20;
                while(!pause.Entered.IsSet){Assert.Less(Time.realtimeSinceStartup,end);yield return null;}
                Assert.IsTrue(first.Accepted);Assert.IsNull(first.Result);Assert.AreNotEqual(ownerThread,pause.Worker);
                var latest=EditHead(-30);int latestVersion=workshop.PoseVersion;
                SamePose(latest,avatar.PoseRig.Capture());SamePose(firstPose,editor.Read("maestro").joints);
                Assert.IsFalse(workshop.DiscardPose(workshop.PoseSessionId,latestVersion,out _,out error));Assert.That(error,Does.Contain("still saving"));
                pause.Release();yield return PoseSettled();
                Assert.IsTrue(workshop.IsPosing);SamePose(latest,avatar.PoseRig.Capture());
                Assert.AreEqual(version,(int)first.Result["version"]);SamePose(firstPose,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").joints);
                yield return PoseAction(PoseArgs("finish"));SamePose(latest,editor.Read("maestro").joints);Assert.IsFalse(workshop.IsPlaying);
            }finally{pause.Release();}
        }
        [UnityTest] public IEnumerator PoseDiscardCancelsQueuedVersionsAndOldObserversCannotAffectANewSession()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));EditHead(20);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                string id=workshop.PoseSessionId;int revision=editor.ObjectRevision("maestro");
                Assert.IsTrue(workshop.BeginPoseSave(id,workshop.PoseVersion,false,out var first,out error),error);
                EditHead(35);Assert.IsTrue(workshop.BeginPoseSave(id,workshop.PoseVersion,false,out var second,out error),error);
                Assert.IsTrue(workshop.DiscardPose(id,workshop.PoseVersion,out _,out error),error);
                Assert.IsFalse(first.Poll(out _,out _));Assert.IsFalse(second.Poll(out _,out _));
                Assert.AreEqual(revision,editor.ObjectRevision("maestro"));Assert.IsNull(editor.Read("maestro").joints);
                yield return PoseAction(PoseArgs("start"));Assert.AreNotEqual(id,workshop.PoseSessionId);
                Assert.IsFalse(first.Poll(out _,out _));Assert.IsTrue(workshop.IsPosing);
            }finally{blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PosePreparationFailureRetainsTheNewestUnsavedEditForExactRetry()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));EditHead(20);string file=BlockPoseSave();
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                Assert.IsTrue(workshop.BeginPoseSave(workshop.PoseSessionId,workshop.PoseVersion,false,out var first,out error),error);
                var latest=EditHead(-35);
                blocker.Dispose();yield return PoseSettled(false);
                Assert.IsFalse(workshop.IsPosing);Assert.IsFalse(first.Accepted);Assert.IsNull(editor.Read("maestro").joints);
                var observed=AnimationAuthoringCapability.Rotation(Fact("animation.pose.joint",PoseJointQuery()));
                Assert.Less(Quaternion.Angle(observed,latest.Single(x=>x.joint==PoseJoint.Head).rotation),.05f);
                Directory.Delete(file);yield return PoseAction(PoseArgs("save"));
                SamePose(latest,editor.Read("maestro").joints);
            }finally{blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PosePublicationFailureRetainsAppliedStateAndNeverBlindlyReplaysIt()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));EditHead(30);editor.SaveNow();yield return null;
            var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            Assert.IsTrue(workshop.BeginPoseSave(workshop.PoseSessionId,workshop.PoseVersion,true,out var attempt,out var error,
                checkpoint:phase=>{if(phase=="publishing")throw new IOException("Synthetic pose publication failure");}),error);
            yield return PoseSettled(false);
            Assert.IsTrue(attempt.Accepted);Assert.IsFalse(attempt.Poll(out _,out error));Assert.That(error,Does.Contain("unconfirmed"));
            Assert.IsNotNull(editor.Read("maestro").joints);Assert.IsFalse(editor.CanSaveRoom);
            Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
            Assert.IsFalse(workshop.SaveRetainedPose(out error));Assert.That(error,Does.Contain("do not save it again"));
            Assert.IsTrue(workshop.DiscardPose(workshop.PoseSessionId,workshop.PoseVersion,out _,out error),error);
            Assert.IsNotNull(editor.Read("maestro").joints);Assert.IsFalse(editor.CanSaveRoom);
        }
        [UnityTest] public IEnumerator PoseSuspensionRetiresPreparationAndSavesLatestVersionAfterResume()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));EditHead(20);
            Assert.IsTrue(workshop.BeginPoseSave(workshop.PoseSessionId,workshop.PoseVersion,false,out var first,out var error),error);
            Assert.IsTrue(first.Poll(out var ready,out error),error);Assert.IsFalse(ready);
            var latest=EditHead(-30);
            using(editor.RuntimeGate.Hold("Synthetic pose suspension")){
                yield return RecordingDrained();yield return null;
                Assert.IsTrue(workshop.IsSavingPose);Assert.IsFalse(workshop.IsPosing);Assert.IsFalse(first.Ended);Assert.IsNull(editor.Read("maestro").joints);
            }
            yield return PoseSettled();SamePose(latest,editor.Read("maestro").joints);
        }
        [UnityTest] public IEnumerator PoseFinishRefusesANewerActorAndDisableRetainsFrozenVersions()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));var pose=EditHead(25);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                Assert.IsTrue(workshop.BeginPoseSave(workshop.PoseSessionId,workshop.PoseVersion,true,out var attempt,out error),error);
                Assert.IsTrue(avatar.BeginUpperBody("new-actor","Greeting"));
                Assert.IsFalse(attempt.Poll(out _,out error));Assert.IsTrue(avatar.UpperBodyOwnedBy("new-actor"));
                Assert.IsFalse(attempt.Accepted);Assert.IsTrue(workshop.HasUnsavedPose);avatar.EndUpperBody("new-actor");
                Assert.IsTrue(workshop.BeginPoseSave(workshop.PoseSessionId,workshop.PoseVersion,true,out var retry,out error),error);
                workshop.enabled=false;Assert.IsTrue(retry.Ended);Assert.IsTrue(workshop.HasUnsavedPose);
                blocker.Dispose();yield return RecordingDrained();workshop.enabled=true;yield return PoseAction(PoseArgs("save"));SamePose(pose,editor.Read("maestro").joints);
            }finally{blocker.Dispose();workshop.enabled=true;avatar.EndUpperBody("new-actor");}
        }
        [UnityTest] public IEnumerator AcceptedPosePublicationSurvivesSuspensionWhileLatestFinishWaits()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));var firstPose=EditHead(20);
            using var pause=new RecordingPublicationPause();
            Assert.IsTrue(workshop.BeginPoseSave(workshop.PoseSessionId,workshop.PoseVersion,false,out var first,out var error,checkpoint:pause.Check),error);
            try{
                float end=Time.realtimeSinceStartup+20;
                while(!pause.Entered.IsSet){Assert.Less(Time.realtimeSinceStartup,end);yield return null;}
                var latest=EditHead(-25);
                using(editor.RuntimeGate.Hold("Suspend accepted pose publication")){
                    Assert.IsFalse(workshop.IsPosing);Assert.IsTrue(workshop.IsSavingPose);
                    pause.Release();end=Time.realtimeSinceStartup+20;
                    while(!first.Ended){Assert.Less(Time.realtimeSinceStartup,end);yield return null;}
                    Assert.IsNotNull(first.Result);SamePose(firstPose,editor.Read("maestro").joints);
                    Assert.IsTrue(workshop.IsSavingPose,"The newer frozen pose must wait for native resume");
                }
                yield return PoseSettled();SamePose(latest,editor.Read("maestro").joints);Assert.IsFalse(workshop.IsPosing);
            }finally{pause.Release();}
        }
        [UnityTest] public IEnumerator PoseQueueLimitRetainsLatestPreviewWithoutLeakingAdmissions()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                for(int i=0;i<RuleScheduler.MaximumConcurrent;i++){
                    EditHead(-32+2*i);
                    Assert.IsTrue(workshop.BeginPoseSave(workshop.PoseSessionId,workshop.PoseVersion,false,out _,out error),error);
                }
                var latest=EditHead(70);
                Assert.IsFalse(workshop.BeginPoseSave(workshop.PoseSessionId,workshop.PoseVersion,false,out _,out error));
                Assert.That(error,Does.Contain("Too many"));Assert.IsFalse(workshop.IsSavingPose);Assert.IsFalse(workshop.IsPosing);
                Assert.IsTrue(workshop.HasUnsavedPose);Assert.IsNull(editor.Read("maestro").joints);
                blocker.Dispose();yield return RecordingDrained();yield return PoseAction(PoseArgs("save"));SamePose(latest,editor.Read("maestro").joints);
            }finally{blocker.Dispose();}
        }
        [UnityTest] public IEnumerator SavingLivePoseKeepsOnlySupportedImportedJointChannelsVisible()
        {
            var asset=Maestro.Quest.Imports.ModelLibrary.Inspect("partial-pose.vrm",ModelFixture.Create(
                j=>((JObject)j["extensions"]["VRMC_vrm"]["humanoid"]["humanBones"]).Remove("chest"),avatar:true));
            var saving=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>saving.IsCompleted);Assert.IsNull(saving.Exception);
            Assert.IsTrue(editor.SetMaestroModel(asset.Hash));yield return new WaitUntil(()=>!avatar.ModelBusy);
            Assert.IsNull(avatar.PoseRig.Bone(PoseJoint.Chest));AuthorRuntime();yield return PoseAction(PoseArgs("start"));
            var supported=(JArray)PoseFact()["joints"];Assert.IsFalse(supported.Values<string>().Contains("Chest"));
            EditHead(20);Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                Assert.IsTrue(workshop.BeginPoseSave(workshop.PoseSessionId,workshop.PoseVersion,false,out _,out error),error);
                var state=PoseFact();Assert.AreEqual("saving",(string)state["phase"]);Assert.IsTrue((bool)state["editing"]);
                Assert.IsTrue(JToken.DeepEquals(supported,state["joints"]));Assert.IsFalse(workshop.CanRotatePose(workshop.PoseSessionId,workshop.PoseVersion,new[]{new JointPose {joint=PoseJoint.Chest,rotation=Quaternion.identity}},out _));
                blocker.Dispose();yield return PoseSettled();yield return PoseAction(PoseArgs("finish"));
            }finally{blocker.Dispose();}
        }
    }
}
