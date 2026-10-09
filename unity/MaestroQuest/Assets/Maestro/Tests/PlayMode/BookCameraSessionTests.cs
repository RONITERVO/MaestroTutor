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
   using(editor.RuntimeGate.Hold("Camera test")){request.pulse++;camera.Receive(request,1.3);var blocked=camera.Poll(1.3);Assert.That(((Newtonsoft.Json.Linq.JArray)blocked["sources"]).Count,Is.EqualTo(0));Assert.That(blocked["frame"].Type,Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Null));}
   request.pulse++;camera.Receive(request,1.4);Assert.That((string)camera.Poll(1.4)["status"],Is.EqualTo("failed"));
   Object.Destroy(viewer);yield return null;
  }
  sealed class CameraFeedFixture:IBookCameraFeed,IBookCameraContextFeed {
   internal string Id=BookCameraSession.HeadsetSource,ContextValue;
   public string SourceId=>Id;
   public void Context(string value){ContextValue=value;}
   public bool Available=>true;
   internal int Starts,Stops;internal bool Allow;internal Newtonsoft.Json.Linq.JObject Pixels;
   public bool Start(out string error){Starts++;error=Allow?null:"permission-required";return Allow;}
   public bool Frame(out Newtonsoft.Json.Linq.JObject image,out string error){image=Pixels;Pixels=null;error=null;return image!=null;}
   public void Stop(){Stops++;}
  }
  [UnityTest] public IEnumerator BookCameraPermissionRequiresNewSelectionAndSourceChangeReleasesSensor(){
   var viewer=new GameObject("Camera viewer");viewer.transform.SetParent(root.transform,false);root.GetComponent<RoomInteraction>().Viewer=viewer.transform;
   var feed=new CameraFeedFixture();bool roomAvailable=true;
   var camera=new BookCameraSession(()=>roomAvailable?editor:null,feed);
   var request=new BookCameraRequest{session=new string('a',32),requestId="",sourceId="",acknowledged="",pulse=1};
   camera.Receive(request,1);Assert.That(((Newtonsoft.Json.Linq.JArray)camera.Poll(1)["sources"]).Count,Is.EqualTo(2));Assert.That(feed.Starts,Is.Zero);
   request.requestId=new string('b',32);request.sourceId=BookCameraSession.HeadsetSource;request.pulse++;
   camera.Receive(request,1.1);var permission=camera.Poll(1.1);Assert.That((string)permission["error"],Is.EqualTo("permission-required"));Assert.That(feed.Starts,Is.EqualTo(1));
   feed.Allow=true;request.pulse++;camera.Receive(request,1.2);Assert.That((string)camera.Poll(1.2)["status"],Is.EqualTo("failed"));Assert.That(feed.Starts,Is.EqualTo(1),"Late permission must never revive a failed lease");
   request.requestId=new string('c',32);request.pulse++;camera.Receive(request,1.3);Assert.That((string)camera.Poll(1.3)["status"],Is.EqualTo("streaming"));Assert.That(feed.Starts,Is.EqualTo(2));
   var pixels=new Texture2D(512,384,TextureFormat.RGBA32,false);feed.Pixels=HeadsetBookCamera.Encode(pixels,System.DateTime.UtcNow);
   var frame=camera.Poll(1.4);Assert.That((string)frame["frame"]["sourceId"],Is.EqualTo(BookCameraSession.HeadsetSource));Assert.That(frame["frame"]["capture"]["position"],Is.Null,"A sensor image is not a virtual room pose");
   roomAvailable=false;request.pulse++;camera.Receive(request,1.5);Assert.That((string)camera.Poll(1.5)["status"],Is.EqualTo("failed"));
   roomAvailable=true;request.pulse++;camera.Receive(request,1.6);Assert.That((string)camera.Poll(1.6)["status"],Is.EqualTo("failed"));Assert.That(feed.Starts,Is.EqualTo(2));
   int stops=feed.Stops;request.requestId=new string('d',32);request.sourceId=BookCameraSession.VirtualSource;request.pulse++;camera.Receive(request,1.7);
   Assert.That(feed.Stops,Is.GreaterThan(stops));Assert.That(camera.Poll(1.7)["frame"].Type,Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Object));Assert.That(feed.Starts,Is.EqualTo(2));
   camera.Suspend();Object.Destroy(pixels);Object.Destroy(viewer);yield return null;
  }
  [TestCase(null)] [TestCase("")] [TestCase("unrecognized")]
  public void BookCameraRejectsMissingOrUnknownSourceWithoutADevice(string source){
   var camera=new BookCameraSession(()=>editor);
   camera.Receive(new BookCameraRequest{session=new string('a',32),requestId="",acknowledged="",pulse=1},1);
   camera.Receive(new BookCameraRequest{session=new string('a',32),requestId=new string('b',32),sourceId=source,acknowledged="",pulse=2},1.1);
   Assert.That(camera.Poll(1.1)["frame"].Type,Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Null));
  }
  [UnityTest] public IEnumerator BookCameraSourceChangeNeedsANewLease(){
   var viewer=new GameObject("Camera viewer");viewer.transform.SetParent(root.transform,false);root.GetComponent<RoomInteraction>().Viewer=viewer.transform;
   var feed=new CameraFeedFixture{Allow=true};var camera=new BookCameraSession(()=>editor,feed);
   var request=new BookCameraRequest{session=new string('a',32),requestId=new string('b',32),sourceId=BookCameraSession.HeadsetSource,acknowledged="",pulse=1};
   camera.Receive(request,1);Assert.That((string)camera.Poll(1)["status"],Is.EqualTo("streaming"));
   int stops=feed.Stops;request.sourceId=BookCameraSession.VirtualSource;request.pulse++;camera.Receive(request,1.1);
   Assert.That(feed.Stops,Is.GreaterThan(stops));Assert.That((string)camera.Poll(1.1)["status"],Is.EqualTo("failed"));
   request.sourceId=BookCameraSession.HeadsetSource;request.pulse++;camera.Receive(request,1.2);
   Assert.That((string)camera.Poll(1.2)["status"],Is.EqualTo("failed"));Assert.That(feed.Starts,Is.EqualTo(1));
   camera.Suspend();Object.Destroy(viewer);yield return null;
  }
  [TestCase(1280,960,512,384)] [TestCase(1280,1280,512,512)] [TestCase(320,240,320,240)]
  public void HeadsetCameraPreservesSensorAspectRatio(int width,int height,int expectedWidth,int expectedHeight){
   Assert.That(HeadsetBookCamera.OutputSize(width,height),Is.EqualTo(new Vector2Int(expectedWidth,expectedHeight)));
  }

  [UnityTest] public IEnumerator BookCameraMultipleSourcesKeepConsentRoomBoundAndNeverStartTogether(){
   var viewer=new GameObject("Camera viewer");viewer.transform.SetParent(root.transform,false);root.GetComponent<RoomInteraction>().Viewer=viewer.transform;
   var physical=new CameraFeedFixture{Allow=true};var mixed=new CameraFeedFixture{Allow=true,Id=BookCameraSession.MixedSource};
   var camera=new BookCameraSession(()=>editor,physical,mixed);
   var request=new BookCameraRequest{session=new string('a',32),requestId=new string('b',32),sourceId=BookCameraSession.MixedSource,acknowledged="",pulse=1};
   camera.Receive(request,1);Assert.That(((Newtonsoft.Json.Linq.JArray)camera.Poll(1)["sources"]).Count,Is.EqualTo(3));
   Assert.That(mixed.Starts,Is.EqualTo(1));Assert.That(physical.Starts,Is.Zero);
   Assert.That(mixed.ContextValue,Does.StartWith(request.session+":"));Assert.That(physical.ContextValue,Is.Empty);
   camera.Suspend();request.pulse++;camera.Receive(request,1.1);Assert.That((string)camera.Poll(1.1)["status"],Is.EqualTo("failed"));Assert.That(mixed.Starts,Is.EqualTo(1));
   request.requestId=new string('c',32);request.sourceId=BookCameraSession.HeadsetSource;request.pulse++;camera.Receive(request,1.2);camera.Poll(1.2);
   Assert.That(mixed.ContextValue,Is.Empty);Assert.That(physical.Starts,Is.EqualTo(1));Assert.That(mixed.Starts,Is.EqualTo(1));
   camera.Close();Assert.That(physical.ContextValue,Is.Empty);Assert.That(mixed.ContextValue,Is.Empty);
   Object.Destroy(viewer);yield return null;
  }

 }
}
