// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
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
    public sealed partial class AnimationWorkshopTests
    {
        JObject poseExecution,poseRequest;
        JObject PoseFact()=>Fact("animation.posing",null);
        JObject PoseArgs(string operation)=>new() {["operation"]=operation,["target"]="maestro",["sessionId"]=workshop.PoseSessionId,[operation=="start"?"revision":"version"]=operation=="start"?editor.ObjectRevision("maestro"):workshop.PoseVersion};
        JObject PoseEdits(float degrees,PoseJoint joint=PoseJoint.Head)
        {
            var args=PoseArgs("rotate");args["joints"]=new JArray(new JObject {["joint"]=joint.ToString(),["rotation"]=QuaternionJson(avatar.PoseRig.RestPose().Single(x=>x.joint==joint).rotation*Quaternion.Euler(degrees,0,0))});return args;
        }
        JObject PoseRequest(JObject args)=>new() {["operation"]="start",["runId"]=authorActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="animation.pose",["version"]=1,["arguments"]=args}};
        IEnumerator PoseAction(JObject args)
        {
            poseRequest=PoseRequest(args);Assert.That(authorActions.Execute(poseRequest,out var error),Is.True,error);yield return null;
            poseExecution=authorActions.Observe();Assert.That((string)poseExecution["selected"]["phase"],Is.EqualTo("completed"),poseExecution.ToString());
        }
        JObject PoseJointQuery(PoseJoint joint=PoseJoint.Head)=>new() {["sessionId"]=workshop.PoseSessionId,["version"]=workshop.PoseVersion,["joint"]=joint.ToString()};
        [UnityTest] public IEnumerator SharedPoseAdjustsTheActualRigAndFinishesWithExactNativeReadback()
        {
            AuthorRuntime();var before=PoseFact();yield return PoseAction(PoseArgs("start"));var start=poseExecution.DeepClone();var active=PoseFact();int revision=editor.ObjectRevision("maestro");
            Assert.That(workshop.IsPosing,Is.True);Assert.That((int)active["version"],Is.GreaterThan(0));Assert.That(((JArray)active["joints"]).Count,Is.EqualTo(17));Assert.That(avatarItem.Grab.enabled,Is.False);
            yield return PoseAction(PoseEdits(150));var rotate=poseExecution.DeepClone();var edited=PoseFact();var query=PoseJointQuery();var joint=Fact("animation.pose.joint",query);
            float angle=Quaternion.Angle(avatar.PoseRig.RestPose().Single(x=>x.joint==PoseJoint.Head).rotation,avatar.PoseRig.CanonicalBone(PoseJoint.Head).localRotation);
            Assert.That(angle,Is.EqualTo(75).Within(.1f));Assert.That(Quaternion.Angle(AnimationAuthoringCapability.Rotation(joint),avatar.PoseRig.CanonicalBone(PoseJoint.Head).localRotation),Is.LessThan(.05f));
            Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(editor.Read("maestro").joints,Is.Null,"Live rotation is a preview, not an implicit saved edit");
            yield return PoseAction(PoseArgs("finish"));var finish=poseExecution.DeepClone();var idle=PoseFact();Assert.That(workshop.IsPosing,Is.False);Assert.That(workshop.IsPlaying,Is.False);Assert.That((string)idle["sessionId"],Is.Not.EqualTo((string)before["sessionId"]));
            SamePose(editor.Read("maestro").joints,new RoomStorage(directory).Load(out _).objects.Single(x=>x.id=="maestro").joints);
            string proof=Environment.GetEnvironmentVariable("MAESTRO_POSE_SESSIONS");if(!string.IsNullOrEmpty(proof)){Directory.CreateDirectory(proof);File.WriteAllText(Path.Combine(proof,"posing.json"),new JObject {["before"]=before,["start"]=start,["active"]=active,["rotate"]=rotate,["edited"]=edited,["jointQuery"]=query,["joint"]=joint,["finish"]=finish,["idle"]=idle}.ToString());}
        }
        [UnityTest] public IEnumerator SharedPoseSaveAndDiscardAreAlsoAvailableFromThePhysicalTrayMethods()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));yield return PoseAction(PoseEdits(25));workshop.SaveRetainedPose();Assert.That(workshop.IsPosing,Is.True);var saved=editor.Read("maestro").joints;int revision=editor.ObjectRevision("maestro");
            yield return PoseAction(PoseEdits(-25));workshop.DiscardRetainedPose();Assert.That(workshop.IsPosing,Is.False);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));SamePose(saved,avatar.PoseRig.Capture());
            editor.Undo();Assert.That(editor.Read("maestro").joints,Is.Null);editor.Redo();SamePose(saved,editor.Read("maestro").joints);
        }
        [UnityTest] public IEnumerator PoseVersionsRejectStaleEditsAndOldSessionsAndDuplicateReceiptsDoNotSaveTwice()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));var stale=PoseEdits(-30);yield return PoseAction(PoseEdits(30));
            Assert.That(authorActions.Execute(PoseRequest(stale),out _),Is.False);Assert.That(workshop.IsPosing,Is.True);
            var bad=PoseEdits(20);((JArray)bad["joints"]).Add(bad["joints"][0].DeepClone());var pose=avatar.PoseRig.Capture();Assert.That(authorActions.Execute(PoseRequest(bad),out _),Is.False);SamePose(pose,avatar.PoseRig.Capture());
            yield return PoseAction(PoseArgs("save"));var savedRequest=(JObject)poseRequest.DeepClone();int revision=editor.ObjectRevision("maestro");Assert.That(authorActions.Execute(savedRequest,out var error),Is.True,error);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));
            var old=PoseArgs("finish");yield return PoseAction(PoseArgs("discard"));yield return PoseAction(PoseArgs("start"));Assert.That(authorActions.Execute(PoseRequest(old),out _),Is.False);Assert.That(workshop.IsPosing,Is.True);
        }
        [UnityTest] public IEnumerator PhysicalJointHoldsBlockSharedMutationAndInvalidateEarlierPoseReads()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));var query=PoseJointQuery();var handle=avatar.GetComponentsInChildren<JointHandle>().Single(x=>x.name.StartsWith("Head "));
            var handRoot=new GameObject("Shared posing hand");handRoot.SetActive(false);handRoot.transform.SetParent(root.transform,false);handRoot.transform.position=handle.transform.position-Vector3.forward*.25f;
            var hand=handRoot.AddComponent<XRRayInteractor>();hand.enableUIInteraction=false;hand.interactionManager=manager;hand.keepSelectedTargetValid=true;hand.manipulateAttachTransform=false;hand.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.State;
            hand.selectInput=new XRInputButtonReader {inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue,manualPerformed=true,manualValue=1};handRoot.SetActive(true);manager.SelectEnter((IXRSelectInteractor)hand,handle.Item.Grab);yield return new WaitForSeconds(.12f);
            Assert.That((bool)PoseFact()["holding"],Is.True);Assert.That(authorActions.Execute(PoseRequest(PoseEdits(-30)),out var error),Is.False);Assert.That(error,Does.Contain("release").IgnoreCase);
            Assert.That(BehaviourCatalog.TryRead("animation.pose.joint",1,PoseJointQuery(),new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
            handRoot.transform.position+=Vector3.right*.08f;yield return null;yield return null;handRoot.SetActive(false);yield return null;
            Assert.That(editor.Read("maestro").joints,Is.Not.Null);Assert.That((int)PoseFact()["version"],Is.GreaterThan((int)query["version"]));Assert.That(BehaviourCatalog.TryRead("animation.pose.joint",1,query,new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
            yield return PoseAction(PoseEdits(20,PoseJoint.LeftUpperArm));yield return PoseAction(PoseArgs("finish"));Assert.That(workshop.IsPosing,Is.False);
        }
        [UnityTest] public IEnumerator AgentCanInspectAndRetryTheSamePoseRetainedByAPhysicalSaveFailure()
        {
            AuthorRuntime();workshop.TogglePose();var pose=EditHead(30);string file=BlockPoseSave();avatar.PoseRig.FinishedHandle();Assert.That(workshop.HasUnsavedPose,Is.True);var retained=PoseFact();Assert.That((string)retained["phase"],Is.EqualTo("unsaved"));
            Assert.That(Quaternion.Angle(AnimationAuthoringCapability.Rotation(Fact("animation.pose.joint",PoseJointQuery())),pose.Single(x=>x.joint==PoseJoint.Head).rotation),Is.LessThan(.05f));Directory.Delete(file);
            Assert.That(editor.Ownership.TryAcquire("other-program","Existing program",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim("maestro","upperBody")},_=>Assert.Fail("Agent recovery must not interrupt"),out var lease,out var error),Is.True,error);
            Assert.That(authorActions.Execute(PoseRequest(PoseArgs("save")),out _),Is.False);Assert.That(lease.Held,Is.True);Assert.That(workshop.HasUnsavedPose,Is.True);lease.Dispose();
            yield return PoseAction(PoseArgs("save"));SamePose(pose,editor.Read("maestro").joints);Assert.That(workshop.HasUnsavedPose,Is.False);Assert.That((string)PoseFact()["sessionId"],Is.Not.EqualTo((string)retained["sessionId"]));
        }
        [UnityTest] public IEnumerator SharedFinishFailureRetainsItsVersionAndStaleRecoveryCanOnlyBeDiscarded()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));yield return PoseAction(PoseEdits(30));string file=BlockPoseSave();
            Assert.That(authorActions.Execute(PoseRequest(PoseArgs("finish")),out _),Is.False);Assert.That(workshop.HasUnsavedPose,Is.True);Assert.That(workshop.IsPosing,Is.False);Directory.Delete(file);
            var pose=avatar.PoseRig.RestPose();Assert.That(editor.SaveAnimation("maestro",null,pose,true),Is.True);int revision=editor.ObjectRevision("maestro");
            Assert.That(authorActions.Execute(PoseRequest(PoseArgs("save")),out _),Is.False);Assert.That(workshop.HasUnsavedPose,Is.True);SamePose(pose,editor.Read("maestro").joints);
            yield return PoseAction(PoseArgs("discard"));Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(workshop.HasUnsavedPose,Is.False);
        }
        [UnityTest] public IEnumerator SharedPoseDoesNotStealActorsAndLifecycleStopInvalidatesTheSession()
        {
            AuthorRuntime();string identity=workshop.PoseSessionId;Assert.That(editor.Ownership.TryAcquire("other","Other actor",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim("maestro","wholeTarget")},_=>Assert.Fail("Shared posing cannot steal"),out var lease,out var error),Is.True,error);
            Assert.That(authorActions.Execute(PoseRequest(PoseArgs("start")),out _),Is.False);Assert.That(workshop.PoseSessionId,Is.EqualTo(identity));lease.Dispose();yield return PoseAction(PoseArgs("start"));yield return PoseAction(PoseEdits(20));var old=PoseArgs("finish");
            editor.SendMessage("OnApplicationPause",true);Assert.That(workshop.IsPosing,Is.False);Assert.That(editor.Read("maestro").joints,Is.Not.Null);int revision=editor.ObjectRevision("maestro");workshop.SendMessage("OnApplicationPause",true);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));editor.SendMessage("OnApplicationPause",false);
            Assert.That(authorActions.Execute(PoseRequest(old),out _),Is.False);Assert.That(workshop.IsPosing,Is.False);
        }
        [UnityTest] public IEnumerator PosingAndRecordingShareTheRigButRecordingOwnsTheirCombinedFinish()
        {
            AuthorRuntime();yield return PoseAction(PoseArgs("start"));yield return RecordAction("start");yield return new WaitForSeconds(.12f);yield return PoseAction(PoseEdits(35));
            Assert.That(authorActions.Execute(PoseRequest(PoseArgs("finish")),out _),Is.False);Assert.That(workshop.IsRecording,Is.True);yield return new WaitForSeconds(.12f);yield return RecordAction("finish");
            Assert.That(workshop.IsPosing,Is.False);Assert.That(workshop.IsRecording,Is.False);Assert.That((string)PoseFact()["phase"],Is.EqualTo("idle"));var motion=editor.Read("maestro").motion;
            Assert.That(motion.frames.Length,Is.GreaterThanOrEqualTo(3));Assert.That(motion.frames.All(f=>f.joints?.Length==17),Is.True);Assert.That(Quaternion.Angle(motion.frames[0].joints.Single(x=>x.joint==PoseJoint.Head).rotation,motion.frames[^1].joints.Single(x=>x.joint==PoseJoint.Head).rotation),Is.GreaterThan(20));
        }
        [UnityTest] public IEnumerator SharedPoseRetargetsImportedAvatarAndTemporaryFinishRequiresKeep()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("shared-pose.vrm",ModelFixture.Create(avatar:true));var saving=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>saving.IsCompleted);Assert.That(saving.Exception,Is.Null);Assert.That(editor.SetMaestroModel(asset.Hash),Is.True);yield return new WaitUntil(()=>!avatar.ModelBusy);
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return FinishAuthoringSave();yield return PoseAction(PoseArgs("start"));var bone=avatar.PoseRig.Bone(PoseJoint.Head);var before=bone.localRotation;
            yield return PoseAction(PoseEdits(30));Assert.That(Quaternion.Angle(before,bone.localRotation),Is.GreaterThan(20));yield return PoseAction(PoseArgs("finish"));var pose=editor.Read("maestro").joints;Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").joints,Is.Null);
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);yield return FinishAuthoringSave();SamePose(pose,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").joints);
        }
        [UnityTest] public IEnumerator SharedPoseRespectsAnActiveStrokeAndPutsThePencilAwayWithoutGlobalCancellation()
        {
            AuthorRuntime();var drawing=root.AddComponent<SpatialDrawing>();drawing.Editor=editor;editor.ToggleDrawing();drawing.Begin(0,new Ray(Vector3.zero,Vector3.forward));drawing.Move(0,new Ray(Vector3.right*.02f,Vector3.forward));
            Assert.That(authorActions.Execute(PoseRequest(PoseArgs("start")),out var error),Is.False);Assert.That(error,Does.Contain("stroke"));Assert.That(drawing.IsDrawing,Is.True);Assert.That(editor.DrawingMode,Is.True);
            drawing.End(0);int strokes=editor.Snapshot().objects.Count(x=>x.kind==RoomObjectKind.Drawing);Assert.That(strokes,Is.EqualTo(1));
            Assert.That(editor.Ownership.TryAcquire("unrelated-book","Book action",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim("book","wholeTarget")},_=>Assert.Fail("Changing pose mode must not stop an unrelated actor"),out var lease,out error),Is.True,error);
            int globalStops=0;editor.Editing+=()=>globalStops++;
            yield return PoseAction(PoseArgs("start"));Assert.That(editor.DrawingMode,Is.False);Assert.That(globalStops,Is.Zero);Assert.That(lease.Held,Is.True);Assert.That(workshop.IsPosing,Is.True);
            yield return PoseAction(PoseArgs("finish"));Assert.That(editor.Snapshot().objects.Count(x=>x.kind==RoomObjectKind.Drawing),Is.EqualTo(strokes));Assert.That(lease.Held,Is.True);lease.Dispose();
        }
        [UnityTest] public IEnumerator ProgramStartsAndFinishesASharedPoseWithoutCancellingItself()
        {
            AuthorRuntime();editor.ToggleDrawing();var source=JObject.Parse(BehaviourProgram.FromInvocation((JObject)PoseRequest(PoseArgs("start"))["call"]));source["version"]=3;source["state"]=new JArray();source["events"]=new JArray();var body=(JArray)source["functions"][0]["body"];
            var wait=JObject.Parse(BehaviourProgram.FromInvocation(new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=.2}}))["functions"][0]["body"][0];wait["id"]="wait";body.Add(wait);
            var finish=PoseArgs("finish");finish["version"]=1;var node=JObject.Parse(BehaviourProgram.FromInvocation((JObject)PoseRequest(finish)["call"]))["functions"][0]["body"][0];node["id"]="finish";body.Add(node);
            var sequence=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Pose once",program=source.ToString()};authorRuntime.Scheduler.Configure(new RuleDocument {sequences=new[]{sequence}});Assert.That(authorRuntime.Scheduler.Trigger(sequence.id,Time.unscaledTime),Is.True,authorRuntime.Scheduler.LastError);yield return new WaitForSeconds(.1f);Assert.That(workshop.IsPosing,Is.True);yield return new WaitForSeconds(.2f);
            Assert.That(authorRuntime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),authorRuntime.Scheduler.LastError);Assert.That(workshop.IsPosing,Is.False);Assert.That(editor.Read("maestro").joints,Is.Not.Null);
        }
    }
}
