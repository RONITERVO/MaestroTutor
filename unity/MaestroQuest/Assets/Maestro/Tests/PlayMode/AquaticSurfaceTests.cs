// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        static JObject SurfaceArgs(string actor,string pool,float x=4.2f)=>new(){["target"]=actor,["mode"]="surface",["habitat"]=pool,
            ["destination"]=new JObject{["x"]=x,["z"]=0},["immersion"]=.6,["speed"]=.5,["facing"]="keep"};
        IEnumerator FinishSurfaceTravel(AquaticMovement swim,RoomItem actor){
            float deadline=Time.realtimeSinceStartup+6;var previous=actor.transform.position;
            while(Time.realtimeSinceStartup<deadline){
                var state=swim.State(out var error);
                Assert.That(Vector3.Distance(previous,actor.transform.position),Is.LessThanOrEqualTo(.0601f),"Surface adjustment must not jump");
                previous=actor.transform.position;
                if(state!=RuleActionState.Preparing){Assert.That(state,Is.EqualTo(RuleActionState.Ready),error);Assert.That(swim.Complete(out error),Is.True,error);yield break;}
                yield return null;
            }
            Assert.Fail($"Surface travel timed out: {swim.Status}, position {actor.transform.position}, result {swim.Result}");
        }
        IEnumerator SurfaceCruising(AquaticMovement swim,RoomItem actor){
            float deadline=Time.realtimeSinceStartup+6;
            while(Time.realtimeSinceStartup<deadline){
                Assert.That(swim.State(out var error),Is.EqualTo(RuleActionState.Preparing),error);
                if(swim.Status=="moving"&&actor.transform.position.x>4.005f)yield break;
                yield return null;
            }
            Assert.Fail($"Surface movement never reached its cruising phase: {swim.Status}, {actor.transform.position}");
        }
        Bounds SurfaceActorBounds(RoomItem item){var c=item.Grab.colliders[0];var bounds=c.bounds;foreach(var other in item.Grab.colliders)if(other)bounds.Encapsulate(other.bounds);return bounds;}
        [UnityTest]public IEnumerator AquaticSurfaceRisesGraduallyAndFinishesAtLiveWaterline(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SurfaceArgs(id,pool);var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);Assert.That((string)AquaticMovement.Observe(editor,actor)["mode"],Is.EqualTo("surface"));
                yield return FinishSurfaceTravel(swim,actor);Physics.SyncTransforms();
                var body=SurfaceActorBounds(actor);Assert.That((1.6f-body.min.y)/body.size.y,Is.EqualTo(.6f).Within(.06f));
                Assert.That(actor.transform.position.x,Is.EqualTo(4.2f).Within(.0051f));
                Assert.That(actor.transform.position.y,Is.GreaterThan(1.5f));
            }finally{swim.Stop(false);}
            Assert.That(RoomLocomotionBudget.Count,Is.Zero);
        }
        [UnityTest]public IEnumerator AquaticSurfaceFollowsAnAcceptedRiseWithoutJumpingOrFalseArrival(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SurfaceArgs(id,pool,4.3f);var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);yield return SurfaceCruising(swim,actor);
                PublishAquaticQuantity(pool,500000);yield return null;
                yield return FinishSurfaceTravel(swim,actor);Physics.SyncTransforms();var body=SurfaceActorBounds(actor);
                Assert.That((1.7f-body.min.y)/body.size.y,Is.EqualTo(.6f).Within(.06f));
                Assert.That(actor.transform.position.x,Is.EqualTo(4.3f).Within(.0051f));
            }finally{swim.Stop(false);}
        }
        [UnityTest]public IEnumerator AquaticSurfaceDoesNotTeleportDownToARecededNonemptyHabitat(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SurfaceArgs(id,pool,4.3f);var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);yield return SurfaceCruising(swim,actor);
                var held=actor.transform.position;PublishAquaticQuantity(pool,300000);yield return null;
                float deadline=Time.realtimeSinceStartup+6;
                while(swim.State(out error)==RuleActionState.Preparing&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(swim.State(out error),Is.EqualTo(RuleActionState.Failed),error);Assert.That(actor.transform.position,Is.EqualTo(held));
                Assert.That(editor.Read(pool).containers[0].amountMl,Is.GreaterThan(0));Assert.That(swim.Complete(out _),Is.False);
            }finally{swim.Stop(false);}
        }
        [UnityTest]public IEnumerator AquaticSurfacePauseDuringAdjustmentNeverResumesOldTravel(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SurfaceArgs(id,pool);var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);yield return SwimmingStarted(swim,actor);
                Assert.That(swim.Status,Is.EqualTo("surfaceAdjusting"));physics.PausePhysics();yield return null;
                Assert.That(swim.State(out error),Is.EqualTo(RuleActionState.Failed));var held=actor.transform.position;
                physics.StartPhysics();for(int i=0;i<8;i++){swim.Tick();yield return null;}
                Assert.That(actor.transform.position,Is.EqualTo(held));Assert.That(swim.Complete(out _),Is.False);
            }finally{swim.Stop(false);}
        }
        [UnityTest]public IEnumerator AquaticSurfaceRejectsDryStartsWithoutImplicitEntry(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);actor.transform.position=new(4,2,0);var before=actor.transform.position;
            var args=SurfaceArgs(id,pool);var swim=new AquaticMovement(editor,actor,args);
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);
                for(int i=0;i<180&&swim.State(out error)==RuleActionState.Preparing;i++)yield return null;
                Assert.That(swim.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(actor.transform.position,Is.EqualTo(before));
            }finally{swim.Stop(false);}
        }
        [UnityTest]public IEnumerator AquaticSurfaceWholeBodyStillStopsAtAnAboveWaterSolid(){
            var(pool,id)=AquaticFixture();var actor=editor.Find(id);var args=SurfaceArgs(id,pool);var swim=new AquaticMovement(editor,actor,args);
            var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.transform.SetParent(root.transform,false);roof.layer=RoomPhysicsLayers.Item;
            roof.transform.position=new(4,1.615f,0);roof.transform.localScale=new(2,.005f,2);Physics.SyncTransforms();
            try{
                Assert.That(swim.Begin(args,out var error),Is.True,error);
                for(int i=0;i<600&&swim.State(out error)==RuleActionState.Preparing;i++)yield return null;
                Assert.That(swim.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(actor.transform.position.y,Is.LessThan(1.615f));
                Assert.That(swim.Complete(out _),Is.False);
            }finally{swim.Stop(false);Object.Destroy(roof);}
        }
    }
}
