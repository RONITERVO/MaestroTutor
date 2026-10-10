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
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject CollisionFact(string id,JObject args=null)
        {
            if(!BehaviourCatalog.TryRead(id,1,args,new BehaviourCatalog.FactContext(editor:editor),out var value))return null;
            Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);
        }
        JObject[] CollisionOwners(string target=null)
        {
            int count=(int)CollisionFact("runtime.collisionResources")["entries"];var result=new List<JObject>();
            for(int i=0;i<count;i++){var owner=CollisionFact("runtime.collisionResource",new JObject{["index"]=i});if(target==null||(string)owner["target"]==target)result.Add(owner);}
            return result.ToArray();
        }
        static CollisionRecipe ResourceRecipe()=>new(){shapes=new[]{
            new CollisionShape{id="base",shape="box",size=Vector3.one*.2f},
            new CollisionShape{id="round",shape="sphere",size=Vector3.one*.2f},
            new CollisionShape{id="post",shape="cylinder",size=Vector3.one*.2f,segments=8},
            new CollisionShape{id="wall",shape="ring",size=Vector3.one*.3f,segments=8,innerRadius=.3f}}};
        [UnityTest] public IEnumerator CollisionResourcesCompoundCountsActualMeshesCopiesOwnerAndRetainsDeferredDestruction()
        {
            root.AddComponent<RuntimeDiagnostics>();var world=editor.WorldIdentity.Copy();string expectedWorld=world.worldId,expectedRegion=world.regionId;
            var owner=new RoomResourceOwner(world,"probe","collision");world.worldId=new string('d',32);world.regionId=new string('e',32);
            using var geometry=new CollisionGeometry(ResourceRecipe(),root.transform,owner);
            var ready=CollisionOwners("probe").Single();Assert.AreEqual(11,(int)ready["colliders"]);Assert.AreEqual(9,(int)ready["meshColliders"]);
            Assert.AreEqual(124,(int)ready["sourceTriangles"]);Assert.AreEqual(geometry.Colliders.OfType<MeshCollider>().Sum(c=>c.sharedMesh.triangles.Length/3),(int)ready["sourceTriangles"]);
            Assert.AreEqual(expectedWorld,(string)ready["worldId"]);Assert.AreEqual(expectedRegion,(string)ready["regionId"]);Assert.AreEqual(0,(int)ready["activeColliders"]);
            geometry.SetActive(true);var active=CollisionOwners("probe").Single();Assert.AreEqual(11,(int)active["activeColliders"]);
            var budget=CollisionFact("runtime.collisionResources");var colliders=geometry.Colliders;var meshes=colliders.OfType<MeshCollider>().Select(c=>c.sharedMesh).ToArray();
            geometry.Dispose();geometry.Dispose();var retiring=CollisionOwners("probe").Single();Assert.AreEqual("retiring",(string)retiring["phase"]);Assert.AreEqual(124,(int)retiring["sourceTriangles"]);Assert.AreEqual(0,(int)retiring["activeColliders"]);
            Assert.IsTrue(colliders.All(c=>c));Assert.IsTrue(meshes.All(m=>m));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_COLLISION_RESOURCE_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence))File.WriteAllText(evidence,new JObject{["boundary"]="Actual desktop Unity compound geometry and shared catalog observations; source counts, not memory, admission, provider or headset evidence.",["budget"]=budget,["ready"]=ready,["active"]=active,["retiring"]=retiring}.ToString());
            yield return null;Assert.IsEmpty(CollisionOwners("probe"));Assert.IsTrue(colliders.All(c=>!c));Assert.IsTrue(meshes.All(m=>!m));
        }
        [UnityTest] public IEnumerator CollisionResourcesHierarchyDestructionDropsOnlyDeadLeaseAndDisabledGeometryStaysOwned()
        {
            root.AddComponent<RuntimeDiagnostics>();var parent=new GameObject("Owned collision parent");parent.transform.SetParent(root.transform,false);
            using var geometry=new CollisionGeometry(ResourceRecipe(),parent.transform,new RoomResourceOwner(editor.WorldIdentity,"external","collision"));geometry.SetActive(true);
            parent.SetActive(false);var inactive=CollisionOwners("external").Single();Assert.AreEqual(11,(int)inactive["colliders"]);Assert.AreEqual(0,(int)inactive["activeColliders"]);
            UnityEngine.Object.Destroy(parent);Assert.AreEqual(1,CollisionOwners("external").Length);yield return null;Assert.IsEmpty(CollisionOwners("external"));
            // A wrapper can outlive externally destroyed hierarchy components. Its
            // later Dispose must still release mesh assets without resurrecting a lease.
            geometry.Dispose();Assert.IsEmpty(CollisionOwners("external"));
        }
        [UnityTest] public IEnumerator CollisionResourcesTerrainEditsTrackAcceptedMeshAndExcludeVisualSculptPreview()
        {
            root.AddComponent<RuntimeDiagnostics>();var ex=new RoomAgentExecutor(editor);string id=Snow(ex);var view=editor.Find(id).GetComponent<HeightFieldView>();
            var initial=CollisionOwners(id).Single(o=>(string)o["kind"]=="terrain");Assert.AreEqual(642,(int)initial["sourceTriangles"]);Assert.AreEqual(editor.WorldIdentity.worldId,(string)initial["worldId"]);
            var preview=editor.Read(id).heightFields[0].Copy();preview.heights[0]=0;view.Preview(preview);
            Assert.IsTrue(JToken.DeepEquals(initial,CollisionOwners(id).Single(o=>(string)o["kind"]=="terrain")));
            var accepted=preview.Copy();accepted.cells=8;accepted.heights=Enumerable.Repeat(.04f,81).ToArray();
            Assert.IsTrue(editor.EditHeightField(id,editor.ObjectRevision(id),accepted,out var error),error);
            var changed=CollisionOwners(id).Single(o=>(string)o["kind"]=="terrain"&&(string)o["phase"]=="ready");Assert.AreNotEqual((string)initial["leaseId"],(string)changed["leaseId"]);Assert.AreEqual(194,(int)changed["sourceTriangles"]);
            Assert.AreEqual(((MeshCollider)view.Collision).sharedMesh.triangles.Length/3,(int)changed["sourceTriangles"]);
            Assert.AreEqual("retiring",(string)CollisionOwners(id).Single(o=>(string)o["leaseId"]==(string)initial["leaseId"])["phase"]);yield return null;
            var invalid=accepted.Copy();invalid.cells=3;Assert.Throws<ArgumentException>(()=>view.Apply(new[]{invalid}));Assert.AreEqual(8,view.Accepted.cells);Assert.IsTrue(JToken.DeepEquals(changed,CollisionOwners(id).Single(o=>(string)o["kind"]=="terrain")));
            Assert.IsTrue(editor.EditHeightField(id,editor.ObjectRevision(id),null,out error),error);Assert.AreEqual("retiring",(string)CollisionOwners(id).Single(o=>(string)o["kind"]=="terrain")["phase"]);
            yield return null;Assert.IsEmpty(CollisionOwners(id).Where(o=>(string)o["kind"]=="terrain"));
            editor.Undo();var restored=CollisionOwners(id).Single(o=>(string)o["kind"]=="terrain");Assert.AreNotEqual((string)initial["leaseId"],(string)restored["leaseId"]);Assert.AreEqual(194,(int)restored["sourceTriangles"]);
            Assert.IsTrue(editor.TryFlush(out error),error);Assert.AreEqual(8,new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==id).heightFields[0].cells);
        }
        [UnityTest] public IEnumerator CollisionResourcesSavedCompoundUndoRedoUsesNewLeasesWithoutLosingSource()
        {
            root.AddComponent<RuntimeDiagnostics>();string id=editor.Identity(block);var recipe=ResourceRecipe();
            Assert.IsTrue(editor.EditCollision(id,editor.ObjectRevision(id),recipe,out var error),error);var first=CollisionOwners(id).Single();
            Assert.AreEqual("collision",(string)first["role"]);Assert.AreEqual(editor.WorldIdentity.regionId,(string)first["regionId"]);
            editor.Undo();Assert.AreEqual("retiring",(string)CollisionOwners(id).Single()["phase"]);
            editor.Redo();var held=CollisionOwners(id);Assert.AreEqual(2,held.Length);Assert.AreEqual(2,held.Select(o=>(string)o["leaseId"]).Distinct().Count());
            yield return null;var current=CollisionOwners(id).Single();Assert.AreEqual("ready",(string)current["phase"]);Assert.AreNotEqual((string)first["leaseId"],(string)current["leaseId"]);
            Assert.IsTrue(editor.TryFlush(out error),error);Assert.AreEqual(4,new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==id).collision.shapes.Length);
            current["worldId"]="changed copy";Assert.AreEqual(editor.WorldIdentity.worldId,(string)CollisionOwners(id).Single()["worldId"]);
        }
        [UnityTest] public IEnumerator CollisionResourcesFailedCompoundSaveRetiresPreparedGeometryWithoutPublishing()
        {
            root.AddComponent<RuntimeDiagnostics>();string id=editor.Identity(block);string before=JsonUtility.ToJson(editor.Read(id));var prior=CollisionFact("runtime.collisionResources");
            string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try{Assert.IsFalse(editor.EditCollision(id,editor.ObjectRevision(id),ResourceRecipe(),out _));}finally{Directory.Delete(pending);}
            Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(id)));var abandoned=CollisionOwners(id).Single();Assert.AreEqual("retiring",(string)abandoned["phase"]);Assert.AreEqual(0,(int)abandoned["activeColliders"]);yield return null;Assert.IsTrue(JToken.DeepEquals(prior,CollisionFact("runtime.collisionResources")));
        }
        [UnityTest] public IEnumerator CollisionResourcesImportedSaveFailureDrainsOnlyPreparedCandidateAndKeepsAcceptedModel()
        {
            root.AddComponent<RuntimeDiagnostics>();yield return GeometryModel(BuildingModelFixture.Create());var ex=new RoomAgentExecutor(editor);
            ContainerRun(ex,GeometryCall(BuildingSettings()));var prior=CollisionOwners(geometryTarget).Single();var view=editor.Find(geometryTarget).GetComponent<CreatedRoomObject>();var model=view.Model;
            var collider=editor.Find(geometryTarget).Grab.colliders.Single();Assert.AreEqual(((MeshCollider)collider).sharedMesh.triangles.Length/3,(int)prior["sourceTriangles"]);
            string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try{var changed=BuildingSettings();changed.pivot="base";Assert.IsFalse(ex.Execute(ContainerRequest(GeometryCall(changed)),out _,out _));}finally{Directory.Delete(pending);}
            var during=CollisionOwners(geometryTarget);Assert.AreEqual(2,during.Length);Assert.AreEqual(1,during.Count(o=>(string)o["phase"]=="retiring"));Assert.AreEqual(1,during.Count(o=>(string)o["phase"]=="ready"));
            Assert.AreSame(collider,editor.Find(geometryTarget).Grab.colliders.Single());Assert.IsTrue(collider.enabled);yield return null;
            Assert.IsTrue(JToken.DeepEquals(prior,CollisionOwners(geometryTarget).Single()));Assert.AreSame(model,view.Model);
            Assert.IsTrue(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==geometryTarget).modelGeometry.meshCollision);
        }
        [UnityTest] public IEnumerator CollisionResourcesImportedPreparedAndActiveCopiesHaveSeparateLeasesForTheSameModel()
        {
            root.AddComponent<RuntimeDiagnostics>();yield return GeometryModel(BuildingModelFixture.Create());var model=editor.Find(geometryTarget).GetComponent<CreatedRoomObject>().Model;
            var owner=new RoomResourceOwner(editor.WorldIdentity,geometryTarget,"collision");using var first=new ImportedCollisionGeometry(model,false,owner);using var second=new ImportedCollisionGeometry(model,false,owner);
            var entries=CollisionOwners(geometryTarget);Assert.AreEqual(2,entries.Length);Assert.AreEqual(2,entries.Select(o=>(string)o["leaseId"]).Distinct().Count());Assert.IsTrue(entries.All(o=>(int)o["activeColliders"]==0));
            first.SetActive(true);Assert.AreEqual(first.Colliders.Length,CollisionOwners(geometryTarget).Sum(o=>(int)o["activeColliders"]));second.Dispose();yield return null;
            Assert.AreEqual(1,CollisionOwners(geometryTarget).Length);Assert.IsTrue(first.Colliders.All(c=>c&&c.enabled));Assert.IsTrue(model);
        }
        [UnityTest] public IEnumerator CollisionResourcesUnsupportedImportedSkinNeverAcquiresGeometryOwnership()
        {
            root.AddComponent<RuntimeDiagnostics>();yield return GeometryModel(ModelFixture.Create(avatar:true));
            Assert.IsFalse(new RoomAgentExecutor(editor).Execute(ContainerRequest(GeometryCall(BuildingSettings())),out _,out _));Assert.IsEmpty(CollisionOwners(geometryTarget));yield return null;
        }
    }
}
