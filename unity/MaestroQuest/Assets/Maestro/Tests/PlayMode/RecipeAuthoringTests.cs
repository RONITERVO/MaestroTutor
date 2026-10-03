// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        string RecipeTarget(bool playing=false){var recipe=RecipeTemplates.BoxRobot(true);recipe.playing=playing;Assert.That(editor.CreateRecipe("Practice robot",new Vector3(.3f,1.3f,.65f),1,recipe,out var target,out var error),Is.True,error);return target;}
        JObject RecipePatch(string target){var args=(JObject)BehaviourCatalog.Action("object.recipe.edit").Example.DeepClone();args["target"]=target;args["revision"]=editor.ObjectRevision(target);args["duration"]=editor.Read(target).recipe.duration;args["loop"]=editor.Read(target).recipe.loop;return new JObject {["id"]="object.recipe.edit",["version"]=1,["arguments"]=args};}
        JObject RecipeRun(RoomAgentExecutor executor,JObject call){Assert.That(executor.Execute(ObjectEditRequest(call),out var error,out _),Is.True,error);return (JObject)executor.Executions.Observe().DeepClone();}
        JObject RecipeFact(string id,string target,int? index=null,int? offset=null){var args=new JObject {["target"]=target};if(index.HasValue){args["revision"]=editor.ObjectRevision(target);args["index"]=index.Value;if(offset.HasValue)args["offset"]=offset.Value;}Assert.That(BehaviourCatalog.TryRead(id,1,args,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return (JObject)value.Value;}
        [UnityTest] public IEnumerator RecipePatchSharesSavedGeometryTintAndUndoWithoutRestartingOtherActors()
        {
            string target=RecipeTarget(true),other=RecipeTarget(true);var item=editor.Find(target);var geometry=item.GetComponent<RecipeObject>();var tint=new Color(.4f,.5f,.6f,1);Assert.That(editor.PaintObject(target,tint,out var error),Is.True,error);yield return null;
            var beforeRecipe=editor.Read(target).recipe;var beforeJson=JsonUtility.ToJson(beforeRecipe);var before=RecipeFact("object.recipe",target);string selection=editor.SelectedId;var call=RecipePatch(target);var head=beforeRecipe.parts.Single(p=>p.id=="Head");var partJson=JObject.Parse(JsonUtility.ToJson(head));partJson["size"]["x"]=(double)partJson["size"]["x"]+.01;head.size.x=(float)partJson["size"]["x"];call["arguments"]["parts"]=new JArray(partJson);
            var executor=new RoomAgentExecutor(editor);var request=ObjectEditRequest(call);Assert.That(executor.Execute(request,out error,out _),Is.True,error);var receipt=(JObject)executor.Executions.Observe().DeepClone();var after=RecipeFact("object.recipe",target);
            Assert.That(geometry.IsPlaying,Is.False);Assert.That(editor.Read(target).recipe.playing,Is.False);Assert.That(editor.Find(other).GetComponent<RecipeObject>().IsPlaying,Is.True);Assert.That(editor.SelectedId,Is.EqualTo(selection));Assert.That(editor.Find(target),Is.SameAs(item));Assert.That(geometry.Part("Head").GetChild(0).localScale.x,Is.EqualTo(head.size.x));var actualColor=geometry.Part("Head").GetChild(0).GetComponent<Renderer>().sharedMaterial.color;var expectedColor=head.color*tint;for(int channel=0;channel<4;channel++)Assert.That(actualColor[channel],Is.EqualTo(expectedColor[channel]).Within(.00001f));
            Assert.That(((BoxCollider)item.Grab.colliders.Single()).size,Is.EqualTo(geometry.LocalBounds.size));Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==target).recipe.parts.Single(p=>p.id=="Head").size.x,Is.EqualTo(head.size.x));
            string output=Environment.GetEnvironmentVariable("MAESTRO_RECIPE_AUTHORING");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"recipe-authoring.json"),new JObject {["before"]=before,["beforeRecipe"]=JObject.Parse(beforeJson),["call"]=call,["receipt"]=receipt,["after"]=after,["afterRecipe"]=JObject.Parse(JsonUtility.ToJson(editor.Read(target).recipe))}.ToString());}
            editor.Undo();Assert.That(editor.Read(target).recipe.parts.Single(p=>p.id=="Head").size.x,Is.EqualTo(head.size.x-.01f).Within(.000001));Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Read(target).recipe.parts.Single(p=>p.id=="Head").size.x,Is.EqualTo(head.size.x-.01f).Within(.000001));yield return null;
        }
        [UnityTest] public IEnumerator RecipePatchReparentsAndRetimesAtomicallyAndLeavesRecordedMotionIntact()
        {
            string target=RecipeTarget();var original=editor.Read(target);Assert.That(editor.SaveAnimation(target,new RoomMotion {frames=new[]{new MotionFrame {position=original.position},new MotionFrame {time=1,position=original.position+Vector3.up*.1f}}},null,false),Is.True);string motion=JsonUtility.ToJson(editor.Read(target).motion);var call=RecipePatch(target);float oldDuration=original.recipe.duration;
            var neck=original.recipe.parts.Single(p=>p.id=="Neck");neck.parent="NewParent";var parent=new RecipePart {id="NewParent",size=Vector3.one*.05f};call["arguments"]["parts"]=new JArray(JObject.Parse(JsonUtility.ToJson(neck)),JObject.Parse(JsonUtility.ToJson(parent)));call["arguments"]["duration"]=oldDuration*2;call["arguments"]["loop"]=false;RecipeRun(new RoomAgentExecutor(editor),call);
            var edited=editor.Read(target);Assert.That(Array.FindIndex(edited.recipe.parts,p=>p.id=="NewParent"),Is.LessThan(Array.FindIndex(edited.recipe.parts,p=>p.id=="Neck")));Assert.That(editor.Find(target).GetComponent<RecipeObject>().Part("Neck").parent.name,Is.EqualTo("NewParent"));Assert.That(edited.recipe.tracks[0].keys[1].time,Is.EqualTo(original.recipe.tracks[0].keys[1].time*2));Assert.That(JsonUtility.ToJson(edited.motion),Is.EqualTo(motion));Assert.That(edited.position,Is.EqualTo(original.position));Assert.That(edited.color,Is.EqualTo(original.color));yield return null;
        }
        [UnityTest] public IEnumerator RecipePatchRejectsCyclesDanglingTracksAndOverlappingOperationsWithoutChangingGeometry()
        {
            string target=RecipeTarget();var executor=new RoomAgentExecutor(editor);string before=JsonUtility.ToJson(editor.Read(target));var geometry=editor.Find(target).GetComponent<RecipeObject>();var head=geometry.Part("Head");var bad=RecipePatch(target);bad["arguments"]["removeParts"]=new JArray("RightUpperArm");Assert.That(executor.Execute(ObjectEditRequest(bad),out _,out _),Is.False);
            bad=RecipePatch(target);var root=editor.Read(target).recipe.parts.First();root.parent="Head";bad["arguments"]["parts"]=new JArray(JObject.Parse(JsonUtility.ToJson(root)));Assert.That(executor.Execute(ObjectEditRequest(bad),out _,out _),Is.False);
            bad=RecipePatch(target);var part=JObject.Parse(JsonUtility.ToJson(editor.Read(target).recipe.parts.First()));bad["arguments"]["parts"]=new JArray(part,part.DeepClone());Assert.That(executor.Execute(ObjectEditRequest(bad),out _,out _),Is.False);
            bad=RecipePatch(target);bad["arguments"]["removeParts"]=new JArray("Missing");Assert.That(executor.Execute(ObjectEditRequest(bad),out _,out _),Is.False);
            Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(before));Assert.That(geometry.Part("Head"),Is.SameAs(head));yield return null;
        }
        [UnityTest] public IEnumerator RecipePatchRejectsStaleHeldOwnedAndFailedWritesWithoutStoppingAmbientPlayback()
        {
            string target=RecipeTarget(true);var executor=new RoomAgentExecutor(editor);var stale=RecipePatch(target);Assert.That(editor.PaintObject(target,Color.cyan,out _),Is.True);Assert.That(executor.Execute(ObjectEditRequest(stale),out _,out _),Is.False);
            var item=editor.Find(target);var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);Assert.That(executor.Execute(ObjectEditRequest(RecipePatch(target)),out _,out _),Is.False);manager.SelectExit((IXRSelectInteractor)hand,item.Grab);yield return null;
            var playback=new JObject {["id"]="animation.play",["version"]=1,["arguments"]=new JObject {["target"]=target,["source"]=new JObject {["kind"]="recipe"},["channel"]="wholeTarget",["seconds"]=20,["loop"]=true}};Assert.That(runtime.Scheduler.Invoke(playback,Time.unscaledTime,out var playing,out var error),Is.True,error);Assert.That(executor.Execute(ObjectEditRequest(RecipePatch(target)),out _,out _),Is.False);Assert.That((string)runtime.Scheduler.Invocation(playing)["phase"],Is.EqualTo("running"));runtime.Scheduler.CancelInvocation(playing,out _);
            item.GetComponent<RecipeObject>().Restart();string saved=JsonUtility.ToJson(editor.Read(target));string obstacle=Path.Combine(directory,"room.v6.json.pending");Directory.CreateDirectory(obstacle);Assert.That(executor.Execute(ObjectEditRequest(RecipePatch(target)),out _,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(saved));Assert.That(item.GetComponent<RecipeObject>().IsPlaying,Is.True);Directory.Delete(obstacle);yield return null;
        }
        [UnityTest] public IEnumerator EveryRecipePartAndAllTrackKeysRemainReadableAtFullNativeCapacity()
        {
            var recipe=new RoomRecipe {duration=3,parts=Enumerable.Range(0,32).Select(i=>new RecipePart {id=new string('p',30)+i.ToString("D2"),parent=i==0?null:new string('p',30)+"00",position=Vector3.one*.01f,size=Vector3.one*.03f,color=new Color(.2345678f,.4567891f,.6789123f,1)}).ToArray()};recipe.tracks=recipe.parts.Take(17).Select((p,j)=>new RecipeTrack {part=p.id,keys=Enumerable.Range(0,16).Select(i=>new RecipeKey {time=i/15f*3,rotation=Quaternion.Euler(i*11.37f,j*2.73f,i*3.67f)}).ToArray()}).ToArray();Assert.That(editor.CreateRecipe("Complete",new Vector3(.2f,1.2f,.3f),1,recipe,out var target,out var error),Is.True,error);
            for(int i=0;i<32;i++){var part=RecipeFact("object.recipe.part",target,i);Assert.That((string)part["part"]["id"],Is.EqualTo(recipe.parts[i].id));Assert.That((float)part["part"]["color"]["r"],Is.EqualTo(recipe.parts[i].color.r));}
            for(int i=0;i<17;i++)for(int offset=0;offset<=16;offset+=4){var page=RecipeFact("object.recipe.track",target,i,offset);Assert.That((string)page["part"],Is.EqualTo(recipe.tracks[i].part));for(int k=0;k<((JArray)page["keys"]).Count;k++)Assert.That((float)page["keys"][k]["rotation"]["x"],Is.EqualTo(recipe.tracks[i].keys[offset+k].rotation.x));}
            int revision=editor.ObjectRevision(target);var call=RecipePatch(target);call["arguments"]["duration"]=6;RecipeRun(new RoomAgentExecutor(editor),call);Assert.That(editor.Read(target).recipe.tracks[16].keys[15].time,Is.EqualTo(6));Assert.That(BehaviourCatalog.TryRead("object.recipe.part",1,new JObject {["target"]=target,["revision"]=revision,["index"]=0},new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator RecipePatchKeepsTemporaryChangesLocalAndEditedKeysDriveRealParts()
        {
            string target=RecipeTarget();var before=editor.Read(target).recipe;Assert.That(RoomSessionCapability.RunManual(editor,"begin",out var error),Is.True,error);float deadline=Time.realtimeSinceStartup+10;while((editor.TemporarySavePending||runtime.Scheduler.RunningCount>0)&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(editor.TemporarySavePending,Is.False);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);var call=RecipePatch(target);var track=before.tracks[0];track.keys=new[]{new RecipeKey(),new RecipeKey {time=before.duration,rotation=Quaternion.Euler(0,0,80)}};call["arguments"]["tracks"]=new JArray(JObject.Parse(JsonUtility.ToJson(track)));RecipeRun(new RoomAgentExecutor(editor),call);Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==target).recipe.tracks[0].keys.Length,Is.GreaterThan(2));
            var geometry=editor.Find(target).GetComponent<RecipeObject>();var part=geometry.Part(track.part);var rotation=part.localRotation;geometry.StartRule(false);for(int i=0;i<12;i++)yield return null;Assert.That(Quaternion.Angle(rotation,part.localRotation),Is.GreaterThan(.01f));geometry.StopRule();Assert.That(RoomSessionCapability.RunManual(editor,"discard",out error),Is.True,error);Assert.That(editor.Read(target).recipe.tracks[0].keys.Length,Is.GreaterThan(2));
        }
    }
}
