// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        List<CollisionResources.Lease> FillCollisionAdmission(int spare=0)
        {
            var leases=new List<CollisionResources.Lease>();int remaining=CollisionResources.MaximumColliders-(int)CollisionResources.ObserveBudget()["colliders"]-spare;
            try{while(remaining>0){int count=Math.Min(16,remaining);leases.Add(CollisionResources.Begin(new RoomResourceOwner(editor.WorldIdentity,Guid.NewGuid().ToString("N"),"collision"),"compound",new(count,0,0)));remaining-=count;}return leases;}
            catch{foreach(var lease in leases)lease.Retire();throw;}
        }
        static CollisionRecipe SingleCollision()=>new(){shapes=new[]{new CollisionShape{id="box"}}};
        [UnityTest] public IEnumerator CollisionAdmissionKeepsSavedGeometryAndUndoWhenFullAndAllowsRetry()
        {
            string id=editor.Identity(block);Assert.That(editor.EditCollision(id,editor.ObjectRevision(id),SingleCollision(),out var error),Is.True,error);yield return null;Assert.That(editor.TryFlush(out error),Is.True,error);
            var old=block.Grab.colliders.ToArray();string saved=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision;bool undo=editor.CanUndo,redo=editor.CanRedo;
            var pending=FillCollisionAdmission();
            try{
                Assert.That(editor.EditCollision(id,editor.ObjectRevision(id),ResourceRecipe(),out error),Is.False);StringAssert.Contains("live collision geometry budget",error);
                Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.CanUndo,Is.EqualTo(undo));Assert.That(editor.CanRedo,Is.EqualTo(redo));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));CollectionAssert.AreEqual(old,block.Grab.colliders);Assert.That(old.All(c=>c),Is.True);
            }finally{foreach(var lease in pending)lease.Retire();}
            Assert.That(editor.EditCollision(id,editor.ObjectRevision(id),ResourceRecipe(),out error),Is.True,error);Assert.That(editor.Read(id).collision.shapes.Length,Is.EqualTo(4));
            editor.Undo();Assert.That(editor.Read(id).collision.shapes.Length,Is.EqualTo(1));yield return null;editor.Redo();Assert.That(editor.Read(id).collision.shapes.Length,Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator CollisionAdmissionDeferredDestructionRetainsCapacityAndPublishesSharedBudget()
        {
            root.AddComponent<RuntimeDiagnostics>();var geometry=new CollisionGeometry(SingleCollision(),null,new RoomResourceOwner(editor.WorldIdentity,"last-slot","collision"));
            var pending=FillCollisionAdmission();JObject full=null,retiring=null;
            try{
                full=CollisionFact("runtime.collisionBudget");Assert.That((int)full["colliders"],Is.EqualTo(CollisionResources.MaximumColliders));
                geometry.Dispose();retiring=CollisionOwners("last-slot").Single();Assert.That((string)retiring["phase"],Is.EqualTo("retiring"));
                Assert.Throws<RoomResourceLimitException>(()=>new CollisionGeometry(SingleCollision(),null,new RoomResourceOwner(editor.WorldIdentity,"blocked","collision")));
                yield return null;
                using var retry=new CollisionGeometry(SingleCollision(),null,new RoomResourceOwner(editor.WorldIdentity,"retry","collision"));Assert.That(retry.Colliders.Length,Is.EqualTo(1));
                string evidence=Environment.GetEnvironmentVariable("MAESTRO_COLLISION_ADMISSION_EVIDENCE");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(evidence,new JObject{["budget"]=full,["retiring"]=retiring}.ToString());
            }finally{geometry.Dispose();foreach(var lease in pending)lease.Retire();}
        }
        [UnityTest] public IEnumerator CollisionAdmissionRejectsUnexpectedActualCostAndChargesItUntilRollback()
        {
            root.AddComponent<RuntimeDiagnostics>();var pending=FillCollisionAdmission(1);var lease=CollisionResources.Begin(new RoomResourceOwner(editor.WorldIdentity,"mismatch","collision"),"compound",new(1,0,0));
            var candidate=new GameObject("Unexpected collision candidate");candidate.SetActive(false);var colliders=new Collider[]{candidate.AddComponent<BoxCollider>(),candidate.AddComponent<SphereCollider>()};
            try{
                Assert.Throws<RoomResourceLimitException>(()=>lease.Ready(colliders));Assert.That((int)CollisionFact("runtime.collisionBudget")["colliders"],Is.EqualTo(CollisionResources.MaximumColliders+1));
                UnityEngine.Object.Destroy(candidate);lease.Retire(colliders);
                Assert.Throws<RoomResourceLimitException>(()=>CollisionResources.Begin(new RoomResourceOwner(editor.WorldIdentity,"later","collision"),"compound",new(0,0,0)));
                yield return null;Assert.That(CollisionOwners("mismatch"),Is.Empty);
            }finally{if(candidate)UnityEngine.Object.Destroy(candidate);lease.Retire(colliders);foreach(var entry in pending)entry.Retire();}
        }
        [UnityTest] public IEnumerator CollisionAdmissionIncludesTerrainAndImportedReplacementWithoutChangingTheirAcceptedState()
        {
            root.AddComponent<RuntimeDiagnostics>();yield return GeometryModel(BuildingModelFixture.Create());var ex=new RoomAgentExecutor(editor);ContainerRun(ex,GeometryCall(BuildingSettings()));
            string snow=Snow(ex);yield return null;var view=editor.Find(geometryTarget).GetComponent<CreatedRoomObject>();var model=view.Model;var old=editor.Find(geometryTarget).Grab.colliders.ToArray();var field=editor.Find(snow).GetComponent<HeightFieldView>().Collision;
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;var pending=FillCollisionAdmission();
            try{
                var settings=BuildingSettings();settings.pivot="base";
                Assert.That(editor.EditModelGeometry(geometryTarget,editor.ObjectRevision(geometryTarget),settings,out var error),Is.False);StringAssert.Contains("live collision geometry budget",error);
                var height=editor.Read(snow).heightFields[0].Copy();height.heights[0]=.03f;
                Assert.That(editor.EditHeightField(snow,editor.ObjectRevision(snow),height,out error),Is.False);StringAssert.Contains("live collision geometry budget",error);
                Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(view.ModelGeometryReady,Is.True);Assert.That(view.Model,Is.SameAs(model));
                CollectionAssert.AreEqual(old,editor.Find(geometryTarget).Grab.colliders);Assert.That(editor.Find(snow).GetComponent<HeightFieldView>().Collision,Is.SameAs(field));
            }finally{foreach(var lease in pending)lease.Retire();}
            var retry=BuildingSettings();retry.pivot="base";Assert.That(editor.EditModelGeometry(geometryTarget,editor.ObjectRevision(geometryTarget),retry,out var retryError),Is.True,retryError);
        }
        [UnityTest] public IEnumerator CollisionAdmissionDormantActivationPreservesSavedTargetOnFailureAndCanRetry()
        {
            string id=editor.Identity(block);Assert.That(editor.EditCollision(id,editor.ObjectRevision(id),ResourceRecipe(),out var error),Is.True,error);string area=NativeAreaFor(id);yield return null;
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;string saved=JsonUtility.ToJson(editor.Read(id));int revision=editor.Revision;var pending=FillCollisionAdmission();
            try{var failed=editor.ActivateNativeArea(area);yield return NativeAreaCompletion(failed,false);StringAssert.Contains("live collision geometry budget",failed.Result);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(saved));}
            finally{foreach(var lease in pending)lease.Retire();}
            var retry=editor.ActivateNativeArea(area);yield return NativeAreaCompletion(retry);Assert.That(editor.Find(id),Is.Not.Null);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(saved));
        }
    }
}
