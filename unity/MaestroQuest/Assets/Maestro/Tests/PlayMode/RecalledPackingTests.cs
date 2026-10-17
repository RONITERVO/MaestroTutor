// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests { public sealed partial class RoomRulesTests {
 void MovePackingRoom(){root.transform.SetPositionAndRotation(new Vector3(3,.4f,-2),Quaternion.Euler(0,63,0));Physics.SyncTransforms();}
 Vector3 PackingPreviewWorld()=>root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Material packing preview (unsaved)").position;
 [UnityTest] public IEnumerator PhysicalPackingRecalledRoomPublishesAtPreviewAndUndoRedoKeepsExactPosition(){
  var(id,view,capture)=PackingStage();MovePackingRoom();var before=editor.Read(id).heightFields[0].Copy();int count=editor.Snapshot().objects.Length;
  capture.Begin(0,SculptRay(view));Assert.That(capture.Active,Is.True,editor.Status);var preview=PackingPreviewWorld();
  Assert.That(editor.Read(id).heightFields[0].heights,Is.EqualTo(before.heights));
  capture.End(0);Assert.That(capture.Busy,Is.False,editor.Status);var ballId=PackedId();var ball=editor.Read(ballId);
  Assert.That(Vector3.Distance(editor.Find(ballId).transform.position,preview),Is.LessThan(.00001f),"Published ball must match the visible world preview after Recall");
  Assert.That(Vector3.Distance(ball.position,editor.transform.InverseTransformPoint(preview)),Is.LessThan(.00001f));
  Assert.That(before.VolumeLitres-editor.Read(id).heightFields[0].VolumeLitres,Is.EqualTo(ball.materialStores[0].amountLitres).Within(.000001));
  editor.Undo();Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));Assert.That(editor.Read(id).heightFields[0].heights,Is.EqualTo(before.heights));
  editor.Redo();Assert.That(Vector3.Distance(editor.Find(ballId).transform.position,preview),Is.LessThan(.00001f));yield return null;
 }
 [UnityTest] public IEnumerator PhysicalPackingRecalledRoomChecksWorldSpaceOnPublicationAndRetry(){
  var(id,view,capture)=PackingStage();MovePackingRoom();var before=editor.Read(id).heightFields[0].Copy();int count=editor.Snapshot().objects.Length;
  capture.Begin(0,SculptRay(view));Assert.That(capture.Active,Is.True,editor.Status);var preview=PackingPreviewWorld();
  var local=JsonUtility.FromJson<Vector3>(SculptFact("material.pack.capture")["position"].ToString());
  Assert.That(Vector3.Distance(editor.transform.TransformPoint(local),preview),Is.LessThan(.00001f),"Shared capture positions use the saved room frame");
  var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.transform.SetParent(root.transform,false);block.transform.position=preview;block.transform.localScale=Vector3.one*.02f;
  capture.End(0);Assert.That(capture.Retained,Is.True);Assert.That(editor.Read(id).heightFields[0].heights,Is.EqualTo(before.heights));Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
  Assert.That(capture.CanResolve(capture.SessionId,false,out var error),Is.False);Assert.That(error,Does.Contain("clear space"));
  Object.DestroyImmediate(block);ContainerRun(new RoomAgentExecutor(editor),SculptResolve(capture));
  Assert.That(capture.Busy,Is.False,editor.Status);Assert.That(Vector3.Distance(editor.Find(PackedId()).transform.position,preview),Is.LessThan(.00001f));yield return null;
 }
}}
