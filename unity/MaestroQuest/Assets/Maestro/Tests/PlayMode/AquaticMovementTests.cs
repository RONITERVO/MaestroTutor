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
        (string pool,string actor) AquaticFixture(){
            var values=MediumFixture(.1f);
            Assert.That(editor.SetItemPhysics(values.prop,new(){mode="fixed",shape="box",mass=1}),Is.True);
            root.GetComponent<RoomInteraction>().Viewer=leftAnchor.transform;
            leftAnchor.transform.position=new(0,1.6f,0);Physics.SyncTransforms();return values;
        }
        static JObject SwimArgs(string actor,string pool,Vector3 goal,string facing="keep")=>new(){
            ["target"]=actor,["mode"]="swim",["habitat"]=pool,["destination"]=new JObject{["x"]=goal.x,["y"]=goal.y,["z"]=goal.z},["speed"]=1.2,["facing"]=facing
        };
        IEnumerator FinishSwimming(AquaticMovement swim,RoomItem actor,int frames=400){
            var previous=actor.transform.position;
            for(int i=0;i<frames;i++){
                var state=swim.State(out var error);
                Assert.That(Vector3.Distance(previous,actor.transform.position),Is.LessThanOrEqualTo(.0601f),"No elapsed waiting time may become a jump");
                previous=actor.transform.position;
                if(state!=RuleActionState.Preparing){Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);Assert.That(swim.Complete(out error),Is.True,error);yield break;}
                yield return null;
            }
            Assert.Fail("Swimming did not complete");
        }
        [UnityTest]public IEnumerator AquaticMovementArrivesGraduallyAndSharesNativeStatusAndPlacement(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var goal=new Vector3(4.15f,1.45f,.1f);
            var args=SwimArgs(id,pool,goal);var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);
                Assert.That((string)AquaticMovement.Observe(editor,actor)["mode"],Is.EqualTo("swim"));
                yield return FinishSwimming(swim,actor);
                Assert.That(Vector3.Distance(actor.transform.position,goal),Is.LessThan(.0051f));
                Assert.That((float)swim.Result["distance"],Is.GreaterThan(.15f));
                Assert.That(Vector3.Distance(editor.Read(id).position,goal),Is.LessThan(.0051f));
            }finally{swim.Stop(false);}
            Assert.That(AquaticMovement.Observe(editor,actor),Is.Null);Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator AquaticMovementCannotCrossSurfaceOrBorrowAnUnchosenHabitat(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var before=actor.transform.position;
            var args=SwimArgs(id,pool,new(4,1.8f,0));var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);
                for(int i=0;i<600&&swim.State(out error)==RuleActionState.Preparing;i++)yield return null;
                Assert.That(swim.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(actor.transform.position,Is.EqualTo(before));Assert.That(swim.Complete(out _),Is.False);
            }finally{swim.Stop(false);}
        }
        [UnityTest]public IEnumerator AquaticMovementStopsOnPauseAndNeverResumesElapsedTravel(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SwimArgs(id,pool,new(4.2f,1.45f,0));var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);swim.Tick();yield return null;physics.PausePhysics();
                Assert.That(swim.State(out error),Is.EqualTo(RuleActionState.Failed));var held=actor.transform.position;
                physics.StartPhysics();for(int i=0;i<8;i++){swim.Tick();yield return null;}
                Assert.That(actor.transform.position,Is.EqualTo(held));
            }finally{swim.Stop(false);}
        }
        [UnityTest]public IEnumerator AquaticMovementSharesTheFourActionBudgetWithWalking(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SwimArgs(id,pool,new(4.2f,1.45f,0));var swim=new AquaticMovement(editor,actor,args);
            var others=new[]{new object(),new object(),new object()};
            try{
                foreach(var owner in others)Assert.That(RoomLocomotionBudget.Acquire(owner,out _),Is.True);
                Assert.That(swim.Begin(args,out var error),Is.True,error);
                Assert.That(GroundedMovement.CanBegin(editor,block,out error),Is.False);Assert.That(error,Does.Contain("four"));
                swim.Stop(false);Assert.That(RoomLocomotionBudget.Count,Is.EqualTo(3));yield return null;
            }finally{swim.Stop(false);foreach(var owner in others)RoomLocomotionBudget.Release(owner);}
            Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator AquaticMovementVirtualActorIgnoresOnlyRealGeometry(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);
            physics.PausePhysics();var profile=new RoomEnvironmentProfile{id=Guid.NewGuid().ToString("N"),name="Aquatic virtual world",realCollisions=false};
            Assert.That(editor.EditEnvironment(profile,profile.id,0,Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(editor.BindEnvironment(id,editor.ObjectRevision(id),profile.id,editor.EnvironmentRevision(profile.id),out error),Is.True,error);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);wall.layer=RoomPhysicsLayers.Scanned;wall.transform.position=new(4.1f,1.4f,0);wall.transform.localScale=new(.01f,1,1);Physics.SyncTransforms();
            physics.Contains=p=>p.x<4.05f;physics.StartPhysics();
            var args=SwimArgs(id,pool,new(4.2f,1.45f,0));var swim=new AquaticMovement(editor,actor,args);
            try{Assert.That(swim.Begin(args,out error),Is.True,error);yield return FinishSwimming(swim,actor);}
            finally{swim.Stop(false);UnityEngine.Object.Destroy(wall);}
            Assert.That(actor.transform.position.x,Is.GreaterThan(4.1f));
        }
        [UnityTest]public IEnumerator AquaticMovementRejectsDisabledRootCollisionAndUnknownGait(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SwimArgs(id,pool,new(4.1f,1.45f,0));
            foreach(var c in actor.Grab.colliders)c.enabled=false;var swim=new AquaticMovement(editor,actor,args);
            try{Assert.That(swim.Begin(args,out _),Is.False);}finally{swim.Stop(false);}
            foreach(var c in actor.Grab.colliders)c.enabled=true;
            args["gait"]=new JObject{["source"]="embedded",["modelHash"]=new string('a',64),["anchorNode"]=0,["rate"]=1,["clip"]=0};
            Assert.That(AquaticMovement.CanBegin(editor,actor,args,out var error),Is.False);Assert.That(error,Does.Contain("model"));yield return null;
        }
        [UnityTest]public IEnumerator AquaticMovementDrainEndsAtLastAcceptedPoseWithoutSwitchingHabitat(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SwimArgs(id,pool,new(4.2f,1.45f,0));var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);swim.Tick();yield return null;
                var c=editor.Read(pool).containers[0];c.amountMl=0;
                yield return ContainerAction(new RoomAgentExecutor(editor),ContainerCall(pool,c));
                var held=actor.transform.position;
                Assert.That(swim.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(error,Does.Contain("liquid").Or.Contain("dependencies changed"));
                for(int i=0;i<5;i++){swim.Tick();yield return null;}Assert.That(actor.transform.position,Is.EqualTo(held));
            }finally{swim.Stop(false);}
        }
        [UnityTest]public IEnumerator AquaticMovementRealParticipantCannotCrossPhysicalRoomBounds(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);physics.Contains=p=>p.x<4.05f;
            var args=SwimArgs(id,pool,new(4.2f,1.45f,0));var swim=new AquaticMovement(editor,actor,args);var initial=actor.transform.position;
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);
                for(int i=0;i<600&&swim.State(out error)==RuleActionState.Preparing;i++)yield return null;
                Assert.That(swim.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(actor.transform.position,Is.EqualTo(initial));
            }finally{swim.Stop(false);}
        }
        IEnumerator SwimmingStarted(AquaticMovement swim,RoomItem actor){
            var start=actor.transform.position;
            for(int i=0;i<120;i++){
                Assert.That(swim.State(out var error),Is.EqualTo(RuleActionState.Preparing),error);
                if(Vector3.Distance(start,actor.transform.position)>.005f)yield break;
                yield return null;
            }
            Assert.Fail("The swimming fixture did not acquire its inputs and start");
        }
        void PublishAquaticQuantity(string pool,double amount){
            var before=editor.Read(pool).containers[0];var after=before.Copy();after.amountMl=amount;
            int revision=editor.ObjectRevision(pool);
            Assert.That(editor.CommitLiquidPour(new System.Collections.Generic.Dictionary<string,RoomContainer>{{pool,before}},new System.Collections.Generic.Dictionary<string,RoomContainer>{{pool,after}},out var error),Is.True,error);
            Assert.That(editor.ObjectRevision(pool),Is.GreaterThan(revision));
        }
        [UnityTest]public IEnumerator AquaticMovementContinuesAcrossAcceptedLiquidCheckpoint(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var goal=new Vector3(4.3f,1.45f,0);
            var args=SwimArgs(id,pool,goal);var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);yield return SwimmingStarted(swim,actor);
                PublishAquaticQuantity(pool,410000);
                yield return FinishSwimming(swim,actor);
                Assert.That(Vector3.Distance(actor.transform.position,goal),Is.LessThan(.0051f));
            }finally{swim.Stop(false);}
            yield return LiquidSaved();
        }
        [UnityTest]public IEnumerator AquaticMovementRechecksDrainedCheckpointBeforeAnyFurtherStep(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SwimArgs(id,pool,new(4.3f,1.45f,0));var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);yield return SwimmingStarted(swim,actor);
                var held=actor.transform.position;PublishAquaticQuantity(pool,0);
                for(int i=0;i<120&&swim.State(out error)==RuleActionState.Preparing;i++){Assert.That(actor.transform.position,Is.EqualTo(held));yield return null;}
                Assert.That(swim.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(swim.Complete(out _),Is.False);
                Assert.That(actor.transform.position,Is.EqualTo(held));
            }finally{swim.Stop(false);}
            yield return LiquidSaved();
        }
        [UnityTest]public IEnumerator AquaticMovementFreshInputsDoNotPermitCrossingANewSolid(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SwimArgs(id,pool,new(4.3f,1.45f,0));var swim=new AquaticMovement(editor,actor,args);
            GameObject wall=null;
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);yield return SwimmingStarted(swim,actor);
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);wall.layer=RoomPhysicsLayers.Environment;
                wall.transform.position=new(4.15f,1.4f,0);wall.transform.localScale=new(.02f,1,1.2f);Physics.SyncTransforms();
                PublishAquaticQuantity(pool,410000);
                for(int i=0;i<600&&swim.State(out error)==RuleActionState.Preparing;i++){Assert.That(actor.transform.position.x,Is.LessThan(4.14f));yield return null;}
                Assert.That(swim.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(actor.transform.position.x,Is.LessThan(4.14f));
            }finally{swim.Stop(false);if(wall)UnityEngine.Object.Destroy(wall);}
            yield return LiquidSaved();
        }
        [UnityTest]public IEnumerator AquaticMovementCheckpointRefreshCannotReviveAnEditedActor(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SwimArgs(id,pool,new(4.3f,1.45f,0));var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);yield return SwimmingStarted(swim,actor);
                PublishAquaticQuantity(pool,410000);
                var held=actor.transform.localPosition+Vector3.forward*.01f;actor.transform.localPosition=held;yield return null;
                Assert.That(swim.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(error,Does.Contain("actor changed"));
                for(int i=0;i<8;i++){swim.Tick();yield return null;}Assert.That(actor.transform.localPosition,Is.EqualTo(held));
            }finally{swim.Stop(false);}
            yield return LiquidSaved();
        }
    }
}
