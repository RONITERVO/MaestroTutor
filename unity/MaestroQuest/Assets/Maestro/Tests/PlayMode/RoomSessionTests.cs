// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
namespace Maestro.Quest.Tests
{
 public sealed class RoomSessionTests
 {
  GameObject root;string directory;RoomEditor editor;RoomInteraction room;RoomRules runtime;RoomAgent observer;RoomExecutions executions;
  [UnitySetUp] public IEnumerator Setup(){
   directory=Path.Combine(Path.GetTempPath(),"MaestroSession-"+Guid.NewGuid().ToString("N"));root=new GameObject("Room session tests");root.AddComponent<XRInteractionManager>();room=root.AddComponent<RoomInteraction>();
   RoomItem Item(string name){var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.transform.SetParent(root.transform,false);obj.name=name;var item=obj.AddComponent<RoomItem>();item.Configure(new[]{obj.GetComponent<Collider>()});room.Register(item);return item;}
   var book=Item("book");var maestro=Item("maestro");editor=root.AddComponent<RoomEditor>();editor.Initialize(room,book,maestro,directory);
   var animations=root.AddComponent<AnimationWorkshop>();animations.Initialize(editor);var workshop=root.AddComponent<RuleWorkshop>();workshop.Initialize(editor,directory);
   runtime=root.AddComponent<RoomRules>();runtime.Initialize(workshop,editor,animations,null,room,null);
   observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);executions=new RoomExecutions(editor);float memoryDeadline=Time.realtimeSinceStartup+5;while(!workshop.Memory.Ready&&Time.realtimeSinceStartup<memoryDeadline)yield return null;Assert.That(workshop.Memory.Ready,Is.True);Assert.That(workshop.Memory.Error,Is.Null);yield return null;
  }
  [UnityTearDown] public IEnumerator Cleanup(){if(root)UnityEngine.Object.Destroy(root);yield return null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
  JObject Call(string operation,string session=null)=>new() {["id"]="room.session",["version"]=1,["arguments"]=new JObject {["operation"]=operation,["sessionId"]=session??editor.TemporarySessionId}};
  JObject Request(JObject call)=>new() {["operation"]="start",["runId"]=runtime.Scheduler.Receipts.NextId,["call"]=call};
  string Start(string operation){var request=Request(Call(operation));Assert.That(executions.Execute(request,out var error),Is.True,error);return (string)request["runId"];}
  string Create(string name){var args=BehaviourCatalog.Action("object.create").Example;args["name"]=name;var request=Request(new JObject {["id"]="object.create",["version"]=1,["arguments"]=args});Assert.That(executions.Execute(request,out var error),Is.True,error);return (string)runtime.Scheduler.Invocation((string)request["runId"])["output"]["objectId"];}
  IEnumerator Finish(){float end=Time.realtimeSinceStartup+5;while((editor.TemporarySavePending||runtime.Scheduler.RunningCount>0)&&Time.realtimeSinceStartup<end)yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(editor.TemporarySavePending,Is.False);}
  RoomDocument Saved()=>new RoomStorage(directory).Load(out _);
  void Evidence(string name){var path=Environment.GetEnvironmentVariable("MAESTRO_SESSION_EVIDENCE");if(string.IsNullOrEmpty(path))return;Directory.CreateDirectory(path);var state=observer.Observe();state.execution=executions.Observe();state.visible=true;File.WriteAllText(Path.Combine(path,name+".json"),RoomAgentWire.Serialize(state));}
  TaskCompletionSource<string> EarlierSaveGate() {
   var gate=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
   typeof(RoomEditor).GetField("saveTask",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(editor,gate.Task);
   // Prevent a future synchronous-wait regression from hanging the entire test runner.
   _=Task.Delay(5000).ContinueWith(_=>gate.TrySetResult("Test writer watchdog elapsed"));return gate;
  }
  [UnityTest] public IEnumerator BeginningDoesNotBlockFramesOrPersistLaterManualEditsAfterCancel() {
   var gate=EarlierSaveGate();string run=null,id=null;
   try {
    run=Start("begin");Assert.That(gate.Task.IsCompleted,Is.False,"Begin must not wait on Unity's thread");
    Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("preparing"));
    Assert.That(editor.ObserveTemporaryRoom().phase,Is.EqualTo("starting"));
    Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"While starting",Vector3.up,1,Color.white,out id,out var error),Is.True,error);
    int frame=Time.frameCount;yield return null;yield return null;Assert.That(Time.frameCount,Is.GreaterThan(frame));
    Assert.That(editor.TemporarySavePending,Is.True);Assert.That(File.Exists(Path.Combine(directory,RoomStorage.FileName)),Is.False);
    Assert.That(runtime.Scheduler.CancelInvocation(run,out error),Is.True,error);
    Assert.That((string)runtime.Scheduler.Invocation(run)["status"],Does.Contain("temporary mode"));
    Assert.That(editor.DiscardTemporaryRoom(out _),Is.False,"Cannot retract a dispatched baseline write");
   } finally {gate.TrySetResult(null);}
   yield return Finish();Assert.That(editor.TemporaryRoom,Is.True);Assert.That(editor.Find(id),Is.Not.Null);
   Assert.That(Saved().objects.Any(x=>x.id==id),Is.False);Assert.That(editor.ObserveTemporaryRoom().phase,Is.EqualTo("ready"));
   Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));
   var kept=Start("keep");yield return Finish();Assert.That(Saved().objects.Any(x=>x.id==id),Is.True);
   Assert.That((string)runtime.Scheduler.Invocation(kept)["phase"],Is.EqualTo("completed"));
  }
  [UnityTest] public IEnumerator BaselineWritesAfterTheEarlierAutosaveAndCapturesOnlyItsStartingState() {
   Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Older",Vector3.up,1,Color.white,out var id,out _),Is.True);
   var older=editor.Snapshot();editor.Find(id).transform.localPosition=Vector3.up*2;editor.RememberPlacement(id);
   var gate=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
   var store=new RoomStorage(directory);var previous=Task.Run(async()=>{await gate.Task;store.Save(older,out var issue);return issue;});
   typeof(RoomEditor).GetField("saveTask",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(editor,previous);
   _=Task.Delay(5000).ContinueWith(_=>gate.TrySetResult("Test writer watchdog elapsed"));
   try {
    var run=Start("begin");Assert.That(gate.Task.IsCompleted,Is.False);
    Assert.That(editor.MoveObject(id,Vector3.up*3,out var error),Is.True,error);
    Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("preparing"));
   } finally {gate.TrySetResult(null);}
   yield return Finish();Assert.That(Saved().objects.Single(x=>x.id==id).position,Is.EqualTo(Vector3.up*2));
   Assert.That(editor.Read(id).position,Is.EqualTo(Vector3.up*3));
  }
  [UnityTest] public IEnumerator FailedBaselineHasFailedReceiptAndCanBeKeptWithoutRecreatingItsEdits() {
   var blocked=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(blocked);
   var request=Request(Call("begin"));executions.Execute(request,out _);yield return Finish();
   Assert.That((string)runtime.Scheduler.Invocation((string)request["runId"])["phase"],Is.EqualTo("failed"));
   Assert.That(editor.TemporaryRoom,Is.True);var id=Create("Preserve after failure");Evidence("failed-start");
   Directory.Delete(blocked);Start("keep");yield return Finish();Assert.That(Saved().objects.Count(x=>x.id==id),Is.EqualTo(1));
   Start("discard");editor.Undo();Assert.That(editor.Find(id),Is.Null);
  }
  [UnityTest] public IEnumerator FaultedEarlierWriterDoesNotEscapeTheCompletionContract() {
   var gate=EarlierSaveGate();gate.SetException(new IOException("Injected earlier save failure"));
   var request=Request(Call("begin"));executions.Execute(request,out _);yield return Finish();
   Assert.That((string)runtime.Scheduler.Invocation((string)request["runId"])["phase"],Is.EqualTo("failed"));
   Assert.That(editor.TemporaryRoom,Is.True);Assert.That(editor.TemporarySaveError,Is.Not.Null);
   Start("keep");yield return Finish();Assert.That(editor.TemporarySaveError,Is.Null);
  }
  [UnityTest] public IEnumerator PauseFinishesOnlyTheStartingSnapshotNotEditsMadeWhileItWasSaving() {
   var gate=EarlierSaveGate();string id;
   try {
    Start("begin");Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Later",Vector3.up,1,Color.white,out id,out _),Is.True);
   } finally {gate.TrySetResult(null);}
   editor.SendMessage("OnApplicationPause",true);Assert.That(editor.TemporarySavePending,Is.False);
   Assert.That(Saved().objects.Any(x=>x.id==id),Is.False);Assert.That(editor.Find(id),Is.Not.Null);yield return Finish();
  }
  [UnityTest] public IEnumerator NativeCatalogCycleHasDurableReceiptsFreshScopesAndOneGroupedUndo(){
   Evidence("initial");string inactive=editor.TemporarySessionId;var gate=EarlierSaveGate();var begin=Request(Call("begin"));
   try {Assert.That(executions.Execute(begin,out var accepted),Is.True,accepted);Assert.That(gate.Task.IsCompleted,Is.False);Evidence("starting");}
   finally {gate.TrySetResult(null);}
   yield return Finish();string error;
   Assert.That(editor.TemporarySessionId,Is.Not.EqualTo(inactive));var scope=editor.TemporarySessionId;Evidence("begun");
   Assert.That(executions.Execute(begin,out error),Is.True,error);Assert.That(editor.TemporarySessionId,Is.EqualTo(scope),"Duplicate Begin never opens another session");
   var created=Create("Temporary shared creation");Assert.That(Saved().objects.Any(x=>x.id==created),Is.False);Evidence("created");
   var keep=Start("keep");yield return Finish();Assert.That((string)runtime.Scheduler.Invocation(keep)["phase"],Is.EqualTo("completed"));
   Assert.That(Saved().objects.Any(x=>x.id==created),Is.True);Evidence("saved");
   string late=Create("Later unsaved");Evidence("later");Start("discard");Assert.That(editor.TemporaryRoom,Is.False);Assert.That(editor.TemporarySessionId,Is.Not.EqualTo(scope));
   Assert.That(editor.Find(late),Is.Null);Assert.That(editor.Find(created),Is.Not.Null);Evidence("ended");
   Assert.That(executions.Execute(Request(Call("begin",inactive)),out error),Is.False);Assert.That(error,Does.Contain("session changed"));
   Assert.That(executions.Execute(Request(Call("keep",scope)),out error),Is.False);
   editor.Undo();Assert.That(editor.Find(created),Is.Null);editor.Redo();Assert.That(editor.Find(created),Is.Not.Null);
  }
  [UnityTest] public IEnumerator CancellingKeepDoesNotRetractItsWriteOrPersistLaterEdits(){
   Start("begin");yield return Finish();string kept=Create("Kept");string run=Start("keep");
   string before=(string)runtime.Scheduler.Invocation(run)["phase"];Assert.That(runtime.Scheduler.CancelInvocation(run,out _),Is.True);
   if(before!="completed")Assert.That((string)runtime.Scheduler.Invocation(run)["status"],Does.Contain("snapshot"));
   string late=Create("Later");yield return Finish();
   Assert.That(Saved().objects.Any(x=>x.id==kept),Is.True);Assert.That(Saved().objects.Any(x=>x.id==late),Is.False);Assert.That(editor.Find(late),Is.Not.Null);
   Evidence("cancelled-save");
  }
  [UnityTest] public IEnumerator OtherProgramsMustStopBeforeGlobalBoundariesAndAreNeverSilentlyCancelled(){
   var wait=new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=20}};
   Assert.That(runtime.Scheduler.Invoke(wait,Time.unscaledTime,out var other,out var error),Is.True,error);
   var catalog=new RoomCapabilityCatalog(editor);Assert.That(catalog.Execute(new JObject {["operation"]="check",["call"]=Call("begin")},out _),Is.True);
   Assert.That((bool)catalog.Observe()["occupied"],Is.True);Assert.That((bool)catalog.Observe()["available"],Is.False);
   Assert.That(executions.Execute(Request(Call("begin")),out error),Is.False);Assert.That(error,Does.Contain("other room actions"));Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
   runtime.Scheduler.CancelInvocation(other,out _);Start("begin");yield return Finish();runtime.Scheduler.Invoke(wait,Time.unscaledTime,out other,out _);
   Assert.That(executions.Execute(Request(Call("discard")),out error),Is.False);Assert.That(editor.TemporaryRoom,Is.True);Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
   runtime.Scheduler.CancelInvocation(other,out _);Start("discard");yield return null;
  }
  [UnityTest] public IEnumerator ActualWriterFailureHasFailedReceiptAndKeepsTemporaryEditsForAnExplicitRetry(){
   Start("begin");yield return Finish();var id=Create("Retry once");var blocked=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(blocked);
   var request=Request(Call("keep"));executions.Execute(request,out _);yield return Finish();
   Assert.That((string)runtime.Scheduler.Invocation((string)request["runId"])["phase"],Is.EqualTo("failed"));Assert.That(editor.Find(id),Is.Not.Null);Evidence("failed-save");
   Directory.Delete(blocked);Start("keep");yield return Finish();Assert.That(Saved().objects.Count(x=>x.id==id),Is.EqualTo(1));
  }
  [UnityTest] public IEnumerator ProgramCanCreateItsFullResourceBudgetThenKeepAndEndThroughTheSameCalls(){
   var source=JObject.Parse(BehaviourProgram.FromInvocation(Call("begin")));source["version"]=3;source["state"]=new JArray();source["events"]=new JArray();
   var body=(JArray)source["functions"][0]["body"];body[0]["bindings"]=new JObject {["sessionId"]=new JObject {["fact"]="room.sessionId"}};
   var creation=JObject.Parse(BehaviourProgram.FromInvocation(new JObject {["id"]="object.create",["version"]=1,["arguments"]=BehaviourCatalog.Action("object.create").Example}))["functions"][0]["body"][0];creation["id"]="create";
   body.Add(new JObject {["id"]="many",["op"]="repeat",["count"]=new JObject {["value"]=16},["body"]=new JArray(creation)});
   foreach(string op in new[]{"keep","discard"}){var node=body[0].DeepClone();node["id"]=op;node["arguments"]["operation"]=op;body.Add(node);}
   var sequence=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Build and keep",program=source.ToString()};runtime.Scheduler.Configure(new RuleDocument {sequences=new[]{sequence}});
   Assert.That(runtime.Scheduler.Trigger(sequence.id,Time.unscaledTime),Is.True,runtime.Scheduler.LastError);yield return Finish();
   Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),runtime.Scheduler.LastError);Assert.That(editor.TemporaryRoom,Is.False);
   Assert.That(Saved().objects.Length,Is.EqualTo(21));editor.Undo();Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(5));
  }
  [UnityTest] public IEnumerator SolidTrayButtonsUseTheSameSessionCapabilityAndReceiptHistory(){
   var tray=new GameObject("Creation tools");tray.transform.SetParent(root.transform,false);tray.transform.localPosition=new Vector3(2,0,1);tray.AddComponent<RoomToolTray>().Build(editor,room);
   var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;yield return null;Physics.SyncTransforms();
   void Click(RoomTool kind){var button=tray.GetComponentsInChildren<PhysicalRoomAction>().Single(x=>x.Tool==kind);var ray=new Ray(button.transform.position-Vector3.forward,Vector3.forward);Assert.That(router.Begin(0,ray),Is.True);router.End(0,ray);}
   Click(RoomTool.BeginTemporary);Assert.That(editor.TemporaryRoom,Is.True);yield return Finish();
   var evidence=Environment.GetEnvironmentVariable("MAESTRO_SESSION_EVIDENCE");
   if(!string.IsNullOrEmpty(evidence)) {
    Directory.CreateDirectory(evidence);var cameraRoot=new GameObject("Session controls camera");cameraRoot.transform.SetParent(root.transform,false);
    var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.85f,.89f,.91f);
    cameraRoot.transform.position=tray.transform.position+new Vector3(0,-.06f,-1.35f);cameraRoot.transform.LookAt(tray.transform.position+Vector3.down*.06f);camera.fieldOfView=38;
    var render=new RenderTexture(1200,1100,24);var pixels=new Texture2D(1200,1100,TextureFormat.RGB24,false);var previous=RenderTexture.active;
    try {camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1200,1100),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(evidence,"room-tools.png"),pixels.EncodeToPNG());}
    finally {RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(cameraRoot);}
   }
   Create("Physical keep");Click(RoomTool.KeepTemporary);yield return Finish();
   Assert.That(editor.TemporarySaveRevision,Is.EqualTo(1));Click(RoomTool.DiscardTemporary);Assert.That(editor.TemporaryRoom,Is.False);
   Assert.That(runtime.Scheduler.ObserveInvocations(null)["outcomes"].Count(x=>(string)x["capability"]=="room.session"),Is.EqualTo(3));
  }
 }
}
