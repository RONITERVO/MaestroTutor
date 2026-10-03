// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject CollisionCall(string target,CollisionRecipe recipe=null)=>new() {["id"]="object.collision.edit",["version"]=1,["arguments"]=new JObject {["target"]=target,["revision"]=editor.ObjectRevision(target),["collision"]=JObject.Parse(JsonUtility.ToJson(recipe??JsonUtility.FromJson<CollisionRecipe>(JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/collision-contract.json")))[0]["collision"].ToString())))}};
        string CollisionCup(){Assert.That(editor.CreateRecipe("Physical cup",new Vector3(2,.111f,0),1,LatheCup(),out var target,out var error),Is.True,error);return target;}
        [UnityTest] public IEnumerator SharedCollisionEditsPersistReadBackUndoAndReleaseOwnedMeshes(){
            string target=CollisionCup();var item=editor.Find(target);var executor=new RoomAgentExecutor(editor);var result=RecipeRun(executor,CollisionCall(target));yield return null;
            Assert.That((string)result["selected"]["phase"],Is.EqualTo("completed"));Assert.That(item.Grab.colliders.Count,Is.EqualTo(13));Assert.That(item.Grab.colliders.All(c=>c.enabled&&c.attachedRigidbody==item.GetComponent<Rigidbody>()),Is.True);
            var meshes=item.Grab.colliders.OfType<MeshCollider>().Select(c=>c.sharedMesh).ToArray();
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==target).collision.Pieces,Is.EqualTo(13));
            var args=new JObject {["target"]=target,["revision"]=editor.ObjectRevision(target),["index"]=1};Assert.That(BehaviourCatalog.TryRead("object.collision.shape",1,args,new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That(fact.Characters,Is.LessThanOrEqualTo(1024));Assert.That((string)((JObject)fact.Value)["shape"]["id"],Is.EqualTo("Wall"));
            var copy=editor.Read(target).Copy();copy.collision.shapes[0].id="changed";Assert.That(editor.Read(target).collision.shapes[0].id,Is.EqualTo("Floor"));
            editor.Undo();yield return null;Assert.That(item.Grab.colliders.Count,Is.EqualTo(1));Assert.That(item.Grab.colliders[0],Is.TypeOf<BoxCollider>());Assert.That(meshes.All(m=>m==null),Is.True);Assert.That(BehaviourCatalog.TryRead("object.collision.shape",1,args,new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
            editor.Redo();yield return null;Assert.That(item.Grab.colliders.Count,Is.EqualTo(13));
            RecipeRun(executor,CollisionCall(target,new CollisionRecipe()));yield return null;Assert.That(item.Grab.colliders.Count,Is.EqualTo(1));Assert.That(editor.Read(target).collision,Is.Null);
        }
        [UnityTest] public IEnumerator CompoundCupCatchesBallInsideWallsAndCanFallAsOneRigidBody(){
            RoomPhysicsLayers.Configure();string target=CollisionCup();RecipeRun(new RoomAgentExecutor(editor),CollisionCall(target));var cup=editor.Find(target);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform,false);floor.transform.position=new Vector3(2,-.1f,0);floor.transform.localScale=new Vector3(4,.2f,4);floor.layer=RoomPhysicsLayers.Scanned;
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball,"Cup ball",new Vector3(2,.6f,0),.3f,Color.white,out var ballId,out var error),Is.True,error);
            Assert.That(editor.ConfigurePhysics(ballId,editor.ObjectRevision(ballId),new ObjectPhysicsSettings {mode="solid",shape="automatic",mass=.1f},out error),Is.True,error);var ball=editor.Find(ballId);var rb=ball.GetComponent<Rigidbody>();
            Physics.SyncTransforms();physics.SetSurfaces(true,"Synthetic test floor");physics.StartPhysics();
            for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();
            Assert.That(rb.position.y,Is.InRange(.035f,.07f),"The ball must rest on the inside floor, below the mouth");
            Assert.That(ball.GetComponent<RigidRoomItem>().Launch(Vector3.right*1.5f,Vector3.zero),Is.True);
            for(int i=0;i<35;i++){yield return new WaitForFixedUpdate();Assert.That(Mathf.Abs(rb.position.x-2),Is.LessThan(.09f),"Ball escaped through the wall");}
            Assert.That(editor.DeleteObject(ballId,out error),Is.True,error);
            Assert.That(editor.ConfigurePhysics(target,editor.ObjectRevision(target),new ObjectPhysicsSettings {mode="solid",shape="automatic",mass=.5f},out error),Is.True,error);
            Assert.That(cup.GetComponent<RigidRoomItem>().Launch(new Vector3(.2f,2,0),Vector3.zero),Is.True);
            float peak=0;for(int i=0;i<140;i++){yield return new WaitForFixedUpdate();peak=Mathf.Max(peak,cup.transform.position.y);Assert.That(cup.transform.position.y,Is.GreaterThan(.09f),"Compound cup penetrated floor");}
            Assert.That(peak,Is.GreaterThan(.2f));Assert.That(cup.transform.position.y,Is.LessThan(.15f));
        }
        [UnityTest] public IEnumerator CompoundHandlesGrabAndStaleHeldOrFailedEditsDoNotChangeCollision(){
            string target=CollisionCup();var executor=new RoomAgentExecutor(editor);var stale=CollisionCall(target);RecipeRun(executor,stale);var item=editor.Find(target);
            Assert.That(executor.Execute(ObjectEditRequest(stale),out _,out _),Is.False);
            var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);Assert.That(item.Grab.isSelected,Is.True);
            Assert.That(executor.Execute(ObjectEditRequest(CollisionCall(target,new CollisionRecipe())),out _,out _),Is.False);Assert.That(item.Grab.colliders.Count,Is.EqualTo(13));
            hand.selectInput.manualPerformed=false;hand.selectInput.manualValue=0;manager.SelectExit((IXRSelectInteractor)hand,item.Grab);yield return null;Assert.That(item.Grab.isSelected,Is.False,"Released controller must stop selecting the cup wall");
            Assert.That(editor.ConfigurePhysics(target,editor.ObjectRevision(target),new ObjectPhysicsSettings {mode="fixed",shape="sphere",mass=.5f},out var error),Is.True,error);Assert.That(item.Grab.colliders.Single(),Is.TypeOf<SphereCollider>());Assert.That(editor.Read(target).collision.Pieces,Is.EqualTo(13));
            Assert.That(editor.ConfigurePhysics(target,editor.ObjectRevision(target),new ObjectPhysicsSettings {mode="fixed",shape="automatic",mass=.5f},out error),Is.True,error);Assert.That(item.Grab.colliders.Count,Is.EqualTo(13));yield return null;
            string saved=JsonUtility.ToJson(editor.Read(target));var meshes=item.Grab.colliders.ToArray();string obstacle=Path.Combine(directory,"room.v6.json.pending");Directory.CreateDirectory(obstacle);
            try{Assert.That(executor.Execute(ObjectEditRequest(CollisionCall(target,new CollisionRecipe())),out _,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(saved));Assert.That(item.Grab.colliders,Is.EqualTo(meshes));}finally{Directory.Delete(obstacle);}
        }
    }
}
