// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;using System.Collections;using System.IO;using System.Linq;
using Maestro.Quest.Rules;using Maestro.Quest.Creation;using Maestro.Quest.Programs;using Newtonsoft.Json.Linq;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {public sealed partial class RoomRulesTests {
 [UnityTest] public IEnumerator ChessKitProgramsCreateCompletePhysicalSetAndSharedSnappingMovesAPawn(){
  var executor=new RoomAgentExecutor(editor);var origin=new Vector3(4,2,4);var before=editor.Snapshot().objects.Select(o=>o.id).ToHashSet();
  var boardId=(string)TemplateRun(executor,TemplateCall("chessboard",origin))["selected"]["output"]["objectId"];
  foreach(var side in new[]{"white","black"}){
   var source=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-chess-"+side+".json")));source["functions"][0]["body"][0]["args"][0]["value"]=JObject.Parse(JsonUtility.ToJson(origin));
   Assert.That(workshop.Execute(new RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit{kind="save",reference="chess",sequence=new RuleSequence{id="",name="Create "+side,program=source.ToString(Newtonsoft.Json.Formatting.None)}}}},out var error,out var saved),Is.True,error);
   int count=editor.Snapshot().objects.Length;Assert.That(runtime.Trigger(saved.Single()),Is.True,runtime.Scheduler.LastError);
   for(int i=0;i<30&&runtime.Scheduler.RunningCount>0;i++){runtime.Scheduler.Tick(Time.unscaledTime);yield return null;}
   Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),runtime.Scheduler.LastError);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+16));editor.Undo();Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));editor.Redo();
  }
  var created=editor.Snapshot().objects.Where(o=>!before.Contains(o.id)).ToArray();Assert.That(created.Length,Is.EqualTo(33));Assert.That(physics.Running,Is.False);Assert.That(new RoomStorage(directory).Load(out _).objects.Count(o=>!before.Contains(o.id)),Is.EqualTo(33));
  var board=editor.Read(boardId);Assert.That(board.snapPoints.Length,Is.EqualTo(64));Assert.That(board.recipe.parts.Single(p=>p.id=="Squares").pattern.columns,Is.EqualTo(8));
  foreach(var piece in created.Where(o=>o.id!=boardId)){
   var square=piece.name.Split(' ').Last();var point=board.snapPoints.Single(p=>p.id==square);Assert.That(Vector3.Distance(piece.position,origin+point.frame.position+Vector3.up*.001f),Is.LessThan(.0001f));Assert.That(editor.Find(piece.id).Grab,Is.Not.Null);Assert.That(piece.snapPoints.Single().id,Is.EqualTo("Foot"));
  }
  Assert.That(created.Single(o=>o.name=="White queen D1"),Is.Not.Null);Assert.That(created.Single(o=>o.name=="Black king E8"),Is.Not.Null);
  // Use real native physics: every collision proxy must settle on the board, not fall through or tip at rest.
  Physics.SyncTransforms();physics.SetSurfaces(true,"Synthetic chess setup");physics.StartPhysics();for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();
  foreach(var piece in created.Where(o=>o.id!=boardId)){var t=editor.Find(piece.id).transform;Assert.That(t.localPosition.y,Is.InRange(2.014f,2.035f),piece.name);Assert.That(Vector3.Dot(t.up,Vector3.up),Is.GreaterThan(.97f),piece.name);}
  physics.PausePhysics();var pawn=created.Single(o=>o.name=="White pawn E2");var from=editor.Find(pawn.id).transform.localPosition;
  var placement=new RoomSnapPlacement{members=new[]{new TransformMember{target=pawn.id,revision=editor.ObjectRevision(pawn.id)}},point="Foot",destination=new SnapDestination{target=boardId,revision=editor.ObjectRevision(boardId),point="E4"}};
  Assert.That(executor.Execute(SnapRequest(placement),out var snapError,out _),Is.True,snapError);Assert.That(Vector3.Distance(editor.Find(pawn.id).transform.localPosition,origin+board.snapPoints.Single(p=>p.id=="E4").frame.position),Is.LessThan(.0001f));editor.Undo();Assert.That(Vector3.Distance(editor.Find(pawn.id).transform.localPosition,from),Is.LessThan(.0001f));
  string output=Environment.GetEnvironmentVariable("MAESTRO_CHESS_PREVIEW");if(!string.IsNullOrEmpty(output)){
   var camera=new GameObject("Chess acceptance",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.51f;camera.nearClipPlane=.01f;camera.farClipPlane=5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.94f,.91f,.85f);camera.transform.position=origin+new Vector3(.65f,.95f,-1.15f);camera.transform.LookAt(origin+Vector3.up*.03f);
   var render=new RenderTexture(1024,1024,24);var pixels=new Texture2D(1024,1024,TextureFormat.RGB24,false);var prior=RenderTexture.active;
   try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1024,1024),0,0);pixels.Apply();File.WriteAllBytes(output,pixels.EncodeToPNG());}finally{RenderTexture.active=prior;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(camera.gameObject);}
  }
 }
}}
