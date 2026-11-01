// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        JObject AttachmentInputs(string id){Assert.That(BehaviourCatalog.TryRead("object.attachment.inputs",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);return JObject.FromObject(value.Value);}
        HeldRoomProp AttachmentStart(string id,RoomPlaybackClock clock,PropRelease release,out string error){
            var holder=editor.Find("book");holder.transform.position=new Vector3(4,.05f,4);
            var anchor=new RoomPropAnchor("object","book","",editor.ObjectRevision("book"),"");
            return HeldRoomProp.Begin(editor,new PropAttachment(id,"",PropHand.Right,release,new Vector3(0,0,.35f),Quaternion.identity,1),anchor,1,clock,out error);
        }
        [UnityTest]public IEnumerator AttachmentInputsWaitWithoutMovingAndFollowOnlyTheLatestAnchor(){
            string ground=AvailabilityTerrain(),id=PhysicsInputBall(new(4,.35f,4.35f));Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);
            ContactStart();float now=0;var clock=new RoomPlaybackClock(()=>now);var prop=AttachmentStart(id,clock,PropRelease.Return,out error);Assert.That(prop,Is.Not.Null,error);
            var item=editor.Find(id);var before=item.transform.position;Assert.That((string)AttachmentInputs(id)["state"],Is.EqualTo("pending"));
            now=10;prop.SendMessage("LateUpdate");Assert.That(clock.Elapsed,Is.Zero);Assert.That(item.transform.position,Is.EqualTo(before));Assert.That(prop.ContactReady,Is.False);
            editor.Find("book").transform.position+=Vector3.right*.2f+Vector3.up*.3f;yield return NavigationLoadingDone();prop.SendMessage("LateUpdate");
            Assert.That(prop.Error,Is.Null);Assert.That((bool)AttachmentInputs(id)["ready"],Is.True);Assert.That(Vector3.Distance(item.transform.position,before+Vector3.right*.2f),Is.LessThan(.001f));
            Assert.That(clock.Elapsed,Is.Zero,"Waiting did not consume playback");prop.End(true);Assert.That((string)AttachmentInputs(id)["state"],Is.EqualTo("inactive"));
        }
        [UnityTest]public IEnumerator AttachmentInputsCancellationRetiresItsLoadAndDoesNotResurrectTheProp(){
            string ground=AvailabilityTerrain(),id=PhysicsInputBall(new(4,.35f,4.35f));Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);ContactStart();
            var prop=AttachmentStart(id,new RoomPlaybackClock(),PropRelease.Return,out error);Assert.That(prop,Is.Not.Null,error);Assert.That((string)AttachmentInputs(id)["state"],Is.EqualTo("pending"));
            physics.PausePhysics();prop.End(true);yield return NavigationLoadingDone();Assert.That(editor.Find(ground),Is.Null);Assert.That(editor.Find(id).GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
        }
        [UnityTest]public IEnumerator AttachmentInputsTimeoutFailsWithoutReleasingOrKeepingPendingGeometry(){
            string ground=AvailabilityTerrain(),id=PhysicsInputBall(new(4,.35f,4.35f));Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);ContactStart();
            float now=0;var clock=new RoomPlaybackClock(()=>now);var prop=AttachmentStart(id,clock,PropRelease.Throw,out error);Assert.That(prop,Is.Not.Null,error);
            now=31;Assert.That(prop.Prepare(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(error,Does.Contain("budget"));Assert.That(prop.Released,Is.False);
            Assert.That((string)AttachmentInputs(id)["state"],Is.EqualTo("unavailable"));physics.PausePhysics();prop.End(true);yield return NavigationLoadingDone();Assert.That(editor.Find(ground),Is.Null);
        }
        [UnityTest]public IEnumerator AttachmentInputsRecoveredThrowNeedsFreshMotionInsteadOfTheOldTrajectory(){
            string ground=AvailabilityTerrain(),id=PhysicsInputBall(new(4,.35f,4.35f));Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);ContactStart();
            float now=0;var clock=new RoomPlaybackClock(()=>now);var prop=AttachmentStart(id,clock,PropRelease.Throw,out error);Assert.That(prop,Is.Not.Null,error);
            Assert.That((string)AttachmentInputs(id)["state"],Is.EqualTo("pending"));editor.Find("book").transform.position=new Vector3(-4,2,-4);
            // Move clear while the old request is still pending. Preparation now
            // checks this anchor; immediate completion has zero fresh history.
            Assert.That(prop.Prepare(out error),Is.EqualTo(RuleActionState.Ready),error);prop.Finish();
            Assert.That(prop.Released,Is.False);Assert.That(prop.Error,Does.Contain("more motion"));Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);
            physics.PausePhysics();prop.End(true);yield return NavigationLoadingDone();
        }
        [UnityTest]public IEnumerator AttachmentInputsFactIsPassiveAndAbsentAttachmentIsInactive(){
            string ground=AvailabilityTerrain(),id=PhysicsInputBall(new(4,.35f,4.35f));Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);int revision=editor.Revision;
            for(int i=0;i<3;i++){Assert.That((string)AttachmentInputs(id)["state"],Is.EqualTo("inactive"));yield return null;}
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(physics.Running,Is.False);
        }
    }
}
