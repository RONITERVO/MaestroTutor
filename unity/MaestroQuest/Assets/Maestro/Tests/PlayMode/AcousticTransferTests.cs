// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        (string id,string area,RoomAcoustics audio) CpuRetirementArea()
        {
            var recipe=new RoomRecipe{parts=Enumerable.Range(0,16).Select(i=>new RecipePart{id="part"+i,position=Vector3.right*i*.1f,size=Vector3.one*.1f}).ToArray()};
            Assert.That(editor.CreateRecipe("Retained CPU assembly",new Vector3(3,0,1),1,recipe,out var id,out var error),Is.True,error);
            var audio=AreaAcoustics();return(id,NativeAreaFor(id),audio);
        }
        GameObject HoldAcousticContext(RoomAcoustics audio)
        {
            audio.SendMessage("Close");var holder=new GameObject("Competing acoustic context");holder.transform.SetParent(root.transform,false);
            var other=holder.AddComponent<RoomAcoustics>();Assert.That(other.ContextOpen,Is.True,other.Issue);Assert.That(audio.ContextOpen,Is.False);return holder;
        }
        IEnumerator CpuReservationStarted(Task<string> task,RoomAcoustics audio)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+8;
            while(!task.IsCompleted&&audio.RetainedGeometryCount==0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(task.IsCompleted,Is.False,"CPU copying must yield after its reservation");Assert.That(audio.RetainedGeometryCount,Is.EqualTo(16));
        }
        [UnityTest] public IEnumerator AsyncRetirementWithoutRendererPreservesGeometryAndIdentityThroughReturn()
        {
            var(id,area,audio)=CpuRetirementArea();var original=editor.Find(id);string saved=JsonUtility.ToJson(editor.Snapshot());
            var surfaces=original.GetComponentsInChildren<AcousticSurface>();var catalog=audio.DescribeGeometry();
            var keys=Enumerable.Range(0,catalog.Count).Select(catalog.At).Where(x=>surfaces.Contains(x.Input as AcousticSurface)).Select(x=>x.Key).ToArray();
            Assert.That(keys.Length,Is.EqualTo(16));var holder=HoldAcousticContext(audio);
            try{
                var task=editor.RetireNativeArea(area);var frames=new System.Collections.Generic.HashSet<int>();
                double deadline=Time.realtimeSinceStartupAsDouble+12;
                while(!task.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline){Assert.That(editor.Find(id),Is.SameAs(original));frames.Add(Time.frameCount);yield return null;}
                yield return NativeAreaCompletion(task);Assert.That(frames.Count,Is.GreaterThanOrEqualTo(2));yield return null;
                Assert.That(!original,Is.True);Assert.That(audio.ContextOpen,Is.False);Assert.That(audio.GeometryCount,Is.Zero);
                var retained=audio.DescribeGeometry();Assert.That(retained.Available,Is.True);
                foreach(var key in keys){var item=Enumerable.Range(0,retained.Count).Select(retained.At).Single(x=>ReferenceEquals(x.Key,key));Assert.That(item.Input.Read(out var points,out var indices,out var error),Is.True,error);Assert.That(RoomAcoustics.ValidateMeshData(points,indices,out error),Is.True,error);}
                yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
                var restored=audio.DescribeGeometry();foreach(var key in keys)Assert.That(Enumerable.Range(0,restored.Count).Select(restored.At).Any(x=>ReferenceEquals(x.Key,key)&&x.Input is AcousticSurface),Is.True);
                Assert.That(audio.ContextOpen,Is.False);Assert.That(audio.RetainedGeometryCount,Is.Zero);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            }finally{UnityEngine.Object.Destroy(holder);}
            double end=Time.realtimeSinceStartupAsDouble+5;while(!audio.ContextOpen&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Assert.That(audio.ContextOpen,Is.True);yield return ColdAcousticSettled(audio);Assert.That(audio.GeometryCount,Is.GreaterThanOrEqualTo(16));
        }
        IEnumerator CancelCpuRetirement(string change)
        {
            var(id,area,audio)=CpuRetirementArea();var original=editor.Find(id);string saved=JsonUtility.ToJson(editor.Snapshot());
            var holder=HoldAcousticContext(audio);using var cancel=new CancellationTokenSource();
            try{
                var task=editor.RetireNativeArea(area,cancel.Token);yield return CpuReservationStarted(task,audio);
                if(change=="cancel")cancel.Cancel();
                else if(change=="hold"){using(var held=editor.RuntimeGate.Hold("Test a brief interrupted lifetime")){};}
                else if(change=="pose")original.transform.position+=Vector3.right;
                else if(change=="membership"){
                    var shape=GameObject.CreatePrimitive(PrimitiveType.Cube);shape.transform.SetParent(original.transform,false);
                    AcousticSurface.Attach(shape,shape.GetComponent<MeshFilter>().sharedMesh);
                }else{
                    var data=editor.Read(id);data.name="Edit while preparing retirement";
                    Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
                    saved=JsonUtility.ToJson(editor.Snapshot());
                }
                yield return NativeAreaCompletion(task,false);Assert.That(editor.Find(id),Is.SameAs(original));Assert.That(original.isActiveAndEnabled,Is.True);
                Assert.That(audio.RetainedGeometryCount,Is.Zero);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
                Assert.That(editor.NativeEntityDormant(id),Is.False);Assert.That(audio.ContextOpen,Is.False);
                yield return NativeAreaCompletion(editor.RetireNativeArea(area));Assert.That(editor.Find(id),Is.Null);
            }finally{UnityEngine.Object.Destroy(holder);}
        }
        [UnityTest] public IEnumerator AsyncRetirementCancellationReleasesReservedCpuInputs()=>CancelCpuRetirement("cancel");
        [UnityTest] public IEnumerator AsyncRetirementSavedEditCannotRemoveNewerContent()=>CancelCpuRetirement("edit");
        [UnityTest] public IEnumerator AsyncRetirementRuntimePoseChangeLeavesTheViewLoaded()=>CancelCpuRetirement("pose");
        [UnityTest] public IEnumerator AsyncRetirementNewSurfacePreventsPartialAdoption()=>CancelCpuRetirement("membership");
        [UnityTest] public IEnumerator AsyncRetirementBriefRuntimeHoldCancelsPreparation()=>CancelCpuRetirement("hold");
        [UnityTest] public IEnumerator AsyncRetirementCompetingPreparationsPreserveTheWinningOwner()
        {
            var(id,area,audio)=CpuRetirementArea();var holder=HoldAcousticContext(audio);
            try{
                var first=editor.RetireNativeArea(area);var second=editor.RetireNativeArea(area);
                double deadline=Time.realtimeSinceStartupAsDouble+12;
                while((!first.IsCompleted||!second.IsCompleted)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(first.IsCompleted&&second.IsCompleted,Is.True);Assert.That(first.Exception??second.Exception,Is.Null);
                Assert.That(new[]{first.Result,second.Result}.Count(result=>result==null),Is.EqualTo(1));
                Assert.That(editor.Find(id),Is.Null);Assert.That(audio.RetainedGeometryCount,Is.EqualTo(16));
                var catalog=audio.DescribeGeometry();var inputs=Enumerable.Range(0,catalog.Count).Select(catalog.At).Where(v=>v.Input is RoomAcoustics.RetainedInput).ToArray();
                Assert.That(inputs.Length,Is.EqualTo(16));foreach(var input in inputs)Assert.That(input.Input.Read(out _,out _,out var error),Is.True,error);
                yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(audio.RetainedGeometryCount,Is.Zero);Assert.That(editor.Find(id),Is.Not.Null);
            }finally{UnityEngine.Object.Destroy(holder);}
        }
        [UnityTest] public IEnumerator AsyncRetirementLaterInvalidObjectReleasesEarlierReservations()
        {
            var(id,area,audio)=CpuRetirementArea();string peer=editor.Identity(block);
            Assert.That(AssignRegion(peer,area,out var error),Is.True,error);
            var original=editor.Find(id);var holder=HoldAcousticContext(audio);
            var graph=editor.ReadRetention();var last=editor.Find(graph.Closure(graph.Members(area)).Last());
            var surface=last.GetComponentsInChildren<AcousticSurface>().Last();var valid=surface.Mesh;var invalid=new Mesh();
            try{
                surface.Bind(invalid);var task=editor.RetireNativeArea(area);int peak=audio.RetainedGeometryCount;
                double deadline=Time.realtimeSinceStartupAsDouble+12;
                while(!task.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline){peak=Math.Max(peak,audio.RetainedGeometryCount);yield return null;}
                yield return NativeAreaCompletion(task,false);Assert.That(peak,Is.GreaterThan(0),"The earlier object's reservation must actually exist before the later failure");
                Assert.That(editor.Find(id),Is.SameAs(original));Assert.That(editor.Find(peer),Is.SameAs(block));
                Assert.That(audio.RetainedGeometryCount,Is.Zero);Assert.That(editor.NativeEntityDormant(id)||editor.NativeEntityDormant(peer),Is.False);
                surface.Bind(valid);yield return NativeAreaCompletion(editor.RetireNativeArea(area));
                Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Find(peer),Is.Null);
            }finally{if(surface)surface.Bind(valid);UnityEngine.Object.Destroy(invalid);UnityEngine.Object.Destroy(holder);}
        }
        [UnityTest] public IEnumerator AsyncRetirementCapacityRefusalPreservesTheViewAndAllowsRetry()
        {
            var(id,area,audio)=CpuRetirementArea();var original=editor.Find(id);var holder=HoldAcousticContext(audio);
            var mesh=new AcousticMeshInput(new float[]{0,0,0,1,0,0,0,1,0},new[]{0,1,2},Matrix4x4.identity);
            Assert.That(audio.TryPrepareInputs(Enumerable.Repeat(mesh,RoomAcoustics.MaximumVirtualSurfaces).ToArray(),root.transform,Matrix4x4.identity,true,out var held,out var error),Is.True,error);
            try{
                using(held){var task=editor.RetireNativeArea(area);yield return NativeAreaCompletion(task,false);Assert.That(task.Result,Does.Contain("budget"));Assert.That(audio.RetainedGeometryCount,Is.EqualTo(RoomAcoustics.MaximumVirtualSurfaces));Assert.That(editor.Find(id),Is.SameAs(original));}
                Assert.That(audio.RetainedGeometryCount,Is.Zero);yield return NativeAreaCompletion(editor.RetireNativeArea(area));Assert.That(editor.Find(id),Is.Null);
            }finally{held.Dispose();UnityEngine.Object.Destroy(holder);}
        }
    }
    public sealed partial class RoomAcousticMapTests
    {
        [UnityTest] public IEnumerator AsyncRetirementChangedCpuGeometryDoesNotReleaseTheBusyNativeInput()
        {
            Box();HoldCompute();Assert.That(room.RequestMap(new[]{Vector3.zero}),Is.True);yield return NativeComputationEntered();
            var wall=root.GetComponentInChildren<AcousticSurface>();var mesh=UnityEngine.Object.Instantiate(wall.Mesh);
            var points=mesh.vertices;for(int i=0;i<points.Length;i++)points[i]+=Vector3.up*.1f;mesh.vertices=points;mesh.RecalculateBounds();wall.Bind(mesh,environment:true);
            try{
                var task=room.PrepareResidencyAsync(wall.gameObject,root.transform,new RoomPreparationBudget(()=>{}),CancellationToken.None);
                double deadline=Time.realtimeSinceStartupAsDouble+12;
                while(!task.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;Assert.That(task.IsCompleted,Is.True);Assert.That(task.Exception,Is.Null);
                using(var retained=task.Result){
                    Assert.That(retained.Commit(),Is.True);var catalog=room.DescribeGeometry();var input=Enumerable.Range(0,catalog.Count).Select(catalog.At).Single(x=>x.Input is RoomAcoustics.RetainedInput).Input;
                    Assert.That(input.Read(out var copied,out _,out _),Is.True);Assert.That(copied[1],Is.EqualTo(points[0].y));
                    UnityEngine.Object.Destroy(wall.gameObject);yield return null;retained.Dispose();room.Synchronize();
                    Assert.That(room.GeometryCount,Is.EqualTo(6),"The old SDK mesh remains alive until its actual worker exits");Assert.That(room.RetainedGeometryCount,Is.Zero);
                    gate.Set();yield return Finish();Assert.That(room.MapReady,Is.False);Assert.That(room.GeometryCount,Is.EqualTo(5));
                }
            }finally{gate.Set();UnityEngine.Object.Destroy(mesh);}
        }
    }
    public sealed partial class RoomAcousticsTests
    {
        [UnityTest] public IEnumerator AsyncRetirementEmptyPreparationRejectsANewlyAttachedSurface()
        {
            var empty=new GameObject("Initially empty view");empty.transform.SetParent(root.transform,false);
            var task=room.PrepareResidencyAsync(empty,root.transform,new RoomPreparationBudget(()=>{}),CancellationToken.None);
            double deadline=Time.realtimeSinceStartupAsDouble+12;
                while(!task.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;Assert.That(task.IsCompleted,Is.True);Assert.That(task.Exception,Is.Null);
            using(var lease=task.Result){var added=Cube();added.transform.SetParent(empty.transform,false);Assert.That(lease.Commit(),Is.False);}
            Assert.That(room.RetainedGeometryCount,Is.Zero);
        }
    }
}
