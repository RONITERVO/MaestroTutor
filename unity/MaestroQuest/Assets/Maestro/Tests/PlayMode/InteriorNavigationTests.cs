// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Avatar;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class AvatarSpatialTests {
        string interiorTarget;
        IEnumerator InteriorScene(){
            Surface(new(0,-.1f,0),new(6,.2f,6));Tutor();viewer.transform.position=new(0,1.6f,5);
            var asset=ModelLibrary.Inspect("Original offset doorway.glb",BuildingModelFixture.Create(false,-1.2f));
            var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            Assert.That(editor.CreateImportedModel(asset.Hash,out interiorTarget,out var error),Is.True,error);
            var item=editor.Find(interiorTarget);var view=item.GetComponent<CreatedRoomObject>();
            float deadline=Time.realtimeSinceStartup+8;
            while(!view.ModelGeometryReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(view.ModelGeometryReady,Is.True,view.ModelGeometryIssue);
            Assert.That(editor.SetItemPhysics(interiorTarget,new(){mode="fixed",shape="automatic",mass=1}),Is.True,editor.Status);
            var data=editor.Read(interiorTarget);data.position=Vector3.zero;data.rotation=Quaternion.identity;data.scale=1;
            data.modelGeometry=new(){scaleMode="source",pivot="source",meshCollision=true,walkable=false};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            avatar.transform.position=new(1.8f,0,-1);Physics.SyncTransforms();Ready();
            Assert.That(navigation.Prepare(.25f,1.7f,out error,editor.Find("maestro")),Is.True,error);yield return NavigationBuilt();
        }
        [UnityTest]public IEnumerator InteriorNavigationUsesAnOffsetDoorwayInAnImportedNonWalkableWall(){
            yield return InteriorScene();
            var collision=editor.Find(interiorTarget).Grab.colliders.Single();
            Assert.That(collision,Is.TypeOf<MeshCollider>());Assert.That(((MeshCollider)collision).convex,Is.False);
            yield return SolidRoute(new(1.8f,0,-1),new(1.8f,0,1),points=>{
                Assert.That(points.Any(p=>p.x<-.8f),Is.True,"The route must find the offset doorway inside the mesh bounds");
                var motor=new RoomGroundMotor();
                for(int i=1;i<points.Length;i++)Assert.That(motor.ClearExact(points[i-1],points[i],.25f,1.7f,1<<RoomPhysicsLayers.Item,c=>c==collision),Is.True,"No accepted segment may cross the wall");
            });
        }

        [UnityTest]public IEnumerator InteriorNavigationActualWalkingUsesTheDoorAndKeepsEveryBodyStepClear(){
            yield return InteriorScene();var collision=editor.Find(interiorTarget).Grab.colliders.Single();
            var walk=new GroundedTravel(editor,editor.Find("maestro"),new(1.8f,0,1),1.2f);
            var motor=new RoomGroundMotor();float nearestX=1.8f;var previous=avatar.transform.position;
            try{
                Assert.That(walk.Begin(out var error),Is.True,error);var state=RuleActionState.Preparing;
                for(int i=0;i<850&&state==RuleActionState.Preparing;i++){
                    state=walk.State(out error);var current=avatar.transform.position;nearestX=Mathf.Min(nearestX,current.x);
                    Assert.That(motor.ClearExact(previous,current,.25f,1.7f,1<<RoomPhysicsLayers.Item,c=>c==collision),Is.True,"Actual walking cannot clip through the imported wall");
                    Assert.That(current.y,Is.EqualTo(0).Within(.02));previous=current;yield return null;
                }
                Assert.That(state,Is.EqualTo(RuleActionState.Ready),error+" at "+avatar.transform.position.ToString("F5"));Assert.That(walk.Complete(out error),Is.True,error);
                Assert.That(nearestX,Is.LessThan(-.8f));Assert.That(Vector3.Distance(avatar.transform.position,new(1.8f,0,1)),Is.LessThan(.025f));
            }finally{walk.Stop(false);}
        }
        [UnityTest]public IEnumerator InteriorNavigationRejectsAnActorWiderThanTheDoor(){
            yield return InteriorScene();Assert.That(navigation.Prepare(.65f,1.7f,out var error,editor.Find("maestro")),Is.True,error);yield return NavigationBuilt();
            yield return SolidRoute(new(1.8f,0,-1),new(1.8f,0,1),points=>Assert.That(points,Is.Empty),false);
            Assert.That(navigation.Prepare(.2f,1.7f,out error,editor.Find("maestro")),Is.True,error);yield return NavigationBuilt();
            yield return SolidRoute(new(1.8f,0,-1),new(1.8f,0,1),points=>Assert.That(points.Length,Is.GreaterThan(2)));
        }
        [UnityTest]public IEnumerator InteriorNavigationRetainsStructureWithoutPromotingItToGround(){
            yield return InteriorScene();var collision=editor.Find(interiorTarget).Grab.colliders.Single();
            var accepted=NavigationField<RoomNavigationGeometry>("accepted");
            Assert.That(accepted.Ground.Contains(collision),Is.False);Assert.That(accepted.BuildSources.Single(x=>x.component==collision).area,Is.EqualTo(1));
            Assert.That(navigation.Sample(new(-2,3,0),.1f,out _),Is.False,"A non-walkable wall roof must not become support");
            Assert.That(NavigationRetained(interiorTarget),Is.True,"The installed map owns its canonical structural dependency");
            world.PausePhysics();Assert.That(navigation.Ready,Is.False);Assert.That(NavigationRetained(interiorTarget),Is.False);yield return null;
        }
        [UnityTest]public IEnumerator InteriorNavigationInvalidatesTheMapWhenTheDoorwayMoves(){
            yield return InteriorScene();uint before=navigation.BuildRevision;var item=editor.Find(interiorTarget);
            item.transform.rotation=Quaternion.Euler(0,180,0);item.GetComponent<RigidRoomItem>().Teleported();Physics.SyncTransforms();
            Assert.That(navigation.Path(new(1.8f,0,-1),new(1.8f,0,1),new UnityEngine.AI.NavMeshPath()),Is.False,"The old doorway must disappear before another route query");
            Assert.That(navigation.PathsPending,Is.True);yield return NavigationBuilt();Assert.That(navigation.BuildRevision,Is.GreaterThan(before));
            yield return SolidRoute(new(1.8f,0,-1),new(1.8f,0,1),points=>{
                bool crossed=false;
                for(int i=1;i<points.Length;i++)if(points[i-1].z<0&&points[i].z>=0){
                    float t=-points[i-1].z/(points[i].z-points[i-1].z);float x=Mathf.Lerp(points[i-1].x,points[i].x,t);
                    Assert.That(x,Is.InRange(.85f,1.55f),"Only the rotated doorway is clear");crossed=true;
                }
                Assert.That(crossed,Is.True);
            });
        }
        [UnityTest]public IEnumerator InteriorNavigationUsesTheSameActorExclusionAndRequiresRealGround(){
            yield return InteriorScene();var item=editor.Find(interiorTarget);var collision=item.Grab.colliders.Single();
            Assert.That(navigation.Prepare(.25f,1.7f,out var error,item),Is.True,error);yield return NavigationBuilt();
            Assert.That(NavigationField<RoomNavigationGeometry>("accepted").BuildSources.Any(x=>x.component==collision),Is.False,"An actor cannot become its own navigation obstacle");
            foreach(var c in physicalRoot.GetComponentsInChildren<Collider>())if(c.gameObject.layer==RoomPhysicsLayers.Scanned)c.enabled=false;
            var capture=new RoomNavigationGeometry();
            Assert.That(capture.Capture(physicalRoot.transform,true,null,_=>true),Is.False,"A wall alone cannot satisfy the accepted-ground prerequisite");
            Assert.That(capture.Ground.Contains(collision),Is.False);capture.Clear();yield return null;
        }

        [UnityTest]public IEnumerator InteriorNavigationFollowingUsesTheSameDoorwayAndCanStop(){
            yield return InteriorScene();viewer.transform.position=new(1.8f,1.6f,2);editor.SetAvatarMovement(.8f,1.2f);
            var collision=editor.Find(interiorTarget).Grab.colliders.Single();var motor=new RoomGroundMotor();var previous=avatar.transform.position;float nearestX=previous.x;
            Assert.That(motion.Begin("interior follow",AvatarSpatialMode.Follow,out var error),Is.True,error);
            try{
                for(int i=0;i<900&&avatar.transform.position.z<1.1f;i++){
                    yield return null;var current=avatar.transform.position;nearestX=Mathf.Min(nearestX,current.x);
                    Assert.That(motor.ClearExact(previous,current,.25f,1.7f,1<<RoomPhysicsLayers.Item,c=>c==collision),Is.True);
                    previous=current;
                }
                Assert.That(avatar.transform.position.z,Is.GreaterThan(1.1f),motion.Status+" at "+avatar.transform.position);
                Assert.That(nearestX,Is.LessThan(-.8f));Assert.That(motion.OwnedBy("interior follow"),Is.True);
            }finally{motion.Stop();}
            Assert.That(NavigationRetained(interiorTarget),Is.False,"Stopping the map owner must release its structural dependency");
            Assert.That(NavigationField<RoomNavigationGeometry>("accepted").BuildSources,Is.Empty);
            var stopped=avatar.transform.position;for(int i=0;i<4;i++)yield return null;Assert.That(avatar.transform.position,Is.EqualTo(stopped));
        }
        [UnityTest]public IEnumerator InteriorNavigationSharedObjectActionCompletesForACreatedActor(){
            yield return InteriorScene();avatar.transform.position=new(2,0,2);var item=WalkingBlock(new(1.8f,0,-1));string id=editor.Identity(item);
            var actions=new RoomRuleActions(editor,authoring);var scheduler=new RuleScheduler(actions);
            var call=new JObject{["id"]="object.navigate",["version"]=1,["arguments"]=new JObject{["target"]=id,["mode"]="walk",["destination"]=new JObject{["x"]=1.8,["y"]=0,["z"]=1},["speed"]=1.2}};
            Assert.That(scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);
            try{
                for(int i=0;i<1000;i++){actions.Tick();scheduler.Tick(Time.unscaledTime);yield return null;if(scheduler.ObserveInvocations(run).ToString().Contains("completed"))break;}
                var result=scheduler.ObserveInvocations(run).ToString();Assert.That(result,Does.Contain("completed"),result);
                Assert.That(item.transform.position.x,Is.EqualTo(1.8f).Within(.02));Assert.That(item.transform.position.z,Is.EqualTo(1).Within(.02));
                Assert.That(editor.Ownership.Covers(run,new[]{new BehaviourCatalog.Claim(id,"locomotion")}),Is.False);
            }finally{scheduler.Suspend(true);actions.Stop(run,false);}
        }
        [UnityTest]public IEnumerator InteriorNavigationVirtualActorFindsTheDoorBelowTheRealFloor(){
            yield return InteriorScene();world.PausePhysics();Terrain(new(0,-2,0),0,out _);
            var profile=new RoomEnvironmentProfile{id=Guid.NewGuid().ToString("N"),name="Virtual interior",realCollisions=false};
            Assert.That(editor.EditEnvironment(profile,profile.id,0,Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(editor.BindEnvironment("maestro",editor.ObjectRevision("maestro"),profile.id,editor.EnvironmentRevision(profile.id),out error),Is.True,error);
            var structure=editor.Find(interiorTarget);structure.transform.position=Vector3.down*2;structure.GetComponent<RigidRoomItem>().Teleported();
            avatar.transform.position=new(1.5f,-2,-1);Physics.SyncTransforms();world.StartPhysics();
            Assert.That(navigation.Prepare(.25f,1.7f,out error,editor.Find("maestro")),Is.True,error);yield return NavigationBuilt();
            yield return SolidRoute(new(1.5f,-2,-1),new(1.5f,-2,1),points=>{
                Assert.That(points.Any(p=>p.x<-.8f),Is.True);Assert.That(points.All(p=>Mathf.Abs(p.y+2)<.02),Is.True);
            });
        }
    }
}
