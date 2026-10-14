// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class AvatarSpatialTests {
        RoomItem SolidRouteProp(Vector3 centre,Vector3 size){
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Route obstacle",centre,1,Color.white,out var id,out var error),Is.True,error);
            Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
            var item=editor.Find(id);var box=item.Grab.colliders.OfType<BoxCollider>().First(c=>c.enabled);box.center=Vector3.zero;box.size=size;Physics.SyncTransforms();return item;
        }
        IEnumerator SolidRoute(Vector3 from,Vector3 to,Action<Vector3[]> done,bool expected=true){
            bool found=false;Vector3[] points=Array.Empty<Vector3>();int ticks=0;
            do{found=navigation.FollowRoute(from,to,c=>!c.transform.IsChildOf(avatar.transform),out points,avoidSolids:true);ticks++;if(navigation.FollowRoutePending)yield return null;}
            while(navigation.FollowRoutePending&&ticks<240);
            Assert.That(navigation.FollowRoutePending,Is.False,"Solid route must remain bounded");Assert.That(found,Is.EqualTo(expected),navigation.FollowRouteStatus);done(points);
        }
        [UnityTest]public IEnumerator SolidRouteFiniteMaestroWalksAroundAPropWithEveryStepClear(){
            WalkingScene();var prop=SolidRouteProp(new(1.2f,.7f,0),new(.6f,1.4f,.8f));var collision=prop.Grab.colliders.First(c=>c.enabled);
            var walk=new GroundedTravel(editor,editor.Find("maestro"),new(2.4f,0,0),1.2f);var motor=new RoomGroundMotor();float lateral=0;
            try{Assert.That(walk.Begin(out var error),Is.True,error);var owned=editor.GetComponentsInChildren<RoomNavigation>().Single();yield return NavigationTestWait.Built(owned);uint built=owned.BuildRevision;var previous=avatar.transform.position;var state=RuleActionState.Preparing;
                for(int i=0;i<500&&state==RuleActionState.Preparing;i++){
                    state=walk.State(out error);var current=avatar.transform.position;lateral=Mathf.Max(lateral,Mathf.Abs(current.z));
                    Assert.That(motor.ClearExact(previous,current,.25f,1.7f,1<<collision.gameObject.layer,c=>c==collision),Is.True,"Every accepted step must clear the prop");previous=current;yield return null;
                }
                Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);Assert.That(walk.Complete(out error),Is.True,error);
                Assert.That(lateral,Is.GreaterThan(.6f));Assert.That(avatar.transform.position.x,Is.EqualTo(2.4f).Within(.02));Assert.That(owned.BuildRevision,Is.EqualTo(built));
            }finally{walk.Stop(false);}
        }
        [UnityTest]public IEnumerator SolidRouteCreatedActorUsesTheSameDetour(){
            WalkingScene();var item=WalkingBlock(new(-1,0,0));SolidRouteProp(new(0,.7f,0),new(.4f,1.4f,.8f));
            // The included Maestro starts at the same origin; move it clear as an independent obstacle.
            avatar.transform.position=new(0,0,4);Physics.SyncTransforms();var walk=new GroundedTravel(editor,item,new(1,0,0),1.2f);float lateral=0;
            try{Assert.That(walk.Begin(out var error),Is.True,error);var state=RuleActionState.Preparing;
                for(int i=0;i<500&&state==RuleActionState.Preparing;i++){state=walk.State(out error);lateral=Mathf.Max(lateral,Mathf.Abs(item.transform.position.z));yield return null;}
                Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);Assert.That(lateral,Is.GreaterThan(.45f));Assert.That(item.transform.position.x,Is.EqualTo(1).Within(.02));
            }finally{walk.Stop(false);}
        }
        [UnityTest]public IEnumerator SolidRouteFollowingRecoversFromAPropOnItsCurrentPath(){
            WalkingScene();viewer.transform.position=new(3.5f,1.6f,0);editor.SetAvatarMovement(.8f,1.2f);
            var prop=SolidRouteProp(new(1.6f,.7f,3),new(.6f,1.4f,.8f));Assert.That(motion.Begin("detour",AvatarSpatialMode.Follow,out var error),Is.True,error);
            try{
                for(int i=0;i<60&&avatar.transform.position.x<.4f;i++)yield return null;Assert.That(avatar.transform.position.x,Is.GreaterThan(.3f));
                prop.transform.position=new(1.6f,.7f,0);prop.GetComponent<RigidRoomItem>().Teleported();Physics.SyncTransforms();float lateral=0;
                // Saving placement broadcasts room changes. Use the same persisted preference as the user,
                // so this obstacle test cannot silently fall back to the saved 1.3 m default.
                editor.SaveNow();Assert.That(motion.Distance,Is.EqualTo(.8f));
                for(int i=0;i<550&&avatar.transform.position.x<2.6f;i++){yield return null;lateral=Mathf.Max(lateral,Mathf.Abs(avatar.transform.position.z));}
                Assert.That(lateral,Is.GreaterThan(.6f),motion.Status);Assert.That(avatar.transform.position.x,Is.GreaterThan(2.6f),motion.Status);
            }finally{motion.Stop();}
        }
        [UnityTest]public IEnumerator SolidRouteOccupiedDestinationFailsWithoutMovingIntoIt(){
            WalkingScene();SolidRouteProp(new(2,.7f,0),new(.6f,1.4f,.8f));var walk=new GroundedTravel(editor,editor.Find("maestro"),new(2,0,0),1.2f);
            try{Assert.That(walk.Begin(out var error),Is.True,error);var start=avatar.transform.position;var state=RuleActionState.Preparing;
                for(int i=0;i<240&&state==RuleActionState.Preparing;i++){state=walk.State(out error);yield return null;}
                Assert.That(state,Is.EqualTo(RuleActionState.Failed),error);Assert.That(error,Does.Contain("destination"));Assert.That(walk.Complete(out _),Is.False);Assert.That(avatar.transform.position,Is.EqualTo(start));
            }finally{walk.Stop(false);}
        }
        [UnityTest]public IEnumerator SolidRouteCombinesWaterAndSolidObstaclesWithoutRebaking(){
            WaterBasin();yield return NavigationBuilt();SolidRouteProp(new(.8f,.7f,-.85f),new(.8f,1.4f,.45f));uint built=navigation.BuildRevision;
            yield return SolidRoute(Vector3.zero,new(3,0,0),points=>{
                Assert.That(points.Length,Is.GreaterThan(2));var motor=new RoomGroundMotor();
                for(int i=1;i<points.Length;i++){
                    Assert.That(editor.Liquids.CheckTraversal(editor.Find("maestro"),points[i-1],points[i],.25f,1.7f,out var hit),Is.True,hit.Reason);
                    Assert.That(motor.ClearExact(points[i-1],points[i],.25f,1.7f,1<<RoomPhysicsLayers.Item,c=>!c.transform.IsChildOf(avatar.transform)),Is.True);
                }
            });Assert.That(navigation.BuildRevision,Is.EqualTo(built));
        }
        [UnityTest]public IEnumerator SolidRouteMovingObstacleInvalidatesPendingCandidates(){
            WalkingScene();var prop=SolidRouteProp(new(1,.7f,0),new(.5f,1.4f,.8f));Assert.That(navigation.Prepare(.25f,1.7f,out var error,editor.Find("maestro")),Is.True,error);yield return NavigationBuilt();
            Assert.That(navigation.FollowRoute(Vector3.zero,new(2,0,0),c=>!c.transform.IsChildOf(avatar.transform),out _,true),Is.False);Assert.That(navigation.FollowRoutePending,Is.True);
            prop.transform.position+=Vector3.forward*3;prop.GetComponent<RigidRoomItem>().Teleported();Physics.SyncTransforms();
            yield return SolidRoute(Vector3.zero,new(2,0,0),points=>Assert.That(points.All(p=>Mathf.Abs(p.z)<.05f),Is.True));
        }
        [UnityTest]public IEnumerator SolidRoutePendingDependenciesHaveCanonicalIdentityAndCancelReleasesThem(){
            WalkingScene();var prop=SolidRouteProp(new(1,.7f,0),new(.5f,1.4f,.8f));string id=editor.Identity(prop);
            Assert.That(navigation.Prepare(.25f,1.7f,out var error,editor.Find("maestro")),Is.True,error);yield return NavigationBuilt();Assert.That(NavigationRetained(id),Is.False);
            Assert.That(navigation.FollowRoute(Vector3.zero,new(2,0,0),c=>!c.transform.IsChildOf(avatar.transform),out _,true),Is.False);
            Assert.That(NavigationRetained(id),Is.True);Assert.That(NavigationRetained(id,RoomRetentionReason.WaterRoute),Is.False);
            navigation.CancelFollowRoute();Assert.That(navigation.FollowRoutePending,Is.False);Assert.That(NavigationRetained(id),Is.False);yield return null;
        }
        [UnityTest]public IEnumerator SolidRouteFiniteTravelReplansWhenAPropAppearsAfterMovementStarts(){
            WalkingScene();var prop=SolidRouteProp(new(1.5f,.7f,3),new(.5f,1.4f,.8f));var walk=new GroundedTravel(editor,editor.Find("maestro"),new(2.8f,0,0),1.2f);
            try{Assert.That(walk.Begin(out var error),Is.True,error);for(int i=0;i<45&&avatar.transform.position.x<.35f;i++){walk.Tick();yield return null;}
                Assert.That(avatar.transform.position.x,Is.GreaterThan(.3f));prop.transform.position=new(1.5f,.7f,0);prop.GetComponent<RigidRoomItem>().Teleported();Physics.SyncTransforms();
                yield return FinishWalking(walk,600);Assert.That(avatar.transform.position.x,Is.EqualTo(2.8f).Within(.02));
            }finally{walk.Stop(false);}
        }
    }
}
