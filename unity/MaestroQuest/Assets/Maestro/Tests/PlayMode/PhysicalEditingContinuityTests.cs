// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.IO;
using Maestro.Quest.Book;
using Maestro.Quest.Rules;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        string PhysicalEditingOther()
        {
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball,"Other creation",new Vector3(2,1,2),1,Color.white,out var id,out var error),Is.True,error);
            return id;
        }
        IEnumerator PhysicalEditingUnrelated(bool erase)
        {
            string id=PhysicalEditingOther();workshop.ToggleRepeat();editor.Select(editor.Find(id));
            Assert.That(runtime.Trigger(sequenceId),Is.True);yield return new WaitForSeconds(.15f);
            var before=block.transform.localPosition;
            if(erase)editor.Erase();else editor.ChoosePaint(Color.red);
            yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1),"An edit must not stop another object's recorded program");
            Assert.That(erase?editor.Read(id)==null:editor.Read(id).color==Color.red,Is.True,editor.Status);
            yield return new WaitForSeconds(.15f);
            Assert.That(Vector3.Distance(before,block.transform.localPosition),Is.GreaterThan(.005f));
            editor.Undo();Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1),"Content Undo must also preserve the unrelated program");
            Assert.That(editor.Read(id),Is.Not.Null,editor.Status);
            editor.Redo();Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1),"Redo must preserve the unrelated program");
            Assert.That(erase?editor.Read(id)==null:editor.Read(id).color==Color.red,Is.True,editor.Status);
        }
        [UnityTest] public IEnumerator PhysicalEditingPaintKeepsUnrelatedProgramMoving()=>PhysicalEditingUnrelated(false);
        [UnityTest] public IEnumerator PhysicalEditingEraseKeepsUnrelatedProgramMoving()=>PhysicalEditingUnrelated(true);
        [UnityTest] public IEnumerator PhysicalEditingTakeoverStopsOnlyTheConflictingProgram()
        {
            Assert.That(runtime.Trigger(sequenceId),Is.True);
            Assert.That(runtime.Scheduler.Invoke(new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=30}},Time.unscaledTime,out var waiting,out var error),Is.True,error);
            yield return new WaitForSeconds(.15f);var before=block.transform.localPosition;
            editor.ChoosePaint(Color.red);yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(editor.Read(editor.Identity(block)).color,Is.EqualTo(Color.red),editor.Status);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1),"Only the target's motion may be interrupted");
            Assert.That((string)runtime.Scheduler.Invocation(waiting)["phase"],Is.EqualTo("running"));
            Assert.That(Vector3.Distance(before,block.transform.localPosition),Is.LessThan(.025f),"A manual takeover must preserve the current placement");
        }
        [UnityTest] public IEnumerator PhysicalEditingBlockedByGripKeepsOtherActivityAndTheGrip()
        {
            string id=PhysicalEditingOther();editor.Select(editor.Find(id));Assert.That(runtime.Trigger(sequenceId),Is.True);
            Assert.That(editor.Ownership.TryAcquire("held","Your grip",RoomActorRole.Grab,new[]{new BehaviourCatalog.Claim(id,"wholeTarget")},_=>Assert.Fail("A lower-priority tool displaced the grip"),out var held,out var error),Is.True,error);
            using(held){
                editor.Erase();yield return PhysicalCreationTestWait.Finish(editor);
                Assert.That(held.Held,Is.True);Assert.That(editor.Read(id),Is.Not.Null);
                Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1),"A refused edit must not stop unrelated actions");
            }
        }
        IEnumerator PhysicalEditingModel(Action<string,ImportedModel> ready)
        {
            var asset=ModelLibrary.Inspect("preview.glb",ModelFixture.Create());
            var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            Assert.That(editor.CreateImportedModel(asset.Hash,out var id,out var error),Is.True,error);
            yield return PhysicalCreationTestWait.Until(()=>editor.Find(id)?.GetComponent<CreatedRoomObject>()?.Model?.Ready==true,"Model did not become ready");
            ready(id,editor.Find(id).GetComponent<CreatedRoomObject>().Model);
        }
        [UnityTest] public IEnumerator PhysicalEditingOwnedPreviewSurvivesOtherEditsAndBlocksCompetingAgent()
        {
            string id=null;ImportedModel model=null;yield return PhysicalEditingModel((at,value)=>{id=at;model=value;});
            string other=PhysicalEditingOther();var imports=root.AddComponent<ImportWorkshop>();imports.Initialize(editor,animations);
            editor.Select(editor.Find(id));imports.ToggleLoop();imports.Play();Assert.That(model.IsPlaying,Is.True,imports.Status);
            Assert.That(editor.Ownership.Observe().owners.Any(x=>x.label=="Model clip preview"&&x.claims.Any(c=>c.target==id)),Is.True);
            var agent=new RoomRuleActions(editor,animations);
            Assert.That(agent.Start("competing-paint",new RuleStep {action=RuleActionKind.PaintObject,targetId=id,editColor=Color.blue},out _,out var issue),Is.False);
            Assert.That(issue,Does.Contain("Model clip preview"));
            editor.Select(editor.Find(other));workshop.ToggleRepeat();Assert.That(runtime.Trigger(sequenceId),Is.True);
            editor.ChoosePaint(Color.red);yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(model.IsPlaying,Is.True);Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            editor.Undo();editor.Redo();Assert.That(model.IsPlaying,Is.True,"Unrelated object history must retain the preview reservation");
            editor.Erase();yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(model.IsPlaying,Is.True);Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            editor.Select(editor.Find(id));editor.ChoosePaint(Color.green);yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(model.IsPlaying,Is.False,"The exact target's preview must yield to its physical edit");
            Assert.That(editor.Read(id).color,Is.EqualTo(Color.green),editor.Status);Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            Assert.That(editor.Ownership.Observe().owners.Any(x=>x.label=="Model clip preview"),Is.False);
        }
        [UnityTest] public IEnumerator PhysicalEditingPreviewControlsAffectOnlyTheSelectedModel()
        {
            string first=null,second=null;ImportedModel a=null,b=null;
            yield return PhysicalEditingModel((id,model)=>{first=id;a=model;});
            yield return PhysicalEditingModel((id,model)=>{second=id;b=model;});
            var imports=root.AddComponent<ImportWorkshop>();imports.Initialize(editor,animations);imports.ToggleLoop();
            editor.Select(editor.Find(first));imports.Play();editor.Select(editor.Find(second));imports.Play();
            Assert.That(a.IsPlaying&&b.IsPlaying,Is.True,imports.Status);
            workshop.ToggleRepeat();Assert.That(runtime.Trigger(sequenceId),Is.True);
            imports.StopPreview();Assert.That(a.IsPlaying,Is.True);Assert.That(b.IsPlaying,Is.False);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            imports.Play();imports.NextClip();Assert.That(a.IsPlaying,Is.True);Assert.That(b.IsPlaying,Is.False);
            imports.Play();imports.ToggleLoop();Assert.That(a.IsPlaying,Is.True);Assert.That(b.IsPlaying,Is.False);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            editor.PrepareAgentEdit();Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Explicit whole-room authoring still interrupts all programs");
            Assert.That(a.IsPlaying,Is.False);Assert.That(editor.Ownership.Observe().owners.Any(x=>x.label=="Model clip preview"),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator PhysicalEditingPreviewNaturalCompletionAndDisableReleaseOwnership()
        {
            string id=null;ImportedModel model=null;yield return PhysicalEditingModel((at,value)=>{id=at;model=value;});
            var imports=root.AddComponent<ImportWorkshop>();imports.Initialize(editor,animations);editor.Select(editor.Find(id));imports.Play();
            Assert.That(model.IsPlaying,Is.True);
            yield return PhysicalCreationTestWait.Until(()=>!model.IsPlaying,"Finite preview did not finish");yield return null;
            Assert.That(editor.Ownership.Observe().owners.Any(x=>x.label=="Model clip preview"),Is.False);
            imports.ToggleLoop();imports.Play();Assert.That(model.IsPlaying,Is.True);model.gameObject.SetActive(false);yield return null;
            Assert.That(editor.Ownership.Observe().owners.Any(x=>x.label=="Model clip preview"),Is.False);
        }
        [UnityTest] public IEnumerator PhysicalEditingEraseKeepsAnUnfinishedTakeRecoverable()
        {
            string id=editor.Identity(block),other=PhysicalEditingOther();editor.Select(block);
            Assert.That(animations.StartRecording(animations.RecordingSessionId,id,editor.ObjectRevision(id),out _,out var error,manual:true),Is.True,error);
            yield return new WaitForSeconds(.15f);block.transform.localPosition+=Vector3.up*.1f;
            string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
            try{
                editor.Erase();yield return PhysicalCreationTestWait.Finish(editor);
                Assert.That(editor.Read(id),Is.Not.Null,"Taking over authoring must not delete a frozen take's target");
                yield return PhysicalCreationTestWait.Until(()=>!animations.IsSavingRecording,"Blocked recording did not finish");
                Assert.That(animations.HasUnsavedRecording,Is.True);Assert.That((int)animations.ObserveRecording()["frames"],Is.GreaterThan(1));
                Assert.That(editor.CanDeleteObject(id,out error),Is.False);Assert.That(error,Does.Contain("unfinished animation"));
                Assert.That(editor.CanDeleteObject(other,out error),Is.True,error);
                Assert.That(editor.DeleteObject(id,out error),Is.False,"Catalog and legacy deletion share the retained-draft guard");
            }finally{Directory.Delete(obstacle);}
            Assert.That(animations.DiscardRecording(animations.RecordingSessionId,out _,out error),Is.True,error);
            editor.Erase();yield return PhysicalCreationTestWait.Finish(editor);Assert.That(editor.Read(id),Is.Null,editor.Status);
        }
        [UnityTest] public IEnumerator PhysicalEditingUnrelatedSoundKeepsItsNativeInstance()
        {
            string id=PhysicalEditingOther(),sound=WorldSound(5);WorldEmitter("book",sound,gain:.25f);
            var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);var enabled=listeners.Select(x=>x.enabled).ToArray();
            var listener=new GameObject("Editing continuity listener",typeof(AudioListener));var probe=listener.AddComponent<SpeechListenerProbe>();
            bool paused=AudioListener.pause;float volume=AudioListener.volume;
            for(int i=0;i<listeners.Length;i++)listeners[i].enabled=false;AudioListener.pause=false;AudioListener.volume=1;
            try{
                Assert.That(runtime.Scheduler.Invoke(new JObject {["id"]="audio.play",["version"]=1,["arguments"]=new JObject {["target"]="book",["emitter"]="sound"}},Time.unscaledTime,out var run,out var error),Is.True,error);
                var audio=WorldAudio.For(editor);
                yield return PhysicalCreationTestWait.Until(()=>audio.Current("book","sound")?.Output,"Sound output did not prepare");
                var voice=audio.Current("book","sound");var output=voice.Output;
                bool Rendered(){var sample=probe.Read();return sample.Left+sample.Right>.00001;}
                // Native output creation precedes its DSP start. Establish actual
                // playback before editing; the listener probe mutes after sampling.
                yield return PhysicalCreationTestWait.Until(()=>voice.Closed||voice.Cursor>0&&Rendered(),"Sound did not reach the native listener before editing");
                Assert.That(voice.Closed,Is.False,voice.Error);double before=voice.Cursor;probe.Reset();
                editor.Select(editor.Find(id));editor.ChoosePaint(Color.red);yield return PhysicalCreationTestWait.Finish(editor);
                editor.Erase();yield return PhysicalCreationTestWait.Finish(editor);
                yield return PhysicalCreationTestWait.Until(()=>voice.Closed||voice.Cursor>before&&Rendered(),"Unrelated sound stopped reaching the native listener during editing");
                Assert.That(voice.Closed,Is.False);Assert.That(audio.Current("book","sound"),Is.SameAs(voice));Assert.That(voice.Output,Is.SameAs(output));
                Assert.That(voice.Cursor,Is.GreaterThan(before));Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            }finally{
                var audio=editor.GetComponent<WorldAudio>();if(audio)audio.enabled=false;
                UnityEngine.Object.Destroy(listener);for(int i=0;i<listeners.Length;i++)if(listeners[i])listeners[i].enabled=enabled[i];AudioListener.pause=paused;AudioListener.volume=volume;
            }
        }
        [UnityTest] public IEnumerator PhysicalEditingHistoryTakesOverOnlyItsAffectedTarget()
        {
            string id=editor.Identity(block);var original=editor.Read(id).color;
            editor.ChoosePaint(Color.red);yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(runtime.Trigger(sequenceId),Is.True);
            Assert.That(runtime.Scheduler.Invoke(new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=30}},Time.unscaledTime,out var waiting,out var error),Is.True,error);
            yield return new WaitForSeconds(.15f);editor.Undo();
            Assert.That(editor.Read(id).color,Is.EqualTo(original),editor.Status);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            Assert.That((string)runtime.Scheduler.Invocation(waiting)["phase"],Is.EqualTo("running"));
            editor.Redo();Assert.That(editor.Read(id).color,Is.EqualTo(Color.red),editor.Status);Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator PhysicalEditingHistoryNeverSubstitutesAReentrantNewEntry()
        {
            string other=PhysicalEditingOther(),id=editor.Identity(block);editor.Select(block);
            editor.ChoosePaint(Color.red);yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(editor.Ownership.TryAcquire("author-test","Authoring fixture",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(id,"wholeTarget")},
                _=>Assert.That(editor.PaintObject(other,Color.green,out var issue),Is.True,issue),out var author,out var error),Is.True,error);
            using(author){
                editor.Undo();Assert.That(author.Held,Is.False);
                Assert.That(editor.Read(id).color,Is.EqualTo(Color.red));Assert.That(editor.Read(other).color,Is.EqualTo(Color.green));
                Assert.That(editor.Status,Does.Contain("changed during Undo"));
            }
            editor.Undo();Assert.That(editor.Read(other).color,Is.EqualTo(Color.white));Assert.That(editor.Read(id).color,Is.EqualTo(Color.red));
        }
    }
}
