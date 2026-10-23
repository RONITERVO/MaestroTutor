// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [UnityTest]public IEnumerator LocomotionLifetimeOrdinaryPhysicsRefreshKeepsTheAcceptedOwner(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);using var owner=new RoomLocomotionOwner(editor,actor);
            var args=SwimArgs(id,pool,new(4.2f,1.45f,0));var first=new AquaticMovement(editor,actor,args,owner);
            var next=new AquaticMovement(editor,actor,args,owner);
            try{
                Assert.That(first.Begin(args,out var error),Is.True,error);yield return SwimmingStarted(first,actor);
                first.Stop(false);physics.StartPhysics();physics.SetSurfaces(true,"Still aligned");
                Assert.That(owner.Validate(out error),Is.True,error);Assert.That(next.Begin(args,out error),Is.True,error);
                yield return FinishSwimming(next,actor);
            }finally{first.Stop(false);next.Stop(false);}
        }
        [UnityTest]public IEnumerator LocomotionLifetimePauseAndResumeInOneFrameCannotReviveBetweenPhases(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);using var owner=new RoomLocomotionOwner(editor,actor);
            var args=SwimArgs(id,pool,new(4.2f,1.45f,0));var first=new AquaticMovement(editor,actor,args,owner);
            var next=new AquaticMovement(editor,actor,args,owner);
            try{
                Assert.That(first.Begin(args,out var error),Is.True,error);yield return SwimmingStarted(first,actor);
                first.Stop(false);var held=actor.transform.position;
                physics.PausePhysics();physics.StartPhysics();
                Assert.That(next.Begin(args,out error),Is.False,"An interruption between ticks must not disappear when physics resumes");
                Assert.That(error,Does.Contain("physics"));Assert.That(actor.transform.position,Is.EqualTo(held));
            }finally{first.Stop(false);next.Stop(false);}
        }
        [UnityTest]public IEnumerator LocomotionLifetimeSurfaceCompletionRechecksWaterInTheArrivalFrame(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SurfaceArgs(id,pool,4);
            var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);
                float deadline=Time.realtimeSinceStartup+6;RuleActionState state;
                while((state=swim.State(out error))==RuleActionState.Preparing&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);
                var held=actor.transform.position;PublishAquaticQuantity(pool,500000);
                Assert.That(swim.Complete(out error),Is.False,"Arrival at the old waterline cannot complete at the new waterline");
                Assert.That(actor.transform.position,Is.EqualTo(held),"Completion must not apply an extra movement step");
                yield return null;
                Assert.That(swim.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                yield return FinishSurfaceTravel(swim,actor);
                Assert.That(actor.transform.position.y,Is.GreaterThan(held.y+.08f));
            }finally{swim.Stop(false);}
        }
        [UnityTest]public IEnumerator LocomotionLifetimeAquaticCompletionRechecksAnEditInTheArrivalFrame(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SwimArgs(id,pool,actor.transform.localPosition);
            var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);
                float deadline=Time.realtimeSinceStartup+6;RuleActionState state;
                while((state=swim.State(out error))==RuleActionState.Preparing&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);
                var edited=actor.transform.localPosition+Vector3.forward*.03f;actor.transform.localPosition=edited;
                Assert.That(swim.Complete(out error),Is.False,"Arrival cached this frame cannot authorize completion after a user edit");
                Assert.That(error,Does.Contain("actor changed"));Assert.That(actor.transform.localPosition,Is.EqualTo(edited));
            }finally{swim.Stop(false);}
        }
        [UnityTest]public IEnumerator LocomotionLifetimeGroundToWaterPreparationKeepsOnePhysicsOwner(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);
            Assert.That(editor.SetItemPhysics(id,new(){mode="solid",shape="box",mass=1}),Is.True);
            var support=GameObject.CreatePrimitive(PrimitiveType.Cube);
            support.transform.SetParent(root.transform,false);support.transform.position=new(4,1.2f,0);
            support.transform.localScale=new(3,.2f,3);support.layer=RoomPhysicsLayers.Environment;
            support.AddComponent<RoomWalkableSurface>().Publish(support.GetComponent<Collider>());Physics.SyncTransforms();
            var body=actor.GetComponent<Rigidbody>();var rigid=actor.GetComponent<RigidRoomItem>();
            using var owner=new RoomLocomotionOwner(editor,actor);
            var walk=new GroundedMovement(editor,actor,new(4,1.3f,0),.5f,owner:owner);
            var args=SwimArgs(id,pool,new(4.15f,1.45f,.1f));
            var swim=new AquaticMovement(editor,actor,args,owner);
            var unrelated=new object();var extra=new[]{new object(),new object(),new object()};
            try{
                foreach(var slot in extra)Assert.That(RoomLocomotionBudget.Acquire(slot,out _),Is.True);
                Assert.That(walk.Begin(out var error),Is.True,error);
                Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(4));
                Assert.That(rigid.AnimationOwned,Is.True);Assert.That(body.isKinematic,Is.True);
                Assert.That(swim.Begin(args,out _),Is.False,"A second solver cannot advance before the first detaches");
                walk.Stop(false);var held=actor.transform.position;
                Assert.That(RoomLocomotionBudget.Acquire(unrelated,out _),Is.False,"A handoff cannot temporarily free capacity");
                for(int i=0;i<3;i++){
                    yield return new WaitForFixedUpdate();
                    Assert.That(owner.Validate(out error),Is.True,error);
                    Assert.That(rigid.AnimationOwned,Is.True);Assert.That(body.isKinematic,Is.True);
                    Assert.That(actor.transform.position,Is.EqualTo(held),"Gravity must stay owned while the next phase prepares");
                }
                Assert.That(AquaticMovement.CanBegin(editor,actor,args,out _),Is.False,"Independent actions cannot borrow the handoff");
                Assert.That(swim.Begin(args,out error),Is.True,error);
                Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(4));
                yield return FinishSwimming(swim,actor);
                swim.Stop(false);Assert.That(body.isKinematic,Is.True);Assert.That(rigid.AnimationOwned,Is.True);
                owner.Dispose();Assert.That(rigid.AnimationOwned,Is.False);Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(3));
                Assert.That(Vector3.Distance(editor.Read(id).position,new Vector3(4.15f,1.45f,.1f)),Is.LessThan(.0051f));
            }finally{walk.Stop(false);swim.Stop(false);owner.Dispose();foreach(var slot in extra)RoomLocomotionBudget.Release(slot);RoomLocomotionBudget.Release(unrelated);Object.Destroy(support);}
            Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator LocomotionLifetimeSurfacePhaseRetainsTheAcceptedSubmergedPose(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);using var owner=new RoomLocomotionOwner(editor,actor);
            var submergedArgs=SwimArgs(id,pool,new(4.1f,1.45f,0));var surfaceArgs=SurfaceArgs(id,pool,4.2f);
            var submerged=new AquaticMovement(editor,actor,submergedArgs,owner);var surface=new AquaticMovement(editor,actor,surfaceArgs,owner);
            try{
                Assert.That(submerged.Begin(submergedArgs,out var error),Is.True,error);yield return FinishSwimming(submerged,actor);
                submerged.Stop(false);var held=actor.transform.position;
                Assert.That(surface.Begin(surfaceArgs,out error),Is.True,error);Assert.That(actor.transform.position,Is.EqualTo(held));
                Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(1));yield return FinishSurfaceTravel(surface,actor);
                surface.Stop(false);Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(1));
            }finally{submerged.Stop(false);surface.Stop(false);}
            owner.Dispose();Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator LocomotionLifetimeEditAtHandoffCannotBeForgottenByRemembering(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);using var owner=new RoomLocomotionOwner(editor,actor);
            var args=SwimArgs(id,pool,new(4.2f,1.45f,0));var first=new AquaticMovement(editor,actor,args,owner);
            var next=new AquaticMovement(editor,actor,args,owner);
            try{
                Assert.That(first.Begin(args,out var error),Is.True,error);yield return SwimmingStarted(first,actor);
                var before=actor.transform.localPosition;actor.transform.localPosition+=Vector3.forward*.02f;
                first.Stop(false);actor.transform.localPosition=before;
                Assert.That(next.Begin(args,out error),Is.False);Assert.That(error,Does.Contain("actor changed"));
                Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(1));
                Assert.That(next.Complete(out _),Is.False);
            }finally{first.Stop(false);next.Stop(false);}
        }
        [UnityTest]public IEnumerator LocomotionLifetimePauseBetweenPhasesNeverRevivesOnResume(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);using var owner=new RoomLocomotionOwner(editor,actor);
            var args=SwimArgs(id,pool,new(4.2f,1.45f,0));var first=new AquaticMovement(editor,actor,args,owner);
            var next=new AquaticMovement(editor,actor,args,owner);
            try{
                Assert.That(first.Begin(args,out var error),Is.True,error);yield return SwimmingStarted(first,actor);
                first.Stop(false);var held=actor.transform.position;physics.PausePhysics();
                Assert.That(owner.Validate(out error),Is.False);physics.StartPhysics();
                Assert.That(next.Begin(args,out error),Is.False);Assert.That(error,Does.Contain("physics"));
                for(int i=0;i<3;i++){yield return new WaitForFixedUpdate();Assert.That(actor.transform.position,Is.EqualTo(held));}
            }finally{first.Stop(false);next.Stop(false);}
        }
        [UnityTest]public IEnumerator LocomotionLifetimeDisposalCannotResumeAnAttachedPhase(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);using var owner=new RoomLocomotionOwner(editor,actor);
            var args=SwimArgs(id,pool,new(4.2f,1.45f,0));var first=new AquaticMovement(editor,actor,args,owner);
            try{
                Assert.That(first.Begin(args,out var error),Is.True,error);yield return SwimmingStarted(first,actor);
                owner.Dispose();var held=actor.transform.position;yield return null;
                Assert.That(first.State(out error),Is.EqualTo(RuleActionState.Failed));
                Assert.That(first.Complete(out _),Is.False);Assert.That(actor.transform.position,Is.EqualTo(held));
                Assert.That(RoomLocomotionBudget.Count,Is.Zero);Assert.That(actor.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
            }finally{first.Stop(false);}
        }
        [UnityTest]public IEnumerator LocomotionLifetimeCannotChangeActorOrEscalatePoseOwnership(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);using var owner=new RoomLocomotionOwner(editor,actor);
            var token=new object();
            Assert.That(owner.Attach(token,out var error),Is.True,error);owner.Detach(token);
            Assert.That(RoomLocomotionOwner.CanBegin(editor,block,owner,false,out _),Is.False);
            Assert.That(RoomLocomotionOwner.CanBegin(editor,actor,owner,true,out _),Is.False);
            Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(1));yield return null;
        }
    }
    public sealed partial class AvatarSpatialTests {
        [UnityTest]public IEnumerator LocomotionLifetimeWalkingCompletionRechecksGroundInTheArrivalFrame(){
            WalkingScene();var actor=editor.Find("maestro");var walk=new GroundedTravel(editor,actor,Vector3.zero,.5f);
            try{
                Assert.That(walk.Begin(out var error),Is.True,error);
                float deadline=Time.realtimeSinceStartup+6;RuleActionState state;
                while((state=walk.State(out error))==RuleActionState.Preparing&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);var held=actor.transform.position;
                foreach(var c in physicalRoot.GetComponentsInChildren<Collider>())if(c.gameObject.layer==RoomPhysicsLayers.Scanned)c.enabled=false;
                Physics.SyncTransforms();
                Assert.That(walk.Complete(out error),Is.False,"Arrival cached this frame cannot borrow a removed floor");
                Assert.That(error,Is.Not.Null.And.Not.Empty);Assert.That(actor.transform.position,Is.EqualTo(held));
            }finally{walk.Stop(false);}
        }
        [UnityTest]public IEnumerator LocomotionLifetimeWalkingCompletionRechecksAnEditInTheArrivalFrame(){
            WalkingScene();var actor=editor.Find("maestro");var walk=new GroundedTravel(editor,actor,Vector3.zero,.5f);
            try{
                Assert.That(walk.Begin(out var error),Is.True,error);
                float deadline=Time.realtimeSinceStartup+6;RuleActionState state;
                while((state=walk.State(out error))==RuleActionState.Preparing&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);
                var edited=actor.transform.localPosition+Vector3.forward*.03f;actor.transform.localPosition=edited;
                Assert.That(walk.Complete(out error),Is.False,"Arrival cached this frame cannot authorize completion after a user edit");
                Assert.That(error,Does.Contain("actor changed"));Assert.That(actor.transform.localPosition,Is.EqualTo(edited));
            }finally{walk.Stop(false);}
        }
        [UnityTest]public IEnumerator LocomotionLifetimeFollowingCompletionRechecksAnEditInTheReadyFrame(){
            WalkingScene();var actor=editor.Find("maestro");var follow=new GroundedFollowing(editor,actor,null,1,.5f,10);
            try{
                Assert.That(follow.Begin(out var error),Is.True,error);yield return null;
                Assert.That(follow.State(out error),Is.EqualTo(RuleActionState.Ready),error);
                var edited=actor.transform.localPosition+Vector3.forward*.03f;actor.transform.localPosition=edited;
                Assert.That(follow.Complete(out error),Is.False,"A cached Ready state cannot authorize completion after a user edit");
                Assert.That(error,Does.Contain("actor changed"));Assert.That(actor.transform.localPosition,Is.EqualTo(edited));
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator LocomotionLifetimeDisposesTheOldPhaseBeforeAReplacementCanOwnThePose(){
            WalkingScene();var actor=editor.Find("maestro");using var oldOwner=new RoomLocomotionOwner(editor,actor);
            var oldWalk=new GroundedMovement(editor,actor,new(.3f,0,0),1.2f,owner:oldOwner);
            using var nextOwner=new RoomLocomotionOwner(editor,actor,fullPose:true);var token=new object();
            try{
                Assert.That(oldWalk.Begin(out var error),Is.True,error);oldWalk.Tick();yield return null;
                oldOwner.Dispose();
                Assert.That(nextOwner.Attach(token,out error),Is.True,error);
                var leg=avatar.PoseRig.CanonicalBone(PoseJoint.LeftUpperLeg);var authored=Quaternion.Euler(23,31,17);
                leg.localRotation=authored;oldWalk.Stop(false);oldWalk.Tick();
                yield return null;yield return null;
                Assert.That(Quaternion.Angle(leg.localRotation,authored),Is.LessThan(.1f),
                    "The disposed solver must not restore idle or an old pose over a replacement");
                Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(1));
            }finally{oldWalk.Stop(false);nextOwner.Detach(token);}
        }
        [UnityTest]public IEnumerator LocomotionLifetimeWaterPoseIsNotOverwrittenByThePreviousWalkingAnimator(){
            WalkingScene();var actor=editor.Find("maestro");using var owner=new RoomLocomotionOwner(editor,actor,fullPose:true);
            var walk=new GroundedMovement(editor,actor,new(.3f,0,0),1.2f,owner:owner);var waterPhase=new object();
            try{
                Assert.That(walk.Begin(out var error),Is.True,error);
                for(int i=0;i<240&&!walk.Arrived&&!walk.Failed&&!walk.Blocked;i++){walk.Tick();yield return null;}
                Assert.That(walk.Arrived,Is.True,walk.Reason);walk.Stop(false);
                Assert.That(owner.Attach(waterPhase,out error),Is.True,error);
                var leg=avatar.PoseRig.CanonicalBone(PoseJoint.LeftUpperLeg);
                var authored=Quaternion.Euler(34,19,11);leg.localRotation=authored;
                yield return null;yield return null;
                Assert.That(Quaternion.Angle(leg.localRotation,authored),Is.LessThan(.1f),
                    "An attached water pose must survive the previous walking animator's next frames");
                Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(1));
            }finally{walk.Stop(false);owner.Detach(waterPhase);}
        }
        [UnityTest]public IEnumerator LocomotionLifetimeWalkingPhasesKeepMaestroExclusiveWithoutARigidWrapper(){
            WalkingScene();var actor=editor.Find("maestro");using var owner=new RoomLocomotionOwner(editor,actor);
            var first=new GroundedMovement(editor,actor,new(.3f,0,0),1.2f,owner:owner);
            var next=new GroundedMovement(editor,actor,new(.6f,0,0),1.2f,owner:owner);
            try{
                Assert.That(first.Begin(out var error),Is.True,error);
                for(int i=0;i<240&&!first.Arrived&&!first.Failed&&!first.Blocked;i++){first.Tick();yield return null;}
                Assert.That(first.Arrived,Is.True,first.Reason);first.Stop(false);
                Assert.That(GroundedMovement.CanBegin(editor,actor,out _),Is.False);
                Assert.That(next.Begin(out error),Is.True,error);Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(1));
                for(int i=0;i<240&&!next.Arrived&&!next.Failed&&!next.Blocked;i++){next.Tick();yield return null;}
                Assert.That(next.Arrived,Is.True,next.Reason);Assert.That(actor.transform.position.x,Is.EqualTo(.6f).Within(.0051f));
            }finally{first.Stop(false);next.Stop(false);}
            owner.Dispose();Assert.That(GroundedMovement.CanBegin(editor,actor,out var reason),Is.True,reason);
        }
    }
}
