// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject PresenceFact(string target)=>SavedFact("object.presence",new JObject{["target"]=target});
        JObject SavedFact(string id,JObject args){Assert.That(BehaviourCatalog.TryRead(id,1,args,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True,id);return (JObject)value.Value;}
        [UnityTest] public IEnumerator ObjectPresenceSeparatesDisabledInstanceFromSavedContentAndRejectsLiveActions()
        {
            string target=editor.Identity(block),before=JsonUtility.ToJson(editor.Snapshot());int revision=editor.ObjectRevision(target);
            Assert.That((string)PresenceFact(target)["state"],Is.EqualTo("active"));
            block.transform.localPosition+=Vector3.up;
            Assert.That(editor.ObserveObjects().Single(x=>x.id==target).positionSource,Is.EqualTo("live"));
            block.gameObject.SetActive(false);
            var presence=PresenceFact(target);Assert.That((string)presence["state"],Is.EqualTo("inactive"));Assert.That((bool)presence["saved"],Is.True);Assert.That((bool)presence["nativeInstance"],Is.True);Assert.That((bool)presence["active"],Is.False);
            var observed=editor.ObserveObjects().Single(x=>x.id==target);Assert.That(observed.positionSource,Is.EqualTo("saved"));Assert.That(observed.position,Is.EqualTo(editor.Read(target).position));
            Assert.That(new CapabilityContext(editor,animations).Target(new JObject{["target"]=target},out _,out var error),Is.False);StringAssert.Contains("saved content",error);
            Assert.That(editor.CanEditObject(target,true,out error),Is.True,"Saved authoring remains possible for an inactive native instance: "+error);
            Assert.That(editor.ObjectRevision(target),Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
            block.gameObject.SetActive(true);Assert.That((string)PresenceFact(target)["state"],Is.EqualTo("active"));yield return null;
        }
        [UnityTest] public IEnumerator ObjectPresenceDestroyedInstanceKeepsDefinitionAnimationAndRevisionFacts()
        {
            string target=editor.Identity(block);int revision=editor.ObjectRevision(target);var args=new JObject{["target"]=target};
            var definition=SavedFact("object.definition",args);var motion=SavedFact("animation.authored",args);
            var frameArgs=new JObject{["target"]=target,["revision"]=revision,["index"]=0};var frame=SavedFact("animation.frame",frameArgs);
            var collision=SavedFact("object.collision",args);string before=JsonUtility.ToJson(editor.Snapshot());
            UnityEngine.Object.Destroy(block.gameObject);yield return null;
            Assert.That((string)PresenceFact(target)["state"],Is.EqualTo("unavailable"));Assert.That((bool)PresenceFact(target)["nativeInstance"],Is.False);
            Assert.That(JToken.DeepEquals(definition,SavedFact("object.definition",args)),Is.True);Assert.That(JToken.DeepEquals(motion,SavedFact("animation.authored",args)),Is.True);Assert.That(JToken.DeepEquals(frame,SavedFact("animation.frame",frameArgs)),Is.True);Assert.That(JToken.DeepEquals(collision,SavedFact("object.collision",args)),Is.True);
            frameArgs["revision"]=revision+1;Assert.That(BehaviourCatalog.TryRead("animation.frame",1,frameArgs,new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
            var observed=editor.ObserveObjects().Single(x=>x.id==target);Assert.That(observed.runtimeState,Is.EqualTo("unavailable"));Assert.That(observed.positionSource,Is.EqualTo("saved"));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator ObjectPresenceSavedRecipeKeysSurviveNativeLossWithoutInventingPlayback()
        {
            string target=RecipeTarget(true);var part=RecipeFact("object.recipe.part",target,0);var track=RecipeFact("object.recipe.track",target,0,0);
            UnityEngine.Object.Destroy(editor.Find(target).gameObject);yield return null;
            Assert.That(JToken.DeepEquals(part,RecipeFact("object.recipe.part",target,0)),Is.True);Assert.That(JToken.DeepEquals(track,RecipeFact("object.recipe.track",target,0,0)),Is.True);
            Assert.That(BehaviourCatalog.TryRead("object.recipe",1,new JObject{["target"]=target},new BehaviourCatalog.FactContext(editor:editor),out _),Is.False,"Unavailable playback is not stopped playback");
            Assert.That(editor.Read(target).recipe.playing,Is.True);
        }
        [UnityTest] public IEnumerator ObjectPresenceSavedDrawingSurvivesNativeLoss()
        {
            string target=NewStroke();var before=DrawingFact(target);var points=JsonUtility.ToJson(editor.Read(target));
            UnityEngine.Object.Destroy(editor.Find(target).gameObject);yield return null;
            Assert.That(JToken.DeepEquals(before,DrawingFact(target)),Is.True);Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(points));
        }
        [UnityTest] public IEnumerator ObjectPresenceAcceptedEditRecreatesLostCreationAndUndoPreservesIdentityAndMotion()
        {
            string target=editor.Identity(block),area=NewRegion();Assert.That(AssignRegion(target,area,out var error),Is.True,error);
            var old=editor.Read(target);string motion=JsonUtility.ToJson(old.motion);UnityEngine.Object.Destroy(block.gameObject);yield return null;
            var changed=editor.Read(target);changed.position+=Vector3.up*.2f;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            Assert.That((string)PresenceFact(target)["state"],Is.EqualTo("active"));Assert.That(editor.Identity(editor.Find(target)),Is.EqualTo(target));Assert.That(editor.RegionFor(target),Is.EqualTo(area));Assert.That(JsonUtility.ToJson(editor.Read(target).motion),Is.EqualTo(motion));
            editor.Undo();Assert.That(editor.Read(target).position,Is.EqualTo(old.position));Assert.That((string)PresenceFact(target)["state"],Is.EqualTo("active"));
        }
        [UnityTest] public IEnumerator ObjectPresenceDeletionAndUndoDoNotLeaveLostNativeIdentityBehind()
        {
            string target=editor.Identity(block);UnityEngine.Object.Destroy(block.gameObject);yield return null;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{target},out var error),Is.True,error);
            var presence=PresenceFact(target);Assert.That((string)presence["state"],Is.EqualTo("missing"));Assert.That((bool)presence["saved"],Is.False);Assert.That((int)presence["revision"],Is.Zero);Assert.That(editor.ObserveObjects().Any(x=>x.id==target),Is.False);
            editor.Undo();Assert.That((string)PresenceFact(target)["state"],Is.EqualTo("active"));Assert.That(editor.Read(target).motion.frames.Length,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator ObjectPresenceLostWorldOwnedObjectsNeverBecomeCreationPrefabs()
        {
            UnityEngine.Object.Destroy(editor.Find("book").gameObject);UnityEngine.Object.Destroy(editor.Find("maestro").gameObject);yield return null;
            var changed=editor.Read(editor.Identity(block));changed.name="Edited after host loss";
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out var error),Is.True,error);
            foreach(var target in new[]{"book","maestro"}){Assert.That(editor.HasSavedObject(target),Is.True);Assert.That((string)PresenceFact(target)["state"],Is.EqualTo("unavailable"));Assert.That((bool)editor.Find(target),Is.False);Assert.That(SavedFact("object.definition",new JObject{["target"]=target}),Is.Not.Null);}
        }
    }
}
