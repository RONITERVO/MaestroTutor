// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Book;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  [UnityTest] public IEnumerator BookCameraLeaseStopsOnSuspendAndOldRequestCannotRestartIt(){
   var viewer=new GameObject("Camera viewer");viewer.transform.SetParent(root.transform,false);root.GetComponent<RoomInteraction>().Viewer=viewer.transform;
   var camera=new BookCameraSession(()=>editor);var request=new BookCameraRequest{session=new string('a',32),requestId="",acknowledged="",pulse=1};
   camera.Receive(request,1);Assert.That(camera.Poll(1)["frame"].Type,Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Null));Assert.That(editor.ViewCaptureMetadata,Is.Null);
   request.requestId=new string('b',32);request.pulse++;camera.Receive(request,1.1);var first=camera.Poll(1.1);Assert.That(first["frame"].Type,Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Object));Assert.That(editor.ViewCaptureMetadata,Is.Null,"Camera pixels must never replace an agent's durable snapshot channel");
   camera.Suspend();request.pulse++;camera.Receive(request,1.2);Assert.That((string)camera.Poll(1.2)["status"],Is.EqualTo("failed"));
   yield return new WaitForSecondsRealtime(1.05f);request.requestId=new string('c',32);request.pulse++;camera.Receive(request,1.3);Assert.That(camera.Poll(1.3)["frame"].Type,Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Object));
   camera.Receive(request,3.2);Assert.That((string)camera.Poll(3.4)["status"],Is.EqualTo("failed"),"Replayed pulses cannot renew a lease");
   Object.Destroy(viewer);yield return null;
  }
  [UnityTest] public IEnumerator BookCameraHonoursRoomGatesAndDropsPendingPixels(){
   var viewer=new GameObject("Camera viewer");viewer.transform.SetParent(root.transform,false);root.GetComponent<RoomInteraction>().Viewer=viewer.transform;
   var camera=new BookCameraSession(()=>editor);var request=new BookCameraRequest{session=new string('a',32),requestId=new string('b',32),acknowledged="",pulse=1};
   camera.Receive(request,1);var first=camera.Poll(1);Assert.That(first["frame"].Type,Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Object));
   Assert.That(Newtonsoft.Json.Linq.JToken.DeepEquals(first["frame"],camera.Poll(1.2)["frame"]),Is.True);
   using(editor.RuntimeGate.Hold("Camera test")){request.pulse++;camera.Receive(request,1.3);var blocked=camera.Poll(1.3);Assert.That((bool)blocked["available"],Is.False);Assert.That(blocked["frame"].Type,Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Null));}
   request.pulse++;camera.Receive(request,1.4);Assert.That((string)camera.Poll(1.4)["status"],Is.EqualTo("failed"));
   Object.Destroy(viewer);yield return null;
  }
 }
}
