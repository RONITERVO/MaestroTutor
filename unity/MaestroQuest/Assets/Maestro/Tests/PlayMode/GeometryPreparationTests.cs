// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator GeometryPreparationTerrainSaveFailureRetiresOnlyDetachedCandidate()
        {
            root.AddComponent<RuntimeDiagnostics>();string id=Snow(new RoomAgentExecutor(editor));var view=editor.Find(id).GetComponent<HeightFieldView>();
            var old=view.Collision;var mesh=((MeshCollider)old).sharedMesh;var vertices=mesh.vertices;var lease=CollisionOwners(id).Single(o=>(string)o["kind"]=="terrain");
            string before=JsonUtility.ToJson(editor.Snapshot());var changed=editor.Read(id).heightFields[0].Copy();changed.heights[0]+=.01f;
            string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try{Assert.IsFalse(editor.EditHeightField(id,editor.ObjectRevision(id),changed,out _));}finally{Directory.Delete(pending);}
            var held=CollisionOwners(id).Where(o=>(string)o["kind"]=="terrain").ToArray();Assert.AreEqual(2,held.Length,"Terrain must be allocated before persistence, then retired on save refusal");
            Assert.AreEqual(1,held.Count(o=>(string)o["phase"]=="retiring"));Assert.IsTrue(JToken.DeepEquals(lease,held.Single(o=>(string)o["phase"]=="ready")));
            Assert.AreEqual(before,JsonUtility.ToJson(editor.Snapshot()));Assert.AreSame(old,view.Collision);Assert.AreSame(mesh,((MeshCollider)view.Collision).sharedMesh);CollectionAssert.AreEqual(vertices,mesh.vertices);
            yield return null;Assert.IsTrue(JToken.DeepEquals(lease,CollisionOwners(id).Single(o=>(string)o["kind"]=="terrain")));
        }
        [UnityTest] public IEnumerator GeometryPreparationCompoundSaveFailureRetiresOnlyDetachedCandidate()
        {
            root.AddComponent<RuntimeDiagnostics>();string id=editor.Identity(block);Assert.IsTrue(editor.EditCollision(id,editor.ObjectRevision(id),ResourceRecipe(),out var error),error);
            var original=block.Grab.colliders.ToArray();var lease=CollisionOwners(id).Single();string before=JsonUtility.ToJson(editor.Snapshot());var changed=ResourceRecipe();changed.shapes[0].size*=1.1f;
            string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try{Assert.IsFalse(editor.EditCollision(id,editor.ObjectRevision(id),changed,out _));}finally{Directory.Delete(pending);}
            var held=CollisionOwners(id);Assert.AreEqual(2,held.Length,"Compound geometry must be allocated before persistence, then retired on save refusal");Assert.AreEqual(1,held.Count(o=>(string)o["phase"]=="retiring"));
            Assert.IsTrue(JToken.DeepEquals(lease,held.Single(o=>(string)o["phase"]=="ready")));Assert.AreEqual(before,JsonUtility.ToJson(editor.Snapshot()));CollectionAssert.AreEqual(original,block.Grab.colliders);
            yield return null;Assert.IsTrue(JToken.DeepEquals(lease,CollisionOwners(id).Single()));
        }
        [UnityTest] public IEnumerator GeometryPreparationAcceptedTerrainReplacesMeshAndPublishesOnlyNewSurface()
        {
            root.AddComponent<RuntimeDiagnostics>();string id=Snow(new RoomAgentExecutor(editor));var item=editor.Find(id);var view=item.GetComponent<HeightFieldView>();
            var old=view.Collision;var mesh=((MeshCollider)old).sharedMesh;var vertices=mesh.vertices;var previous=view.Surface;var before=CollisionOwners(id).Single(o=>(string)o["kind"]=="terrain");
            var changed=editor.Read(id).heightFields[0].Copy();changed.heights[0]+=.01f;Assert.IsTrue(editor.EditHeightField(id,editor.ObjectRevision(id),changed,out var error),error);
            Assert.AreNotSame(mesh,((MeshCollider)view.Collision).sharedMesh,"Never refill the accepted mesh in place");CollectionAssert.AreEqual(vertices,mesh.vertices);Assert.IsFalse(previous.gameObject.activeSelf);
            Assert.AreNotSame(old,view.Collision);Assert.IsTrue(item.Grab.colliders.Contains(view.Collision));Assert.AreSame(view.Collision,view.Surface.GetComponent<RoomWalkableSurface>().Collision);
            Assert.AreSame(((MeshCollider)view.Collision).sharedMesh,view.Surface.GetComponent<AcousticSurface>().Mesh);
            var held=CollisionOwners(id).Where(o=>(string)o["kind"]=="terrain").ToArray();Assert.AreEqual(2,held.Length);Assert.AreEqual("retiring",(string)held.Single(o=>(string)o["leaseId"]==(string)before["leaseId"])["phase"]);
            yield return null;Assert.IsFalse(mesh);Assert.IsFalse(old);Assert.AreEqual(1,CollisionOwners(id).Count(o=>(string)o["kind"]=="terrain"));
        }
        [UnityTest] public IEnumerator GeometryPreparationNewEntityReservesBothKindsWithoutPublishingAndCancellationDrains()
        {
            root.AddComponent<RuntimeDiagnostics>();string original=Snow(new RoomAgentExecutor(editor));var data=editor.Read(original);data.id=Guid.NewGuid().ToString("N");data.name="Prepared terrain with compound";data.collision=ResourceRecipe();
            var journal=new RoomJournal(editor.Snapshot());string before=JsonUtility.ToJson(editor.Snapshot());Assert.IsTrue(journal.Prepare(new[]{data},Array.Empty<string>(),out var edit,out var error),error);
            using(edit){using(var prepared=RoomEditPreparation.TryCreate(editor,edit,out error)){
                Assert.IsNotNull(prepared,error);var owned=CollisionOwners(data.id);Assert.AreEqual(2,owned.Length,"New entities need reservations before their runtime components exist");
                Assert.AreEqual(0,owned.Sum(o=>(int)o["activeColliders"]));Assert.IsNull(editor.Find(data.id));Assert.AreEqual(before,JsonUtility.ToJson(editor.Snapshot()));
            }}
            yield return null;Assert.IsEmpty(CollisionOwners(data.id));Assert.IsNull(editor.Read(data.id));
        }
        [UnityTest] public IEnumerator GeometryPreparationNewCompositeAcceptsSavesAndRestoresThroughUndoRedo()
        {
            root.AddComponent<RuntimeDiagnostics>();string source=Snow(new RoomAgentExecutor(editor));var data=editor.Read(source);data.id=Guid.NewGuid().ToString("N");data.name="Compound hill";data.position+=Vector3.right;data.collision=ResourceRecipe();
            Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),error);Assert.IsTrue(editor.TryFlush(out error),error);
            var owned=CollisionOwners(data.id);Assert.AreEqual(2,owned.Length);Assert.IsTrue(owned.All(o=>(string)o["phase"]=="ready"));Assert.AreEqual(12,owned.Sum(o=>(int)o["activeColliders"]));
            var item=editor.Find(data.id);Assert.AreEqual(12,item.Grab.colliders.Count);var view=item.GetComponent<HeightFieldView>();Assert.AreSame(view.Collision,view.Surface.GetComponent<RoomWalkableSurface>().Collision);
            Assert.AreEqual(JsonUtility.ToJson(data),JsonUtility.ToJson(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==data.id)));
            editor.Undo();Assert.IsNull(editor.Read(data.id));Assert.IsTrue(CollisionOwners(data.id).All(o=>(int)o["activeColliders"]==0));yield return null;Assert.IsEmpty(CollisionOwners(data.id));
            editor.Redo();Assert.IsTrue(editor.TryFlush(out error),error);yield return null;var restored=CollisionOwners(data.id);Assert.AreEqual(2,restored.Length);Assert.AreEqual(12,restored.Sum(o=>(int)o["activeColliders"]));
            Assert.IsFalse(restored.Any(o=>owned.Any(old=>(string)old["leaseId"]==(string)o["leaseId"])));Assert.AreEqual(JsonUtility.ToJson(data),JsonUtility.ToJson(editor.Read(data.id)));
        }
        [UnityTest] public IEnumerator GeometryPreparationLaterImportedFailureReleasesNewCompositeBeforeSourcePublication()
        {
            root.AddComponent<RuntimeDiagnostics>();string source=Snow(new RoomAgentExecutor(editor));yield return GeometryModel(BuildingModelFixture.Create());
            var data=editor.Read(source);data.id=new string('0',32);data.collision=ResourceRecipe();var model=editor.Read(geometryTarget);model.modelGeometry=new(){scaleMode="source",metresPerUnit=100};
            Assert.That(string.CompareOrdinal(data.id,model.id),Is.LessThan(0));string before=JsonUtility.ToJson(editor.Snapshot());
            Assert.IsFalse(editor.ApplyAgentEdit(editor.Revision,new[]{data,model},Array.Empty<string>(),out var error));StringAssert.Contains("12.5",error);
            var owned=CollisionOwners(data.id);Assert.AreEqual(2,owned.Length);Assert.IsTrue(owned.All(o=>(string)o["phase"]=="retiring"&&(int)o["activeColliders"]==0));Assert.AreEqual(before,JsonUtility.ToJson(editor.Snapshot()));Assert.IsNull(editor.Find(data.id));
            yield return null;Assert.IsEmpty(CollisionOwners(data.id));
        }
        [UnityTest] public IEnumerator GeometryPreparationTransferRequiresExactSourceAndOutlivesPreparationDisposal()
        {
            root.AddComponent<RuntimeDiagnostics>();string source=Snow(new RoomAgentExecutor(editor));var data=editor.Read(source);data.id=Guid.NewGuid().ToString("N");data.collision=ResourceRecipe();
            var journal=new RoomJournal(editor.Snapshot());Assert.IsTrue(journal.Prepare(new[]{data},Array.Empty<string>(),out var edit,out var error),error);
            using(edit){using var prepared=RoomEditPreparation.TryCreate(editor,edit,out error);Assert.IsNotNull(prepared,error);
                var wrongCollision=data.collision.Copy();wrongCollision.shapes[0].size*=1.1f;var wrongField=data.heightFields[0].Copy();wrongField.heights[0]+=.01f;
                Assert.Throws<InvalidOperationException>(()=>prepared.TakeCollision(data.id,wrongCollision));Assert.Throws<InvalidOperationException>(()=>prepared.TakeHeightField(data.id,wrongField));
                using var collision=prepared.TakeCollision(data.id,data.collision);using var field=prepared.TakeHeightField(data.id,data.heightFields[0]);Assert.IsNotNull(collision);Assert.IsNotNull(field);Assert.IsNull(prepared.TakeCollision(data.id,data.collision));Assert.IsNull(prepared.TakeHeightField(data.id,data.heightFields[0]));
                Assert.IsNull(field.Surface.transform.parent);Assert.IsFalse(field.Surface.activeInHierarchy);Assert.IsNull(field.Surface.GetComponent<AcousticSurface>().Owner);Assert.IsFalse(field.Surface.GetComponent<RoomWalkableSurface>().Available);
                prepared.Dispose();Assert.IsTrue(CollisionOwners(data.id).All(o=>(string)o["phase"]=="ready"));Assert.Throws<InvalidOperationException>(()=>prepared.TakeCollision(data.id,data.collision));
            }
            yield return null;Assert.IsEmpty(CollisionOwners(data.id));
        }
        [UnityTest] public IEnumerator GeometryPreparationDynamicBodyCanAcceptTerrainAndFixedPhysicsTogether()
        {
            string source=Snow(new RoomAgentExecutor(editor));var field=editor.Read(source).heightFields[0];
            Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Block,"Moving terrain candidate",new Vector3(6,2,4),1,Color.white,out var id,out var error),error);
            Assert.IsTrue(editor.SetItemPhysics(id,new ObjectPhysicsSettings{mode="solid",shape="automatic",mass=1}));physics.SetSurfaces(true,"Synthetic transition floor");physics.StartPhysics();var item=editor.Find(id);Assert.IsTrue(item.GetComponent<RigidRoomItem>().Simulating);
            var data=editor.Read(id);data.heightFields=new[]{field.Copy()};data.physics=ItemPhysics.Fixed;
            Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),error);LogAssert.NoUnexpectedReceived();Assert.IsTrue(item.GetComponent<Rigidbody>().isKinematic);
            var view=item.GetComponent<HeightFieldView>();Assert.IsTrue(view.Collision.enabled);Assert.Contains(view.Collision,item.Grab.colliders);yield return new WaitForFixedUpdate();LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator GeometryPreparationUndoRestoresTerrainWhilePhysicsIsRunning()
        {
            string id=Snow(new RoomAgentExecutor(editor));var item=editor.Find(id);var before=editor.Read(id);var moving=before.Copy();moving.heightFields=Array.Empty<RoomHeightField>();moving.physics=ItemPhysics.Solid;
            physics.SetSurfaces(true,"Synthetic undo transition floor");physics.StartPhysics();Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{moving},Array.Empty<string>(),out var error),error);Assert.IsTrue(item.GetComponent<RigidRoomItem>().Simulating);
            editor.Undo();LogAssert.NoUnexpectedReceived();Assert.IsTrue(item.GetComponent<Rigidbody>().isKinematic);Assert.AreEqual(JsonUtility.ToJson(before),JsonUtility.ToJson(editor.Read(id)));Assert.IsNotNull(item.GetComponent<HeightFieldView>().Collision);
            editor.Redo();LogAssert.NoUnexpectedReceived();Assert.IsTrue(item.GetComponent<RigidRoomItem>().Simulating);Assert.IsNull(item.GetComponent<HeightFieldView>().Collision);yield return new WaitForFixedUpdate();LogAssert.NoUnexpectedReceived();
        }
    }
}
