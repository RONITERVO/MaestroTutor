// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        RoomRecipe SteppedRecipe()=>new(){parts=Enumerable.Range(0,32).Select(i=>new RecipePart{
            id="Part"+i,parent=i==0?null:"Part0",shape="sweep",position=new Vector3(i*.01f,0,0),rotation=Quaternion.Euler(0,i,0),
            color=new Color(.271f+i*.005f,.583f,.819f),
            profile=Enumerable.Range(0,32).Select(j=>new Vector2(Mathf.Cos(j*2*Mathf.PI/32),Mathf.Sin(j*2*Mathf.PI/32))*.2f).ToArray(),
            path=Enumerable.Range(0,16).Select(j=>new Vector3(0,0,-.5f+j/15f)).ToArray()
        }).ToArray()};
        RoomObjectData SteppedData(RoomRecipe recipe)=>new(){id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Assembly,recipe=recipe};
        HashSet<int> GeometryRootIds()=>Resources.FindObjectsOfTypeAll<GameObject>().Where(x=>x.name=="Recipe geometry").Select(x=>x.GetInstanceID()).ToHashSet();
        GameObject PrivateGeometryRoot(HashSet<int> before)=>Resources.FindObjectsOfTypeAll<GameObject>().SingleOrDefault(x=>x.name=="Recipe geometry"&&!before.Contains(x.GetInstanceID()));
        IEnumerator GeometryTaskDone(Task task)
        {
            float deadline=Time.realtimeSinceStartup+10;while(!task.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(task.IsCompleted,Is.True,"Procedural preparation did not terminate");
        }
        [UnityTest] public IEnumerator NativeGeometryStepsOneRecipeSpansFramesAndPreservesExactGeometry()
        {
            var data=SteppedData(SteppedRecipe());using var expected=new RecipeVisual(data.recipe,new RoomResourceOwner(editor.WorldIdentity,data.id,"object"));
            var before=GeometryRootIds();var task=RoomEditPreparation.PrepareNativeAsync(editor,new[]{data},new RoomPreparationBudget(()=>{}));var frames=new HashSet<int>();
            float deadline=Time.realtimeSinceStartup+10;
            while(!task.IsCompleted&&Time.realtimeSinceStartup<deadline){
                var candidate=PrivateGeometryRoot(before);Assert.That(candidate,Is.Not.Null);Assert.That(candidate.activeInHierarchy,Is.False);
                int count=candidate.GetComponentsInChildren<Renderer>(true).Length;Assert.That(count,Is.InRange(1,32));frames.Add(Time.frameCount);
                Assert.That(editor.Find(data.id),Is.Null);yield return null;
            }
            yield return GeometryTaskDone(task);Assert.That(task.Exception,Is.Null);Assert.That(frames.Count,Is.GreaterThanOrEqualTo(2));
            using var prepared=task.Result;using var actual=prepared.TakeRecipe(data.id,data.recipe);
            Assert.That(actual.Ready,Is.True);Assert.That(actual.Root.activeInHierarchy,Is.False);Assert.That(actual.LocalBounds,Is.EqualTo(expected.LocalBounds));
            Assert.That(actual.Encoded,Is.EqualTo(expected.Encoded));Assert.That(actual.Nodes.Keys,Is.EquivalentTo(expected.Nodes.Keys));
            foreach(var pair in actual.Nodes){var other=expected.Nodes[pair.Key];Assert.That(pair.Value.localPosition,Is.EqualTo(other.localPosition));Assert.That(pair.Value.localRotation,Is.EqualTo(other.localRotation));Assert.That(pair.Value.parent.name,Is.EqualTo(other.parent.name));}
            Assert.That(actual.Meshes.Count,Is.EqualTo(32));
            for(int i=0;i<actual.Meshes.Count;i++){
                var a=actual.Meshes[i];var e=expected.Meshes[i];CollectionAssert.AreEqual(e.vertices,a.vertices);CollectionAssert.AreEqual(e.normals,a.normals);CollectionAssert.AreEqual(e.uv,a.uv);CollectionAssert.AreEqual(e.triangles,a.triangles);Assert.That(a.bounds,Is.EqualTo(e.bounds));
                Assert.That(actual.Renderers[i].sharedMaterial.GetColor("_Color"),Is.EqualTo(expected.Renderers[i].sharedMaterial.GetColor("_Color")));
            }
        }
        [UnityTest] public IEnumerator NativeGeometryStepsCancellationWithinOneRecipeReleasesCustomMeshesAndPaint()
        {
            var data=SteppedData(SteppedRecipe());var before=GeometryRootIds();using var cancel=new CancellationTokenSource();
            var task=RoomEditPreparation.PrepareNativeAsync(editor,new[]{data},new RoomPreparationBudget(()=>cancel.Token.ThrowIfCancellationRequested()));
            Assert.That(task.IsCompleted,Is.False);var candidate=PrivateGeometryRoot(before);Assert.That(candidate,Is.Not.Null);
            var meshes=candidate.GetComponentsInChildren<MeshFilter>(true).Select(x=>x.sharedMesh).ToArray();var paint=candidate.GetComponentsInChildren<Renderer>(true).Select(x=>x.sharedMaterial).ToArray();
            Assert.That(meshes.Length,Is.InRange(1,31));cancel.Cancel();yield return GeometryTaskDone(task);Assert.That(task.IsCanceled,Is.True);yield return null;
            Assert.That((bool)candidate,Is.False);Assert.That(meshes.All(x=>!x),Is.True);Assert.That(paint.All(x=>!x),Is.True);Assert.That(editor.Find(data.id),Is.Null);
        }
        IEnumerator GeometryRejectsInvalidSource(bool track)
        {
            var source=SteppedRecipe();if(track)source.tracks=new[]{new RecipeTrack{part="Missing",keys=new[]{new RecipeKey(),new RecipeKey{time=source.duration}}}};else source.parts[31].parent="Missing";
            Assert.That(source.Validate(out _),Is.False);var data=SteppedData(source);var before=GeometryRootIds();
            var task=RoomEditPreparation.PrepareNativeAsync(editor,new[]{data},new RoomPreparationBudget(()=>{}));var candidate=PrivateGeometryRoot(before);Assert.That(candidate,Is.Not.Null);
            var meshes=candidate.GetComponentsInChildren<MeshFilter>(true).Select(x=>x.sharedMesh).ToArray();Assert.That(meshes.Length,Is.GreaterThan(0));
            yield return GeometryTaskDone(task);Assert.That(task.Exception?.GetBaseException(),Is.TypeOf<ArgumentException>());yield return null;
            Assert.That((bool)candidate,Is.False);Assert.That(meshes.All(x=>!x),Is.True);Assert.That(editor.HasSavedObject(data.id),Is.False);
        }
        [UnityTest] public IEnumerator NativeGeometryStepsLateInvalidPartDiscardsEarlierWork()=>GeometryRejectsInvalidSource(false);
        [UnityTest] public IEnumerator NativeGeometryStepsInvalidTrackDiscardsTheConstructedMeshGroup()=>GeometryRejectsInvalidSource(true);
        [UnityTest] public IEnumerator NativeGeometryStepsIncompleteOrDisposedVisualCannotBePublishedOrContinued()
        {
            using var candidate=RecipeVisual.BeginPreparation(SteppedRecipe(),new RoomResourceOwner(editor.WorldIdentity,"staged","object"));
            Assert.That(candidate.Ready,Is.False);Assert.Throws<InvalidOperationException>(()=>candidate.Attach(root.transform));Assert.Throws<InvalidOperationException>(()=>candidate.SetActive(true));
            using var steps=candidate.BuildSteps().GetEnumerator();Assert.That(steps.MoveNext(),Is.True);var mesh=candidate.Meshes.Single();candidate.Dispose();
            Assert.Throws<ObjectDisposedException>(()=>steps.MoveNext());Assert.Throws<InvalidOperationException>(()=>candidate.Attach(root.transform));yield return null;Assert.That((bool)mesh,Is.False);
        }
        [UnityTest] public IEnumerator NativeGeometryStepsSavedEditDuringSingleRecipePreparationRejectsOldGeometry()
        {
            Assert.That(editor.CreateRecipe("Detailed object",new Vector3(1,1,1),1,SteppedRecipe(),out var id,out var error),Is.True,error);string area=NativeAreaFor(id);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;var before=GeometryRootIds();var activation=editor.ActivateNativeArea(area);Assert.That(activation.IsCompleted,Is.False);
            var privateRoot=PrivateGeometryRoot(before);Assert.That(privateRoot,Is.Not.Null);Assert.That(editor.Find(id),Is.Null);
            var changed=editor.Read(id);changed.recipe.parts[0].size*=1.1f;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            yield return NativeAreaCompletion(activation,false);yield return null;Assert.That((bool)privateRoot,Is.False);Assert.That(editor.Find(id),Is.Null);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));var part=editor.Find(id).GetComponent<RecipeObject>().Part("Part0");Assert.That(part.GetChild(0).localScale,Is.EqualTo(changed.recipe.parts[0].size));
        }
        [UnityTest] public IEnumerator NativeGeometryStepsAdoptionRequiresTheExactPreparedSourceAndDoesNotRebuildIt()
        {
            var data=SteppedData(SteppedRecipe());var original=data.recipe.Copy();var task=RoomEditPreparation.PrepareNativeAsync(editor,new[]{data},new RoomPreparationBudget(()=>{}));
            data.recipe.parts[0].size*=1.1f;yield return GeometryTaskDone(task);Assert.That(task.Exception,Is.Null);using var prepared=task.Result;
            Assert.Throws<InvalidOperationException>(()=>prepared.TakeRecipe(data.id,data.recipe));
            var target=new GameObject("Adopt prepared source");target.transform.SetParent(root.transform,false);var view=target.AddComponent<RecipeObject>();view.ConfigureResourceOwner(new RoomResourceOwner(editor.WorldIdentity,data.id,"object"));
            Assert.That(view.Apply(original,prepared,data.id),Is.True);var mesh=view.Part("Part0").GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(view.Part("Part0").GetChild(0).localScale,Is.EqualTo(original.parts[0].size));Assert.That(view.Apply(original.Copy()),Is.False);Assert.That(view.Part("Part0").GetComponentInChildren<MeshFilter>().sharedMesh,Is.SameAs(mesh));
            var invalid=original.Copy();invalid.parts[0].size=new Vector3(float.NaN,.1f,.1f);Assert.That(view.Apply(invalid),Is.False);Assert.That(view.Part("Part0").GetComponentInChildren<MeshFilter>().sharedMesh,Is.SameAs(mesh));
        }
    }
}
