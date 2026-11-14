// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  [UnityTest] public IEnumerator NativeLiquidObservationIdleStreamAllowsOffscreenRetirementAndRestore() {
   var (_,from,_)=Containers();string area=NativeAreaFor(from);
   var data=editor.Read(from);data.position=new Vector3(0,.8f,-6);
   Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
   NativeObservationCamera();SampleViewerDemand();
   string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
   Assert.That(editor.ReadSpatialBounds(from,out _).Visual.Known,Is.True,"A reserved but hidden liquid stream must not make the loaded container's geometry unknown.");
   Assert.That(RetainedFor(from,RoomRetentionReason.ApproachingView),Is.False);
   yield return new WaitForSecondsRealtime((float)RoomEditor.ObservationGrace+.1f);
   Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
   Assert.That(editor.Find(from),Is.Null);
   Assert.That(editor.ReadSpatialBounds(from,out _).Visual.Known,Is.True);
   yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
   Assert.That(editor.Find(from).GetComponent<ContainerFillView>().NativeReady,Is.True);
   Assert.That(editor.Read(from).containers[0].amountMl,Is.EqualTo(200));
   Assert.That(editor.ReadSpatialBounds(from,out _).Visual.Known,Is.True);
   Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
  }
  [UnityTest] public IEnumerator NativeLiquidObservationActiveAndUnownedStreamsRemainConservative() {
   var (_,from,_)=Containers();var item=editor.Find(from);var fill=item.GetComponent<ContainerFillView>();
   Assert.That(editor.ReadSpatialBounds(from,out _).Visual.Known,Is.True);
   fill.ShowStream(new[]{new Vector3(0,1,2),new Vector3(0,0,3)},2,100,Color.blue);
   Assert.That(editor.ReadSpatialBounds(from,out _).Visual.Known,Is.False,"Active stream geometry must not be silently excluded.");
   var stream=item.GetComponentInChildren<LineRenderer>(true);
   Assert.That(stream,Is.Not.Null);Assert.That(stream.enabled,Is.True);
   stream.forceRenderingOff=true;
   Assert.That(editor.ReadSpatialBounds(from,out _).Visual.Known,Is.False,"Visibility must not shrink the prepared envelope of an active stream.");
   stream.forceRenderingOff=false;stream.gameObject.SetActive(false);
   Assert.That(editor.ReadSpatialBounds(from,out _).Visual.Known,Is.False,"An inactive child must not hide an active stream from the prepared envelope.");
   stream.gameObject.SetActive(true);
   fill.HideStream();Assert.That(editor.ReadSpatialBounds(from,out _).Visual.Known,Is.True);
   var unrelated=new GameObject("Unowned disabled stream");unrelated.transform.SetParent(item.transform,false);
   unrelated.AddComponent<LineRenderer>().enabled=false;
   Assert.That(editor.ReadSpatialBounds(from,out _).Visual.Known,Is.False,"Only the exact inactive renderer owned by the liquid reservation can be omitted.");
   UnityEngine.Object.Destroy(unrelated);yield return null;
   Assert.That(editor.ReadSpatialBounds(from,out _).Visual.Known,Is.True);
  }
 }
}
