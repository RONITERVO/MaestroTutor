// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
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
        static JObject WaterFollowArgs(string actor,string pool,string leader,string mode="swim",float seconds=30){
            var a=new JObject{["target"]=actor,["mode"]=mode,["habitat"]=pool,["leader"]=new JObject{["kind"]="object",["objectId"]=leader},
                ["distance"]=.8,["speed"]=1.2,["seconds"]=seconds,["facing"]="keep"};
            if(mode!="swim")a["immersion"]=.6;return a;
        }
        RoomItem WaterLeader(Vector3 point){
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Moving leader",point,.2f,Color.yellow,out var id,out var error),Is.True,error);
            Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True);
            Physics.SyncTransforms();return editor.Find(id);
        }
        (string pool,RoomItem actor,RoomItem leader) FollowingLake(){
            var(pool,id)=AquaticFixture();physics.PausePhysics();var c=editor.Read(pool).containers[0];
            c.rectangle.width=2;c.rectangle.depth=2;c.height=2;c.capacityMl=8000000;c.amountMl=4000000;
            ContainerFixture(new RoomAgentExecutor(editor),ContainerCall(pool,c));
            var leader=WaterLeader(new(5.4f,1.6f,.2f));Physics.SyncTransforms();physics.StartPhysics();return(pool,editor.Find(id),leader);
        }
        void RepositionLeader(RoomItem leader,Vector3 point){
            leader.transform.position=point;leader.GetComponent<RigidRoomItem>().Teleported();Physics.SyncTransforms();
        }
        IEnumerator FollowFor(WaterFollowing follow,RoomItem actor,int frames,bool slope=false,Action<JObject> observe=null){
            var previous=actor.transform.position;float until=Time.realtimeSinceStartup+frames/60f;
            while(Time.realtimeSinceStartup<until){
                Assert.That(follow.State(out var error),Is.EqualTo(RuleActionState.Ready),error);observe?.Invoke(follow.Result);
                var change=actor.transform.position-previous;
                Assert.That(Vector3.ProjectOnPlane(change,Vector3.up).magnitude,Is.LessThan(.0601f),"The horizontal speed budget is never accumulated while waiting");
                float bound=slope?.06f/Mathf.Cos(RoomGroundQuery.MaximumSlope*Mathf.Deg2Rad)+.001f:.0601f;
                Assert.That(change.magnitude,Is.LessThan(bound),"The continuous test shore adds only its supported slope to the horizontal speed budget");
                previous=actor.transform.position;Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(1));yield return null;
            }
        }
        [UnityTest]public IEnumerator WaterFollowingSubmergedTracksMovingLeaderInThreeDimensionsWithOneOwner(){
            var(pool,actor,leader)=FollowingLake();var args=WaterFollowArgs(editor.Identity(actor),pool,editor.Identity(leader));var follow=new WaterFollowing(editor,actor,args);
            try{
                Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowFor(follow,actor,220);
                Assert.That(Vector3.Distance(actor.transform.position,leader.transform.position),Is.InRange(.77f,.9f));
                Assert.That((string)follow.Result["state"],Is.EqualTo("keepingDistance"));
                RepositionLeader(leader,new(3,1.9f,-.3f));yield return FollowFor(follow,actor,300);
                Assert.That(Vector3.Distance(actor.transform.position,leader.transform.position),Is.InRange(.77f,.9f));
                Assert.That(actor.transform.position.y,Is.GreaterThan(1.6f));
                var fact=AquaticMovement.Observe(editor,actor);Assert.That((string)fact["mode"],Is.EqualTo("follow"));Assert.That((string)fact["movement"],Is.EqualTo("swim"));
            }finally{follow.Stop(false);}Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator WaterFollowingSurfaceTracksHorizontallyAndKeepsTheLiveWaterline(){
            var(pool,actor,leader)=FollowingLake();var args=WaterFollowArgs(editor.Identity(actor),pool,editor.Identity(leader),"surface");var follow=new WaterFollowing(editor,actor,args);
            try{
                Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowFor(follow,actor,230);
                Assert.That(Vector3.ProjectOnPlane(actor.transform.position-leader.transform.position,Vector3.up).magnitude,Is.InRange(.77f,.9f));
                var before=actor.transform.position;PublishAquaticQuantity(pool,4800000);RepositionLeader(leader,new(3.1f,1.5f,-.3f));
                yield return FollowFor(follow,actor,300);Assert.That(actor.transform.position.y,Is.GreaterThan(before.y+.15f));
                Assert.That(Vector3.ProjectOnPlane(actor.transform.position-leader.transform.position,Vector3.up).magnitude,Is.InRange(.77f,.9f));
                Assert.That((string)AquaticMovement.Observe(editor,actor)["movement"],Is.EqualTo("surface"));
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator WaterFollowingShoreCrossesIntoWaterAndReturnsBehindAnIndependentLeader(){
            var(_,pool,id,dry,wet)=ShoreFixture();var actor=editor.Find(id);var leader=WaterLeader(new(4.4f,wet.y,0));
            var args=WaterFollowArgs(id,pool,editor.Identity(leader),"shore");var follow=new WaterFollowing(editor,actor,args);
            try{
                Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowFor(follow,actor,300,true);
                Assert.That(actor.transform.position.x,Is.LessThan(5.3f));Assert.That((string)follow.Result["state"],Is.EqualTo("keepingDistance"));
                RepositionLeader(leader,new(7.5f,dry.y,0));yield return FollowFor(follow,actor,350,true);
                Assert.That(actor.transform.position.x,Is.GreaterThan(6.6f));Assert.That((string)follow.Result["state"],Is.EqualTo("keepingDistance"));
                Assert.That(editor.Read(id).waterTraversal.mode,Is.EqualTo("avoid"));Assert.That((string)ShoreMovement.Observe(editor,actor)["movement"],Is.EqualTo("shore"));
            }finally{follow.Stop(false);}Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator WaterFollowingBlockedGoalWaitsThenRecoversWithoutRestartingOrJumping(){
            var(pool,actor,leader)=FollowingLake();var start=actor.transform.position;
            RepositionLeader(leader,new(7.5f,1.6f,0));var args=WaterFollowArgs(editor.Identity(actor),pool,editor.Identity(leader));var follow=new WaterFollowing(editor,actor,args);
            try{
                Assert.That(follow.Begin(out var error),Is.True,error);bool observedBlocked=false;
                yield return FollowFor(follow,actor,350,observe:r=>observedBlocked|=(string)r["state"]=="blocked");
                Assert.That(observedBlocked,Is.True,"An unreachable goal must publish its blocked state between bounded retries");
                Assert.That((string)follow.Result["state"],Is.EqualTo("blocked").Or.EqualTo("planning"));Assert.That(actor.transform.position,Is.EqualTo(start));
                RepositionLeader(leader,new(5.4f,1.6f,.2f));yield return new WaitForSecondsRealtime(.4f);yield return FollowFor(follow,actor,230);
                Assert.That(Vector3.Distance(actor.transform.position,leader.transform.position),Is.InRange(.77f,.9f));
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator WaterFollowingDrainWhileBlockedIsTerminalAndCannotSelectAnotherLake(){
            var(pool,actor,leader)=FollowingLake();RepositionLeader(leader,new(7.5f,1.6f,0));
            var follow=new WaterFollowing(editor,actor,WaterFollowArgs(editor.Identity(actor),pool,editor.Identity(leader)));
            try{
                Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowFor(follow,actor,350);
                var held=actor.transform.position;PublishAquaticQuantity(pool,0);
                float until=Time.realtimeSinceStartup+5;
                while(follow.State(out error)!=RuleActionState.Failed&&Time.realtimeSinceStartup<until)yield return null;
                Assert.That(follow.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(follow.Complete(out _),Is.False);
                PublishAquaticQuantity(pool,4000000);RepositionLeader(leader,new(5,1.5f,0));for(int i=0;i<5;i++){follow.Tick();yield return null;}
                Assert.That(actor.transform.position,Is.EqualTo(held));
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator WaterFollowingLeaderLossCannotReviveTheOwner()=>FollowingInterruption(true);
        [UnityTest]public IEnumerator WaterFollowingSameFramePauseCannotReviveTheOwner()=>FollowingInterruption(false);
        IEnumerator FollowingInterruption(bool loseLeader){
                var(pool,actor,leader)=FollowingLake();var follow=new WaterFollowing(editor,actor,WaterFollowArgs(editor.Identity(actor),pool,editor.Identity(leader)));
                try{
                    Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowFor(follow,actor,40);var held=actor.transform.position;
                    if(loseLeader)leader.gameObject.SetActive(false);else{physics.PausePhysics();physics.StartPhysics();}
                    Assert.That(follow.State(out error),Is.EqualTo(RuleActionState.Failed));leader.gameObject.SetActive(true);
                    for(int i=0;i<8;i++){follow.Tick();yield return null;}Assert.That(actor.transform.position,Is.EqualTo(held));
                }finally{follow.Stop(false);}
                Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator WaterFollowingShoreBelowFloorUsesOnlyTheFollowersCollisionProfile(){
            var(_,pool,id,dry,wet)=ShoreFixture(true);var actor=editor.Find(id);var leader=WaterLeader(new(4.4f,wet.y,0));
            var args=WaterFollowArgs(id,pool,editor.Identity(leader),"shore");var follow=new WaterFollowing(editor,actor,args);
            try{
                Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowFor(follow,actor,300,true);
                Assert.That(actor.transform.position.x,Is.LessThan(5.3f));Assert.That(actor.transform.position.y,Is.LessThan(-1.4f));
                Assert.That(physics.RealCollisions,Is.True,"One actor's profile cannot disable the physical room for others");
            }finally{follow.Stop(false);}
            physics.PausePhysics();Assert.That(editor.BindEnvironment(id,editor.ObjectRevision(id),"",0,out var e),Is.True,e);physics.StartPhysics();
            var held=actor.transform.position;var denied=new WaterFollowing(editor,actor,args);
            try{
                if(denied.Begin(out e)){
                    float until=Time.realtimeSinceStartup+5;
                    while(denied.State(out e)!=RuleActionState.Failed&&Time.realtimeSinceStartup<until)yield return null;
                    Assert.That(denied.State(out e),Is.EqualTo(RuleActionState.Failed),e);
                }
                Assert.That(actor.transform.position,Is.EqualTo(held));Assert.That(denied.Complete(out _),Is.False);
            }finally{denied.Stop(false);}Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator WaterFollowingTracksTheUserAndStopRemainsTerminalAtKeepingDistance(){
            var(pool,actor,_)=FollowingLake();leftAnchor.transform.position=new(5.4f,1.6f,.2f);Physics.SyncTransforms();
            var args=WaterFollowArgs(editor.Identity(actor),pool,null);args["leader"]=new JObject{["kind"]="user"};
            var follow=new WaterFollowing(editor,actor,args);
            try{
                Assert.That(follow.Begin(out var error),Is.True,error);yield return FollowFor(follow,actor,220);
                Assert.That(Vector3.Distance(actor.transform.position,leftAnchor.transform.position),Is.InRange(.77f,.9f));
                Assert.That((string)follow.Result["state"],Is.EqualTo("keepingDistance"));var held=actor.transform.position;
                follow.Stop(false);leftAnchor.transform.position+=Vector3.right;
                Assert.That(follow.State(out _),Is.EqualTo(RuleActionState.Failed));Assert.That(follow.Complete(out _),Is.False);
                yield return null;follow.Tick();Assert.That(actor.transform.position,Is.EqualTo(held));Assert.That(RoomLocomotionBudget.Count,Is.Zero);
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator WaterFollowingPublicTimedActionReportsActualStateAndExactReplay(){
            var(pool,actor,leader)=FollowingLake();var ex=new RoomAgentExecutor(editor);
            var call=new JObject{["id"]="object.follow",["version"]=1,["arguments"]=WaterFollowArgs(editor.Identity(actor),pool,editor.Identity(leader),"swim",1)};
            Assert.That(BehaviourCatalog.TryCall("object.follow",1,(JObject)call["arguments"],out _,out var error),Is.True,error);
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(FollowingNavigationSchema.Feature));
            var request=ContainerRequest(call);Assert.That(ex.Execute(request,out error,out _),Is.True,error);
            yield return SpatialSettingsCompletion.Wait(ex.Executions,"completed");
            var result=ex.Executions.Observe()["selected"];Assert.That((float)result["output"]["distance"],Is.GreaterThan(0));
            var held=actor.transform.position;Assert.That(ex.Execute(request,out error,out _),Is.True,error);yield return null;
            Assert.That(actor.transform.position,Is.EqualTo(held));Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
    }
}
