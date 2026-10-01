// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarPropTests
    {
        GameObject root;
        RoomEditor editor;
        MaestroAvatar avatar;
        RoomRules runtime;
        RuleWorkshop rules;
        RoomPhysicsWorld world;
        XRInteractionManager manager;
        RoomItem ball;
        string directory;
        float delta;
        [UnitySetUp] public IEnumerator Setup()
        {
            delta=Time.captureDeltaTime; Time.captureDeltaTime=1f/72;
            directory=Path.Combine(Path.GetTempPath(),"MaestroPropRuntime-"+Guid.NewGuid().ToString("N"));
            root=new GameObject("Prop room"); manager=root.AddComponent<XRInteractionManager>(); var room=root.AddComponent<RoomInteraction>();
            world=root.AddComponent<RoomPhysicsWorld>(); RoomPhysicsLayers.Configure();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(root.transform,false); floor.transform.position=new Vector3(0,-.1f,0); floor.transform.localScale=new Vector3(20,.2f,20); floor.layer=RoomPhysicsLayers.Scanned;
            RoomItem Included(string name,Vector3 position)
            {
                var obj=new GameObject(name); obj.transform.SetParent(root.transform,false); obj.transform.position=position;
                var box=obj.AddComponent<BoxCollider>(); box.size=Vector3.one*.1f;
                var item=obj.AddComponent<RoomItem>(); item.Configure(new Collider[] { box }); room.Register(item); return item;
            }
            var book=Included("book",new Vector3(5,1,1)); var tutor=Included("maestro",Vector3.zero); avatar=tutor.gameObject.AddComponent<MaestroAvatar>();
            editor=root.AddComponent<RoomEditor>(); editor.Initialize(room,book,tutor,directory,world);
            var animations=root.AddComponent<AnimationWorkshop>(); animations.Initialize(editor); rules=root.AddComponent<RuleWorkshop>(); rules.Initialize(editor,directory);
            runtime=root.AddComponent<RoomRules>(); runtime.Initialize(rules,editor,animations,null,room,null);
            foreach (var data in editor.Snapshot().objects.Where(x => !x.IsBuiltIn)) { var item=editor.Find(data.id); item.transform.position=new Vector3(6,2,2); editor.RememberPlacement(data.id); }
            editor.Create(RoomObjectKind.Ball); ball=editor.Find(editor.SelectedId);
            yield return null;
        }
        void Configure(PropRelease release,float at=.5f)
        {
            var hand=avatar.PoseRig.Bone(PoseJoint.RightHand); ball.transform.position=hand.position+Vector3.forward*.25f;
            ball.GetComponent<RigidRoomItem>().Teleported(); editor.RememberPlacement(editor.Identity(ball));
            var pose=avatar.PoseRig.Capture();
            editor.SaveAnimation("maestro",new RoomMotion { frames=new[] { new MotionFrame { position=avatar.transform.localPosition,joints=pose },new MotionFrame { time=1,position=avatar.transform.localPosition+Vector3.right,joints=pose } } },null,false);
            rules.NewSequence(); for (int i=0;i<Enum.GetValues(typeof(RuleActionKind)).Length && rules.Selected.SimpleSteps()[0].action != RuleActionKind.RecordedAnimation;i++) rules.CycleAction();
            Assert.That(rules.Selected.SimpleSteps()[0].action,Is.EqualTo(RuleActionKind.RecordedAnimation));
            editor.Select(editor.Find("maestro"));rules.UseTarget();Assert.That(rules.Selected.SimpleSteps()[0].targetId,Is.EqualTo("maestro"));
            editor.Select(ball); rules.UseProp(); rules.FitProp();
            for(int i=0;i<Enum.GetValues(typeof(PropRelease)).Length && rules.Selected.SimpleSteps()[0].propRelease != release;i++) rules.CyclePropRelease();
            Assert.That(rules.Selected.SimpleSteps()[0].propId,Is.EqualTo(editor.Identity(ball)));
            Assert.That(rules.Selected.SimpleSteps()[0].propRelease,Is.EqualTo(release));
            for (int i=0;i<20 && Mathf.Abs(rules.Selected.SimpleSteps()[0].propReleaseAt-at)>.001f;i++) rules.CyclePropTime();
            world.SetSurfaces(true,"Synthetic room aligned"); world.StartPhysics();
        }
        static IEnumerator Until(Func<bool> value)
        {
            float deadline=Time.realtimeSinceStartup+10; while (!value() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(value(),Is.True,"Operation did not finish");
        }
        [UnityTest] public IEnumerator RecordedAvatarCarriesThenThrowsBallWithRealGravityAndFloorBounce()
        {
            Configure(PropRelease.Throw); Assert.That(runtime.Trigger(rules.Selected.id),Is.True,rules.Status);
            yield return new WaitForSeconds(.2f);
            var hold=ball.GetComponent<HeldRoomProp>(); Assert.That(hold && hold.Holding,Is.True); Assert.That(ball.GetComponent<Rigidbody>().isKinematic,Is.True);
            var hand=avatar.PoseRig.Bone(PoseJoint.RightHand); var step=rules.Selected.SimpleSteps()[0];
            Assert.That(Vector3.Distance(ball.transform.position,hand.position+hand.rotation*step.propOffset),Is.LessThan(.02f));
            yield return Until(() => hold && hold.Released || runtime.Scheduler.RunningCount == 0);
            Assert.That(hold && hold.Released,Is.True,runtime.Scheduler.LastError);
            var body=ball.GetComponent<Rigidbody>(); Assert.That(body.linearVelocity.x,Is.GreaterThan(.3f));
            bool fell=false,bounced=false;
            for (int i=0;i<120;i++) { yield return new WaitForFixedUpdate(); fell |= body.linearVelocity.y < -1; bounced |= fell && body.linearVelocity.y > .5f; Assert.That(ball.transform.position.y,Is.GreaterThan(.04f)); }
            Assert.That(bounced,Is.True,"The released prop must use actual room collision"); yield return Until(() => runtime.Scheduler.RunningCount == 0);
            Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
        }
        [UnityTest] public IEnumerator BlockingWallAndPausedPhysicsStopWithoutThrowingOrReplaying()
        {
            Configure(PropRelease.Throw,.9f); var start=ball.transform.position;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform,false); wall.layer=RoomPhysicsLayers.Scanned;
            wall.transform.position=start+Vector3.right*.4f; wall.transform.localScale=new Vector3(.05f,4,4); Physics.SyncTransforms();
            yield return null; // Let scene setup finish before measuring a live motion gap.
            Assert.That(runtime.Trigger(rules.Selected.id),Is.True,rules.Status); yield return Until(() => runtime.Scheduler.RunningCount == 0);
            Assert.That(runtime.Scheduler.LastError,Does.Contain("blocked")); Assert.That(ball.GetComponent<Rigidbody>().linearVelocity.x,Is.EqualTo(0).Within(.01f));
            wall.SetActive(false); world.PausePhysics(); ball.transform.position=start; ball.GetComponent<RigidRoomItem>().Teleported(); world.StartPhysics();
            Assert.That(runtime.Trigger(rules.Selected.id),Is.True,rules.Status); yield return new WaitForSeconds(.1f); world.PausePhysics();
            yield return null; yield return null; Assert.That(runtime.Scheduler.RunningCount,Is.Zero); Assert.That(ball.GetComponent<Rigidbody>().isKinematic,Is.True);
            world.StartPhysics(); yield return null; Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
        }
        [UnityTest] public IEnumerator UserGripTakesPropWithoutSnappingAndAssignmentsUndoAndPersist()
        {
            Configure(PropRelease.Return); string id=rules.Selected.SimpleSteps()[0].propId;
            rules.ClearProp(); rules.Undo(); Assert.That(rules.Selected.SimpleSteps()[0].propId,Is.EqualTo(id));
            rules.SendMessage("OnApplicationPause",true); Assert.That(new RuleStorage(directory).Load(out _).sequences[0].SimpleSteps()[0].propId,Is.EqualTo(id));
            rules.SendMessage("OnApplicationPause",false); world.StartPhysics();
            Assert.That(runtime.Trigger(rules.Selected.id),Is.True,rules.Status); yield return new WaitForSeconds(.2f); var before=ball.transform.position;
            var hand=new GameObject("User prop grip"); hand.SetActive(false); hand.transform.SetParent(root.transform,false); hand.transform.position=before;
            var ray=hand.AddComponent<XRRayInteractor>(); ray.enableUIInteraction=false; ray.interactionManager=manager; ray.keepSelectedTargetValid=true; ray.manipulateAttachTransform=false;
            ray.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.State; ray.selectInput=new XRInputButtonReader { inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue,manualPerformed=true,manualValue=1 };
            hand.SetActive(true); manager.SelectEnter((IXRSelectInteractor)ray,ball.Grab);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero); Assert.That(Vector3.Distance(before,ball.transform.position),Is.LessThan(.01f));
            Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False); hand.SetActive(false); yield return null;
        }
        [UnityTest] public IEnumerator CustomAvatarLibraryMotionCarriesAtTheDisplayedHandAndCancelRestoresProp()
        {
            var bytes=ModelFixture.Mixamo(json => json["animations"][0]["channels"][0]["target"]["node"]=9);
            var source=ModelLibrary.Inspect("prop-avatar.glb",bytes); var save=editor.Models.SaveAsync(source); yield return Until(() => save.IsCompleted); Assert.That(save.Exception,Is.Null);
            Assert.That(editor.SetMaestroModel(source.Hash),Is.True); yield return Until(() => !avatar.ModelBusy); Assert.That(avatar.CustomModel,Is.Not.Null,avatar.ModelStatus);
            var import=editor.Motions.ImportAsync("prop-motion.glb",bytes); yield return Until(() => import.IsCompleted); Assert.That(import.Exception,Is.Null);
            Configure(PropRelease.Return); rules.AssignLibraryMotion(import.Result.Single().id); var home=ball.transform.position;
            Assert.That(runtime.Trigger(rules.Selected.id),Is.True,rules.Status); yield return Until(() => avatar.LibraryMotionId != null || runtime.Scheduler.RunningCount == 0);
            Assert.That(avatar.LibraryMotionId,Is.Not.Null,runtime.Scheduler.LastError);
            yield return new WaitForSeconds(.3f); var step=rules.Selected.SimpleSteps()[0]; var hand=avatar.PoseRig.Bone(PoseJoint.RightHand);
            Assert.That(Vector3.Distance(ball.transform.position,hand.position+hand.rotation*step.propOffset),Is.LessThan(.02f),runtime.Scheduler.LastError);
            runtime.SendMessage("OnApplicationPause",true); Assert.That(Vector3.Distance(ball.transform.position,home),Is.LessThan(.001f));
            runtime.SendMessage("OnApplicationPause",false); yield return null; Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
        }
        [UnityTest] public IEnumerator PhysicalPropTabSwitchesControlsAndEditsTheSelectedStep()
        {
            Configure(PropRelease.Return);
            var board=new GameObject("Rule tray"); board.transform.SetParent(root.transform,false); board.transform.position=new Vector3(2,1,0);
            var tools=board.AddComponent<RuleTools>(); tools.Build(rules,root.GetComponent<RoomInteraction>());
            var router=root.AddComponent<BookPointerRouter>(); router.Editor=editor;
            void Click(string label)
            {
                Physics.SyncTransforms(); var button=board.GetComponentsInChildren<RuleToolAction>().Single(x => x.AccessibleName == label);
                var ray=new Ray(button.transform.position-Vector3.forward*.2f,Vector3.forward);
                Assert.That(router.Begin(1,ray),Is.True,label); router.End(1,ray);
            }
            Click("Show prop controls"); Assert.That(tools.PropsVisible,Is.True);
            Assert.That(board.GetComponentsInChildren<RuleToolAction>().Any(x => x.AccessibleName == "Step type"),Is.False);
            Click("Clear prop"); Assert.That(rules.Selected.SimpleSteps()[0].propId,Is.Null.Or.Empty);
            Click("Undo rules"); Assert.That(rules.Selected.SimpleSteps()[0].propId,Is.EqualTo(editor.Identity(ball)));
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Opening or editing props never starts their motion");
            yield return new WaitForSecondsRealtime(.3f); Click("Show action controls"); Assert.That(tools.PropsVisible,Is.False);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEngine.Object.Destroy(root); Time.captureDeltaTime=delta; yield return null; yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
    }
}
