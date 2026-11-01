// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class AvatarPropTests {
        string FittedInputGround(){
            world.PausePhysics();var at=ball.transform.position;Physics.SyncTransforms();float reach=ball.Grab.colliders[0].bounds.extents.magnitude;
            ball.transform.position+=Vector3.right*.5f;ball.GetComponent<RigidRoomItem>().Teleported();
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Fitted input ground",at+new Vector3(.25f,-reach*.8f-.1f,reach*.8f+.05f),1,Color.white,out var id,out var error),Is.True,error);
            var data=editor.Read(id);data.physics=ItemPhysics.Fixed;data.heightFields=new[]{new RoomHeightField{cells=4,width=.1f,depth=.1f,maxHeight=.5f,heights=Enumerable.Repeat(.1f,25).ToArray()}};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            string area=RegionFixture.Create(editor);
            Assert.That(editor.BindRegion(id,editor.ObjectRevision(id),area,editor.RegionRevision(area),out error),Is.True,error);
            Assert.That(RoomContactSweep.Item(ball,new Pose(at,ball.transform.rotation),ball.transform.localScale,out var sweep),Is.True);
            Assert.That(editor.SpatialInputTargets(sweep,sweep,true,editor.Identity(ball)),Does.Contain(id),"Fixture must intersect the admitted swept envelope: "+sweep);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);return id;
        }
        [UnityTest]public IEnumerator FittedPropPreparationFreezesRecordedRootUntilGeometryIsAvailable(){
            Configure(PropRelease.Return);string ground=FittedInputGround(); // Native retirement must drain before this required area can reload.
            Assert.That(editor.Find(ground),Is.Null,"Fixture must still be dormant before the action");
            var actions=new RoomRuleActions(editor,editor.GetComponent<AnimationWorkshop>());string owner="fitted-input-clock";
            Assert.That(actions.Start(owner,rules.Selected.SimpleSteps()[0],out _,out var error),Is.True,error);
            try {
                Assert.That(actions.State(owner,out error),Is.EqualTo(RuleActionState.Preparing),error+" "+ball.GetComponent<HeldRoomProp>().ObserveInputs());var before=avatar.transform.position;float remaining=actions.RemainingSeconds(owner).Value;
                actions.Tick();Assert.That(avatar.transform.position,Is.EqualTo(before));Assert.That(ball.GetComponent<HeldRoomProp>().ContactReady,Is.False);
                // Only the loader is allowed to change readiness; polling does not
                // fabricate successful motion or consume a frame of playback.
                Assert.That(actions.RemainingSeconds(owner).Value,Is.EqualTo(remaining));
                yield return Until(()=>!editor.NativeActivationPending);Assert.That(editor.Find(ground),Is.Not.Null);
                Assert.That(actions.State(owner,out error),Is.EqualTo(RuleActionState.Ready),error);yield return null;actions.Tick();
                Assert.That(avatar.transform.position.x,Is.GreaterThanOrEqualTo(before.x));Assert.That(ball.GetComponent<HeldRoomProp>().Error,Is.Null);
            } finally {actions.Stop(owner,true);}
            Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
        }
        [UnityTest]public IEnumerator FittedPropCancellationDoesNotKeepAnimatorPausedOrPublishAnOldLoad(){
            Configure(PropRelease.Return);string ground=FittedInputGround(); // Native retirement must drain before this required area can reload.
            Assert.That(editor.Find(ground),Is.Null,"Fixture must still be dormant before the action");
            var actions=new RoomRuleActions(editor,editor.GetComponent<AnimationWorkshop>());const string owner="cancel-fitted-input";
            Assert.That(actions.Start(owner,rules.Selected.SimpleSteps()[0],out _,out var error),Is.True,error);Assert.That(actions.State(owner,out error),Is.EqualTo(RuleActionState.Preparing),error+" "+ball.GetComponent<HeldRoomProp>().ObserveInputs());
            world.PausePhysics();actions.Stop(owner,true);yield return Until(()=>!editor.NativeActivationPending);Assert.That(editor.Find(ground),Is.Null);
            avatar.Gesture("Greeting");Assert.That(avatar.GetComponentInChildren<Animator>().speed,Is.EqualTo(1));Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
        }
    }
}
