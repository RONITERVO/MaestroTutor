// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static GameObject[] RecipeRoots()=>Resources.FindObjectsOfTypeAll<GameObject>().Where(g=>g.scene.IsValid()&&g.name=="Recipe geometry").ToArray();
        [UnityTest] public IEnumerator RecipePreparationNewEntityBuildsDetachedAndCancellationPreservesAcceptedWorld()
        {
            string source=RecipeTarget();yield return null;
            var roots=RecipeRoots();var data=editor.Read(source);data.id=Guid.NewGuid().ToString("N");data.name="Prepared robot";
            var journal=new RoomJournal(editor.Snapshot());string before=JsonUtility.ToJson(editor.Snapshot());
            Assert.IsTrue(journal.Prepare(new[]{data},Array.Empty<string>(),out var edit,out var error),error);GameObject candidate=null;
            using(edit)using(var prepared=RoomEditPreparation.TryCreate(editor,edit,out error)){
                Assert.IsNotNull(prepared,error);var added=RecipeRoots().Except(roots).ToArray();Assert.AreEqual(1,added.Length,"Procedural geometry must exist before the new entity is accepted");candidate=added[0];
                Assert.IsFalse(candidate.activeInHierarchy);Assert.IsNull(candidate.transform.parent);Assert.IsNull(editor.Find(data.id));Assert.AreEqual(before,JsonUtility.ToJson(editor.Snapshot()));
                Assert.IsTrue(candidate.GetComponentsInChildren<AcousticSurface>(true).All(s=>s.Owner==null));
            }
            yield return null;Assert.IsFalse(candidate);CollectionAssert.AreEquivalent(roots,RecipeRoots());Assert.IsNull(editor.Read(data.id));
        }
        [UnityTest] public IEnumerator RecipePreparationFailedSaveReleasesCandidateWithoutRetiringAcceptedParts()
        {
            string id=RecipeTarget(true);yield return null;var view=editor.Find(id).GetComponent<RecipeObject>();var old=view.Part("Head");var paint=old.GetComponentInChildren<Renderer>().sharedMaterial;var roots=RecipeRoots();
            var call=RecipePatch(id);var head=editor.Read(id).recipe.parts.Single(p=>p.id=="Head");head.size.x+=.01f;call["arguments"]["parts"]=new JArray(JObject.Parse(JsonUtility.ToJson(head)));
            string before=JsonUtility.ToJson(editor.Snapshot());string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try{Assert.IsFalse(editor.EditRecipe(id,editor.ObjectRevision(id),(JObject)call["arguments"],out _));}finally{Directory.Delete(pending);}
            var added=RecipeRoots().Except(roots).ToArray();Assert.AreEqual(1,added.Length,"Failed persistence must retire the geometry prepared before saving");Assert.IsFalse(added[0].activeSelf);Assert.IsNull(added[0].transform.parent);
            Assert.AreEqual(before,JsonUtility.ToJson(editor.Snapshot()));Assert.AreSame(old,view.Part("Head"));Assert.AreSame(paint,old.GetComponentInChildren<Renderer>().sharedMaterial);Assert.IsTrue(view.IsPlaying);
            yield return null;Assert.IsFalse(added[0]);Assert.IsTrue(old);Assert.IsTrue(paint);CollectionAssert.AreEquivalent(roots,RecipeRoots());
        }
        [UnityTest] public IEnumerator RecipePreparationLaterMixedFailureReleasesNewRecipeBeforePublication()
        {
            string source=RecipeTarget();yield return GeometryModel(BuildingModelFixture.Create());yield return null;
            var roots=RecipeRoots();var data=editor.Read(source);data.id=new string('0',32);var model=editor.Read(geometryTarget);model.modelGeometry=new(){scaleMode="source",metresPerUnit=100};
            Assert.That(string.CompareOrdinal(data.id,model.id),Is.LessThan(0));string before=JsonUtility.ToJson(editor.Snapshot());
            Assert.IsFalse(editor.ApplyAgentEdit(editor.Revision,new[]{data,model},Array.Empty<string>(),out var error));StringAssert.Contains("12.5",error);
            var added=RecipeRoots().Except(roots).ToArray();Assert.AreEqual(1,added.Length);Assert.IsFalse(added[0].activeSelf);Assert.IsNull(added[0].transform.parent);
            Assert.AreEqual(before,JsonUtility.ToJson(editor.Snapshot()));Assert.IsNull(editor.Find(data.id));yield return null;Assert.IsFalse(added[0]);CollectionAssert.AreEquivalent(roots,RecipeRoots());
        }
        [UnityTest] public IEnumerator RecipePreparationExactTransferRetainsSharedPaintAndReleasesOnlyOwnedCustomMesh()
        {
            Assert.IsTrue(editor.CreateRecipe("Accepted cup",new Vector3(.2f,1,.4f),1,LatheCup(),out var id,out var error),error);yield return null;
            var old=editor.Find(id).GetComponent<RecipeObject>().Part("Body");var oldMesh=old.GetComponentInChildren<MeshFilter>().sharedMesh;var paint=old.GetComponentInChildren<Renderer>().sharedMaterial;
            var data=editor.Read(id);data.id=Guid.NewGuid().ToString("N");var journal=new RoomJournal(editor.Snapshot());Assert.IsTrue(journal.Prepare(new[]{data},Array.Empty<string>(),out var edit,out error),error);
            GameObject candidateRoot;Mesh candidateMesh;
            using(edit)using(var prepared=RoomEditPreparation.TryCreate(editor,edit,out error)){
                Assert.IsNotNull(prepared,error);var wrong=data.recipe.Copy();wrong.parts[0].segments++;
                Assert.Throws<InvalidOperationException>(()=>prepared.TakeRecipe(data.id,wrong));
                using var visual=prepared.TakeRecipe(data.id,data.recipe);Assert.IsNotNull(visual);Assert.IsNull(prepared.TakeRecipe(data.id,data.recipe));
                Assert.AreEqual(data.id,visual.Owner.Target);Assert.AreEqual(editor.WorldIdentity.worldId,visual.Owner.World);Assert.AreEqual(editor.WorldIdentity.regionId,visual.Owner.Region);
                candidateRoot=visual.Root;candidateMesh=visual.Meshes.Single();Assert.AreNotSame(oldMesh,candidateMesh);Assert.AreSame(paint,visual.Renderers.Single().sharedMaterial);
                prepared.Dispose();Assert.IsTrue(candidateRoot);Assert.IsTrue(candidateMesh);Assert.Throws<InvalidOperationException>(()=>prepared.TakeRecipe(data.id,data.recipe));
                visual.Attach(root.transform);Assert.IsTrue(candidateRoot.activeInHierarchy);
            }
            yield return null;Assert.IsFalse(candidateRoot);Assert.IsFalse(candidateMesh);Assert.IsTrue(oldMesh);Assert.IsTrue(paint);Assert.AreSame(paint,old.GetComponentInChildren<Renderer>().sharedMaterial);
        }
        [UnityTest] public IEnumerator RecipePreparationAcceptsCompoundRecipeAndUndoRedoPreserveExactGeometryAndPose()
        {
            Assert.IsTrue(editor.CreateRecipe("Source cup",new Vector3(.2f,1,.4f),1,LatheCup(),out var source,out var error),error);yield return null;
            var sourceMesh=editor.Find(source).GetComponent<RecipeObject>().Part("Body").GetComponentInChildren<MeshFilter>().sharedMesh;
            var data=editor.Read(source);data.id=Guid.NewGuid().ToString("N");data.position=new Vector3(2,1,2);data.rotation=Quaternion.Euler(12,35,7);data.scale=1.3f;data.recipe.parts[0].profile[1].x=.48f;data.collision=ResourceRecipe();
            Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),error);Assert.IsTrue(editor.TryFlush(out error),error);
            var item=editor.Find(data.id);var view=item.GetComponent<RecipeObject>();var accepted=view.Part("Body").GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.AreNotSame(sourceMesh,accepted);Assert.AreEqual(1,item.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Recipe geometry"));Assert.AreEqual(11,item.Grab.colliders.Count);
            Assert.That(Vector3.Distance(editor.Frame.PointToWorld(data.position),item.transform.position),Is.LessThan(.00001f));Assert.That(Quaternion.Angle(data.rotation,item.transform.localRotation),Is.LessThan(.001f));
            Assert.AreEqual(JsonUtility.ToJson(data),JsonUtility.ToJson(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==data.id)));
            editor.Undo();Assert.IsNull(editor.Read(data.id));yield return null;Assert.IsFalse(accepted);Assert.IsTrue(sourceMesh);
            editor.Redo();Assert.IsTrue(editor.TryFlush(out error),error);yield return null;
            var restored=editor.Find(data.id).GetComponent<RecipeObject>().Part("Body").GetComponentInChildren<MeshFilter>().sharedMesh;Assert.IsTrue(restored);Assert.AreNotSame(accepted,restored);Assert.IsTrue(sourceMesh);
            Assert.AreEqual(JsonUtility.ToJson(data),JsonUtility.ToJson(editor.Read(data.id)));Assert.AreEqual(1,editor.Find(data.id).GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Recipe geometry"));
        }
    }
}
