// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace Maestro.Quest.Tests
{
    public sealed class AnimationWorkshopTests
    {
        GameObject root;
        XRInteractionManager manager;
        RoomEditor editor;
        AnimationWorkshop workshop;
        MaestroAvatar avatar;
        RoomItem avatarItem;
        string directory;
        [UnitySetUp] public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(),"MaestroWorkshopTests-"+Guid.NewGuid().ToString("N"));
            root = new GameObject("Animation workshop test"); manager = root.AddComponent<XRInteractionManager>();
            var room = root.AddComponent<RoomInteraction>();
            RoomItem Included(string name)
            {
                var item = new GameObject(name); item.transform.SetParent(root.transform,false);
                var collider = item.AddComponent<BoxCollider>(); collider.size = Vector3.one * .1f;
                var result = item.AddComponent<RoomItem>(); result.Configure(new Collider[] { collider }); room.Register(result); return result;
            }
            var book = Included("book"); avatarItem = Included("maestro"); avatar = avatarItem.gameObject.AddComponent<MaestroAvatar>();
            editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,avatarItem,directory);
            workshop = root.AddComponent<AnimationWorkshop>(); workshop.Initialize(editor);
            yield return null;
        }
        [UnityTest] public IEnumerator GrabbingHeadHandleRotatesSkeletonAndSavesPoseWithUndoAndKeyframes()
        {
            workshop.TogglePose(); Assert.That(workshop.IsPosing,Is.True);
            var head = avatar.PoseRig.Bone(PoseJoint.Head); Assert.That(head,Is.Not.Null);
            Assert.That(avatar.PoseRig.Capture().Length,Is.EqualTo(17));
            var handle = avatar.GetComponentsInChildren<JointHandle>().Single(x => x.name.StartsWith("Head "));
            var before = head.localRotation; var handRoot = new GameObject("Test posing hand"); handRoot.SetActive(false); handRoot.transform.SetParent(root.transform,false);
            handRoot.transform.position = handle.transform.position - Vector3.forward * .25f;
            var hand = handRoot.AddComponent<XRRayInteractor>(); hand.enableUIInteraction = false; hand.interactionManager = manager;
            hand.keepSelectedTargetValid = true; hand.manipulateAttachTransform = false; hand.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
            hand.selectInput = new XRInputButtonReader { inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue, manualPerformed = true, manualValue = 1 };
            handRoot.SetActive(true); manager.SelectEnter((IXRSelectInteractor)hand,handle.Item.Grab);
            yield return new WaitForSeconds(.15f); var handleBefore = handle.transform.position; handRoot.transform.position += Vector3.right * .08f;
            yield return null; yield return null;
            Assert.That(handle.Item.Grab.isSelected,Is.True,"The hand still owns the joint handle");
            Assert.That(Vector3.Distance(handleBefore,handle.transform.position),Is.GreaterThan(.05f),"XRI moved the handle");
            Assert.That(Quaternion.Angle(before,head.localRotation),Is.GreaterThan(5));
            Assert.That(Quaternion.Angle(before,head.localRotation),Is.LessThanOrEqualTo(76));
            handRoot.SetActive(false); yield return null;
            Assert.That(editor.Read("maestro").joints,Is.Not.Null);
            workshop.AddFrame();
            avatar.PoseRig.Rotate(PoseJoint.Head,head.parent.rotation * before); avatar.PoseRig.FinishedHandle(); workshop.AddFrame();
            Assert.That(editor.Read("maestro").motion.frames.Length,Is.EqualTo(2));
            workshop.Play(); Assert.That(workshop.IsPlaying,Is.True); yield return new WaitForSeconds(.2f);
            workshop.Stop(); Assert.That(avatarItem.Grab.enabled,Is.True); Assert.That(workshop.IsPosing,Is.False);
            workshop.ResetPose(); Assert.That(editor.Read("maestro").joints,Is.Null);
            editor.Undo(); Assert.That(editor.Read("maestro").joints,Is.Not.Null);
        }
        [UnityTest] public IEnumerator ObjectRecordingAndFocusLossKeepTakeAndStopPreviewWithoutAutoplay()
        {
            var block = editor.Snapshot().objects.First(x => x.kind == RoomObjectKind.Block); var item = editor.Find(block.id); editor.Select(item);
            workshop.ToggleRecord(); yield return new WaitForSeconds(.12f);
            item.transform.localPosition += Vector3.right * .4f; yield return new WaitForSeconds(.12f);
            workshop.SendMessage("OnApplicationPause",true); Assert.That(workshop.IsRecording,Is.False);
            var motion = editor.Read(block.id).motion; Assert.That(motion.frames.Length,Is.GreaterThanOrEqualTo(3));
            Assert.That(motion.frames[^1].position.x - motion.frames[0].position.x,Is.EqualTo(.4f).Within(.001f));
            workshop.Play(); yield return new WaitForSeconds(.14f); Assert.That(workshop.IsPlaying,Is.True);
            workshop.SendMessage("OnApplicationFocus",false); Assert.That(workshop.IsPlaying,Is.False);
            Assert.That(item.transform.localPosition,Is.EqualTo(editor.Read(block.id).position));
            workshop.SendMessage("OnApplicationFocus",true); yield return null; Assert.That(workshop.IsPlaying,Is.False);
            editor.SaveNow(); yield return null; UnityEngine.Object.Destroy(workshop); yield return null;
            UnityEngine.Object.Destroy(editor); yield return null;
            var restored = new RoomStorage(directory).Load(out var error); Assert.That(restored,Is.Not.Null,error);
            Assert.That(restored.objects.Single(x => x.id == block.id).motion.frames.Length,Is.EqualTo(motion.frames.Length));
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(root); yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
    }
}
