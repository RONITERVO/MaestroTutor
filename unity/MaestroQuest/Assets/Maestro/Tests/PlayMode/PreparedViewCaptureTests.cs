// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        Transform PreparedCaptureViewer(){
            var viewer=new GameObject("Prepared capture viewer");viewer.transform.SetParent(root.transform,false);
            root.GetComponent<RoomInteraction>().Viewer=viewer.transform;return viewer.transform;
        }
        IEnumerator PreparedCaptureDone(VirtualRoomCapture capture){
            double deadline=Time.realtimeSinceStartupAsDouble+12;
            while(!capture.Done&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(capture.Done,Is.True,"Owned capture must complete or fail within the test deadline");
        }
        int CaptureCameraCount()=>root.scene.GetRootGameObjects().Count(go=>go.name=="Virtual room snapshot");
        [UnityTest] public IEnumerator PreparedViewCaptureLoadsVisibleGeometryAndReturnsItsPixelsWithoutChangingSavedRoom(){
            string id=editor.Identity(block);var data=editor.Read(id);data.color=Color.red;data.scale=1;data.position=new Vector3(0,0,3);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            string area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            string behind=editor.Snapshot().objects.First(x=>!x.IsBuiltIn&&x.id!=id).id;string hidden=NativeAreaFor(behind);
            editor.Find(behind).transform.position=new Vector3(0,0,-4);Assert.That(editor.RetireNativeArea(hidden,out error),Is.True,error);yield return null;
            PreparedCaptureViewer();int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
            using var capture=editor.BeginViewCapture(out error);Assert.That(capture,Is.Not.Null,error);
            yield return PreparedCaptureDone(capture);Assert.That(capture.Read(out var image,out error),Is.True,error);
            Assert.That(editor.Find(id),Is.Not.Null);Assert.That(editor.Find(behind),Is.Null,"Independent off-camera geometry is not required");
            var pixels=new Texture2D(2,2);Assert.That(pixels.LoadImage(Convert.FromBase64String((string)image["data"])),Is.True);
            Assert.That(pixels.GetPixels32().Count(p=>p.r>p.g*1.5f&&p.r>p.b*1.5f&&p.r>90),Is.GreaterThan(50),"Loaded red object must exist in the delivered pixels");
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(editor.ViewCaptureMetadata,Is.Null);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_VIEW_CAPTURE_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllBytes(Path.Combine(evidence,"prepared-room.jpg"),Convert.FromBase64String((string)image["data"]));}
            UnityEngine.Object.Destroy(pixels);yield return null;Assert.That(CaptureCameraCount(),Is.Zero);
        }
        [UnityTest] public IEnumerator PreparedViewCaptureAgentReceiptWaitsForWholeConnectedGroup(){
            string id=RetireDisplayGroup();yield return null;PreparedCaptureViewer();
            var executor=new RoomAgentExecutor(editor);var request=TemplateRequest(new JObject{["id"]="room.view.capture",["version"]=1,["arguments"]=new JObject()});
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);
            Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("preparing"));Assert.That(editor.ViewCaptureMetadata,Is.Null);
            double deadline=Time.realtimeSinceStartupAsDouble+12;
            while((string)executor.Executions.Observe()["selected"]["phase"]=="preparing"&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            var receipt=executor.Executions.Observe()["selected"];Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());
            Assert.That(editor.Find(id),Is.Not.Null);Assert.That(JToken.DeepEquals(receipt["output"],editor.ViewCaptureMetadata),Is.True);
            editor.ClearViewCapture();Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.ViewCaptureMetadata,Is.Null);
        }
        IEnumerator CancelPreparedCapture(string reason){
            string id=RetireDisplayGroup();yield return null;PreparedCaptureViewer();
            using var capture=editor.BeginViewCapture(out var error);Assert.That(capture,Is.Not.Null,error);Assert.That(capture.Done,Is.False);
            IDisposable hold=null;
            try{
                if(reason=="stop")capture.Dispose();
                else if(reason=="session")editor.ClearViewCapture();
                else if(reason=="focus")editor.SendMessage("OnApplicationFocus",false);
                else if(reason=="disable")editor.enabled=false;
                else if(reason=="hold")hold=editor.RuntimeGate.Hold("Capture cancellation");
                else{var data=editor.Read(editor.Identity(block));data.name="Changed while loading";Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);}
                yield return PreparedCaptureDone(capture);Assert.That(capture.Read(out _,out error),Is.False);Assert.That(error,Is.Not.Null);
                Assert.That(editor.ViewCaptureMetadata,Is.Null);yield return null;Assert.That(CaptureCameraCount(),Is.Zero);
            }finally{hold?.Dispose();editor.enabled=true;editor.SendMessage("OnApplicationFocus",true);}
        }
        [UnityTest] public IEnumerator PreparedViewCaptureStopCancelsPendingWork()=>CancelPreparedCapture("stop");
        [UnityTest] public IEnumerator PreparedViewCaptureSessionChangeDiscardsPendingPixels()=>CancelPreparedCapture("session");
        [UnityTest] public IEnumerator PreparedViewCaptureFocusLossCancelsPreparation()=>CancelPreparedCapture("focus");
        [UnityTest] public IEnumerator PreparedViewCaptureDisableCancelsPreparation()=>CancelPreparedCapture("disable");
        [UnityTest] public IEnumerator PreparedViewCaptureRoomHoldCancelsPreparation()=>CancelPreparedCapture("hold");
        [UnityTest] public IEnumerator PreparedViewCaptureSceneEditRejectsStalePreparation()=>CancelPreparedCapture("edit");
        [UnityTest] public IEnumerator PreparedViewCaptureRefreshesViewerPoseAfterLoading(){
            string id=RetireDisplayGroup();yield return null;var viewer=PreparedCaptureViewer();
            using var capture=editor.BeginViewCapture(out var error);Assert.That(capture,Is.Not.Null,error);Assert.That(capture.Done,Is.False);
            viewer.SetPositionAndRotation(new Vector3(.2f,.1f,0),Quaternion.Euler(0,12,0));
            yield return PreparedCaptureDone(capture);Assert.That(capture.Read(out var image,out error),Is.True,error);
            Assert.That((float)image["capture"]["position"]["x"],Is.EqualTo(.2f).Within(.001));Assert.That((float)image["capture"]["rotation"]["y"],Is.EqualTo(viewer.rotation.y).Within(.001));
        }
        [UnityTest] public IEnumerator PreparedViewCaptureSynchronousCallerCannotReturnAnIncompleteRoom(){
            RetireDisplayGroup();yield return null;PreparedCaptureViewer();
            Assert.That(editor.CaptureCameraView(out var image,out var error),Is.False);Assert.That(image,Is.Null);Assert.That(error,Does.Contain("loading"));
            yield return null;Assert.That(CaptureCameraCount(),Is.Zero);
        }
        [UnityTest] public IEnumerator PreparedViewCaptureBookWaitsForContentAndKeepsSnapshotChannelSeparate(){
            string id=RetireDisplayGroup();yield return null;PreparedCaptureViewer();
            var camera=new BookCameraSession(()=>editor);var request=new BookCameraRequest{session=new string('a',32),requestId=new string('b',32),acknowledged="",pulse=1};
            camera.Receive(request,1);var first=camera.Poll(1);Assert.That(first["frame"].Type,Is.EqualTo(JTokenType.Null));
            double deadline=Time.realtimeSinceStartupAsDouble+12;JObject state=first;
            while(state["frame"].Type==JTokenType.Null&&(string)state["status"]!="failed"&&Time.realtimeSinceStartupAsDouble<deadline){
                yield return null;request.pulse++;camera.Receive(request,1.1);state=camera.Poll(1.1);
            }
            Assert.That((string)state["status"],Is.EqualTo("streaming"),state.ToString());Assert.That(state["frame"].Type,Is.EqualTo(JTokenType.Object));Assert.That(editor.Find(id),Is.Not.Null);
            Assert.That(editor.ViewCaptureMetadata,Is.Null);camera.Close();yield return null;Assert.That(CaptureCameraCount(),Is.Zero);
        }
        [UnityTest] public IEnumerator PreparedViewCaptureBookRevocationDropsLateFrameAndReleasesCamera(){
            RetireDisplayGroup();yield return null;PreparedCaptureViewer();
            var camera=new BookCameraSession(()=>editor);var request=new BookCameraRequest{session=new string('a',32),requestId=new string('b',32),acknowledged="",pulse=1};
            camera.Receive(request,1);Assert.That(camera.Poll(1)["frame"].Type,Is.EqualTo(JTokenType.Null));camera.Suspend();
            double deadline=Time.realtimeSinceStartupAsDouble+12;while(CaptureCameraCount()>0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(CaptureCameraCount(),Is.Zero);request.pulse++;camera.Receive(request,1.2);var result=camera.Poll(1.2);
            Assert.That((string)result["status"],Is.EqualTo("failed"));Assert.That(result["frame"].Type,Is.EqualTo(JTokenType.Null));Assert.That(editor.ViewCaptureMetadata,Is.Null);
        }
        [UnityTest] public IEnumerator PreparedViewCaptureMissingModelFailsWithoutReplacingPreviousSnapshot(){
            yield return GeometryModel(StaticModelFixture.Create());string id=geometryTarget,area=NativeAreaFor(id);
            editor.Find(id).transform.position=new Vector3(0,0,3);PreparedCaptureViewer();
            Assert.That(editor.CaptureView(out var previous,out var error),Is.True,error);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            string path=Path.Combine(directory,"models",editor.Read(id).modelHash+".glb"),parked=path+".capture-missing";File.Move(path,parked);
            try{
                yield return new WaitForSecondsRealtime(1.05f);using var capture=editor.BeginViewCapture(out error);Assert.That(capture,Is.Not.Null,error);
                yield return PreparedCaptureDone(capture);Assert.That(capture.Read(out var image,out error),Is.False);Assert.That(image,Is.Null);Assert.That(error,Is.Not.Empty);
                Assert.That(JToken.DeepEquals(previous,editor.ViewCaptureMetadata),Is.True);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.HasSavedObject(id),Is.True);
            }finally{File.Move(parked,path);}
        }
        [UnityTest] public IEnumerator PreparedViewCaptureUnknownAuthoredBoundsStillRequireGeometry(){
            string id=RetireDisplayBlock();yield return null;
            var changed=editor.Read(id);changed.scale=2;changed.color=Color.red;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out var error),Is.True,error);
            Assert.That((bool)editor.ObserveAuthoredBounds(id)["prepared"],Is.False);PreparedCaptureViewer();
            using var capture=editor.BeginViewCapture(out error);Assert.That(capture,Is.Not.Null,error);yield return PreparedCaptureDone(capture);
            Assert.That(capture.Read(out _,out error),Is.True,error);Assert.That(editor.Find(id),Is.Not.Null);
        }
        [UnityTest] public IEnumerator PreparedViewCaptureBookExpiryCancelsWithoutAnotherPoll(){
            RetireDisplayGroup();yield return null;PreparedCaptureViewer();
            var camera=new BookCameraSession(()=>editor);var request=new BookCameraRequest{session=new string('a',32),requestId=new string('b',32),acknowledged="",pulse=1};
            camera.Receive(request,1);Assert.That(camera.Poll(1)["frame"].Type,Is.EqualTo(JTokenType.Null));
            typeof(BookCameraSession).GetField("receivedAt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(camera,-10d);
            double deadline=Time.realtimeSinceStartupAsDouble+12;while(CaptureCameraCount()>0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(CaptureCameraCount(),Is.Zero);var state=camera.Poll(3.1);Assert.That((string)state["status"],Is.EqualTo("failed"));Assert.That(state["frame"].Type,Is.EqualTo(JTokenType.Null));camera.Close();
        }
        [UnityTest] public IEnumerator PreparedViewCaptureResidentImportWaitsForActualLoaderInsteadOfTreatingWaitingAsFailure(){
            var asset=Maestro.Quest.Imports.ModelLibrary.Inspect("capture.glb",StaticModelFixture.Create());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var barrier=new ModelLoadBarrier();var blocker=ResidencyModel();var blocked=blocker.LoadAsync(asset,barrier);VirtualRoomCapture capture=null;
            try{
                yield return new WaitUntil(()=>barrier.Entered||blocked.IsCompleted);Assert.That(barrier.Entered,Is.True);
                Assert.That(editor.CreateImportedModel(asset.Hash,out var id,out var error),Is.True,error);editor.Find(id).transform.position=new Vector3(0,0,3);PreparedCaptureViewer();
                var view=editor.Find(id).GetComponent<CreatedRoomObject>();Assert.That(view.ModelLoading,Is.True);
                capture=editor.BeginViewCapture(out error);Assert.That(capture,Is.Not.Null,error);for(int i=0;i<5;i++)yield return null;
                Assert.That(capture.Done,Is.False,"A pending import is not a failed or complete view");Assert.That(editor.ViewCaptureMetadata,Is.Null);
                barrier.Open();blocker.Dispose();yield return new WaitUntil(()=>blocked.IsCompleted);Assert.That(blocked.Exception,Is.Null);
                yield return PreparedCaptureDone(capture);Assert.That(capture.Read(out _,out error),Is.True,error);Assert.That(view.ModelGeometryReady,Is.True);Assert.That(view.ModelLoading,Is.False);
            }finally{capture?.Dispose();barrier.Open();blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PreparedViewCaptureResidentMissingImportReportsFailure(){
            Assert.That(editor.CreateImportedModel(new string('e',64),out var id,out var error),Is.True,error);editor.Find(id).transform.position=new Vector3(0,0,3);PreparedCaptureViewer();
            using var capture=editor.BeginViewCapture(out error);Assert.That(capture,Is.Not.Null,error);yield return PreparedCaptureDone(capture);
            Assert.That(capture.Read(out var image,out error),Is.False);Assert.That(image,Is.Null);Assert.That(error,Does.Contain("model"));Assert.That(editor.ViewCaptureMetadata,Is.Null);
        }
    }
}
