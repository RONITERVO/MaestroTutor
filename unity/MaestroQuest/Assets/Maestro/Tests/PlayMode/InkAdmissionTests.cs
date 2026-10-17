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
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        List<InkResources.Lease> FillInk(int spare=0){
            var held=new List<InkResources.Lease>();int count=InkResources.MaximumStrokes-(int)InkResources.ObserveBudget()["strokes"]-spare;
            try{for(int i=0;i<count;i++)held.Add(InkResources.Stroke(0,new RoomResourceOwner(editor.WorldIdentity,"pending","ink"),"pending"));return held;}
            catch{foreach(var lease in held)lease.Dispose();throw;}
        }
        static void ReleaseInk(IEnumerable<InkResources.Lease> held){foreach(var lease in held)lease.Dispose();}
        JObject[] InkReservations(){int count=(int)CollisionFact("runtime.inkBudget")["strokes"]+(int)CollisionFact("runtime.inkBudget")["surfaces"];return Enumerable.Range(0,count).Select(i=>CollisionFact("runtime.inkReservation",new JObject{["index"]=i})).ToArray();}
        [UnityTest]public IEnumerator InkAdmissionRefusalPreservesSaveHistoryAndGeometryWhileUnchangedInkReuses(){
            string id=NewStroke();var item=editor.Find(id);var mesh=item.GetComponent<MeshFilter>().sharedMesh;Assert.That(editor.TryFlush(out var error),Is.True,error);
            string saved=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision;bool undo=editor.CanUndo,redo=editor.CanRedo;var held=FillInk();
            try{
                var data=editor.Read(id);data.radius=.009f;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.False);StringAssert.Contains("live drawing budget",error);
                Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.CanUndo,Is.EqualTo(undo));Assert.That(editor.CanRedo,Is.EqualTo(redo));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.That(item.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));
                Assert.That(editor.PaintObject(id,Color.red,out error),Is.True,error);Assert.That(item.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));
                Assert.That(editor.DeleteObject(id,out error),Is.True,error);Assert.That((int)InkResources.ObserveBudget()["strokes"],Is.EqualTo(InkResources.MaximumStrokes));yield return null;Assert.That((int)InkResources.ObserveBudget()["strokes"],Is.EqualTo(InkResources.MaximumStrokes-1));
            }finally{ReleaseInk(held);}
            editor.Undo();Assert.That(editor.Find(id).GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(0));yield return null;
        }
        [UnityTest]public IEnumerator InkAdmissionBatchAndFailedStorageReleaseOnlyNewInk(){
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,SurfaceConfigure());SurfaceRun(ex,SurfaceAdd());string id=editor.Identity(block);var view=block.GetComponent<DrawingSurfaceView>();var patch=view.Surface("Front");var mesh=patch.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(editor.TryFlush(out var error),Is.True,error);string saved=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision;var data=editor.Read(id);
            var a=data.surfaces[0].strokes[0].Copy();a.id=Guid.NewGuid().ToString("N");var b=a.Copy();b.id=Guid.NewGuid().ToString("N");data.surfaces[0].strokes=new[]{data.surfaces[0].strokes[0],a,b};var held=FillInk(1);
            try{
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.False);StringAssert.Contains("live drawing budget",error);Assert.That(view.StrokeCount,Is.EqualTo(1));Assert.That(view.Surface("Front"),Is.SameAs(patch));Assert.That(patch.GetComponentInChildren<MeshFilter>().sharedMesh,Is.SameAs(mesh));Assert.That((int)InkResources.ObserveBudget()["retiring"],Is.GreaterThanOrEqualTo(1));
            }finally{ReleaseInk(held);}
            yield return null;
            using(var locked=new FileStream(Path.Combine(directory,RoomStorage.FileName),FileMode.Open,FileAccess.Read,FileShare.Read))Assert.That(editor.EditSurface(id,editor.ObjectRevision(id),(JObject)SurfaceAdd()["arguments"],out _,out error),Is.False);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.That(view.StrokeCount,Is.EqualTo(1));Assert.That(patch.GetComponentInChildren<MeshFilter>().sharedMesh,Is.SameAs(mesh));yield return null;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);Assert.That(view.StrokeCount,Is.EqualTo(3));Assert.That(patch.GetComponentInChildren<MeshFilter>().sharedMesh,Is.SameAs(mesh));
        }
        [UnityTest]public IEnumerator InkAdmissionUnchangedSurfacesAndWindowsSurviveRecipeRebuildAtFullBudget(){
            string id=RecipeTarget();var item=editor.Find(id);var data=editor.Read(id);string part=data.recipe.parts[0].id;
            data.surfaces=new[]{new DrawingSurface{id="Front",part=part,strokes=new[]{new SurfaceStroke{id=Guid.NewGuid().ToString("N"),points=new[]{Vector3.left*.02f,Vector3.right*.02f}}}}};data.windows=new[]{new RoomWindow{id="Window",surface="Front"}};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            var patch=item.GetComponent<DrawingSurfaceView>().Surface("Front");var marks=patch.GetComponentInChildren<PencilMarks>();var mesh=marks.GetComponent<MeshFilter>().sharedMesh;var pigment=marks.GetComponent<MeshRenderer>().sharedMaterial;var mask=item.GetComponentsInChildren<Renderer>(true).Single(RoomWindowView.IsMask);var windowMesh=mask.GetComponent<MeshFilter>().sharedMesh;
            var held=FillInk();
            try{
                data=editor.Read(id);data.recipe.parts[0].position+=Vector3.up*.2f;data.surfaces[0].width=.7f;data.surfaces[0].height=.6f;data.surfaces[0].enabled=false;data.surfaces[0].strokes[0].color=Color.blue;
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);yield return null;
                Assert.That(item.GetComponent<DrawingSurfaceView>().Surface("Front"),Is.SameAs(patch));Assert.That(patch.parent,Is.SameAs(item.GetComponent<RecipeObject>().Part(part)));Assert.That(patch.gameObject.activeInHierarchy,Is.True,"Drawing disabled does not hide existing ink");Assert.That(marks.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));Assert.That(marks.GetComponent<MeshRenderer>().sharedMaterial,Is.SameAs(pigment));Assert.That(item.GetComponentsInChildren<Renderer>(true).Single(RoomWindowView.IsMask),Is.SameAs(mask));Assert.That(mask.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(windowMesh));Assert.That(mask.transform.localScale,Is.EqualTo(new Vector3(.7f,.6f,1)));
            }finally{ReleaseInk(held);}
        }
        [UnityTest]public IEnumerator InkAdmissionUndoTemporaryDiscardAndDormantActivationRetainRetry(){
            string id=NewStroke();var data=editor.Read(id);float original=data.radius;data.radius=.009f;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);yield return null;
            int revision=editor.Revision;var held=FillInk();try{editor.Undo();Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.Read(id).radius,Is.EqualTo(.009f));Assert.That(editor.CanUndo,Is.True);}finally{ReleaseInk(held);}
            editor.Undo();Assert.That(editor.Read(id).radius,Is.EqualTo(original));editor.Redo();Assert.That(editor.Read(id).radius,Is.EqualTo(.009f));yield return null;
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;data=editor.Read(id);data.radius=.012f;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);yield return null;
            string session=editor.TemporarySessionId;held=FillInk();try{Assert.That(editor.DiscardTemporaryRoom(out error),Is.False);StringAssert.Contains("live drawing budget",error);Assert.That(editor.TemporarySessionId,Is.EqualTo(session));Assert.That(editor.Read(id).radius,Is.EqualTo(.012f));}finally{ReleaseInk(held);}
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(id).radius,Is.EqualTo(.009f));yield return null;
            string area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;held=FillInk();
            try{var failed=editor.ActivateNativeArea(area);yield return NativeAreaCompletion(failed,false);StringAssert.Contains("live drawing budget",failed.Result);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeEntityDormant(id),Is.True);}finally{ReleaseInk(held);}
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Find(id).GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(0));
        }
        [UnityTest]public IEnumerator InkAdmissionPreviewFailureRetainsAllSamplesForRetryOrDiscard(){
            var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;editor.ToggleDrawing();Ray At(float x)=>new(new Vector3(x,1,0),Vector3.forward);
            pencil.Begin(0,At(0));var held=FillInk();int count=editor.Snapshot().objects.Length;
            try{pencil.Move(0,At(.03f));Assert.That(pencil.IsDrawing,Is.False);Assert.That(pencil.HasUnsavedStroke,Is.True);Assert.That((int)pencil.Observe()["points"],Is.EqualTo(2));Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));Assert.That(pencil.Resolve(pencil.SessionId,false,out _,out var error),Is.False);StringAssert.Contains("live drawing budget",error);}finally{ReleaseInk(held);}
            Assert.That(pencil.Resolve(pencil.SessionId,false,out var result,out var issue),Is.True,issue);Assert.That(editor.Read((string)result["objectId"]).points.Length,Is.EqualTo(2));yield return null;
            pencil.Begin(0,At(0));held=FillInk();try{pencil.Move(0,At(.03f));Assert.That(pencil.HasUnsavedStroke,Is.True);Assert.That(pencil.Resolve(pencil.SessionId,true,out _,out issue),Is.True,issue);Assert.That(pencil.HasUnsavedStroke,Is.False);}finally{ReleaseInk(held);}yield return null;
        }
        [UnityTest]public IEnumerator InkAdmissionPreparedSourceIsExactAndPreviewReusesItsPigment(){
            var host=new GameObject("Unpublished ink");host.transform.SetParent(root.transform,false);var view=host.AddComponent<DrawingSurfaceView>();var definitions=new[]{new DrawingSurface{id="Front",strokes=new[]{new SurfaceStroke{id=Guid.NewGuid().ToString("N"),points=new[]{Vector3.zero,Vector3.right*.03f}}}}};
            using(var candidate=new PreparedDrawingSurfaces(editor.WorldIdentity,"candidate",definitions,null)){
                Assert.Throws<InvalidOperationException>(()=>candidate.Validate("other",definitions));var changed=definitions.Select(s=>s.Copy()).ToArray();changed[0].width=.7f;Assert.Throws<InvalidOperationException>(()=>candidate.Validate("candidate",changed));Assert.That(view.StrokeCount,Is.Zero);
                view.Apply("candidate",definitions,candidate);Assert.Throws<InvalidOperationException>(()=>candidate.Validate("candidate",definitions));
            }
            var marks=view.Surface("Front").GetComponentInChildren<PencilMarks>();var pigment=marks.GetComponent<MeshRenderer>().sharedMaterial;var previous=marks.GetComponent<MeshFilter>().sharedMesh;
            for(int i=0;i<4;i++){
                using var geometry=PencilGeometry.Prepare(new[]{new[]{Vector3.zero,Vector3.right*(.04f+i*.01f)}},.003f,new RoomResourceOwner(editor.WorldIdentity,"candidate","preview"),"draft",reusePigment:marks);marks.SetPrepared(geometry);Assert.That(marks.GetComponent<MeshRenderer>().sharedMaterial,Is.SameAs(pigment));
            }
            Assert.That(previous,Is.Not.Null);yield return null;Assert.That(!previous,Is.True);Assert.That(pigment,Is.Not.Null);NativeResourceLifetime.Release(host);UnityEngine.Object.Destroy(host);yield return null;Assert.That(!pigment,Is.True);
        }
        [UnityTest]public IEnumerator InkAdmissionPartiallyInitializedNativeObjectsStayChargedUntilDestroyed(){
            var owner=new RoomResourceOwner(editor.WorldIdentity,"partial","ink");int count=(int)InkResources.ObserveBudget()["strokes"];
            var lease=InkResources.Stroke(2,owner,"mesh");Mesh mesh=null;
            Assert.Throws<InvalidOperationException>(()=>PencilStrokeMesh.Build(new[]{new[]{Vector3.zero,Vector3.right}},.003f,71,m=>{mesh=m;lease.Own(m);throw new InvalidOperationException("injected initialization failure");}));lease.Dispose();
            var second=InkResources.Stroke(2,owner,"material");Material pigment=null;
            Assert.Throws<InvalidOperationException>(()=>IllustratedMaterials.Create(Color.white,.08f,m=>{pigment=m;second.Own(m);throw new InvalidOperationException("injected initialization failure");}));second.Dispose();
            Assert.That((int)InkResources.ObserveBudget()["strokes"],Is.EqualTo(count+2));Assert.That(mesh,Is.Not.Null);Assert.That(pigment,Is.Not.Null);yield return null;Assert.That(!mesh&&!pigment,Is.True);Assert.That((int)InkResources.ObserveBudget()["strokes"],Is.EqualTo(count));
        }
        [UnityTest]public IEnumerator InkAdmissionNativeFactsCapturePrivateReadyInactiveAndRetiringOwnership(){
            root.AddComponent<RuntimeDiagnostics>();string id=Guid.NewGuid().ToString("N");var host=new GameObject("Diagnostic ink");host.transform.SetParent(root.transform,false);var marks=host.AddComponent<PencilMarks>();var held=new List<InkResources.Lease>();
            try{
                using var geometry=PencilGeometry.Prepare(new[]{new[]{Vector3.zero,Vector3.right}},.003f,new RoomResourceOwner(editor.WorldIdentity,id,"ink"),"spatial");
                var preparing=InkReservations().Single(x=>(string)x["target"]==id);string reservation=(string)preparing["reservationId"];JObject Read()=>InkReservations().Single(x=>(string)x["reservationId"]==reservation);
                marks.SetPrepared(geometry);var ready=Read();host.SetActive(false);var disabled=Read();held=FillInk();var budget=CollisionFact("runtime.inkBudget");var again=CollisionFact("runtime.inkBudget");Assert.That(again.ToString(),Is.EqualTo(budget.ToString()));again["strokes"]=0;Assert.That((int)CollisionFact("runtime.inkBudget")["strokes"],Is.EqualTo(InkResources.MaximumStrokes));
                NativeResourceLifetime.Release(host);UnityEngine.Object.Destroy(host);var retiring=Read();Assert.That((string)retiring["phase"],Is.EqualTo("retiring"));Assert.Throws<RoomResourceLimitException>(()=>InkResources.Stroke(2,new RoomResourceOwner(null,"blocked","ink"),"blocked"));
                string output=Environment.GetEnvironmentVariable("MAESTRO_INK_ADMISSION_EVIDENCE");if(!string.IsNullOrEmpty(output))File.WriteAllText(output,new JObject{["budget"]=budget,["preparing"]=preparing,["ready"]=ready,["disabled"]=disabled,["retiring"]=retiring}.ToString());
                yield return null;Assert.That(InkReservations().Any(x=>(string)x["reservationId"]==reservation),Is.False);using var retry=InkResources.Stroke(2,new RoomResourceOwner(null,"retry","ink"),"retry");
            }finally{ReleaseInk(held);if(host){NativeResourceLifetime.Release(host);UnityEngine.Object.Destroy(host);}}
        }
    }
}
