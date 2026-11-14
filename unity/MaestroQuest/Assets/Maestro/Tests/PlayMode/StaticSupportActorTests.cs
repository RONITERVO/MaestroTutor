// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class AvatarSpatialTests {
        [UnityTest]public IEnumerator StaticSupportActorWalksOverSeparateRealAndVirtualGroundWithoutSupportingItself(){
            WalkingScene();Terrain(Vector3.zero,0,out _);var actor=WalkingBlock(new(1,0,0));string id=editor.Identity(actor);
            var data=editor.Read(id);data.staticSupport.enabled=true;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            var native=actor.GetComponent<CreatedRoomObject>();Assert.That(native.StaticSupportReady,Is.True,native.StaticSupportIssue);
            var original=JsonUtility.ToJson(editor.Read(id).staticSupport);
            foreach(bool real in new[]{true,false}){
                if(!real){
                    var profile=new RoomEnvironmentProfile{id=Guid.NewGuid().ToString("N"),name="Virtual support actor",realCollisions=false};
                    Assert.That(editor.EditEnvironment(profile,profile.id,0,Array.Empty<string>(),out error),Is.True,error);
                    Assert.That(editor.BindEnvironment(id,editor.ObjectRevision(id),profile.id,editor.EnvironmentRevision(profile.id),out error),Is.True,error);
                }
                float goal=real?1.3f:1.6f;var before=actor.transform.position;
                var travel=new GroundedTravel(editor,actor,new(goal,0,0),1.2f);
                try{
                    Assert.That(travel.Begin(out error),Is.True,error);
                    Assert.That(native.StaticSupportReady,Is.False,"Movement owns the actor and suspends its own floor");
                    Assert.That(actor.GetComponent<RigidRoomItem>().AnimationOwned,Is.True);
                    var probe=before+Vector3.up*.1f;
                    Assert.That(world.ContainsSimulation(probe,actor),Is.True,"Another accepted floor supports this actor");
                    yield return FinishWalking(travel);
                    Assert.That(actor.transform.position.x,Is.EqualTo(goal).Within(.015f));
                    Assert.That(actor.transform.position.x-before.x,Is.GreaterThan(.28f));
                }finally{travel.Stop(false);}
                Assert.That(native.StaticSupportReady,Is.True,native.StaticSupportIssue);
                Assert.That(JsonUtility.ToJson(editor.Read(id).staticSupport),Is.EqualTo(original));
                Assert.That(editor.Read(id).motion,Is.Null,"Movement never disables or records over saved support intent");
            }
        }
    }
    public sealed partial class RoomRulesTests {
        static readonly Vector3 StaticActorPoint=new(4,.3f,4);
        static readonly Bounds StaticActorEnvelope=new(new(4,0,4),new(1,1,1));
        void StaticActorVirtualWorld(){
            RoomPhysicsLayers.Configure();
            Assert.That(physics.SetEnvironment((string)physics.ObserveEnvironment()["stateId"],false,out _,out var error),Is.True,error);
            Assert.That(physics.SetRunning(true,out error),Is.True,error);Physics.SyncTransforms();
        }
        void StaticActorLowerFloor(){
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Independent actor ground";floor.transform.SetParent(root.transform,false);
            floor.transform.position=new(4,-.3f,4);floor.transform.localScale=new(4,.2f,4);floor.layer=RoomPhysicsLayers.Environment;
            floor.AddComponent<RoomWalkableSurface>().Publish(floor.GetComponent<Collider>());Physics.SyncTransforms();
        }
        [Test]public void StaticSupportActorExemptionIsExactAndOtherSuspendedGroundStillBlocks(){
            string id=ProviderPlatform();StaticActorLowerFloor();StaticActorVirtualWorld();
            var rigid=block.GetComponent<RigidRoomItem>();var owner=new object();rigid.SetAnimationOwner(owner,true);
            try{
                Assert.That(editor.GroundDependencies(StaticActorEnvelope).Any(v=>(string)v["target"]==id),Is.True);
                Assert.That(editor.GroundDependencies(StaticActorEnvelope,id).Count,Is.Zero);
                Assert.That(editor.SpatialInputsReady(Array.Empty<string>(),StaticActorEnvelope,StaticActorEnvelope,false,id,out var error),Is.EqualTo(RoomSpatialReadiness.Ready),error);
                Assert.That(physics.ContainsSimulation(StaticActorPoint,block),Is.True);
                Assert.That(editor.SampleFootGround(block,new(4,-.2f,4),.05f,out var foot),Is.True,"Foot sampling carries the same actor-specific completeness proof");
                Assert.That(foot.point.y,Is.EqualTo(-.2f).Within(.002f));
                Assert.That(physics.ContainsSimulation(StaticActorPoint),Is.False,"An actorless observer cannot borrow the exemption");
                Assert.That(physics.ContainsSimulation(StaticActorPoint,editor.Find("maestro")),Is.False);
                string other=StaticGroundBlock(new(4,.1f,4));var obstacle=editor.Find(other).GetComponent<RigidRoomItem>();
                var otherOwner=new object();obstacle.SetAnimationOwner(otherOwner,true);
                try{
                    var missing=editor.GroundDependencies(StaticActorEnvelope,id);
                    Assert.That(missing.Count,Is.EqualTo(1));Assert.That((string)missing[0]["target"],Is.EqualTo(other));
                    Assert.That(editor.SpatialInputsReady(Array.Empty<string>(),StaticActorEnvelope,StaticActorEnvelope,false,id,out error),Is.EqualTo(RoomSpatialReadiness.Unavailable));
                    Assert.That(physics.ContainsSimulation(StaticActorPoint,block),Is.False);
                }finally{obstacle.SetAnimationOwner(otherOwner,false);}
                Assert.That(editor.Read(other).staticSupport.enabled,Is.True);
            }finally{rigid.SetAnimationOwner(owner,false);}
            Assert.That(editor.Read(id).staticSupport.enabled,Is.True);
        }
        [Test]public void StaticSupportActorExemptionCreatesNoFloorAndPreservesMixedLegacyObligations(){
            string id=ProviderPlatform();StaticActorVirtualWorld();var rigid=block.GetComponent<RigidRoomItem>();var owner=new object();
            rigid.SetAnimationOwner(owner,true);
            try{
                Assert.That(editor.GroundComplete(StaticActorEnvelope,id),Is.True,"Only a missing obligation is excluded");
                Assert.That(physics.ContainsSimulation(StaticActorPoint,block),Is.False,"Suspended self is not an accepted floor");
                Assert.That(ProviderCapture(root.transform).Sample(StaticActorPoint,.5f,.8f,out _),Is.False);
            }finally{rigid.SetAnimationOwner(owner,false);}
            // This owner also has a legacy terrain role. The ordinary-only actor
            // exception must not suppress that separate role or permit its motion.
            ProviderPlatform(terrain:true);rigid.SetAnimationOwner(owner,true);
            try{
                Assert.That(editor.GroundComplete(StaticActorEnvelope,id),Is.False);
                Assert.That(editor.GroundDependencies(StaticActorEnvelope,id).Any(v=>(string)v["target"]==id),Is.True);
                Assert.That(physics.ContainsSimulation(StaticActorPoint,block),Is.False);
            }finally{rigid.SetAnimationOwner(owner,false);}
        }
        [Test]public void StaticSupportBatchChecksEachVirtualParticipantInsteadOfSharingItsActorExemption(){
            string id=ProviderPlatform();StaticActorLowerFloor();StaticActorVirtualWorld();var other=editor.Find("maestro");
            var rigid=block.GetComponent<RigidRoomItem>();var owner=new object();rigid.SetAnimationOwner(owner,true);
            var queries=new RoomEnvironmentQueries();
            try{
                using(var batch=queries.Begin(physics)){
                    Assert.That(batch.CanSimulate(StaticActorPoint,block,block),Is.True);
                    Assert.That(batch.CanSimulate(StaticActorPoint,block,other),Is.False,"The second virtual actor still requires the first actor's missing floor");
                    Assert.That(batch.CanSimulate(StaticActorPoint,other,block),Is.False);
                }
                Assert.That(physics.SetEnvironment((string)physics.ObserveEnvironment()["stateId"],true,out _,out var error),Is.True,error);
                physics.Contains=_=>true;physics.SetSurfaces(true,"Synthetic independent physical bounds");
                block.GetComponent<RoomEnvironmentBinding>().Apply(physics,block,false);
                other.GetComponent<RoomEnvironmentBinding>().Apply(physics,other,true);physics.StartPhysics();
                using(var batch=queries.Begin(physics)){
                    Assert.That(batch.CanSimulate(StaticActorPoint,block,other),Is.True);
                    Assert.That(batch.CanSimulate(StaticActorPoint,other,block),Is.True);
                }
                other.GetComponent<RoomEnvironmentBinding>().Apply(physics,other,false);
                using(var batch=queries.Begin(physics))Assert.That(batch.CanSimulate(StaticActorPoint,block,other),Is.False);
            }finally{rigid.SetAnimationOwner(owner,false);}
            Assert.That(editor.Read(id).staticSupport.enabled,Is.True);
        }
    }
}
