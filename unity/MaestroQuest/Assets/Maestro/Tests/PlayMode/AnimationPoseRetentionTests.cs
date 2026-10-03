// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        string BlockPoseSave()
        {
            Assert.That(editor.TryFlush(out var error),Is.True,error);
            string path=Path.Combine(directory,"room.v4.json");if(File.Exists(path))File.Delete(path);Directory.CreateDirectory(path);return path;
        }
        JointPose[] EditHead(float degrees)
        {
            var head=avatar.PoseRig.Bone(PoseJoint.Head);
            avatar.PoseRig.Rotate(PoseJoint.Head,head.parent.rotation*Quaternion.Euler(degrees,0,0));
            return avatar.PoseRig.Capture();
        }
        static void SamePose(JointPose[] expected,JointPose[] actual)
        {
            Assert.That(actual,Has.Length.EqualTo(expected.Length));
            foreach(var joint in expected)Assert.That(Quaternion.Angle(joint.rotation,actual.Single(x=>x.joint==joint.joint).rotation),Is.LessThan(.05f),joint.joint.ToString());
        }
        [UnityTest] public IEnumerator FailedPoseCannotBeReplacedByStartingAnotherPoseSession()
        {
            editor.SaveNow();yield return null;workshop.TogglePose();string file=BlockPoseSave();
            var expected=EditHead(25);avatar.PoseRig.FinishedHandle();
            workshop.Stop();Directory.Delete(file);workshop.TogglePose();
            Assert.That(workshop.IsPosing,Is.False,"A failed pose must be saved or explicitly discarded before another pose can replace it");
            Assert.That(workshop.HasUnsavedPose,Is.True);Assert.That(avatarItem.Grab.enabled,Is.True);Assert.That(workshop.ControlsTarget("maestro"),Is.False);
            var block=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block);editor.Select(editor.Find(block.id));
            Assert.That(workshop.Status,Does.Contain("Pose retained"));Assert.That(workshop.SaveRetainedPose(out var error),Is.True,error);
            SamePose(expected,editor.Read("maestro").joints);SamePose(expected,avatar.PoseRig.Capture());
            SamePose(expected,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").joints);
            Assert.That(editor.Read(block.id).joints,Is.Null);Assert.That(workshop.HasUnsavedPose,Is.False);Assert.That(workshop.IsPlaying,Is.False);
        }
        [UnityTest] public IEnumerator FailedStopRetainsPoseWithoutRepeatedSavesAndRetryIsOneUndo()
        {
            editor.Select(avatarItem);workshop.AddFrame();workshop.Stop();editor.SaveNow();yield return null;
            var before=editor.Read("maestro").joints;workshop.TogglePose();var expected=EditHead(35);string file=BlockPoseSave();int revision=editor.ObjectRevision("maestro");
            workshop.Stop();Assert.That(workshop.HasUnsavedPose,Is.True);Assert.That(workshop.SaveRetainedPose(out _),Is.False);
            workshop.SendMessage("OnApplicationFocus",false);workshop.SendMessage("OnApplicationPause",true);workshop.SendMessage("OnApplicationFocus",true);workshop.SendMessage("OnApplicationPause",false);
            Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(workshop.HasUnsavedPose,Is.True);
            Directory.Delete(file);Assert.That(workshop.SaveRetainedPose(out var error),Is.True,error);SamePose(expected,editor.Read("maestro").joints);
            Assert.That(editor.Read("maestro").motion.frames.Length,Is.EqualTo(1));int savedRevision=editor.ObjectRevision("maestro");
            Assert.That(workshop.SaveRetainedPose(out _),Is.False);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(savedRevision));
            editor.Undo();SamePose(before,editor.Read("maestro").joints);Assert.That(editor.Read("maestro").motion.frames.Length,Is.EqualTo(1));editor.Redo();SamePose(expected,editor.Read("maestro").joints);
        }
        [UnityTest] public IEnumerator RetainedPoseBlocksAvatarAndRoomReplacementAndOtherAuthoring()
        {
            editor.SaveNow();yield return null;workshop.TogglePose();EditHead(20);string file=BlockPoseSave();
            workshop.Play();Assert.That(workshop.HasUnsavedPose,Is.True);Assert.That(workshop.IsPlaying,Is.False);
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.False);Assert.That(error,Does.Contain("retained pose"));
            Directory.Delete(file);Assert.That(editor.SetMaestroModel(null),Is.False);Assert.That(editor.Status,Does.Contain("retained pose"));
            workshop.ToggleRecord();workshop.AddFrame();workshop.ResetPose();workshop.Gesture();workshop.PreviewWalk();workshop.TogglePose();
            Assert.That(workshop.IsRecording,Is.False);Assert.That(workshop.IsPosing,Is.False);Assert.That(workshop.ControlsTarget("maestro"),Is.False);Assert.That(editor.Read("maestro").motion,Is.Null);
            workshop.DiscardRetainedPose();Assert.That(workshop.HasUnsavedPose,Is.False);Assert.That(editor.Read("maestro").joints,Is.Null);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);yield return FinishAuthoringSave();Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(editor.SetMaestroModel(null),Is.True,editor.Status);workshop.TogglePose();Assert.That(workshop.IsPosing,Is.True);
        }
        [UnityTest] public IEnumerator RetainedPoseNeverOverwritesANewerEditAndDiscardPreservesIt()
        {
            editor.SaveNow();yield return null;workshop.TogglePose();EditHead(30);string file=BlockPoseSave();avatar.PoseRig.FinishedHandle();Directory.Delete(file);
            var newer=avatar.PoseRig.RestPose();newer.Single(x=>x.joint==PoseJoint.Head).rotation=Quaternion.Euler(-20,0,0);
            Assert.That(editor.SaveAnimation("maestro",null,newer,true),Is.True,editor.Status);int revision=editor.ObjectRevision("maestro");
            Assert.That(workshop.SaveRetainedPose(out var error),Is.False);Assert.That(error,Does.Contain("changed"));Assert.That(workshop.HasUnsavedPose,Is.True);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));
            SamePose(newer,editor.Read("maestro").joints);workshop.DiscardRetainedPose();SamePose(newer,editor.Read("maestro").joints);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));
        }
        [UnityTest] public IEnumerator RetainedPoseRetryHonoursHoldsAndOtherManualOwners()
        {
            editor.SaveNow();yield return null;workshop.TogglePose();EditHead(20);string file=BlockPoseSave();workshop.Stop();Directory.Delete(file);
            using(editor.RuntimeGate.Hold("Workspace test hold")){Assert.That(workshop.SaveRetainedPose(out var error),Is.False);Assert.That(error,Is.EqualTo("Workspace test hold"));}
            using(editor.WriteGate.TryFreeze(out _)){Assert.That(workshop.SaveRetainedPose(out _),Is.False);}
            Assert.That(editor.Ownership.TryAcquire("other-controls","Other manual controls",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim("maestro","wholeTarget")},_=>Assert.Fail("Retry must not replace other manual controls"),out var lease,out var issue),Is.True,issue);
            Assert.That(workshop.SaveRetainedPose(out _),Is.False);Assert.That(lease.Held,Is.True);lease.Dispose();Assert.That(workshop.SaveRetainedPose(out issue),Is.True,issue);
        }
        [UnityTest] public IEnumerator FailedTemporaryPoseStaysInItsForkUntilKeep()
        {
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return FinishAuthoringSave();workshop.TogglePose();var expected=EditHead(20);
            using(editor.WriteGate.TryFreeze(out error)){workshop.Stop();Assert.That(workshop.HasUnsavedPose,Is.True);}
            Assert.That(editor.KeepTemporaryRoom(out error),Is.False);Assert.That(error,Does.Contain("retained pose"));Assert.That(editor.DiscardTemporaryRoom(out error),Is.False);
            Assert.That(workshop.SaveRetainedPose(out error),Is.True,error);SamePose(expected,editor.Read("maestro").joints);
            Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").joints,Is.Null);
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);yield return FinishAuthoringSave();SamePose(expected,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").joints);
        }
        [UnityTest] public IEnumerator RetainedPoseRetryUsesTheImportedDisplayRig()
        {
            var asset=ModelLibrary.Inspect("retained-pose.vrm",ModelFixture.Create(avatar:true));var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            Assert.That(editor.SetMaestroModel(asset.Hash),Is.True);yield return new WaitUntil(()=>!avatar.ModelBusy);
            workshop.TogglePose();var expected=EditHead(20);var displayed=avatar.PoseRig.Bone(PoseJoint.Head);var expectedDisplayed=displayed.localRotation;string file=BlockPoseSave();avatar.PoseRig.FinishedHandle();Directory.Delete(file);
            Assert.That(workshop.SaveRetainedPose(out var error),Is.True,error);SamePose(expected,editor.Read("maestro").joints);Assert.That(Quaternion.Angle(expectedDisplayed,displayed.localRotation),Is.LessThan(.05f));
            Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash));
        }
        [UnityTest] public IEnumerator SolidPoseRecoveryControlsSaveOrDiscardTheActualRetainedPose()
        {
            editor.SaveNow();yield return null;var tray=new GameObject("Pose recovery tools");tray.transform.SetParent(root.transform,false);tray.transform.localPosition=new Vector3(2,0,1);tray.AddComponent<AnimationTools>().Build(workshop,root.GetComponent<RoomInteraction>());
            var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;yield return null;Physics.SyncTransforms();
            foreach(var action in tray.GetComponentsInChildren<AnimationToolAction>()){
                var label=action.GetComponentInChildren<TextMesh>().GetComponent<MeshRenderer>().bounds;
                Assert.That(label.min.x,Is.GreaterThanOrEqualTo(action.transform.position.x-.06f),action.AccessibleName);
                Assert.That(label.max.x,Is.LessThanOrEqualTo(action.transform.position.x+.06f),action.AccessibleName);
                Assert.That(label.min.y,Is.GreaterThan(tray.transform.position.y-.20f),action.AccessibleName);
            }
            void Click(float x){var ray=new Ray(new Vector3(x,-.10f,0),Vector3.forward);Assert.That(router.Begin(0,ray),Is.True);router.End(0,ray);}
            workshop.TogglePose();var expected=EditHead(25);string file=BlockPoseSave();avatar.PoseRig.FinishedHandle();Directory.Delete(file);
            CapturePoseRecoveryControls(tray);Click(2.062f);Assert.That(workshop.HasUnsavedPose,Is.False);SamePose(expected,editor.Read("maestro").joints);
            workshop.TogglePose();EditHead(-25);file=BlockPoseSave();workshop.Stop();Directory.Delete(file);Click(2.186f);
            Assert.That(workshop.HasUnsavedPose,Is.False);SamePose(expected,editor.Read("maestro").joints);
        }
        void CapturePoseRecoveryControls(GameObject tray)
        {
            string path=Environment.GetEnvironmentVariable("MAESTRO_POSE_RETENTION");if(string.IsNullOrEmpty(path))return;
            Directory.CreateDirectory(path);var cameraRoot=new GameObject("Pose recovery camera");cameraRoot.transform.SetParent(root.transform,false);var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.85f,.89f,.91f);cameraRoot.transform.position=tray.transform.position+Vector3.back*1.25f;cameraRoot.transform.LookAt(tray.transform.position);camera.fieldOfView=40;
            var render=new RenderTexture(1200,900,24);var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(path,"controls.png"),pixels.EncodeToPNG());}finally{RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);}
        }
    }
}
