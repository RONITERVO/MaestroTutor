// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Book;
using Maestro.Quest.Programs;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
namespace Maestro.Quest.Tests
{
    static class FrameSaveCompletion
    {
        internal static IEnumerator Wait(AnimationWorkshop workshop,bool success=true)
        {
            float end=Time.realtimeSinceStartup+20;
            while(workshop.IsSavingFrameEdit){Assert.Less(Time.realtimeSinceStartup,end,"Keyframe save timed out");yield return null;}
            Assert.AreEqual(!success,workshop.HasUnsavedFrameEdit,workshop.Status);
        }
    }
    public sealed partial class AnimationWorkshopTests
    {
        IEnumerator FrameSettled(bool success=true){yield return FrameSaveCompletion.Wait(workshop,success);yield return RecordingDrained();}
        JObject FramePending()=>Fact("animation.edit.pending",null);
        JObject FrameResolution(string operation)=>new() {["operation"]=operation,["sessionId"]=FramePending()["sessionId"].DeepClone()};
        bool FrameObserver(out CapabilityOperation operation,out string error)=>BehaviourCatalog.Action("animation.edit.resolve").Module.Start(
            new CapabilityContext(editor,workshop),"frame-observer",FrameResolution("retry"),out operation,out error);
        RoomItem FrameBlock()
        {
            var data=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block);
            var item=editor.Find(data.id);editor.Select(item);return item;
        }
        [UnityTest] public IEnumerator PhysicalFrameSaveCapturesOnceSurvivesSelectionAndObserverCancellation()
        {
            AuthorRuntime();var item=FrameBlock();string id=editor.Identity(item);var point=item.transform.localPosition+Vector3.up*.2f;
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                item.transform.localPosition=point;workshop.AddFrame();var waiting=FramePending();
                Assert.AreEqual("saving",(string)waiting["phase"]);Assert.IsNull(editor.Read(id).motion);
                Assert.IsTrue(FrameObserver(out var observer,out error),error);Assert.AreEqual(RuleActionState.Preparing,observer.State(out error));
                observer.Stop(false);workshop.AddFrame();Assert.AreEqual(waiting["sessionId"],FramePending()["sessionId"]);
                item.transform.localPosition+=Vector3.right;editor.Select(avatarItem);
                Assert.IsFalse(editor.BeginTemporaryRoom(out error));Assert.IsTrue(workshop.HasUnsavedFrameEdit);
                blocker.Dispose();yield return FrameSettled();
                Assert.AreEqual(point,editor.Read(id).motion.frames.Single().position);Assert.IsNull(editor.Read("maestro").motion);
                Assert.AreEqual(RuleActionState.Failed,observer.State(out _));Assert.IsFalse(observer.Complete(out _));
                Assert.AreEqual("idle",(string)FramePending()["phase"]);Assert.AreNotEqual(waiting["sessionId"],FramePending()["sessionId"]);
                var stored=new RoomStorage(directory).Load(out error);Assert.IsNotNull(stored,error);
                Assert.AreEqual(point,stored.objects.Single(x=>x.id==id).motion.frames.Single().position);
                editor.Undo();Assert.IsNull(editor.Read(id).motion);Assert.IsFalse(workshop.IsPlaying);
            }finally{blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PhysicalFramePreparationFailureRetainsExactValuesAndCannotStealANewerActor()
        {
            AuthorRuntime();var item=FrameBlock();string id=editor.Identity(item);string blocked=BlockPoseSave();
            var point=item.transform.localPosition+Vector3.up*.1f;item.transform.localPosition=point;workshop.AddFrame();
            yield return FrameSettled(false);var retained=FramePending();workshop.Stop();Directory.Delete(blocked);
            Assert.IsTrue(editor.Ownership.TryAcquire("new-frame-actor","New actor",RoomActorRole.Control,
                new[]{new BehaviourCatalog.Claim(id,"wholeTarget")},null,out var lease,out var error),error);
            try{Assert.IsFalse(workshop.BeginFrameEditSave((string)retained["sessionId"],out _,out error));Assert.IsTrue(lease.Held);}
            finally{lease.Dispose();}
            item.transform.localPosition+=Vector3.right;editor.Select(avatarItem);
            Assert.IsTrue(workshop.BeginFrameEditSave((string)retained["sessionId"],out var saved,out error),error);
            yield return FrameSettled();Assert.AreEqual(point,editor.Read(id).motion.frames.Single().position);
            Assert.IsTrue(saved.Poll(out var ready,out error),error);Assert.IsTrue(ready);
            Assert.AreEqual((string)retained["sessionId"],(string)saved.Result["sessionId"]);
        }
        [UnityTest] public IEnumerator KeyframeDiscardCancelsOnlyUnacceptedWorkAndOldObserversStayFailed()
        {
            AuthorRuntime();FrameBlock();
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                workshop.AddFrame();var old=FramePending();Assert.IsTrue(FrameObserver(out var observer,out error),error);
                Assert.IsTrue(workshop.DiscardFrameEdit((string)old["sessionId"],out _,out error),error);
                Assert.AreEqual(RuleActionState.Failed,observer.State(out _));Assert.IsFalse(workshop.HasUnsavedFrameEdit);
                workshop.AddFrame();Assert.AreNotEqual(old["sessionId"],FramePending()["sessionId"]);
                Assert.IsFalse(workshop.DiscardFrameEdit((string)old["sessionId"],out _,out error));
                blocker.Dispose();yield return FrameSettled();Assert.AreEqual(RuleActionState.Failed,observer.State(out _));
            }finally{blocker.Dispose();}
        }
        [UnityTest] public IEnumerator KeyframeAndLaterPoseSavesShareAcceptedRevisionWithoutLosingEitherVersion()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));var first=EditHead(20);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                workshop.AddFrame();Assert.IsTrue(workshop.IsSavingFrameEdit);var latest=EditHead(-30);workshop.Stop();
                Assert.IsTrue(workshop.IsSavingPose);blocker.Dispose();yield return FrameSettled();yield return PoseSettled();
                SamePose(first,editor.Read("maestro").motion.frames.Single().joints);
                SamePose(latest,editor.Read("maestro").joints);
                editor.Undo();SamePose(first,editor.Read("maestro").joints);Assert.IsNotNull(editor.Read("maestro").motion);
                editor.Undo();Assert.IsNull(editor.Read("maestro").motion);
            }finally{blocker.Dispose();}
        }
        [UnityTest] public IEnumerator KeyframePublicationLeavesNewerLivePoseVisibleAndHasImmutableReceipt()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));var first=EditHead(25);
            var data=editor.Read("maestro");var motion=new RoomMotion {frames=new[]{new MotionFrame {position=data.position,rotation=data.rotation,scale=data.scale,joints=first}}};
            using var pause=new RecordingPublicationPause();int ownerThread=Thread.CurrentThread.ManagedThreadId;
            Assert.IsTrue(workshop.QueueFrameEdit(motion,true,first,"Frame saved",0,pause.Check));
            Assert.IsTrue(FrameObserver(out var observer,out var error),error);
            try{
                float end=Time.realtimeSinceStartup+20;
                while(!pause.Entered.IsSet){Assert.Less(Time.realtimeSinceStartup,end);yield return null;}
                Assert.AreNotEqual(ownerThread,pause.Worker);Assert.AreEqual(RuleActionState.Preparing,observer.State(out error));
                Assert.IsTrue((bool)FramePending()["applied"]);
                var later=EditHead(-35);
                Assert.IsFalse(workshop.DiscardFrameEdit((string)FramePending()["sessionId"],out _,out error));
                pause.Release();yield return FrameSettled();SamePose(later,avatar.PoseRig.Capture());SamePose(first,editor.Read("maestro").joints);
                Assert.AreEqual(RuleActionState.Ready,observer.State(out error),error);Assert.IsTrue(observer.Complete(out error),error);
                int revision=(int)observer.Result["revision"];var receipt=observer.Result;receipt["revision"]=0;Assert.AreEqual(revision,(int)observer.Result["revision"]);
                yield return PoseAction(PoseArgs("finish"));SamePose(later,editor.Read("maestro").joints);Assert.AreEqual(revision,(int)observer.Result["revision"]);
            }finally{pause.Release();}
        }
        [UnityTest] public IEnumerator KeyframeAppliedPublicationFailureNeverReplaysAndPreservesOriginalFiles()
        {
            AuthorRuntime();var item=FrameBlock();string id=editor.Identity(item);editor.SaveNow();yield return null;
            byte[] original=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));var data=editor.Read(id);
            var motion=new RoomMotion {frames=new[]{new MotionFrame {position=data.position,rotation=data.rotation,scale=data.scale}}};
            Assert.IsTrue(workshop.QueueFrameEdit(motion,false,null,"Frame saved",0,phase=>{if(phase=="publishing")throw new IOException("Synthetic keyframe publication failure");}));
            yield return FrameSettled(false);var failed=FramePending();Assert.IsTrue((bool)failed["applied"]);Assert.IsNotNull(editor.Read(id).motion);
            Assert.IsFalse(editor.CanSaveRoom);Assert.AreEqual(original,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
            Assert.IsFalse(workshop.BeginFrameEditSave((string)failed["sessionId"],out _,out var error));Assert.That(error,Does.Contain("replaying"));
            Assert.IsTrue(workshop.DiscardFrameEdit((string)failed["sessionId"],out _,out error),error);
            Assert.IsNotNull(editor.Read(id).motion);Assert.IsFalse(editor.CanSaveRoom);
        }
        [UnityTest] public IEnumerator KeyframeSuspensionRejoinsAfterResumeAndDisableRetainsTheCapturedEdit()
        {
            AuthorRuntime();FrameBlock();workshop.AddFrame();
            editor.SendMessage("OnApplicationPause",true);workshop.SendMessage("OnApplicationPause",true);
            Assert.IsTrue(workshop.IsSavingFrameEdit);Assert.IsTrue(workshop.HasUnsavedFrameEdit);
            yield return null;editor.SendMessage("OnApplicationPause",false);workshop.SendMessage("OnApplicationPause",false);
            yield return FrameSettled();
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                workshop.AddFrame();workshop.enabled=false;Assert.IsTrue(workshop.HasUnsavedFrameEdit);Assert.IsFalse(workshop.IsSavingFrameEdit);
                var retained=FramePending();blocker.Dispose();workshop.enabled=true;
                Assert.IsTrue(workshop.BeginFrameEditSave((string)retained["sessionId"],out _,out error),error);yield return FrameSettled();
            }finally{blocker.Dispose();}
        }
        [UnityTest] public IEnumerator KeyframeRetryRefusesChangedTargetsAndTemporarySavesUseTheFork()
        {
            AuthorRuntime();var item=FrameBlock();string id=editor.Identity(item);string blocked=BlockPoseSave();workshop.AddFrame();
            yield return FrameSettled(false);workshop.Stop();Directory.Delete(blocked);
            Assert.IsTrue(editor.MoveObject(id,editor.Read(id).position+Vector3.right*.1f,out var error),error);
            Assert.IsFalse(workshop.BeginFrameEditSave((string)FramePending()["sessionId"],out _,out error));Assert.That(error,Does.Contain("changed"));
            workshop.DiscardFrameEdit();Assert.IsTrue(editor.BeginTemporaryRoom(out error),error);yield return FinishAuthoringSave();
            editor.Select(editor.Find(id));workshop.AddFrame();Assert.IsTrue(FrameObserver(out var observer,out error),error);yield return FrameSettled();
            Assert.AreEqual(RuleActionState.Ready,observer.State(out error),error);Assert.IsTrue(observer.Complete(out error),error);
            Assert.IsTrue((bool)observer.Result["temporary"]);workshop.Stop();
            Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);Assert.IsNull(editor.Read(id).motion);
        }
        [UnityTest] public IEnumerator AcceptedKeyframeSaveRemainsObservableAcrossWorkshopDisableAndSuspension()
        {
            AuthorRuntime();var item=FrameBlock();var data=editor.Read(editor.Identity(item));
            var motion=new RoomMotion {frames=new[]{new MotionFrame {position=data.position,rotation=data.rotation,scale=data.scale}}};
            using var pause=new RecordingPublicationPause();
            Assert.IsTrue(workshop.QueueFrameEdit(motion,false,null,"Frame saved",0,pause.Check));
            Assert.IsTrue(FrameObserver(out var observer,out var error),error);
            try{
                float end=Time.realtimeSinceStartup+20;
                while(!pause.Entered.IsSet){Assert.Less(Time.realtimeSinceStartup,end);yield return null;}
                Assert.AreEqual(RuleActionState.Preparing,observer.State(out error),error);
                workshop.enabled=false;Assert.IsTrue(workshop.IsSavingFrameEdit);
                using(editor.RuntimeGate.Hold("Suspend accepted keyframe publication")){
                    Assert.IsNotNull(workshop.SuspendAuthoringSave(),"Accepted publication stays owned by its existing operation");
                    pause.Release();
                    while(observer.State(out error)==RuleActionState.Preparing){Assert.Less(Time.realtimeSinceStartup,end);yield return null;}
                    Assert.AreEqual(RuleActionState.Ready,observer.State(out error),error);Assert.IsTrue(observer.Complete(out error),error);
                    Assert.IsFalse(workshop.HasUnsavedFrameEdit);Assert.IsNotNull(editor.Read(data.id).motion);
                }
                workshop.enabled=true;
            }finally{pause.Release();}
        }
        [UnityTest] public IEnumerator PhysicalKeyframeSaveCooperatesWithItsExistingGripWithoutRewindingMovement()
        {
            AuthorRuntime();var item=FrameBlock();string id=editor.Identity(item);workshop.AddFrame();yield return FrameSettled();
            var handRoot=new GameObject("Keyframe hand");handRoot.SetActive(false);handRoot.transform.SetParent(root.transform,false);
            handRoot.transform.position=item.transform.position-Vector3.forward*.25f;
            var hand=handRoot.AddComponent<XRRayInteractor>();hand.enableUIInteraction=false;hand.interactionManager=manager;
            hand.keepSelectedTargetValid=true;hand.manipulateAttachTransform=false;hand.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.State;
            hand.selectInput=new XRInputButtonReader {inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue,manualPerformed=true,manualValue=1};
            try{
                handRoot.SetActive(true);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);yield return new WaitForSeconds(.12f);
                Assert.IsTrue(item.Grab.isSelected);Assert.IsTrue(workshop.ControlsTarget(id));
                handRoot.transform.position+=Vector3.right*.25f;yield return new WaitForSeconds(.12f);
                var captured=editor.Frame.Placement(id,item.transform).position;workshop.AddFrame();
                Assert.IsTrue(workshop.IsSavingFrameEdit,workshop.Status);
                handRoot.transform.position+=Vector3.right*.25f;yield return new WaitForSeconds(.12f);yield return FrameSettled();
                Assert.IsTrue(item.Grab.isSelected,"Saving content does not release the user's grip");
                Assert.AreEqual(captured,editor.Read(id).motion.frames.Last().position);
                Assert.Greater(editor.Frame.Placement(id,item.transform).position.x-captured.x,.15f,"Saving an earlier frame must not rewind ongoing XRI movement");
                var replacement=editor.Frame.Placement(id,item.transform).position;workshop.ReplaceFrame();
                Assert.IsTrue(workshop.IsSavingFrameEdit,workshop.Status);yield return FrameSettled();
                Assert.IsTrue(item.Grab.isSelected);Assert.AreEqual(replacement,editor.Read(id).motion.frames.Last().position);
                Assert.AreEqual(2,editor.Read(id).motion.frames.Length,"Replace does not create a third keyframe");
            }finally{handRoot.SetActive(false);}
            yield return null;
        }
        [UnityTest] public IEnumerator SolidKeyframeControlsRetryTheCapturedEditAndDiscardOnlyThePendingBuffer()
        {
            AuthorRuntime();var item=FrameBlock();string id=editor.Identity(item);yield return FinishAuthoringSave();
            var tray=new GameObject("Keyframe recovery tools");tray.transform.SetParent(root.transform,false);tray.transform.localPosition=new Vector3(2,0,1);
            tray.AddComponent<AnimationTools>().Build(workshop,root.GetComponent<RoomInteraction>());
            var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;yield return null;Physics.SyncTransforms();
            void Click(AnimationTool tool){
                var button=tray.GetComponentsInChildren<AnimationToolAction>().Single(x=>x.Tool==tool);
                var ray=new Ray(button.transform.position+Vector3.back,Vector3.forward);
                Assert.IsTrue(router.Begin(0,ray),button.AccessibleName);router.End(0,ray);
            }
            var point=item.transform.localPosition+Vector3.up*.12f;item.transform.localPosition=point;
            string blocked=BlockPoseSave();yield return new WaitForSeconds(.31f);Click(AnimationTool.AddFrame);yield return FrameSettled(false);Directory.Delete(blocked);
            item.transform.localPosition+=Vector3.right;yield return new WaitForSeconds(.31f);Click(AnimationTool.AddFrame);yield return FrameSettled();
            Assert.AreEqual(point,editor.Read(id).motion.frames.Single().position);
            blocked=BlockPoseSave();yield return new WaitForSeconds(.31f);Click(AnimationTool.AddFrame);yield return FrameSettled(false);Directory.Delete(blocked);
            yield return null;
            foreach(var label in tray.GetComponentsInChildren<TextMesh>()){
                var bounds=label.GetComponent<MeshRenderer>().bounds;
                Assert.GreaterOrEqual(bounds.min.x,tray.transform.position.x-.38f,label.text);
                Assert.LessOrEqual(bounds.max.x,tray.transform.position.x+.38f,label.text);
                Assert.GreaterOrEqual(bounds.min.y,tray.transform.position.y-.25f,label.text);
                Assert.LessOrEqual(bounds.max.y,tray.transform.position.y+.25f,label.text);
            }
            CapturePoseRecoveryControls(tray);Click(AnimationTool.DiscardEdit);
            Assert.IsFalse(workshop.HasUnsavedFrameEdit);Assert.AreEqual(1,editor.Read(id).motion.frames.Length);
        }
        [UnityTest] public IEnumerator SharedKeyframeResolutionCapturesNativePendingAndCompletedBookReceipts()
        {
            AuthorRuntime();FrameBlock();var before=FramePending();var beforeExecution=authorActions.Observe();
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                workshop.AddFrame();var pending=FramePending();
                var request=new JObject {["operation"]="start",["runId"]=beforeExecution["nextRunId"].DeepClone(),
                    ["call"]=new JObject {["id"]="animation.edit.resolve",["version"]=1,["arguments"]=FrameResolution("retry")}};
                Assert.IsTrue(authorActions.Execute(request,out error),error);var preparing=authorActions.Observe();
                Assert.AreEqual("preparing",(string)preparing["selected"]["phase"]);Assert.IsTrue(workshop.HasUnsavedFrameEdit);
                blocker.Dispose();yield return SpatialSettingsCompletion.Wait(authorActions);yield return FrameSettled();
                var complete=authorActions.Observe();var idle=FramePending();
                Assert.AreEqual("completed",(string)complete["selected"]["phase"]);
                Assert.AreEqual(pending["sessionId"],complete["selected"]["output"]["sessionId"]);
                Assert.AreNotEqual(pending["sessionId"],idle["sessionId"]);
                string output=Environment.GetEnvironmentVariable("MAESTRO_FRAME_EDITS");
                if(!string.IsNullOrEmpty(output)){
                    Directory.CreateDirectory(output);
                    File.WriteAllText(Path.Combine(output,"frames.json"),new JObject {["before"]=before,["beforeExecution"]=beforeExecution,
                        ["pending"]=pending,["preparing"]=preparing,["complete"]=complete,["idle"]=idle}.ToString());
                }
            }finally{blocker.Dispose();}
        }

    }
}
