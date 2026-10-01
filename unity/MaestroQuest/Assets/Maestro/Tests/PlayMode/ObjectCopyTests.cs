// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject CopyFact(string target){Assert.That(BehaviourCatalog.TryRead("object.definition",1,new JObject {["target"]=target},new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);}
        JObject CopyCall(string target,Vector3 position)=>new() {["id"]="object.create",["version"]=1,["arguments"]=new JObject {["kind"]="copy",["target"]=target,["revision"]=editor.ObjectRevision(target),["name"]="",["x"]=position.x,["y"]=position.y,["z"]=position.z}};
        [UnityTest] public IEnumerator CopyPreservesDrawingAndRecordedPathWithOneSavedCreationAndHistoricalReceipt()
        {
            Assert.That(editor.AddDrawing(new[]{new Vector3(.2f,1,.5f),new Vector3(.3f,1,.5f),new Vector3(.3f,1.1f,.5f)},Color.blue),Is.True);string source=editor.SelectedId;
            var data=editor.Read(source);Assert.That(editor.SaveAnimation(source,new RoomMotion {loop=true,frames=new[]{new MotionFrame {position=data.position},new MotionFrame {time=1,position=data.position+Vector3.up*.2f}}},null,false),Is.True);
            var sourceData=editor.Read(source);var before=CopyFact(source);string encoded=JsonUtility.ToJson(sourceData);var executor=new RoomAgentExecutor(editor);var call=CopyCall(source,sourceData.position+Vector3.right*.4f);var request=ObjectEditRequest(call);int count=editor.Snapshot().objects.Length;
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);var receipt=executor.Executions.Observe().DeepClone();string id=(string)receipt["selected"]["output"]["objectId"];Assert.That(id,Is.Not.EqualTo(source));var copy=editor.Read(id);
            Assert.That(copy.points,Is.EqualTo(sourceData.points));Assert.That(copy.color,Is.EqualTo(sourceData.color));Assert.That(copy.radius,Is.EqualTo(sourceData.radius));Assert.That(copy.rotation,Is.EqualTo(sourceData.rotation));Assert.That(copy.motion.loop,Is.True);
            for(int i=0;i<copy.motion.frames.Length;i++)Assert.That(copy.motion.frames[i].position,Is.EqualTo(sourceData.motion.frames[i].position+Vector3.right*.4f));
            Assert.That(JsonUtility.ToJson(editor.Read(source)),Is.EqualTo(encoded));Assert.That(editor.SelectedId,Is.EqualTo(source));Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
            var saved=new RoomStorage(directory).Load(out error);Assert.That(saved.objects.Any(x=>x.id==id),Is.True,error);Assert.That(editor.Find(id).GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
            var copied=CopyFact(id);editor.Undo();Assert.That(editor.Find(id),Is.Null);Assert.That(JsonUtility.ToJson(editor.Read(source)),Is.EqualTo(encoded));Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            var output=Environment.GetEnvironmentVariable("MAESTRO_OBJECT_COPY");if(!string.IsNullOrWhiteSpace(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"object-copy.json"),new JObject {["before"]=before,["call"]=call,["request"]=request.commands[0].execution,["receipt"]=receipt,["copied"]=copied,["sourceUnchanged"]=true}.ToString());}
            yield return null;
        }
        [UnityTest] public IEnumerator CopyRetainsRecipeTracksButStartsIdleAndLeavesAnotherActorRunning()
        {
            var recipe=RecipeTemplates.BoxRobot(true);Assert.That(editor.CreateRecipe("Robot",new Vector3(1,1,1),.3f,recipe,out var source,out var error),Is.True,error);var before=editor.Read(source);
            var wait=new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=20}};Assert.That(runtime.Scheduler.Invoke(wait,Time.unscaledTime,out var waiting,out error),Is.True,error);
            var executor=new RoomAgentExecutor(editor);Assert.That(executor.Execute(ObjectEditRequest(CopyCall(source,new Vector3(2,1,1))),out error,out _),Is.True,error);string id=(string)executor.Executions.Observe()["selected"]["output"]["objectId"];
            var copy=editor.Read(id);Assert.That(copy.recipe.playing,Is.False);Assert.That(copy.recipe.parts.Select(x=>x.id),Is.EqualTo(before.recipe.parts.Select(x=>x.id)));Assert.That(copy.recipe.tracks.Length,Is.EqualTo(before.recipe.tracks.Length));
            Assert.That(editor.Find(id).GetComponent<RecipeObject>().IsPlaying,Is.False);Assert.That(editor.Read(source).recipe.playing,Is.True);Assert.That((string)runtime.Scheduler.Invocation(waiting)["phase"],Is.EqualTo("running"));
            copy.recipe.parts[0].size=Vector3.one;Assert.That(editor.Read(source).recipe.parts[0].size,Is.EqualTo(before.recipe.parts[0].size));yield return null;
        }
        [UnityTest] public IEnumerator PhysicalDuplicateAndTemporaryCopyUseTheSameIsolatedSavedPath()
        {
            string source=editor.Identity(block);var before=editor.Read(source);editor.Select(block);editor.Duplicate();string id=editor.SelectedId;Assert.That(id,Is.Not.EqualTo(source));Assert.That(editor.Read(id).motion.frames[0].position,Is.EqualTo(before.motion.frames[0].position+Vector3.right*.18f));Assert.That(new RoomStorage(directory).Load(out _).objects.Any(x=>x.id==id),Is.True);
            editor.Undo();Assert.That(editor.Find(id),Is.Null);Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            var executor=new RoomAgentExecutor(editor);Assert.That(executor.Execute(ObjectEditRequest(CopyCall(source,new Vector3(1,1,1))),out error,out _),Is.True,error);string temporary=(string)executor.Executions.Observe()["selected"]["output"]["objectId"];
            Assert.That(new RoomStorage(directory).Load(out _).objects.Any(x=>x.id==temporary),Is.False);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Find(temporary),Is.Null);Assert.That(editor.Find(source),Is.Not.Null);
        }
        [UnityTest] public IEnumerator CopyRefusesStaleHeldOwnedAndFailedSaveWithoutChangingTheSource()
        {
            string source=editor.Identity(block);int count=editor.Snapshot().objects.Length;var executor=new RoomAgentExecutor(editor);var stale=CopyCall(source,new Vector3(1,1,1));Assert.That(editor.PaintObject(source,Color.red,out var error),Is.True,error);Assert.That(executor.Execute(ObjectEditRequest(stale),out _,out _),Is.False);
            var hand=Hand(1,block.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);Assert.That(executor.Execute(ObjectEditRequest(CopyCall(source,new Vector3(1,1,1))),out _,out _),Is.False);manager.SelectExit((IXRSelectInteractor)hand,block.Grab);yield return null;
            Assert.That(runtime.Trigger(sequenceId),Is.True);Assert.That(executor.Execute(ObjectEditRequest(CopyCall(source,new Vector3(1,1,1))),out _,out _),Is.False);Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));runtime.Scheduler.StopAll();
            string original=JsonUtility.ToJson(editor.Read(source));Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"room.v999.json"),"preserve newer data");Assert.That(executor.Execute(ObjectEditRequest(CopyCall(source,new Vector3(1,1,1))),out error,out _),Is.False);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));Assert.That(JsonUtility.ToJson(editor.Read(source)),Is.EqualTo(original));
        }
        [UnityTest] public IEnumerator CopyValidatesAggregateGeometryAndTranslatedRecordingBeforeAnyCommit()
        {
            var recipe=RecipeTemplates.BoxRobot(false);string source=null;while(editor.Snapshot().objects.Sum(x=>x.recipe?.parts.Length??0)+recipe.parts.Length<=256)Assert.That(editor.CreateRecipe("Robot",Vector3.one,.2f,recipe,out source,out var error),Is.True,error);
            int count=editor.Snapshot().objects.Length;Assert.That(editor.CopyObject(source,editor.ObjectRevision(source),"",Vector3.one,out _,out _),Is.False);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            string blockId=editor.Identity(block);var before=editor.Read(blockId);Assert.That(editor.SaveAnimation(blockId,new RoomMotion {frames=new[]{new MotionFrame {position=new Vector3(24.9f,0,0)}}},null,false),Is.True);
            Assert.That(editor.CopyObject(blockId,editor.ObjectRevision(blockId),"",before.position+Vector3.right,out _,out _),Is.False);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));yield return null;
        }
        [UnityTest] public IEnumerator ImportedCopySharesExactAssetButLoadsIndependentIdleGeometry()
        {
            var asset=Maestro.Quest.Imports.ModelLibrary.Inspect("copy.glb",ModelFixture.Create());var saved=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>saved.IsCompleted);Assert.That(saved.Exception,Is.Null);
            Assert.That(editor.CreateImportedModel(asset.Hash,out var source,out var error),Is.True,error);var originalObject=editor.Find(source).GetComponent<CreatedRoomObject>();for(int i=0;i<360&&(!originalObject.Model||!originalObject.Model.Ready);i++)yield return null;var original=originalObject.Model;Assert.That(original&&original.Ready,Is.True,originalObject.ModelStatus);Assert.That(original.ClipCount,Is.EqualTo(1));
            Assert.That(editor.CopyObject(source,editor.ObjectRevision(source),"Copied model",Vector3.one,out var id,out error),Is.True,error);var copiedObject=editor.Find(id).GetComponent<CreatedRoomObject>();for(int i=0;i<360&&(!copiedObject.Model||!copiedObject.Model.Ready);i++)yield return null;var model=copiedObject.Model;Assert.That(model&&model.Ready,Is.True,copiedObject.ModelStatus);
            Assert.That(editor.Read(id).modelHash,Is.EqualTo(asset.Hash));Assert.That(editor.Read(id).name,Is.EqualTo("Copied model"));Assert.That(model.Instance,Is.Not.SameAs(original.Instance));Assert.That(model.ClipCount,Is.EqualTo(original.ClipCount));Assert.That(model.IsPlaying,Is.False);
            model.Play(0,true);yield return new WaitForSeconds(.1f);Assert.That(model.IsPlaying,Is.True);Assert.That(original.IsPlaying,Is.False);editor.Undo();Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Find(source),Is.Not.Null);
        }
        [UnityTest] public IEnumerator CopiedResultCanDriveTheSameProgramWithoutInventingIdentity()
        {
            string target=editor.Identity(block);var source=JObject.Parse(BehaviourProgram.FromInvocation(CopyCall(target,new Vector3(1,1,1))));source["version"]=3;source["state"]=new JArray();source["events"]=new JArray();source["functions"][0]["locals"]=new JArray(new JObject {["name"]="made",["initial"]=""});var body=(JArray)source["functions"][0]["body"];body[0]["results"]=new JObject {["objectId"]="made"};
            var paint=JObject.Parse(BehaviourProgram.FromInvocation(ObjectEditCall("object.color.set",target,("red",0),("green",.5),("blue",1))))["functions"][0]["body"][0];paint["id"]="paintCopy";paint["bindings"]=new JObject {["target"]=new JObject {["var"]="made"}};body.Add(paint);
            Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out var error),Is.True,error);int count=editor.Snapshot().objects.Length;var seq=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Copy then paint",program=source.ToString()};runtime.Scheduler.Configure(new RuleDocument {sequences=new[]{seq}});Assert.That(runtime.Scheduler.Trigger(seq.id,Time.unscaledTime),Is.True,runtime.Scheduler.LastError);for(int i=0;i<8&&runtime.Scheduler.RunningCount>0;i++){runtime.Scheduler.Tick(Time.unscaledTime);yield return null;}
            Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),runtime.Scheduler.LastError);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));Assert.That(editor.Snapshot().objects.Any(x=>x.id!=target&&x.color==new Color(0,.5f,1,1)),Is.True);Assert.That(editor.Read(target).color,Is.Not.EqualTo(new Color(0,.5f,1,1)));
        }
    }
}
