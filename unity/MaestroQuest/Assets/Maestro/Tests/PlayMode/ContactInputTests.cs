// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        JObject ContactFact(string id){Assert.That(BehaviourCatalog.TryRead("object.contact.inputs",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);}
        void ContactStart(){physics.ConfigureCollisionAdmission(editor,()=>null);physics.SetSurfaces(true,"Synthetic aligned contact fixture");physics.StartPhysics();}
        (RoomItem item,XRRayInteractor hand) ContactGrab(string id){var item=editor.Find(id);var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);Assert.That(item.Grab.isSelected,Is.True);return(item,hand);}
        Pose ContactTarget(RoomItem item,Vector3 position){var target=new Pose(position,item.transform.rotation);var scale=item.transform.localScale;item.GetComponent<RoomGrabContactInputs>().Process(item.Grab,XRInteractionUpdateOrder.UpdatePhase.Dynamic,ref target,ref scale);return target;}
        TrackedPusher ContactPusher(Vector3 position,out XRRayInteractor hand){
            hand=Hand(1,position);hand.selectInput.manualPerformed=false;hand.selectInput.manualValue=0;
            var input=root.GetComponent<BookControllerInput>();if(!input)input=root.AddComponent<BookControllerInput>();input.Editor=editor;input.PhysicsWorld=physics;
            var go=new GameObject("Contact admission fixture");go.transform.SetParent(root.transform,false);var pusher=go.AddComponent<TrackedPusher>();pusher.Source=hand.transform;pusher.Interactor=hand;pusher.Input=input;return pusher;
        }
        [UnityTest]public IEnumerator ContactInputsAcquireOnlyTheLatestCorridorAndReadsDoNotAcquire(){
            string near=AvailabilityTerrain();Assert.That(editor.RetireNativeArea(NativeAreaFor(near),out var error),Is.True,error);
            var bound=new Bounds(GroundPoint,Vector3.one*.3f);using var contact=new RoomContactInputs(editor,"book",()=>true);
            Assert.That((string)contact.Observe()["state"],Is.EqualTo("unavailable"));Assert.That(editor.NativeActivationPending,Is.False);
            Assert.That(contact.Require(bound,out error),Is.False);Assert.That((string)contact.Observe()["state"],Is.EqualTo("pending"));
            yield return NavigationLoadingDone();Assert.That(contact.Require(bound,out error),Is.True,error);Assert.That((bool)contact.Observe()["ready"],Is.True);
            Assert.That(editor.RetireNativeArea(editor.RegionFor(near),out error),Is.False,"The active contact retains its geometry");
            var far=new Bounds(new Vector3(-12,3,-12),Vector3.one*.1f);Assert.That(contact.Require(far,out error),Is.True,error);
            Assert.That(editor.RetireNativeArea(editor.RegionFor(near),out error),Is.True,error);Assert.That((bool)contact.Observe()["ready"],Is.True,"Old displacement must not remain in the corridor");
        }
        [UnityTest]public IEnumerator ContactInputsGrabWaitsForGeometryThenUsesTheCurrentTargetWithoutOldThrowSamples(){
            string ground=AvailabilityTerrain(),id=PhysicsInputBall(new(4,.18f,4));Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);ContactStart();
            var(item,hand)=ContactGrab(id);var before=item.transform.position;
            var blocked=ContactTarget(item,before+Vector3.right*.3f+Vector3.down*.2f);Assert.That(blocked.position,Is.EqualTo(before));Assert.That((string)ContactFact(id)["state"],Is.EqualTo("pending"));Assert.That(item.Grab.throwOnDetach,Is.False);
            yield return NavigationLoadingDone();var latest=item.transform.position+Vector3.left*.1f;var accepted=ContactTarget(item,latest);
            Assert.That(accepted.position,Is.EqualTo(latest));Assert.That((bool)ContactFact(id)["ready"],Is.True);Assert.That(item.Grab.throwOnDetach,Is.False,"The pre-loading throw history must age out");
            hand.selectInput.manualPerformed=false;hand.selectInput.manualValue=0;manager.SelectExit((IXRSelectInteractor)hand,item.Grab);yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(item.GetComponent<Rigidbody>().linearVelocity.x),Is.LessThan(.1f));Assert.That((string)ContactFact(id)["state"],Is.EqualTo("inactive"));
        }
        [UnityTest]public IEnumerator ContactInputsActualGrabCannotMoveThroughAnUnavailableSweptRegion(){
            string ground=AvailabilityTerrain(),id=PhysicsInputBall(new(4,.13f,4));ContactStart();var(item,hand)=ContactGrab(id);ContactTarget(item,item.transform.position);Assert.That((bool)ContactFact(id)["ready"],Is.True);editor.Find(ground).gameObject.SetActive(false);var before=item.transform.position;
            for(int i=0;i<8;i++){hand.transform.position+=Vector3.right*.02f;yield return new WaitForFixedUpdate();yield return null;}
            Assert.That(Vector3.Distance(before,item.transform.position),Is.LessThan(.01f));Assert.That((bool)ContactFact(id)["ready"],Is.False);Assert.That(item.Grab.throwOnDetach,Is.False);
        }
        [UnityTest]public IEnumerator ContactInputsPauseImmediatelyReleasesPendingGrabDemand(){
            string ground=AvailabilityTerrain(),id=PhysicsInputBall(new(4,.18f,4));Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);ContactStart();var(item,_)=ContactGrab(id);
            ContactTarget(item,item.transform.position+Vector3.down*.2f);Assert.That((string)ContactFact(id)["state"],Is.EqualTo("pending"));physics.PausePhysics();
            Assert.That((string)ContactFact(id)["state"],Is.EqualTo("inactive"));yield return NavigationLoadingDone();Assert.That(editor.Find(ground),Is.Null,"Pause retires every old contact owner");
        }
        [UnityTest]public IEnumerator ContactInputsPusherWaitsForGeometryAndNeverReplaysItsMissedPath(){
            string ground=AvailabilityTerrain();Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);ContactStart();var pusher=ContactPusher(new Vector3(4,.12f,4),out var hand);
            pusher.SendMessage("FixedUpdate");pusher.SendMessage("FixedUpdate");Assert.That(pusher.GetComponent<SphereCollider>().enabled,Is.False);Assert.That((string)pusher.ObserveInputs()["state"],Is.EqualTo("pending"));
            hand.transform.position=new Vector3(4,.4f,4);pusher.SendMessage("FixedUpdate");yield return NavigationLoadingDone();pusher.SendMessage("FixedUpdate");
            Assert.That(pusher.GetComponent<SphereCollider>().enabled,Is.True,pusher.ObserveInputs().ToString());Assert.That(Vector3.Distance(pusher.GetComponent<Rigidbody>().position,hand.transform.position),Is.LessThan(.01f));
        }
        [UnityTest]public IEnumerator ContactInputsTrackingJumpCannotSpawnContactInsideAnObject(){
            string ground=AvailabilityTerrain(),id=PhysicsInputBall(new(4,.22f,4));ContactStart();var pusher=ContactPusher(new Vector3(3,.25f,4),out var hand);
            pusher.SendMessage("FixedUpdate");pusher.SendMessage("FixedUpdate");var ball=editor.Find(id);hand.transform.position=ball.transform.position;
            pusher.SendMessage("FixedUpdate");Assert.That(pusher.GetComponent<SphereCollider>().enabled,Is.False);pusher.SendMessage("FixedUpdate");Assert.That(pusher.GetComponent<SphereCollider>().enabled,Is.False,pusher.ObserveInputs().ToString());
            hand.transform.position+=Vector3.up*.3f;pusher.SendMessage("FixedUpdate");Assert.That(pusher.GetComponent<SphereCollider>().enabled,Is.True,pusher.ObserveInputs().ToString());
            Assert.That(Mathf.Abs(ball.GetComponent<Rigidbody>().linearVelocity.x),Is.LessThan(.01f));yield return null;
        }
        [UnityTest]public IEnumerator ContactInputsPusherDisableReleasesItsLoadButPreservesAnotherConsumer(){
            string ground=AvailabilityTerrain();Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);ContactStart();var pusher=ContactPusher(new Vector3(4,.12f,4),out _);
            pusher.SendMessage("FixedUpdate");pusher.SendMessage("FixedUpdate");using var other=editor.DemandNativeEntities(new[]{ground},()=>new[]{ground},()=>true);
            pusher.enabled=false;Assert.That(pusher.GetComponent<SphereCollider>().enabled,Is.False);yield return NavigationLoadingDone();Assert.That(editor.Find(ground),Is.Not.Null);
        }
        [UnityTest]public IEnumerator ContactInputsPassiveGrabFactNeverStartsPhysicsOrLoadsObjects(){
            string id=PhysicsInputBall(new(4,.3f,4));int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
            for(int i=0;i<3;i++){Assert.That((string)ContactFact(id)["state"],Is.EqualTo("inactive"));yield return null;}
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(physics.Running,Is.False);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest]public IEnumerator ContactInputsGrabMustRecheckAfterPauseAndGeometryChange(){
            string id=PhysicsInputBall(new(4,2,4));ContactStart();var(item,_)=ContactGrab(id);var rigid=item.GetComponent<RigidRoomItem>();var gate=item.GetComponent<RoomGrabContactInputs>();
            ContactTarget(item,item.transform.position);Assert.That(gate.Ready,Is.True);
            physics.PausePhysics();physics.StartPhysics();Assert.That(gate.Ready,Is.False,"Restart must not reuse a pre-pause target");Assert.That(item.Grab.throwOnDetach,Is.False);
            ContactTarget(item,item.transform.position);Assert.That(gate.Ready,Is.True);Assert.That(gate.ThrowReady,Is.False);
            rigid.SetGeometryReady(false);rigid.SetGeometryReady(true);Assert.That(gate.Ready,Is.False,"Geometry replacement needs a fresh sweep");
            ContactTarget(item,item.transform.position);Assert.That(gate.Ready,Is.True);Assert.That(gate.ThrowReady,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator ContactInputsPusherPauseCancelsPendingOwnershipAndRechecksOnRestart(){
            string ground=AvailabilityTerrain();Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);ContactStart();var pusher=ContactPusher(new Vector3(4,.12f,4),out var hand);
            pusher.SendMessage("FixedUpdate");pusher.SendMessage("FixedUpdate");Assert.That((string)pusher.ObserveInputs()["state"],Is.EqualTo("pending"));
            physics.PausePhysics();Assert.That((string)pusher.ObserveInputs()["state"],Is.EqualTo("inactive"));Assert.That(pusher.GetComponent<SphereCollider>().enabled,Is.False);
            yield return NavigationLoadingDone();Assert.That(editor.Find(ground),Is.Null,"A stopped contact cannot finish its old load");
            hand.transform.position=new Vector3(-4,2,-4);physics.StartPhysics();pusher.SendMessage("FixedUpdate");Assert.That(pusher.GetComponent<SphereCollider>().enabled,Is.False);
            pusher.SendMessage("FixedUpdate");Assert.That(pusher.GetComponent<SphereCollider>().enabled,Is.True,pusher.ObserveInputs().ToString());
        }
        [UnityTest]public IEnumerator ContactInputsSameFrameTrackingCancelCannotCarryContactIntoNewSource(){
            ContactStart();var pusher=ContactPusher(new Vector3(-4,2,-4),out var hand);pusher.SendMessage("FixedUpdate");pusher.SendMessage("FixedUpdate");
            var sphere=pusher.GetComponent<SphereCollider>();Assert.That(sphere.enabled,Is.True);
            pusher.CancelTrackedInput();hand.gameObject.SetActive(false);hand.transform.position+=Vector3.right*.1f;hand.gameObject.SetActive(true);
            Assert.That(sphere.enabled,Is.False,"The old source must lose contact synchronously");pusher.SendMessage("FixedUpdate");Assert.That(sphere.enabled,Is.False,"Reacquisition starts with a fresh sample");
            pusher.SendMessage("FixedUpdate");Assert.That(sphere.enabled,Is.True);Assert.That(Vector3.Distance(pusher.GetComponent<Rigidbody>().position,hand.transform.position),Is.LessThan(.001f));yield return null;
        }
        [UnityTest]public IEnumerator ContactInputsInvalidBoundsRetirePendingDemand(){
            string ground=AvailabilityTerrain();Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);
            using var contact=new RoomContactInputs(editor,"book",()=>true);Assert.That(contact.Require(new Bounds(GroundPoint,Vector3.one*.3f),out error),Is.False);
            Assert.That((string)contact.Observe()["state"],Is.EqualTo("pending"));Assert.That(contact.Require(new Bounds(Vector3.zero,Vector3.zero),out error),Is.False);
            Assert.That((string)contact.Observe()["state"],Is.EqualTo("unavailable"));yield return NavigationLoadingDone();Assert.That(editor.Find(ground),Is.Null);
        }
        [Test]public void ContactInputsSweepIncludesRotationOffsetsAndBothGrabScales(){
            string id=PhysicsInputBall(new(4,2,4));var item=editor.Find(id);var sphere=item.GetComponent<SphereCollider>();sphere.center=Vector3.right*3;
            var target=new Pose(item.transform.position+Vector3.forward*2,Quaternion.Euler(0,180,0));Assert.That(RoomContactSweep.Item(item,target,item.transform.localScale*2,out var bounds),Is.True);
            var old=item.transform.TransformPoint(sphere.center);var final=target.position+target.rotation*Vector3.Scale(sphere.center,item.transform.lossyScale*2);
            Assert.That(bounds.Contains(old),Is.True);Assert.That(bounds.Contains(final),Is.True);Assert.That(bounds.Contains(target.position+Vector3.forward*Vector3.Distance(final,target.position)),Is.True);
            Assert.That(RoomContactSweep.Item(item,new Pose(new(float.NaN,0,0),Quaternion.identity),item.transform.localScale,out _),Is.False);
            Assert.That(RoomContactSweep.Item(item,target,new Vector3(0,1,1),out _),Is.False);
        }
    }
}
