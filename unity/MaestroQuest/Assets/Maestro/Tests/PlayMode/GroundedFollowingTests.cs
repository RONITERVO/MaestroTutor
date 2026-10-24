// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class AvatarSpatialTests {
        JObject FollowingCall(string target,string leader=null,float seconds=10)=>new(){["id"]="object.follow",["version"]=1,["arguments"]=new JObject{["target"]=target,["leader"]=leader==null?new JObject{["kind"]="user"}:new JObject{["kind"]="object",["objectId"]=leader},["distance"]=1,["speed"]=1.2,["seconds"]=seconds}};
        JObject WalkingStatus(string id){Assert.That(BehaviourCatalog.TryRead("object.navigation",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);return JObject.FromObject(fact.Value);}
        IEnumerator FollowingFrames(GroundedFollowing follow,int frames){for(int i=0;i<frames;i++){Assert.That(follow.State(out var error),Is.EqualTo(RuleActionState.Ready),error);yield return null;}}
        static float Horizontal(Vector3 a,Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;
        [UnityTest]public IEnumerator GroundedFollowingCreatedObjectTracksAnIndependentMovingLeader(){
            WalkingScene();avatar.transform.position=new(0,0,3);var actor=WalkingBlock(new(-2,0,-2));var leader=WalkingBlock(new(1,0,-2));string id=editor.Identity(actor);
            var follow=new GroundedFollowing(editor,actor,editor.Identity(leader),1,1.2f,30);
            try{Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowingFrames(follow,300);
                Assert.That(Horizontal(actor.transform.position,leader.transform.position),Is.InRange(.96f,1.1f));Assert.That((string)WalkingStatus(id)["state"],Is.EqualTo("keepingDistance"));
                leader.transform.position+=new Vector3(1.5f,0,1);leader.GetComponent<RigidRoomItem>().Teleported();Physics.SyncTransforms();yield return FollowingFrames(follow,300);
                Assert.That(Horizontal(actor.transform.position,leader.transform.position),Is.InRange(.96f,1.1f));Assert.That(actor.transform.position.z,Is.GreaterThan(-1.7f));
                Assert.That((float)follow.Result["distance"],Is.GreaterThan(3));
            }finally{follow.Stop(false);}Assert.That((bool)WalkingStatus(id)["active"],Is.False);
        }
        [UnityTest]public IEnumerator GroundedFollowingCreatedObjectTracksSharedViewerAndKeepsItsDistance(){
            WalkingScene(true);avatar.transform.position=new(0,0,3);var actor=WalkingBlock(new(-2,0,-2));viewer.transform.position=new(1,1.6f,-2);
            var follow=new GroundedFollowing(editor,actor,null,1,1.2f,30);
            try{Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowingFrames(follow,300);Assert.That(Horizontal(actor.transform.position,viewer.transform.position),Is.InRange(.96f,1.1f));
                viewer.transform.position+=Vector3.right;yield return FollowingFrames(follow,220);Assert.That(actor.transform.position.x,Is.GreaterThan(.9f));
                var placed=actor.transform.position;follow.Stop(false);yield return null;follow.Tick();Assert.That(actor.transform.position,Is.EqualTo(placed));
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedFollowingWaitsAtOccupiedGoalThenRecoversWithoutJumping(){
            WalkingScene();avatar.transform.position=new(0,0,3);var actor=WalkingBlock(new(-2,0,-2));var leader=WalkingBlock(new(2,0,-2));var prop=SolidRouteProp(new(1,.7f,-2),new(.6f,1.4f,.8f));
            var follow=new GroundedFollowing(editor,actor,editor.Identity(leader),1,1.2f,30);var before=actor.transform.position;
            try{Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowingFrames(follow,40);Assert.That(actor.transform.position,Is.EqualTo(before));Assert.That((string)follow.Result["state"],Is.EqualTo("blocked"));
                prop.transform.position+=Vector3.forward*4;prop.GetComponent<RigidRoomItem>().Teleported();Physics.SyncTransforms();
                // The retry cadence uses unscaled time, independently of this suite's captured simulation delta.
                yield return new WaitForSecondsRealtime(.4f);
                var previous=actor.transform.position;for(int i=0;i<350;i++){follow.Tick();var current=actor.transform.position;Assert.That(Vector3.Distance(previous,current),Is.LessThan(.061f));previous=current;yield return null;}
                Assert.That(actor.transform.position.x,Is.GreaterThan(.9f));Assert.That((string)follow.Result["state"],Is.EqualTo("keepingDistance"));
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedFollowingUsesSharedSolidDetour(){
            WalkingScene();avatar.transform.position=new(0,0,3);var actor=WalkingBlock(new(-2,0,-2));var leader=WalkingBlock(new(2,0,-2));SolidRouteProp(new(-.5f,.7f,-2),new(.5f,1.4f,.8f));
            var follow=new GroundedFollowing(editor,actor,editor.Identity(leader),1,1.2f,30);float lateral=0;
            try{Assert.That(follow.Begin(out var error),Is.True,error);for(int i=0;i<600;i++){Assert.That(follow.State(out error),Is.EqualTo(RuleActionState.Ready),error);lateral=Mathf.Max(lateral,Mathf.Abs(actor.transform.position.z+2));yield return null;}
                Assert.That(lateral,Is.GreaterThan(.5f));Assert.That(Horizontal(actor.transform.position,leader.transform.position),Is.InRange(.96f,1.1f));
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedFollowingTrackingLossWhileKeepingDistanceIsTerminal(){
            WalkingScene();var actor=WalkingBlock(new(1,0,0));viewer.transform.position=new(1.4f,1.6f,0);editor.ConfigureObservationTracking(()=>tracked);
            var follow=new GroundedFollowing(editor,actor,null,1,1.2f,10);
            try{Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowingFrames(follow,4);var before=actor.transform.position;tracked=false;
                Assert.That(follow.State(out error),Is.EqualTo(RuleActionState.Failed));tracked=true;viewer.transform.position+=Vector3.right*2;for(int i=0;i<12;i++){follow.Tick();yield return null;}
                Assert.That(actor.transform.position,Is.EqualTo(before));Assert.That(follow.Complete(out _),Is.False);Assert.That((string)WalkingStatus(editor.Identity(actor))["state"],Is.EqualTo("failed"));
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedFollowingDoesNotSwitchToAnUnavailableLeader(){
            WalkingScene();avatar.transform.position=new(0,0,3);var actor=WalkingBlock(new(-2,0,-2));var leader=WalkingBlock(new(1,0,-2));
            var follow=new GroundedFollowing(editor,actor,editor.Identity(leader),1,1.2f,10);
            try{Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowingFrames(follow,4);leader.gameObject.SetActive(false);var before=actor.transform.position;
                Assert.That(follow.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(error,Does.Contain("leader"));leader.gameObject.SetActive(true);yield return null;follow.Tick();Assert.That(actor.transform.position,Is.EqualTo(before));
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedFollowingTimedBlockedCompletionDoesNotClaimArrival(){
            WalkingScene();avatar.transform.position=new(0,0,3);var actor=WalkingBlock(new(-2,0,-2));var leader=WalkingBlock(new(2,0,-2));SolidRouteProp(new(1,.7f,-2),new(.6f,1.4f,.8f));
            var actions=new RoomRuleActions(editor,authoring);var scheduler=new RuleScheduler(actions);float start=Time.unscaledTime;var call=FollowingCall(editor.Identity(actor),editor.Identity(leader),1);
            Assert.That(scheduler.Invoke(call,start,out var run,out var error),Is.True,error);
            try{for(int i=0;i<20;i++){actions.Tick();scheduler.Tick(start+i*.06f);yield return null;}
                var receipt=scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());Assert.That((string)receipt["output"]["state"],Is.EqualTo("blocked"));Assert.That((float)receipt["output"]["distance"],Is.Zero);Assert.That((bool)WalkingStatus(editor.Identity(actor))["active"],Is.False);
            }finally{scheduler.Suspend(true);actions.Stop(run,false);}
        }
        [UnityTest]public IEnumerator GroundedFollowingObservesLeaderGrabButFollowerGrabCancels(){
            WalkingScene();avatar.transform.position=new(0,0,3);var actor=WalkingBlock(new(-2,0,-2));var leader=WalkingBlock(new(1,0,-2));string id=editor.Identity(actor),leaderId=editor.Identity(leader);var actions=new RoomRuleActions(editor,authoring);var scheduler=new RuleScheduler(actions);
            Assert.That(scheduler.Invoke(FollowingCall(id,leaderId),Time.unscaledTime,out var run,out var error),Is.True,error);
            try{scheduler.GrabTarget(leaderId);Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("running"));Assert.That(editor.Ownership.Covers(run,new[]{new BehaviourCatalog.Claim(leaderId,"locomotion")}),Is.False);
                for(int i=0;i<20;i++){actions.Tick();scheduler.Tick(Time.unscaledTime);yield return null;}
                Assert.That(actor.transform.position.x,Is.GreaterThan(-1.9f));scheduler.GrabTarget(id);Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));Assert.That((bool)WalkingStatus(id)["active"],Is.False);
            }finally{scheduler.Suspend(true);actions.Stop(run,false);}
        }
        [UnityTest]public IEnumerator GroundedFollowingReadinessAndFactsArePassiveAndSelfFollowingRefuses(){
            WalkingScene();var actor=WalkingBlock(new(1,0,0));string id=editor.Identity(actor);var before=JsonUtility.ToJson(editor.Snapshot());int maps=physicalRoot.GetComponentsInChildren<RoomNavigation>(true).Length;
            Assert.That(BehaviourCatalog.TryCall("object.follow",1,(JObject)FollowingCall(id,id)["arguments"],out _,out var error),Is.False);Assert.That(error,Does.Contain("itself"));
            for(int i=0;i<4;i++){Assert.That(GroundedFollowing.CanBegin(editor,actor,null,1,out error),Is.True,error);Assert.That((bool)WalkingStatus(id)["active"],Is.False);}
            Assert.That(physicalRoot.GetComponentsInChildren<RoomNavigation>(true).Length,Is.EqualTo(maps));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));yield return null;
        }
        [UnityTest]public IEnumerator GroundedFollowingNativeDependenciesObserveLeaderWithoutClaimingIt(){
            WalkingScene();var actor=WalkingBlock(new(1,0,0));var leader=WalkingBlock(new(2,0,0));var args=(JObject)FollowingCall(editor.Identity(actor),editor.Identity(leader))["arguments"];var module=new FollowObjectCapability();
            CollectionAssert.AreEquivalent(new[]{editor.Identity(actor),editor.Identity(leader)},module.NativeEntities(args));Assert.That(module.Claims(args).Length,Is.EqualTo(1));Assert.That(module.Claims(args)[0].Target,Is.EqualTo(editor.Identity(actor)));yield return null;
        }
        [UnityTest]public IEnumerator GroundedFollowingVirtualActorCanFollowBelowThePhysicalFloor(){
            WalkingScene();world.PausePhysics();Terrain(new(0,-2.2f,0),.2f,out _);var actor=WalkingBlock(new(-1,-2,0));var leader=WalkingBlock(new(2,-2,0));var profile=new RoomEnvironmentProfile{id=Guid.NewGuid().ToString("N"),name="Underground actors",realCollisions=false};
            Assert.That(editor.EditEnvironment(profile,profile.id,0,Array.Empty<string>(),out var error),Is.True,error);foreach(var value in new[]{actor,leader}){var id=editor.Identity(value);Assert.That(editor.BindEnvironment(id,editor.ObjectRevision(id),profile.id,editor.EnvironmentRevision(profile.id),out error),Is.True,error);}
            Physics.SyncTransforms();world.StartPhysics();var follow=new GroundedFollowing(editor,actor,editor.Identity(leader),1,1.2f,10);
            try{Assert.That(follow.Begin(out error),Is.True,error);yield return FollowingFrames(follow,300);Assert.That(actor.transform.position.x,Is.GreaterThan(.9f));Assert.That(actor.transform.position.y,Is.LessThan(-1.8f));}
            finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedFollowingAndFiniteWalkingShareAdmissionAndRelease(){
            WalkingScene();avatar.transform.position=new(0,0,4);var actors=new List<RoomItem>();for(int i=0;i<5;i++)actors.Add(WalkingBlock(new(-4+i*1.5f,0,-3)));
            var following=new GroundedFollowing(editor,actors[0],null,1,1.2f,10);var walks=new List<GroundedTravel>();
            try{Assert.That(following.Begin(out var error),Is.True,error);for(int i=1;i<4;i++){var walk=new GroundedTravel(editor,actors[i],new(-4+i*1.5f,0,-2),1.2f);Assert.That(walk.Begin(out error),Is.True,error);walks.Add(walk);}
                Assert.That(GroundedFollowing.CanBegin(editor,actors[4],null,1,out error),Is.False);Assert.That(error,Does.Contain("four"));following.Stop(false);Assert.That(GroundedTravel.CanBegin(editor,actors[4],out error),Is.True,error);yield return null;
            }finally{following.Stop(false);foreach(var walk in walks)walk.Stop(false);}
        }
    }
    public sealed partial class RoomRulesTests {
        [UnityTest] public IEnumerator GroundedFollowingAdvertisesItsFeatureToConnectedClients(){
            var features=RoomControls.Capabilities(editor);
            Assert.That(features,Does.Contain(FollowObjectCapability.Feature));
            Assert.That(features,Does.Contain(NavigateObjectCapability.Feature));
            yield return null;
        }
    }

}
