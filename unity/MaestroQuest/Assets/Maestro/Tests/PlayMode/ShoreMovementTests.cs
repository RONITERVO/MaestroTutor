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
        (RoomAgentExecutor executor,string pool,string actor,Vector3 dry,Vector3 wet) ShoreFixture(bool belowRoom=false){
            var(ex,pool)=TerrainReservoir(true);physics.PausePhysics();
            Assert.That(editor.MoveObject(pool,new Vector3(6,belowRoom?-2:0,0),out var error),Is.True,error);
            var water=editor.Read(pool).containers[0];water.amountMl=225000;ContainerFixture(ex,ContainerCall(pool,water));
            var dry=TerrainPoint(pool,.75f,.35f);var wet=TerrainPoint(pool,-.75f,.3f);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Shore robot",dry,.5f,Color.white,out var id,out error),Is.True,error);
            Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True);
            Assert.That(editor.ConfigureWaterTraversal(id,editor.ObjectRevision(id),new(){mode="avoid",maxDepthMetres=.02f},out error),Is.True,error);
            var actor=editor.Find(id);Physics.SyncTransforms();Assert.That(GroundedMovement.Body(actor,out _,out _,out var foot,out error),Is.True,error);
            Assert.That(editor.MoveObject(id,actor.transform.position+dry-foot,out error),Is.True,error);
            if(belowRoom){
                var profile=new RoomEnvironmentProfile{id=Guid.NewGuid().ToString("N"),name="Below the physical floor",realCollisions=false};
                Assert.That(editor.EditEnvironment(profile,profile.id,0,Array.Empty<string>(),out error),Is.True,error);
                foreach(var target in new[]{pool,id})Assert.That(editor.BindEnvironment(target,editor.ObjectRevision(target),profile.id,editor.EnvironmentRevision(profile.id),out error),Is.True,error);
                physics.Contains=point=>point.y>=0;
            }
            root.GetComponent<RoomInteraction>().Viewer=leftAnchor.transform;leftAnchor.transform.position=new(0,1.6f,3);
            Physics.SyncTransforms();physics.StartPhysics();return(ex,pool,id,dry,wet);
        }
        static JObject ShoreCall(string actor,string pool,Vector3 point,bool surface=true)=>new(){
            ["id"]="object.navigate",["version"]=1,["arguments"]=new JObject{
                ["target"]=actor,["mode"]="shore",["habitat"]=pool,["destination"]=surface?
                    new JObject{["kind"]="surface",["x"]=point.x,["z"]=point.z}:
                    new JObject{["kind"]="ground",["x"]=point.x,["y"]=point.y,["z"]=point.z},
                ["immersion"]=.6,["speed"]=1.2,["facing"]="travel"}};
        IEnumerator ShoreCompleted(RoomAgentExecutor ex,RoomAgentRequest request,RoomItem actor){
            Assert.That(ex.Execute(request,out var error,out _),Is.True,error);var previous=actor.transform.position;bool observed=false;
            float deadline=Time.realtimeSinceStartup+15;
            while(Time.realtimeSinceStartup<deadline){
                var receipt=ex.Executions.Observe()["selected"];
                Assert.That(Vector3.Distance(previous,actor.transform.position),Is.LessThan(.065f),"No queued elapsed time may become a jump on this continuous slope");previous=actor.transform.position;
                var fact=ShoreMovement.Observe(editor,actor);if(fact!=null){observed=true;Assert.That((string)fact["mode"],Is.EqualTo("shore"));Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(1));}
                if((string)receipt["phase"]=="completed"){
                    Assert.That(observed,Is.True);Assert.That((float)receipt["output"]["distance"],Is.GreaterThan(1));
                    var held=actor.transform.position;Assert.That(ex.Execute(request,out error,out _),Is.True,error);yield return null;Assert.That(actor.transform.position,Is.EqualTo(held),"Receipt replay must not restart a trip");yield break;
                }
                Assert.That((string)receipt["phase"],Is.Not.EqualTo("failed"),receipt.ToString());yield return null;
            }
            Assert.Fail(ex.Executions.Observe().ToString());
        }
        [UnityTest]public IEnumerator ShoreMovementPublicActionWalksIntoWaterAndBackWithOneOwnerAndSavedAvoidance(){
            var(ex,pool,id,dry,wet)=ShoreFixture();var actor=editor.Find(id);
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(ShoreNavigationSchema.Feature));
            var request=ContainerRequest(ShoreCall(id,pool,wet));yield return ShoreCompleted(ex,request,actor);
            Assert.That(GroundedMovement.Body(actor,out _,out float height,out var foot,out var error),Is.True,error);
            Assert.That((wet.y-foot.y-RoomGroundQuery.Skin)/(height-RoomGroundQuery.Skin),Is.EqualTo(.6f).Within(.04f));
            Assert.That(editor.Read(id).waterTraversal.mode,Is.EqualTo("avoid"));Assert.That(RoomLocomotionBudget.Count,Is.Zero);
            request=ContainerRequest(ShoreCall(id,pool,dry,false));yield return ShoreCompleted(ex,request,actor);
            Assert.That(GroundedMovement.Body(actor,out _,out _,out foot,out error),Is.True,error);Assert.That(Vector3.Distance(foot,dry),Is.LessThan(.0051f));
            Assert.That(editor.Read(id).waterTraversal.mode,Is.EqualTo("avoid"));Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator ShoreMovementPublicActionFollowsVirtualBedBelowPhysicalRoomFloor(){
            var(ex,pool,id,_,wet)=ShoreFixture(true);var actor=editor.Find(id);
            yield return ShoreCompleted(ex,ContainerRequest(ShoreCall(id,pool,wet)),actor);
            Assert.That(actor.transform.position.y,Is.LessThan(-1.4f));Assert.That(physics.RealCollisions,Is.True,"The actor profile must not disable everyone else's real room");
        }
        [UnityTest]public IEnumerator ShoreMovementCrossesBothBanksInOnePublicJourney(){
            var(ex,pool,id,_,_)=ShoreFixture();physics.PausePhysics();var field=editor.Read(pool).heightFields[0];
            field.cells=16;field.width=4;field.depth=2;field.heights=new float[289];
            for(int z=0;z<=16;z++)for(int x=0;x<=16;x++)field.heights[z*17+x]=Mathf.Abs(x-8)*.0625f;
            Assert.That(editor.EditHeightField(pool,editor.ObjectRevision(pool),field,out var error),Is.True,error);
            var contents=editor.Read(pool).containers[0];contents.amountMl=720000;ContainerFixture(ex,ContainerCall(pool,contents));
            var actor=editor.Find(id);Physics.SyncTransforms();Assert.That(GroundedMovement.LeaderFoot(actor,out var foot),Is.True);
            var start=TerrainPoint(pool,-1.5f,.375f);var end=TerrainPoint(pool,1.5f,.375f);
            Assert.That(editor.MoveObject(id,actor.transform.position+start-foot,out error),Is.True,error);
            Physics.SyncTransforms();physics.StartPhysics();
            yield return ShoreCompleted(ex,ContainerRequest(ShoreCall(id,pool,end,false)),actor);
            Assert.That(GroundedMovement.LeaderFoot(actor,out foot),Is.True);Assert.That(Vector3.Distance(foot,end),Is.LessThan(.0051f));
            Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator ShoreMovementRealRoomParticipantCannotBorrowTheVirtualActorsPermission(){
            var(ex,pool,id,_,wet)=ShoreFixture(true);physics.PausePhysics();var actor=editor.Find(id);
            Assert.That(editor.BindEnvironment(id,editor.ObjectRevision(id),"",0,out var error),Is.True,error);physics.StartPhysics();
            var held=actor.transform.position;
            if(ex.Execute(ContainerRequest(ShoreCall(id,pool,wet)),out error,out _))yield return SpatialSettingsCompletion.Wait(ex.Executions,"failed");
            else Assert.That(error,Does.Contain("accepted world"));
            Assert.That(actor.transform.position,Is.EqualTo(held));Assert.That(ex.Executions.Observe()["selected"]["output"],Is.Null);Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator ShoreMovementPlansAroundAnAboveWaterSolidAndRechecksActualSteps(){
            var(ex,pool,id,_,wet)=ShoreFixture();var actor=editor.Find(id);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);wall.layer=RoomPhysicsLayers.Environment;
            wall.transform.position=new(5.65f,.64f,0);wall.transform.localScale=new(.08f,.3f,.25f);Physics.SyncTransforms();
            try{yield return ShoreCompleted(ex,ContainerRequest(ShoreCall(id,pool,wet)),actor);}
            finally{UnityEngine.Object.Destroy(wall);}
        }
        [UnityTest]public IEnumerator ShoreMovementPauseAndResumeSameFrameCannotRevivePublicJourney(){
            var(ex,pool,id,_,wet)=ShoreFixture();var actor=editor.Find(id);
            Assert.That(ex.Execute(ContainerRequest(ShoreCall(id,pool,wet)),out var error,out _),Is.True,error);
            for(int i=0;i<120&&ShoreMovement.Observe(editor,actor)==null;i++)yield return null;
            Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(1));var held=actor.transform.position;
            physics.PausePhysics();physics.StartPhysics();
            for(int i=0;i<60&&(string)ex.Executions.Observe()["selected"]["phase"]!="failed";i++)yield return null;
            Assert.That((string)ex.Executions.Observe()["selected"]["phase"],Is.EqualTo("failed"));Assert.That(ex.Executions.Observe()["selected"]["output"],Is.Null);
            Assert.That(actor.transform.position,Is.EqualTo(held));Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator ShoreMovementSelectedWaterDoesNotOverrideAnotherAvoidedLiquid(){
            var(_,pool,id,_,wet)=ShoreFixture();physics.PausePhysics();
            Assert.That(editor.CopyObject(pool,editor.ObjectRevision(pool),"Overlapping other lake",editor.Find(pool).transform.position,out var other,out var error),Is.True,error);
            Physics.SyncTransforms();physics.StartPhysics();var actor=editor.Find(id);
            Assert.That(GroundedMovement.Body(actor,out var r,out var h,out _,out error),Is.True,error);var body=new RoomUprightBody(r,h);
            Assert.That(body.FloatFoot(wet.y,.6f,out var y),Is.True);var foot=new Vector3(wet.x,y,wet.z);
            Assert.That(editor.Liquids.ShoreAdmission(actor,pool,foot,foot,body,true,out error),Is.False);
            Assert.That(error,Does.Contain("water").Or.Contain("wading"));Assert.That(editor.Read(id).waterTraversal.mode,Is.EqualTo("avoid"));yield return null;
        }
        [UnityTest]public IEnumerator ShoreMovementCannotClaimArrivalAfterSelectedLakeDrains(){
            var(_,pool,id,_,wet)=ShoreFixture();var actor=editor.Find(id);var args=(JObject)ShoreCall(id,pool,wet)["arguments"];var move=new ShoreMovement(editor,actor,args);
            try{
                Assert.That(move.Begin(args,out var error),Is.True,error);var before=actor.transform.position;
                for(int i=0;i<180&&Vector3.Distance(before,actor.transform.position)<.02f;i++){move.Tick();Assert.That(move.State(out error),Is.Not.EqualTo(RuleActionState.Failed),error);yield return null;}
                var held=actor.transform.position;PublishAquaticQuantity(pool,0);
                for(int i=0;i<120&&move.State(out error)==RuleActionState.Preparing;i++)yield return null;
                Assert.That(move.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(move.Complete(out _),Is.False);Assert.That(actor.transform.position,Is.EqualTo(held));
            }finally{move.Stop(false);}
        }
    }
}
