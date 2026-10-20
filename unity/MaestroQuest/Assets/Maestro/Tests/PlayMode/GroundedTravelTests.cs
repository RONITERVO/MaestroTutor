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
        void WalkingScene(bool separateInteraction=false){Surface(new(0,-.1f,0),new(12,.2f,12));Tutor(separateInteraction);viewer.transform.position=new(0,1.6f,5);Ready();}
        RoomItem WalkingBlock(Vector3 foot){
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Walking robot",foot+Vector3.up,1,Color.white,out var id,out var error),Is.True,error);
            Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
            var item=editor.Find(id);Physics.SyncTransforms();var bounds=item.Grab.colliders[0].bounds;
            Assert.That(editor.MoveObject(id,item.transform.position+foot-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),out error),Is.True,error);Physics.SyncTransforms();return item;
        }
        IEnumerator FinishWalking(GroundedTravel travel,int frames=400){
            for(int i=0;i<frames;i++){var state=travel.State(out var error);if(state!=RuleActionState.Preparing){Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);Assert.That(travel.Complete(out error),Is.True,error);yield break;}yield return null;}
            Assert.Fail("Walking did not reach an accepted destination");
        }
        IEnumerator WaitingForWalkingActor(GroundedTravel travel,string target){
            for(int i=0;i<120;i++){
                Assert.That(travel.State(out var error),Is.EqualTo(RuleActionState.Preparing),error);
                if((string)WalkingStatus(target)["state"]=="waitingForActor")yield break;
                yield return null;
            }
            Assert.Fail("The occupied destination did not expose its walking-actor wait");
        }
        [UnityTest]public IEnumerator GroundedTravelWaitsForWalkingActorThenArrivesWithoutJumping(){
            WalkingScene();var actor=editor.Find("maestro");var crossing=WalkingBlock(new(1.5f,0,0));
            var walk=new GroundedTravel(editor,actor,new(1.5f,0,0),1.2f);
            var other=new GroundedTravel(editor,crossing,new(1.5f,0,2),1.2f);
            try{
                Assert.That(other.Begin(out var error),Is.True,error);Assert.That(walk.Begin(out error),Is.True,error);
                yield return WaitingForWalkingActor(walk,"maestro");Assert.That(walk.Complete(out _),Is.False);
                var held=actor.transform.position;for(int i=0;i<8;i++){walk.Tick();yield return null;}
                Assert.That(actor.transform.position,Is.EqualTo(held));
                var state=RuleActionState.Preparing;var previous=actor.transform.position;
                for(int i=0;i<450&&state==RuleActionState.Preparing;i++){
                    Assert.That(other.State(out error),Is.Not.EqualTo(RuleActionState.Failed),error);
                    state=walk.State(out error);Assert.That(Vector3.Distance(previous,actor.transform.position),Is.LessThan(.061f));
                    previous=actor.transform.position;yield return null;
                }
                Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);Assert.That(walk.Complete(out error),Is.True,error);
                Assert.That(actor.transform.position.x,Is.EqualTo(1.5f).Within(.015f));
                Assert.That((string)WalkingStatus("maestro")["state"],Is.EqualTo("arrived"));
            }finally{walk.Stop(false);other.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelWalkingActorWaitExpiresWithoutClaimingArrival(){
            WalkingScene();var crossing=WalkingBlock(new(1.5f,0,0));
            var walk=new GroundedTravel(editor,editor.Find("maestro"),new(1.5f,0,0),1.2f);
            var other=new GroundedTravel(editor,crossing,new(1.5f,0,2),1.2f);
            try{
                Assert.That(other.Begin(out var error),Is.True,error);Assert.That(walk.Begin(out error),Is.True,error);
                yield return WaitingForWalkingActor(walk,"maestro");var held=avatar.transform.position;
                yield return new WaitForSecondsRealtime(3.1f);
                Assert.That(walk.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(error,Does.Contain("waiting"));
                Assert.That(walk.Complete(out _),Is.False);Assert.That(avatar.transform.position,Is.EqualTo(held));
                other.Stop(false);crossing.gameObject.SetActive(false);yield return null;
                Assert.That(walk.State(out _),Is.EqualTo(RuleActionState.Failed));Assert.That(avatar.transform.position,Is.EqualTo(held));
            }finally{walk.Stop(false);other.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelStopDuringWalkingActorWaitCannotResume(){
            WalkingScene();var crossing=WalkingBlock(new(1.5f,0,0));
            var walk=new GroundedTravel(editor,editor.Find("maestro"),new(1.5f,0,0),1.2f);
            var other=new GroundedTravel(editor,crossing,new(1.5f,0,2),1.2f);
            try{
                Assert.That(other.Begin(out var error),Is.True,error);Assert.That(walk.Begin(out error),Is.True,error);
                yield return WaitingForWalkingActor(walk,"maestro");var held=avatar.transform.position;
                walk.Stop(false);other.Stop(false);crossing.gameObject.SetActive(false);
                yield return new WaitForSecondsRealtime(.4f);
                for(int i=0;i<8;i++){walk.Tick();yield return null;}
                Assert.That((bool)WalkingStatus("maestro")["active"],Is.False);
                Assert.That(avatar.transform.position,Is.EqualTo(held));Assert.That(walk.Complete(out _),Is.False);
            }finally{walk.Stop(false);other.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelWalkingActorWaitPreservesVirtualGroundBelowRealFloor(){
            WalkingScene();world.PausePhysics();Terrain(new(0,-2.2f,0),.2f,out _);
            var actor=WalkingBlock(new(-1,-2,0));var crossing=WalkingBlock(new(1,-2,0));
            var profile=new RoomEnvironmentProfile{id=Guid.NewGuid().ToString("N"),name="Independent underground actors",realCollisions=false};
            Assert.That(editor.EditEnvironment(profile,profile.id,0,Array.Empty<string>(),out var error),Is.True,error);
            foreach(var value in new[]{actor,crossing}){
                string id=editor.Identity(value);Assert.That(editor.BindEnvironment(id,editor.ObjectRevision(id),profile.id,editor.EnvironmentRevision(profile.id),out error),Is.True,error);
            }
            Physics.SyncTransforms();world.StartPhysics();
            var walk=new GroundedTravel(editor,actor,new(1,-2,0),1.2f);
            var other=new GroundedTravel(editor,crossing,new(1,-2,2),1.2f);
            try{
                Assert.That(other.Begin(out error),Is.True,error);Assert.That(walk.Begin(out error),Is.True,error);
                yield return WaitingForWalkingActor(walk,editor.Identity(actor));
                var state=RuleActionState.Preparing;
                for(int i=0;i<450&&state==RuleActionState.Preparing;i++){
                    Assert.That(other.State(out error),Is.Not.EqualTo(RuleActionState.Failed),error);
                    state=walk.State(out error);Assert.That(actor.transform.position.y,Is.LessThan(-1.8f));yield return null;
                }
                Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);Assert.That(walk.Complete(out error),Is.True,error);
                Assert.That(actor.transform.position.x,Is.EqualTo(1).Within(.015f));
            }finally{walk.Stop(false);other.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelWalkingActorWaitIsVisibleAndCancellableThroughSharedCapability(){
            WalkingScene();var crossing=WalkingBlock(new(1.5f,0,0));
            var other=new GroundedTravel(editor,crossing,new(1.5f,0,2),1.2f);
            var actions=new RoomRuleActions(editor,authoring);var scheduler=new RuleScheduler(actions);string run=null;
            try{
                Assert.That(other.Begin(out var error),Is.True,error);
                var call=new JObject{["id"]="object.navigate",["version"]=1,["arguments"]=new JObject{["target"]="maestro",["mode"]="walk",["destination"]=new JObject{["x"]=1.5,["y"]=0,["z"]=0},["speed"]=1.2}};
                Assert.That(scheduler.Invoke(call,Time.unscaledTime,out run,out error),Is.True,error);
                for(int i=0;i<120;i++){
                    actions.Tick();scheduler.Tick(Time.unscaledTime);
                    Assert.That((string)scheduler.Invocation(run)["phase"],Is.Not.EqualTo("failed"),scheduler.Invocation(run).ToString());
                    if((string)WalkingStatus("maestro")["state"]=="waitingForActor")break;
                    yield return null;
                }
                Assert.That((string)WalkingStatus("maestro")["state"],Is.EqualTo("waitingForActor"));
                Assert.That(editor.Ownership.Covers(run,new[]{new BehaviourCatalog.Claim("maestro","locomotion")}),Is.True);
                var before=avatar.transform.position;scheduler.GrabTarget("maestro");
                Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));
                Assert.That((bool)WalkingStatus("maestro")["active"],Is.False);
                other.Stop(false);crossing.gameObject.SetActive(false);yield return new WaitForSecondsRealtime(.4f);
                actions.Tick();scheduler.Tick(Time.unscaledTime);
                Assert.That(avatar.transform.position,Is.EqualTo(before));
            }finally{scheduler.Suspend(true);if(run!=null)actions.Stop(run,false);other.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelCreatedObjectArrivesAndRetainsItsRootOffset(){
            WalkingScene();var item=WalkingBlock(new(1,0,0));float offset=item.transform.position.y;
            var travel=new GroundedTravel(editor,item,new(1.6f,0,0),1.2f);
            try{Assert.That(travel.Begin(out var error),Is.True,error);Assert.That(travel.State(out error),Is.EqualTo(RuleActionState.Preparing));
                yield return FinishWalking(travel);Assert.That(item.transform.position.x,Is.EqualTo(1.6f).Within(.015f));Assert.That(item.transform.position.y,Is.EqualTo(offset).Within(.015f));Assert.That((float)travel.Result["distance"],Is.GreaterThan(.58f));
            }finally{travel.Stop(false);}Assert.That(editor.Read(editor.Identity(item)).position.x,Is.EqualTo(item.transform.localPosition.x).Within(.001f));
        }
        [UnityTest]public IEnumerator GroundedTravelMaestroUsesTheExistingWalkAndCanBeCancelledWithoutTeleporting(){
            WalkingScene();var item=editor.Find("maestro");var travel=new GroundedTravel(editor,item,new(1.2f,0,0),1.2f);var leg=avatar.PoseRig.CanonicalBone(PoseJoint.LeftUpperLeg);var rest=leg.localRotation;float swing=0;
            try{Assert.That(travel.Begin(out var error),Is.True,error);for(int i=0;i<35;i++){Assert.That(travel.State(out error),Is.Not.EqualTo(RuleActionState.Failed),error);yield return null;swing=Mathf.Max(swing,Quaternion.Angle(rest,leg.localRotation));}
                Assert.That(item.transform.position.x,Is.GreaterThan(.3f));Assert.That(swing,Is.GreaterThan(2));
            }finally{travel.Stop(false);}var stopped=item.transform.position;for(int i=0;i<5;i++){travel.Tick();yield return null;}Assert.That(item.transform.position,Is.EqualTo(stopped));Assert.That(editor.Read("maestro").position.x,Is.EqualTo(stopped.x).Within(.001f));
        }
        [UnityTest]public IEnumerator GroundedTravelCannotReportArrivalAcrossASolidObstacle(){
            WalkingScene();var wall=Surface(new(.65f,.8f,0),new(.06f,1.6f,14));Physics.SyncTransforms();var travel=new GroundedTravel(editor,editor.Find("maestro"),new(1.2f,0,0),1.2f);
            try{Assert.That(travel.Begin(out var error),Is.True,error);var state=RuleActionState.Preparing;for(int i=0;i<160&&state==RuleActionState.Preparing;i++){state=travel.State(out error);yield return null;}
                Assert.That(state,Is.EqualTo(RuleActionState.Failed),error);Assert.That(avatar.transform.position.x,Is.LessThan(.4f));Assert.That(travel.Complete(out _),Is.False);
            }finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelStopsWhenPhysicsPausesAndDoesNotResume(){
            WalkingScene();var item=WalkingBlock(new(1,0,0));var travel=new GroundedTravel(editor,item,new(2,0,0),1.2f);
            try{Assert.That(travel.Begin(out var error),Is.True,error);for(int i=0;i<10;i++){travel.Tick();yield return null;}world.PausePhysics();yield return null;
                Assert.That(travel.State(out error),Is.EqualTo(RuleActionState.Failed));var held=item.transform.position;world.StartPhysics();for(int i=0;i<8;i++){travel.Tick();yield return null;}Assert.That(item.transform.position,Is.EqualTo(held));
            }finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelTracksAuthoredDestinationWhenViewedWorldMoves(){
            WalkingScene();Terrain(Vector3.zero,0,out _);var item=WalkingBlock(new(1,0,0));
            var profile=new RoomEnvironmentProfile{id=Guid.NewGuid().ToString("N"),name="Virtual walking",realCollisions=false};
            Assert.That(editor.EditEnvironment(profile,profile.id,0,Array.Empty<string>(),out var error),Is.True,error);Assert.That(editor.BindEnvironment(editor.Identity(item),editor.ObjectRevision(editor.Identity(item)),profile.id,editor.EnvironmentRevision(profile.id),out error),Is.True,error);
            var travel=new GroundedTravel(editor,item,new(1.6f,0,0),1.2f);
            try{Assert.That(travel.Begin(out error),Is.True,error);for(int i=0;i<10;i++){travel.Tick();yield return null;}root.transform.SetPositionAndRotation(new(3,0,2),Quaternion.Euler(0,30,0));yield return null;
                yield return FinishWalking(travel);Assert.That(item.transform.localPosition.x,Is.EqualTo(1.6f).Within(.015f));
            }finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelFourOwnersHaveIndependentCancellationAndBoundedAdmission(){
            WalkingScene();var actors=new List<RoomItem>();for(int i=0;i<5;i++)actors.Add(WalkingBlock(new(-4+i*1.5f,0,-2)));
            var walks=new List<GroundedTravel>();try{
                for(int i=0;i<4;i++){var walk=new GroundedTravel(editor,actors[i],new(-4+i*1.5f,0,-1),.65f);walks.Add(walk);Assert.That(walk.Begin(out var error),Is.True,error);}
                Assert.That(GroundedTravel.CanBegin(editor,actors[4],out var reason),Is.False);Assert.That(reason,Does.Contain("four"));walks[0].Stop(false);Assert.That(GroundedTravel.CanBegin(editor,actors[4],out reason),Is.True,reason);
                float deadline=Time.realtimeSinceStartup+10;while(actors[1].transform.position.z<=-1.99f&&Time.realtimeSinceStartup<deadline){walks[1].Tick();yield return null;}Assert.That(actors[1].transform.position.z,Is.GreaterThan(-1.99f));
            }finally{foreach(var walk in walks)walk.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelReadinessDoesNotBakeAcquireOrEditAndRejectsMissingBodies(){
            WalkingScene();var item=WalkingBlock(new(1,0,0));var saved=JsonUtility.ToJson(editor.Snapshot());int count=physicalRoot.GetComponentsInChildren<RoomNavigation>(true).Length;
            for(int i=0;i<4;i++)Assert.That(GroundedTravel.CanBegin(editor,item,out var error),Is.True,error);
            Assert.That(physicalRoot.GetComponentsInChildren<RoomNavigation>(true).Length,Is.EqualTo(count));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            foreach(var c in item.Grab.colliders)c.enabled=false;Assert.That(GroundedTravel.CanBegin(editor,item,out _),Is.False);yield return null;
        }
        [UnityTest]public IEnumerator GroundedTravelStopsOnExternalScaleChangeWithoutPublishingAnotherStep(){
            WalkingScene();var item=WalkingBlock(new(1,0,0));var travel=new GroundedTravel(editor,item,new(2,0,0),1.2f);
            try{Assert.That(travel.Begin(out var error),Is.True,error);travel.Tick();yield return null;item.transform.localScale*=1.1f;var held=item.transform.position;
                Assert.That(travel.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(error,Does.Contain("changed"));Assert.That(item.transform.position,Is.EqualTo(held));
            }finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelKeepsClearOfTheUsersSweptPersonalSpace(){
            WalkingScene();viewer.transform.position=new(.8f,1.6f,0);var travel=new GroundedTravel(editor,editor.Find("maestro"),new(1.6f,0,0),1.2f);
            try{Assert.That(travel.Begin(out var error),Is.True,error);var state=RuleActionState.Preparing;for(int i=0;i<150&&state==RuleActionState.Preparing;i++){state=travel.State(out error);yield return null;}Assert.That(state,Is.EqualTo(RuleActionState.Failed));Assert.That(error,Does.Contain("user"));Assert.That(avatar.transform.position.x,Is.LessThan(.21f));}
            finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelUsesSharedWaterDetourAndExactCompletion(){
            WaterBasin();viewer.transform.position=new(0,1.6f,5);var travel=new GroundedTravel(editor,editor.Find("maestro"),new(3,0,0),1.2f);float lateral=0;
            try{Assert.That(travel.Begin(out var error),Is.True,error);var state=RuleActionState.Preparing;for(int i=0;i<500&&state==RuleActionState.Preparing;i++){state=travel.State(out error);lateral=Mathf.Max(lateral,Mathf.Abs(avatar.transform.position.z));yield return null;}Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);Assert.That(lateral,Is.GreaterThan(.6f));Assert.That(travel.Complete(out error),Is.True,error);}
            finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelSharedCapabilityReturnsArrivalAndReleasesItsChannel(){
            WalkingScene();var item=WalkingBlock(new(1,0,0));string id=editor.Identity(item);var actions=new RoomRuleActions(editor,authoring);var scheduler=new RuleScheduler(actions);
            var call=new JObject{["id"]="object.navigate",["version"]=1,["arguments"]=new JObject{["target"]=id,["mode"]="walk",["destination"]=new JObject{["x"]=1.5,["y"]=0,["z"]=0},["speed"]=1.2}};
            Assert.That(scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);
            try{for(int i=0;i<180;i++){actions.Tick();scheduler.Tick(Time.unscaledTime);yield return null;}var result=scheduler.ObserveInvocations(run).ToString();Assert.That(result,Does.Contain("completed"),result);Assert.That(item.transform.position.x,Is.EqualTo(1.5f).Within(.015f));Assert.That(editor.Ownership.Covers(run,new[]{new BehaviourCatalog.Claim(id,"locomotion")}),Is.False);}
            finally{scheduler.Suspend(true);actions.Stop(run,false);}
        }
        [UnityTest]public IEnumerator GroundedTravelAlreadyAtSupportedDestinationCompletesWithoutMovement(){
            WalkingScene();var item=WalkingBlock(new(1,0,0));var before=item.transform.position;var travel=new GroundedTravel(editor,item,new(1,0,0),.65f);
            try{Assert.That(travel.Begin(out var error),Is.True,error);yield return FinishWalking(travel);Assert.That(item.transform.position,Is.EqualTo(before));Assert.That((float)travel.Result["distance"],Is.Zero);}
            finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelVirtualActorWalksBelowPhysicalRoomFloor(){
            WalkingScene();world.PausePhysics();Terrain(new(0,-2.2f,0),.2f,out _);var item=WalkingBlock(new(1,-2,0));
            var profile=new RoomEnvironmentProfile{id=Guid.NewGuid().ToString("N"),name="Below real floor",realCollisions=false};
            Assert.That(editor.EditEnvironment(profile,profile.id,0,Array.Empty<string>(),out var error),Is.True,error);string id=editor.Identity(item);
            Assert.That(editor.BindEnvironment(id,editor.ObjectRevision(id),profile.id,editor.EnvironmentRevision(profile.id),out error),Is.True,error);
            Physics.SyncTransforms();world.StartPhysics();var travel=new GroundedTravel(editor,item,new(1.6f,-2,0),1.2f);
            try{Assert.That(travel.Begin(out error),Is.True,error);yield return FinishWalking(travel);Assert.That(item.transform.position.y,Is.LessThan(-1.8f));Assert.That(item.transform.position.x,Is.EqualTo(1.6f).Within(.015f));}
            finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelCreatedRobotKeepsItsPartAnimation(){
            WalkingScene();Assert.That(editor.CreateRecipe("Walking animated robot",new Vector3(1,0,0),1,RecipeTemplates.BoxRobot(true),out var id,out var error),Is.True,error);
            var item=editor.Find(id);Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);Physics.SyncTransforms();
            var bounds=item.Grab.colliders[0].bounds;Assert.That(editor.MoveObject(id,item.transform.position+new Vector3(1,0,0)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),out error),Is.True,error);Physics.SyncTransforms();
            var recipe=item.GetComponent<RecipeObject>();var arm=recipe.Part("RightUpperArm");var start=arm.localRotation;float swing=0;var travel=new GroundedTravel(editor,item,new(1.6f,0,0),1.2f);
            try{Assert.That(travel.Begin(out error),Is.True,error);var state=RuleActionState.Preparing;for(int i=0;i<180&&state==RuleActionState.Preparing;i++){state=travel.State(out error);yield return null;swing=Mathf.Max(swing,Quaternion.Angle(start,arm.localRotation));}
                Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);Assert.That(swing,Is.GreaterThan(5));Assert.That(recipe.IsPlaying,Is.True);
            }finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelTrackingLossStopsVirtualOnlyActorAndDoesNotRestart(){
            WalkingScene();Terrain(Vector3.zero,0,out _);var item=WalkingBlock(new(1,0,0));
            var profile=new RoomEnvironmentProfile{id=Guid.NewGuid().ToString("N"),name="Virtual walking",realCollisions=false};
            Assert.That(editor.EditEnvironment(profile,profile.id,0,Array.Empty<string>(),out var error),Is.True,error);string id=editor.Identity(item);
            Assert.That(editor.BindEnvironment(id,editor.ObjectRevision(id),profile.id,editor.EnvironmentRevision(profile.id),out error),Is.True,error);
            editor.ConfigureObservationTracking(()=>tracked);tracked=false;Assert.That(GroundedTravel.CanBegin(editor,item,out error),Is.False);tracked=true;
            var travel=new GroundedTravel(editor,item,new(2,0,0),1.2f);
            try{Assert.That(travel.Begin(out error),Is.True,error);for(int i=0;i<10;i++){travel.Tick();yield return null;}tracked=false;var before=item.transform.position;
                Assert.That(world.SimulationActive,Is.True);Assert.That(travel.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(item.transform.position,Is.EqualTo(before));
                tracked=true;for(int i=0;i<8;i++){travel.Tick();yield return null;}Assert.That(item.transform.position,Is.EqualTo(before));Assert.That(travel.Complete(out _),Is.False);
            }finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelUsesSharedViewerAboveTheWorkspace(){
            WalkingScene(true);Assert.That(editor.GetComponent<RoomInteraction>(),Is.Null);Assert.That(editor.Viewer,Is.SameAs(viewer.transform));var item=WalkingBlock(new(1,0,0));
            var travel=new GroundedTravel(editor,item,new(1.6f,0,0),1.2f);
            try{Assert.That(travel.Begin(out var error),Is.True,error);yield return FinishWalking(travel);Assert.That(item.transform.position.x,Is.EqualTo(1.6f).Within(.015f));}
            finally{travel.Stop(false);}
        }
        [UnityTest]public IEnumerator GroundedTravelDeletedTargetFailsAndReleasesTheOwnedMap(){
            WalkingScene();var item=WalkingBlock(new(1,0,0));var travel=new GroundedTravel(editor,item,new(2,0,0),1.2f);int maps=physicalRoot.GetComponentsInChildren<RoomNavigation>(true).Length;
            try{Assert.That(travel.Begin(out var error),Is.True,error);travel.Tick();yield return null;Assert.That(editor.DeleteObject(editor.Identity(item),out error),Is.False,"Normal edits cannot delete an owned walking body");UnityEngine.Object.Destroy(item.gameObject);yield return null;Assert.That(travel.State(out error),Is.EqualTo(RuleActionState.Failed));}
            finally{travel.Stop(false);}yield return null;Assert.That(physicalRoot.GetComponentsInChildren<RoomNavigation>(true).Length,Is.EqualTo(maps));
        }
    }
}
