// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Book;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        JObject RecordingCall(string operation,string id="maestro",string session=null)
        {
            var args=new JObject {["operation"]=operation,["sessionId"]=session??workshop.RecordingSessionId};
            if(operation!="discard")args["target"]=id;if(operation=="start")args["revision"]=editor.ObjectRevision(id);
            return new JObject {["id"]="animation.record",["version"]=1,["arguments"]=args};
        }
        JObject RecordingRequest(JObject call)=>new() {["operation"]="start",["runId"]=authorActions.Observe()["nextRunId"].DeepClone(),["call"]=call};
        JObject RecordingFact(){Assert.That(BehaviourCatalog.TryRead("animation.recording",new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);return JObject.FromObject(value.Value);}
        IEnumerator RecordAction(string operation,string id="maestro")
        {
            Assert.That(authorActions.Execute(RecordingRequest(RecordingCall(operation,id)),out var error),Is.True,error);yield return null;
            Assert.That((string)authorActions.Observe()["selected"]["phase"],Is.EqualTo("completed"),authorActions.Observe().ToString());
        }
        [UnityTest] public IEnumerator RecordingReceiptsSharePhysicalMovementAndFinishWithoutAutoplayOrReplay()
        {
            AuthorRuntime();var before=RecordingFact();var start=RecordingRequest(RecordingCall("start"));Assert.That(authorActions.Execute(start,out var error),Is.True,error);yield return new WaitForSeconds(.12f);
            var started=authorActions.Observe();Assert.That((string)started["selected"]["phase"],Is.EqualTo("completed"));Assert.That(workshop.IsRecording,Is.True);
            Assert.That(authorActions.Execute(new JObject {["operation"]="cancel",["runId"]=start["runId"].DeepClone()},out error),Is.True,error);Assert.That(workshop.IsRecording,Is.True,"Cancelling a completed receipt does not end a live session");
            avatarItem.transform.localPosition+=Vector3.right*.25f;yield return new WaitForSeconds(.14f);var active=RecordingFact();yield return RecordAction("finish");
            var finished=authorActions.Observe();var idle=RecordingFact();Assert.That(workshop.IsRecording,Is.False);Assert.That(workshop.ControlsTarget("maestro"),Is.False);Assert.That(workshop.IsPlaying,Is.False);Assert.That((string)idle["phase"],Is.EqualTo("idle"));Assert.That(idle["sessionId"],Is.Not.EqualTo(before["sessionId"]));
            var motion=editor.Read("maestro").motion;Assert.That(motion.frames[^1].position.x-motion.frames[0].position.x,Is.EqualTo(.25f).Within(.001f));Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").motion.frames.Length,Is.EqualTo(motion.frames.Length),error);
            int revision=editor.ObjectRevision("maestro");Assert.That(authorActions.Execute(start,out error),Is.True,error);Assert.That(workshop.IsRecording,Is.False);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));
            string path=Environment.GetEnvironmentVariable("MAESTRO_RECORDING_SESSIONS");if(!string.IsNullOrEmpty(path)){Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"recording.json"),new JObject {["before"]=before,["start"]=started,["active"]=active,["finish"]=finished,["idle"]=idle}.ToString());}
            editor.Undo();Assert.That(editor.Read("maestro").motion,Is.Null);editor.Redo();Assert.That(editor.Read("maestro").motion.frames.Length,Is.EqualTo(motion.frames.Length));
        }
        [UnityTest] public IEnumerator DiscardAndStaleFinishCannotChangeSavedMotionOrTheNextTake()
        {
            AuthorRuntime();editor.Select(avatarItem);workshop.ToggleRecord();Assert.That(workshop.IsRecording,Is.True);yield return new WaitForSeconds(.11f);workshop.ToggleRecord();var saved=editor.Read("maestro").motion;int revision=editor.ObjectRevision("maestro");
            string old=workshop.RecordingSessionId;yield return RecordAction("start");yield return RecordAction("discard");Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(editor.Read("maestro").motion.frames.Length,Is.EqualTo(saved.frames.Length));
            yield return RecordAction("start");Assert.That(authorActions.Execute(RecordingRequest(RecordingCall("finish",session:old)),out _),Is.False);Assert.That(authorActions.Execute(RecordingRequest(RecordingCall("discard",session:old)),out _),Is.False);Assert.That(workshop.IsRecording,Is.True);workshop.DiscardTake();Assert.That(workshop.IsRecording,Is.False);
        }
        [UnityTest] public IEnumerator FailedRecordingSaveRetainsFrozenFramesForAnExactRetry()
        {
            AuthorRuntime();yield return RecordAction("start");yield return new WaitForSeconds(.12f);avatarItem.transform.localPosition+=Vector3.right*.2f;editor.SaveNow();yield return null;
            string file=Path.Combine(directory,"room.v4.json");if(File.Exists(file))File.Delete(file);Directory.CreateDirectory(file);int revision=editor.ObjectRevision("maestro");string session=workshop.RecordingSessionId;
            Assert.That(authorActions.Execute(RecordingRequest(RecordingCall("finish")),out var error),Is.False);Assert.That(error,Does.Contain("frames retained"));var frozen=RecordingFact();Assert.That((string)frozen["phase"],Is.EqualTo("unsaved"));Assert.That(workshop.IsRecording,Is.False);Assert.That(workshop.ControlsTarget("maestro"),Is.False);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(editor.Read("maestro").motion,Is.Null);
            yield return new WaitForSeconds(.12f);Assert.That(RecordingFact()["frames"],Is.EqualTo(frozen["frames"]));Assert.That(workshop.CanStartRecording(session,"maestro",revision,out _),Is.False);Assert.That(editor.BeginTemporaryRoom(out error),Is.False);Assert.That(error,Does.Contain("retained recording"));Assert.That(workshop.HasUnsavedRecording,Is.True);
            Directory.Delete(file);yield return RecordAction("finish");Assert.That(editor.Read("maestro").motion.frames.Length,Is.EqualTo((int)frozen["frames"]));Assert.That(workshop.HasUnsavedRecording,Is.False);Assert.That(workshop.RecordingSessionId,Is.Not.EqualTo(session));
        }
        [UnityTest] public IEnumerator RetainedTakeRefusesChangedObjectsButCanBeDiscardedAfterDeletion()
        {
            AuthorRuntime();string id=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;yield return RecordAction("start",id);yield return new WaitForSeconds(.12f);editor.SaveNow();yield return null;
            string file=Path.Combine(directory,"room.v4.json");if(File.Exists(file))File.Delete(file);Directory.CreateDirectory(file);Assert.That(authorActions.Execute(RecordingRequest(RecordingCall("finish",id)),out _),Is.False);Directory.Delete(file);
            Assert.That(editor.MoveObject(id,Vector3.right,out var error),Is.True,error);Assert.That(authorActions.Execute(RecordingRequest(RecordingCall("finish",id)),out _),Is.False);Assert.That(workshop.HasUnsavedRecording,Is.True);
            Assert.That(editor.DeleteObject(id,out error),Is.True,error);yield return RecordAction("discard",id);Assert.That(workshop.HasUnsavedRecording,Is.False);Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator RecordingInTemporaryRoomRequiresKeepAndPauseFinishesOnlyOnce()
        {
            AuthorRuntime();Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return FinishAuthoringSave();yield return RecordAction("start");yield return new WaitForSeconds(.12f);
            workshop.SendMessage("OnApplicationPause",true);Assert.That(workshop.IsRecording,Is.False);string next=workshop.RecordingSessionId;int revision=editor.ObjectRevision("maestro");workshop.SendMessage("OnApplicationFocus",false);workshop.SendMessage("OnApplicationPause",false);workshop.SendMessage("OnApplicationFocus",true);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(workshop.RecordingSessionId,Is.EqualTo(next));
            Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").motion,Is.Null,error);Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);yield return FinishAuthoringSave();Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").motion,Is.Not.Null,error);
        }
        [UnityTest] public IEnumerator RecorderDoesNotStealAProgramChannelAndLeavesTheIssuedIdentityReusable()
        {
            AuthorRuntime();string id=workshop.RecordingSessionId;Assert.That(editor.Ownership.TryAcquire("other-program","Existing animation",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim("maestro","upperBody")},_=>Assert.Fail("Must not interrupt"),out var lease,out var error),Is.True,error);
            Assert.That(authorActions.Execute(RecordingRequest(RecordingCall("start")),out _),Is.False);Assert.That(lease.Held,Is.True);Assert.That(workshop.RecordingSessionId,Is.EqualTo(id));lease.Dispose();yield return RecordAction("start");workshop.DiscardTake();
        }
        [UnityTest] public IEnumerator SolidRecordAndDiscardButtonsUseTheSameSessionAndRetainTheSavedMotion()
        {
            AuthorRuntime();editor.Select(avatarItem);var tray=new GameObject("Animation controls");tray.transform.SetParent(root.transform,false);tray.transform.localPosition=new Vector3(2,0,1);tray.AddComponent<AnimationTools>().Build(workshop,root.GetComponent<RoomInteraction>());
            var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;yield return null;Physics.SyncTransforms();
            void Click(float x,float y){var ray=new Ray(new Vector3(x,y,0),Vector3.forward);Assert.That(router.Begin(0,ray),Is.True);router.End(0,ray);}
            string identity=workshop.RecordingSessionId;Click(1.814f,.17f);Assert.That(workshop.IsRecording,Is.True);yield return new WaitForSeconds(.12f);Click(1.938f,-.10f);Assert.That(workshop.IsRecording,Is.False);Assert.That(workshop.RecordingSessionId,Is.Not.EqualTo(identity));Assert.That(editor.Read("maestro").motion,Is.Null);
            string path=Environment.GetEnvironmentVariable("MAESTRO_RECORDING_SESSIONS");if(!string.IsNullOrEmpty(path)){
                Directory.CreateDirectory(path);var cameraRoot=new GameObject("Animation controls camera");cameraRoot.transform.SetParent(root.transform,false);var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.85f,.89f,.91f);cameraRoot.transform.position=tray.transform.position+Vector3.back*1.25f;cameraRoot.transform.LookAt(tray.transform.position);camera.fieldOfView=40;
                var render=new RenderTexture(1200,900,24);var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
                try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(path,"controls.png"),pixels.EncodeToPNG());}finally{RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);}
            }
        }
        [UnityTest] public IEnumerator ProgramCanStartWaitAndFinishARecordingThroughTheSameRuntime()
        {
            AuthorRuntime();var source=JObject.Parse(BehaviourProgram.FromInvocation(RecordingCall("start")));source["version"]=3;source["state"]=new JArray();source["events"]=new JArray();var body=(JArray)source["functions"][0]["body"];
            var wait=JObject.Parse(BehaviourProgram.FromInvocation(new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=.25}}))["functions"][0]["body"][0];wait["id"]="wait";body.Add(wait);
            var finish=JObject.Parse(BehaviourProgram.FromInvocation(RecordingCall("finish")))["functions"][0]["body"][0];finish["id"]="finish";body.Add(finish);
            var sequence=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Record once",program=source.ToString()};authorRuntime.Scheduler.Configure(new RuleDocument {sequences=new[]{sequence}});Assert.That(authorRuntime.Scheduler.Trigger(sequence.id,Time.unscaledTime),Is.True,authorRuntime.Scheduler.LastError);yield return new WaitForSeconds(.12f);Assert.That(workshop.IsRecording,Is.True);avatarItem.transform.localPosition+=Vector3.right*.2f;yield return new WaitForSeconds(.25f);
            Assert.That(authorRuntime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),authorRuntime.Scheduler.LastError);Assert.That(workshop.IsRecording,Is.False);Assert.That(editor.Read("maestro").motion.frames.Length,Is.GreaterThanOrEqualTo(3));
        }
    }
}
