// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        IEnumerator NavigationLoadingDone(){
            float deadline=Time.realtimeSinceStartup+10;
            while(editor.NativeActivationPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.NativeActivationPending,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator NavigationDemandAcquiresOffsetWaterInWorldCoordinatesAndLeavesDistantWaterDormant(){
            var(pool,_)=MediumFixture(.1f);physics.PausePhysics();var c=editor.Read(pool).containers[0];c.frame.position=new(3,.2f,0);
            yield return ContainerAction(new RoomAgentExecutor(editor),ContainerCall(pool,c));string area=NativeAreaFor(pool);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Distant water",new Vector3(-10,1,0),1,Color.white,out var far,out var error),Is.True,error);
            yield return ContainerAction(new RoomAgentExecutor(editor),ContainerCall(far,new RoomContainer{radius=.2f,height=.3f,capacityMl=1000,amountMl=800}));
            Assert.That(editor.RetireNativeArea(NativeAreaFor(far),out error),Is.True,error);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            root.transform.SetPositionAndRotation(new(3,2,5),Quaternion.Euler(0,30,0));root.transform.localScale=Vector3.one*1.5f;
            var body=new Bounds(root.transform.TransformPoint(new Vector3(7,1.4f,0)),Vector3.one*.3f);var support=body;
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            using var scope=new RoomSpatialInputs(editor,"maestro",()=>true,true);
            Assert.That(scope.Require(body,support,true,out error),Is.False);Assert.That(scope.Pending,Is.True);Assert.That(scope.Targets,Does.Contain(pool));Assert.That(scope.Targets,Does.Not.Contain(far));
            yield return NavigationLoadingDone();Assert.That(scope.Require(body,support,true,out error),Is.True,error);
            Assert.That(editor.Find(pool),Is.Not.Null);Assert.That(editor.Find(far),Is.Null);Assert.That(physics.Running,Is.False);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            Assert.That(editor.RetireNativeArea(area,out error),Is.False);scope.Dispose();Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
        }
        [UnityTest]public IEnumerator NavigationDemandCancelledFrontierCannotPublishLateOrMoveAnything(){
            string id=editor.Identity(block),area=NativeAreaFor(id);var body=new Bounds(block.transform.position,Vector3.one*2);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);string saved=JsonUtility.ToJson(editor.Snapshot());
            using var scope=new RoomSpatialInputs(editor,"maestro",()=>true,true);
            Assert.That(scope.Require(body,body,false,out error),Is.False);Assert.That(scope.Pending,Is.True);scope.Dispose();
            yield return NavigationLoadingDone();Assert.That(editor.Find(id),Is.Null);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            Assert.That(editor.ReadRetention().Reasons(id)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));
        }
        [UnityTest]public IEnumerator NavigationDemandTimeoutIsBoundedAndRequiresAFreshOwner(){
            string id=editor.Identity(block),area=NativeAreaFor(id);var body=new Bounds(block.transform.position,Vector3.one*2);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);float now=0;
            using var scope=new RoomSpatialInputs(editor,"maestro",()=>true,true,()=>now);
            Assert.That(scope.Require(body,body,false,out error),Is.False);now=31;
            Assert.That(scope.Require(body,body,false,out error),Is.False);Assert.That(error,Does.Contain("timed out"));yield return NavigationLoadingDone();
            Assert.That(scope.Require(body,body,false,out error),Is.False);Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest]public IEnumerator NavigationDemandLateCompletionCannotBypassThePreparationDeadline(){
            string id=editor.Identity(block);var body=new Bounds(block.transform.position,Vector3.one*2);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(id),out var error),Is.True,error);float now=0;
            using var scope=new RoomSpatialInputs(editor,"maestro",()=>true,false,()=>now);
            Assert.That(scope.Require(body,body,false,out error),Is.False);yield return NavigationLoadingDone();now=31;
            Assert.That(scope.Require(body,body,false,out error),Is.False);Assert.That(error,Does.Contain("timed out"));
            Assert.That(editor.ReadRetention().Reasons(id)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));
        }
        [UnityTest]public IEnumerator NavigationDemandChangingPendingTargetsDoesNotRestartItsDeadline(){
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Second input",new(10,1,0),1,Color.white,out var second,out var error),Is.True,error);
            string first=editor.Identity(block);var a=new Bounds(block.transform.position,Vector3.one);var b=new Bounds(new(10,1,0),Vector3.one);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(first),out error),Is.True,error);Assert.That(editor.RetireNativeArea(NativeAreaFor(second),out error),Is.True,error);float now=0;
            using var scope=new RoomSpatialInputs(editor,"maestro",()=>true,false,()=>now);
            Assert.That(scope.Require(a,a,false,out error),Is.False);now=20;Assert.That(scope.Require(b,b,false,out error),Is.False);now=31;
            Assert.That(scope.Require(b,b,false,out error),Is.False);Assert.That(error,Does.Contain("timed out"));yield return NavigationLoadingDone();
        }
        [UnityTest]public IEnumerator NavigationDemandStaleSourceReleasesOwnershipWithoutBlockingEdits(){
            string id=editor.Identity(block);var body=new Bounds(block.transform.position,Vector3.one*2);
            using var scope=new RoomSpatialInputs(editor,"maestro",()=>true,true);
            Assert.That(scope.Require(body,body,false,out var error),Is.True,error);
            Assert.That(editor.ReadRetention().Reasons(id)&RoomRetentionReason.Ownership,Is.Not.EqualTo(RoomRetentionReason.None));
            Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=2}),Is.True,editor.Status);
            Assert.That(scope.Require(body,body,false,out error),Is.False);Assert.That(error,Does.Contain("changed"));
            Assert.That(editor.ReadRetention().Reasons(id)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));yield return null;
        }
        [UnityTest]public IEnumerator NavigationDemandLiteralStepReplacesInterestWhileRouteAccumulatesIt(){
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"First obstacle",new(8,1,0),1,Color.white,out var a,out var error),Is.True,error);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Second obstacle",new(-8,1,0),1,Color.white,out var b,out error),Is.True,error);
            NativeAreaFor(a);NativeAreaFor(b);
            var first=new Bounds(new(8,1,0),Vector3.one);var second=new Bounds(new(-8,1,0),Vector3.one);
            using var route=new RoomSpatialInputs(editor,"maestro",()=>true,true);using var step=new RoomSpatialInputs(editor,"maestro",()=>true,false);
            Assert.That(route.Require(first,first,false,out error),Is.True,error);Assert.That(step.Require(first,first,false,out error),Is.True,error);
            Assert.That(route.Require(second,second,false,out error),Is.True,error);Assert.That(step.Require(second,second,false,out error),Is.True,error);
            Assert.That(route.Targets,Does.Contain(a));Assert.That(route.Targets,Does.Contain(b));Assert.That(step.Targets,Does.Not.Contain(a));Assert.That(step.Targets,Does.Contain(b));
            route.Dispose();Assert.That(editor.ReadRetention().Reasons(a)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));
            Assert.That(editor.ReadRetention().Reasons(b)&RoomRetentionReason.Ownership,Is.Not.EqualTo(RoomRetentionReason.None));yield return null;
        }
    }
    public sealed partial class AvatarSpatialTests {
        [UnityTest]public IEnumerator NavigationDemandMissingUpperGroundCannotBorrowTheLowerScannedFloor(){
            Surface(new(0,-.1f,0),new(12,.2f,12));Tutor();var(id,_)=NavigationGround(Vector3.zero);Ready();
            Assert.That(navigation.Prepare(.25f,1.7f,out var error,editor.Find("maestro")),Is.True,error);
            Assert.That(navigation.Sample(new(0,.2f,0),.25f,out _),Is.True);editor.Find(id).gameObject.SetActive(false);
            Assert.That(navigation.Sample(new(0,.2f,0),.25f,out _),Is.False,"Missing upper ground cannot silently become the lower floor");
            Assert.That(navigation.Traverse(new(0,.2f,0),new(.02f,.2f,0),_=>false,out _,out error),Is.False);
            Assert.That(editor.NativeActivationPending,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator NavigationDemandCompletedRouteRetainsWaterAndCancelReleasesOnlyItsInput(){
            string pool=WaterBasin();string saved=JsonUtility.ToJson(editor.Snapshot());
            yield return WaterRoute(Vector3.zero,new(3,0,0),p=>Assert.That(p.Length,Is.GreaterThan(2)));
            Assert.That(NavigationRetained(pool),Is.True);Assert.That(NavigationRetained(pool,RoomRetentionReason.Ownership),Is.True);
            using var other=editor.DemandNativeEntities(new[]{pool},()=>new[]{pool},()=>true);
            navigation.CancelFollowRoute();Assert.That(NavigationRetained(pool),Is.False);Assert.That(NavigationRetained(pool,RoomRetentionReason.Ownership),Is.True);
            other.Dispose();Assert.That(NavigationRetained(pool,RoomRetentionReason.Ownership),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest]public IEnumerator NavigationDemandLiteralMovementReleasesInputOnNeutralStopAndDisable(){
            Surface(new(0,-.1f,0),new(12,.2f,12));Tutor();
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Raised-step obstacle",new(0,1.86f,0),.4f,Color.white,out var id,out var error),Is.True,error);
            Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);Ready();
            Assert.That(motion.Begin("manual",AvatarSpatialMode.Manual,out error),Is.True,error);
            for(int i=0;i<3;i++){motion.ManualDirection("manual",Vector3.right);yield return null;}
            Assert.That(NavigationRetained(id,RoomRetentionReason.Ownership),Is.True,motion.Status);
            motion.ManualDirection("manual",Vector3.zero);Assert.That(NavigationRetained(id,RoomRetentionReason.Ownership),Is.False);
            for(int i=0;i<3;i++){motion.ManualDirection("manual",Vector3.right);yield return null;}Assert.That(NavigationRetained(id,RoomRetentionReason.Ownership),Is.True,motion.Status);
            motion.Stop();Assert.That(NavigationRetained(id,RoomRetentionReason.Ownership),Is.False);
            Assert.That(navigation.Ready,Is.False,"A stopped owner must not leave a configured map");
            Assert.That(navigation.Prepare(.25f,1.7f,out error,editor.Find("maestro")),Is.True,error);
            Assert.That(navigation.Traverse(avatar.transform.position,avatar.transform.position+Vector3.right*.02f,_=>false,out _,out error),Is.True,error);
            Assert.That(NavigationRetained(id,RoomRetentionReason.Ownership),Is.True,motion.Status);navigation.enabled=false;Assert.That(NavigationRetained(id,RoomRetentionReason.Ownership),Is.False);
        }
        [UnityTest]public IEnumerator NavigationDemandAbsentOffsetCavityRefusesRouteWithoutInventingAWaterDetour(){
            string pool=WaterBasin();yield return NavigationBuilt();var c=editor.Read(pool).containers[0];c.frame.position=new(-4,.2f,0);
            Assert.That(editor.EditContainer(pool,editor.ObjectRevision(pool),c,out var error),Is.True,error);
            editor.Find(pool).transform.position=new(4.8f,-.2f,0);editor.Find(pool).gameObject.SetActive(false);
            Assert.That(navigation.FollowRoute(Vector3.zero,new(3,0,0),_=>false,out _),Is.False);
            Assert.That(navigation.FollowRoutePending,Is.False);Assert.That(navigation.FollowRouteStatus,Does.Contain("unavailable"));
            Assert.That(NavigationRetained(pool,RoomRetentionReason.WaterRoute),Is.False);Assert.That(editor.NativeActivationPending,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator NavigationDemandReleasedOwnerCannotQueryADestroyedWorld(){
            var local=new GameObject("Retired query world");var retired=local.AddComponent<RoomEditor>();
            using var inputs=new RoomSpatialInputs(retired,"maestro",()=>true,false);inputs.Dispose();
            UnityEngine.Object.Destroy(local);yield return null;string error=null;bool ready=true;
            Assert.DoesNotThrow(()=>ready=inputs.Require(new Bounds(Vector3.zero,Vector3.one),new Bounds(Vector3.zero,Vector3.one),false,out error));
            Assert.That(ready,Is.False);Assert.That(error,Does.Contain("no longer owned"));
        }
    }
}
