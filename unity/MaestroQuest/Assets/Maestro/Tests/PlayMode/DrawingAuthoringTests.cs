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
        RoomAgentRequest DrawingRequest(JObject call){var request=ObjectEditRequest(call);if(call["arguments"]["target"]==null)request.conditions=Array.Empty<RoomObjectCondition>();return request;}
        JObject DrawingCreate()=>new() {["id"]="object.create",["version"]=1,["arguments"]=((JObject)BehaviourCatalog.Action("object.create").InputSchema["oneOf"][3]["examples"][0]).DeepClone()};
        JObject DrawingCall(string target,float radius)=>new() {["id"]="object.drawing.edit",["version"]=1,["arguments"]=new JObject {["operation"]="radius",["target"]=target,["revision"]=editor.ObjectRevision(target),["radius"]=radius}};
        JObject Splice(string target,int index,int remove,params Vector3[] points)=>new() {["id"]="object.drawing.edit",["version"]=1,["arguments"]=new JObject {["operation"]="splice",["target"]=target,["revision"]=editor.ObjectRevision(target),["index"]=index,["deleteCount"]=remove,["points"]=new JArray(points.Select(p=>new JObject {["x"]=p.x,["y"]=p.y,["z"]=p.z}))}};
        JObject DrawingRun(RoomAgentExecutor executor,JObject call){Assert.That(executor.Execute(DrawingRequest(call),out var error,out _),Is.True,error);return (JObject)executor.Executions.Observe().DeepClone();}
        JObject DrawingFact(string target){Assert.That(BehaviourCatalog.TryRead("object.drawing",1,new JObject {["target"]=target},new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);return (JObject)value.Value;}
        string NewStroke(){var executor=new RoomAgentExecutor(editor);return (string)DrawingRun(executor,DrawingCreate())["selected"]["output"]["objectId"];}
        [UnityTest] public IEnumerator SharedStrokeCreationSavesGeometryWithoutSelectingOrReplayingIt()
        {
            var executor=new RoomAgentExecutor(editor);string selection=editor.SelectedId;var call=DrawingCreate();var request=DrawingRequest(call);Assert.That(executor.Execute(request,out var error,out _),Is.True,error);var receipt=(JObject)executor.Executions.Observe().DeepClone();string target=(string)receipt["selected"]["output"]["objectId"];
            var item=editor.Find(target);var data=editor.Read(target);Assert.That(data.points.Length,Is.EqualTo(3));Assert.That(item.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(0));Assert.That(item.Grab.colliders.Single().bounds.size.y,Is.GreaterThan(.1f));Assert.That(editor.SelectedId,Is.EqualTo(selection));Assert.That(editor.DrawingMode,Is.False);Assert.That(data.physics,Is.EqualTo(ItemPhysics.Fixed));Assert.That(new RoomStorage(directory).Load(out _).objects.Any(x=>x.id==target),Is.True);
            var before=DrawingFact(target);var edit=Splice(target,3,0,new Vector3(.22f,.15f,0));var edited=DrawingRun(executor,edit);var after=DrawingFact(target);
            string output=Environment.GetEnvironmentVariable("MAESTRO_DRAWING_AUTHORING");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"drawing-authoring.json"),new JObject {["createCall"]=call,["creation"]=receipt,["before"]=before,["editCall"]=edit,["edit"]=edited,["after"]=after}.ToString());}
            editor.Undo();Assert.That(editor.Read(target).points.Length,Is.EqualTo(3));editor.Undo();Assert.That(editor.Find(target),Is.Null);Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Find(target),Is.Null);yield return null;
        }
        [UnityTest] public IEnumerator StrokeEditsRefreshVisibleMeshCollisionAndUndoWithoutChangingMotionOrOtherActors()
        {
            string target=NewStroke();var item=editor.Find(target);var original=editor.Read(target);Assert.That(editor.SaveAnimation(target,new RoomMotion {frames=new[]{new MotionFrame {position=original.position},new MotionFrame {time=1,position=original.position+Vector3.up*.1f}}},null,false),Is.True);
            string motion=JsonUtility.ToJson(editor.Read(target).motion);var oldMesh=item.GetComponent<MeshFilter>().sharedMesh;float oldHeight=oldMesh.bounds.size.y;
            var executor=new RoomAgentExecutor(editor);var wait=new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=20}};Assert.That(runtime.Scheduler.Invoke(wait,Time.unscaledTime,out var waiting,out var error),Is.True,error);
            DrawingRun(executor,Splice(target,1,1,new Vector3(.08f,.4f,0)));var mesh=item.GetComponent<MeshFilter>().sharedMesh;Assert.That(mesh,Is.Not.SameAs(oldMesh));Assert.That(mesh.bounds.size.y,Is.GreaterThan(oldHeight+.2f));Assert.That(((BoxCollider)item.Grab.colliders.Single()).size.y,Is.GreaterThan(.4f));
            DrawingRun(executor,DrawingCall(target,.012f));Assert.That(editor.Read(target).radius,Is.EqualTo(.012f));Assert.That(JsonUtility.ToJson(editor.Read(target).motion),Is.EqualTo(motion));Assert.That(editor.Read(target).position,Is.EqualTo(original.position));Assert.That((string)runtime.Scheduler.Invocation(waiting)["phase"],Is.EqualTo("running"));
            editor.Undo();Assert.That(editor.Read(target).radius,Is.EqualTo(.003f));editor.Undo();Assert.That(editor.Read(target).points,Is.EqualTo(original.points));Assert.That(item.GetComponent<MeshFilter>().sharedMesh.bounds.size.y,Is.EqualTo(oldHeight).Within(.00001f));yield return null;
        }
        [UnityTest] public IEnumerator BatchedEditsReachEveryNativePointAndPagedReadsKeepExactCoordinates()
        {
            string target=NewStroke();var executor=new RoomAgentExecutor(editor);var expected=Enumerable.Range(0,2048).Select(i=>new Vector3(i*.001f,Mathf.Sin(i*.1f)*.01f,0)).ToArray();DrawingRun(executor,Splice(target,0,3,expected.Take(64).ToArray()));
            for(int offset=64;offset<expected.Length;offset+=64)DrawingRun(executor,Splice(target,offset,0,expected.Skip(offset).Take(64).ToArray()));
            Assert.That(editor.Read(target).points,Is.EqualTo(expected));int revision=editor.ObjectRevision(target);var observed=new System.Collections.Generic.List<Vector3>();
            for(int offset=0;offset<=2048;offset+=8){Assert.That(BehaviourCatalog.TryRead("object.drawing.points",1,new JObject {["target"]=target,["revision"]=revision,["offset"]=offset},new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));var page=(JObject)value.Value;observed.AddRange(((JArray)page["points"]).Select(p=>new Vector3((float)p["x"],(float)p["y"],(float)p["z"])));}
            Assert.That(observed,Is.EqualTo(expected));Assert.That(executor.Execute(DrawingRequest(Splice(target,2048,0,Vector3.up)),out _,out _),Is.False);Assert.That(editor.Read(target).points.Length,Is.EqualTo(2048));DrawingRun(executor,DrawingCall(target,.004f));Assert.That(BehaviourCatalog.TryRead("object.drawing.points",1,new JObject {["target"]=target,["revision"]=revision,["offset"]=0},new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator DrawingFailuresPreserveSavedDataVisibleGeometryAndOwnership()
        {
            string target=NewStroke();var executor=new RoomAgentExecutor(editor);var stale=DrawingCall(target,.01f);Assert.That(editor.PaintObject(target,Color.red,out var error),Is.True,error);Assert.That(executor.Execute(DrawingRequest(stale),out _,out _),Is.False);
            var item=editor.Find(target);var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);Assert.That(executor.Execute(DrawingRequest(DrawingCall(target,.01f)),out _,out _),Is.False);manager.SelectExit((IXRSelectInteractor)hand,item.Grab);yield return null;
            Assert.That(animations.StartRecording((string)animations.ObserveRecording()["sessionId"],target,editor.ObjectRevision(target),out _,out error),Is.True,error);Assert.That(executor.Execute(DrawingRequest(DrawingCall(target,.01f)),out _,out _),Is.False);Assert.That(animations.DiscardRecording((string)animations.ObserveRecording()["sessionId"],out _,out error),Is.True,error);
            string saved=JsonUtility.ToJson(editor.Read(target));var mesh=item.GetComponent<MeshFilter>().sharedMesh;foreach(var bad in new[]{Splice(target,20,0,Vector3.one),Splice(target,0,3),Splice(target,0,3,Vector3.zero,Vector3.zero),DrawingCall(editor.Identity(block),.004f)})Assert.That(executor.Execute(DrawingRequest(bad),out _,out _),Is.False);
            string obstacle=Path.Combine(directory,"room.v6.json.pending");Directory.CreateDirectory(obstacle);Assert.That(executor.Execute(DrawingRequest(DrawingCall(target,.01f)),out _,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(saved));Assert.That(item.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));Directory.Delete(obstacle);yield return null;
        }
        [UnityTest] public IEnumerator StrokeUndoRequiresReleaseAndTemporaryEditsStayInTheirFork()
        {
            string target=NewStroke();var item=editor.Find(target);var executor=new RoomAgentExecutor(editor);DrawingRun(executor,DrawingCall(target,.015f));var mesh=item.GetComponent<MeshFilter>().sharedMesh;var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);editor.Undo();Assert.That(editor.Read(target).radius,Is.EqualTo(.015f));Assert.That(item.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));var pose=editor.Read(target);item.transform.SetLocalPositionAndRotation(pose.position,pose.rotation);manager.SelectExit((IXRSelectInteractor)hand,item.Grab);editor.Undo();Assert.That(editor.Read(target).radius,Is.EqualTo(.003f));Assert.That(item.GetComponent<MeshFilter>().sharedMesh,Is.Not.SameAs(mesh));yield return null;
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;DrawingRun(executor,Splice(target,3,0,Vector3.up*.3f));Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==target).points.Length,Is.EqualTo(3));Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(target).points.Length,Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator PhysicalPencilUsesTheSameSavedGeometryWithRoomCoordinateConversion()
        {
            root.transform.SetPositionAndRotation(new Vector3(2,0,3),Quaternion.Euler(0,35,0));var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;editor.ToggleDrawing();var a=root.transform.TransformPoint(new Vector3(.2f,1,.5f));var b=root.transform.TransformPoint(new Vector3(.3f,1.1f,.5f));
            pencil.Begin(0,new Ray(a-Vector3.forward*.12f,Vector3.forward));pencil.Move(0,new Ray(b-Vector3.forward*.12f,Vector3.forward));pencil.End(0);Assert.That(pencil.HasUnsavedStroke,Is.False);var data=editor.Read(editor.SelectedId);Assert.That(data.kind,Is.EqualTo(RoomObjectKind.Drawing));Assert.That(Vector3.Distance(data.position,new Vector3(.2f,1,.5f)),Is.LessThan(.00001f));Assert.That(Vector3.Distance(data.points[1],new Vector3(.1f,.1f,0)),Is.LessThan(.00001f));Assert.That(new RoomStorage(directory).Load(out _).objects.Any(x=>x.id==data.id),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator FailedPhysicalStrokeRetainsPointsAndBlocksBoundariesUntilExplicitRetryOrDiscard()
        {
            var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;editor.ToggleDrawing();string obstacle=Path.Combine(directory,"room.v6.json.pending");Directory.CreateDirectory(obstacle);int count=editor.Snapshot().objects.Length;
            pencil.Begin(0,new Ray(new Vector3(.2f,1,.5f),Vector3.forward));pencil.Move(0,new Ray(new Vector3(.3f,1,.5f),Vector3.forward));pencil.End(0);string session=pencil.SessionId;var capture=pencil.Observe();
            Assert.That(pencil.HasUnsavedStroke,Is.True);Assert.That((int)pencil.Observe()["points"],Is.EqualTo(2));Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);Assert.That(editor.BeginTemporaryRoom(out _),Is.False);
            pencil.Begin(1,new Ray(Vector3.one,Vector3.forward));Assert.That(pencil.SessionId,Is.EqualTo(session));Assert.That(pencil.IsDrawing,Is.False);Directory.Delete(obstacle);
            var call=new JObject {["id"]="object.drawing.resolve",["version"]=1,["arguments"]=new JObject {["operation"]="retry",["sessionId"]=session}};var executor=new RoomAgentExecutor(editor);var request=DrawingRequest(call);Assert.That(executor.Execute(request,out var error,out _),Is.True,error);Assert.That(pencil.HasUnsavedStroke,Is.False);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);string evidence=Environment.GetEnvironmentVariable("MAESTRO_DRAWING_AUTHORING");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"drawing-recovery.json"),new JObject {["before"]=capture,["call"]=call,["receipt"]=executor.Executions.Observe().DeepClone(),["after"]=pencil.Observe()}.ToString());}Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
            Directory.CreateDirectory(obstacle);pencil.Begin(0,new Ray(new Vector3(.2f,1,.5f),Vector3.forward));pencil.Move(0,new Ray(new Vector3(.3f,1,.5f),Vector3.forward));pencil.End(0);Assert.That(pencil.HasUnsavedStroke,Is.True);Assert.That(pencil.Resolve(session,true,out _,out _),Is.False);Assert.That(pencil.Resolve(pencil.SessionId,true,out _,out error),Is.True,error);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);Directory.Delete(obstacle);yield return null;
        }
        [UnityTest] public IEnumerator PhysicalDraftControlsNameTheirActualEffectAndPreserveSelectedSavedObjects()
        {
            var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;editor.ToggleDrawing();var trayObject=new GameObject("Draft tray");trayObject.transform.SetParent(root.transform,false);trayObject.AddComponent<RoomToolTray>().Build(editor,root.GetComponent<RoomInteraction>());var tools=trayObject.GetComponentsInChildren<PhysicalRoomAction>();
            string obstacle=Path.Combine(directory,"room.v6.json.pending");Directory.CreateDirectory(obstacle);void Draw(){pencil.Begin(0,new Ray(Vector3.one,Vector3.forward));pencil.Move(0,new Ray(Vector3.one+Vector3.right*.1f,Vector3.forward));pencil.End(0);}
            Draw();Assert.That(tools.Single(t=>t.Tool==RoomTool.Pencil).AccessibleName,Is.EqualTo("Retry stroke"));Assert.That(tools.Single(t=>t.Tool==RoomTool.Erase).AccessibleName,Is.EqualTo("Discard stroke"));Directory.Delete(obstacle);tools.Single(t=>t.Tool==RoomTool.Save).Activate();Assert.That(pencil.HasUnsavedStroke,Is.False);string saved=editor.SelectedId;Assert.That(editor.Read(saved).kind,Is.EqualTo(RoomObjectKind.Drawing));
            Directory.CreateDirectory(obstacle);Draw();Assert.That(pencil.HasUnsavedStroke,Is.True);tools.Single(t=>t.Tool==RoomTool.Erase).Activate();Assert.That(pencil.HasUnsavedStroke,Is.False);Assert.That(editor.Find(saved),Is.Not.Null);Assert.That(tools.Single(t=>t.Tool==RoomTool.Erase).AccessibleName,Is.EqualTo("Erase"));Directory.Delete(obstacle);yield return null;
        }
        [UnityTest] public IEnumerator RetiringFailedPhysicalStrokeReleasesItsWriteLeaseWithoutSavingOrReplaying()
        {
            var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;editor.ToggleDrawing();Directory.CreateDirectory(Path.Combine(directory,"room.v6.json.pending"));pencil.Begin(0,new Ray(Vector3.one,Vector3.forward));pencil.Move(0,new Ray(Vector3.one+Vector3.right*.1f,Vector3.forward));pencil.End(0);Assert.That(pencil.HasUnsavedStroke,Is.True);var retirement=editor.WriteGate.Retire();Assert.That(retirement.IsCompleted,Is.False);UnityEngine.Object.Destroy(pencil);yield return null;Assert.That(retirement.IsCompleted,Is.True);
        }
    }
}
