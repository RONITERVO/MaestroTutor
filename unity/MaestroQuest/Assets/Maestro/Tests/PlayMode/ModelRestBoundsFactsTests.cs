// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject RestModelFact(string id){
            Assert.That(BehaviourCatalog.TryRead("object.model.restBounds",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);
            return (JObject)value.Value;
        }
        [UnityTest] public IEnumerator RestBoundsSharedFactsAndDormantGeometryStayPassiveAndUseCurrentSettings(){
            yield return GeometryModel(RestModelFixture.Skin());string id=geometryTarget,area=NativeAreaFor(id);
            Assert.That((bool)ModelAssetFact(id)["known"],Is.False);Assert.That((bool)RestModelFact(id)["known"],Is.True);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var data=editor.Read(id);data.modelGeometry=new RoomModelGeometry{scaleMode="source",metresPerUnit=.5f,pivot="base"};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            for(int i=0;i<80&&!(bool)editor.ObserveAuthoredBounds(id)["prepared"];i++)yield return null;
            Assert.That((bool)editor.ObserveAuthoredBounds(id)["visual"]["known"],Is.True);
            var budget=ImportedModel.LiveBudget;string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            for(int i=0;i<12;i++){
                var fact=RestModelFact(id);Assert.That((bool)fact["known"],Is.True);Assert.That((int)fact["revision"],Is.EqualTo(editor.ObjectRevision(id)));
                Assert.That((float)fact["min"]["y"],Is.EqualTo(0).Within(.001));Assert.That(editor.Find(id),Is.Null);
            }
            Assert.That(ImportedModel.LiveBudget,Is.EqualTo(budget));Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            var bounds=editor.ReadSpatialBounds(id,out var source);Assert.That(source,Is.EqualTo("authored"));Assert.That(bounds.Visual.Known,Is.True);Assert.That(bounds.Collision.Known,Is.False);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            var model=editor.Find(id).GetComponent<CreatedRoomObject>().Model;
            foreach(var skin in model.Instance.SkinnedMeshRenderers)SkinBoundsContainBaked(skin,bounds.Visual,editor.transform.worldToLocalMatrix);
        }
        [UnityTest] public IEnumerator RestBoundsDoNotReleaseNativeAnimationOrClaimItsCurrentPose(){
            yield return GeometryModel(ModelFixture.TranslationMotion("LINEAR",4));string id=geometryTarget;
            var model=editor.Find(id).GetComponent<CreatedRoomObject>().Model;var rest=RestModelFact(id).ToString();
            model.Play(0,true);yield return null;Assert.That(model.IsPlaying,Is.True);Assert.That(RetainedFor(id,RoomRetentionReason.Animation),Is.True);
            Assert.That(RestModelFact(id).ToString(),Is.EqualTo(rest));Assert.That(editor.RetireNativeArea(NativeAreaFor(id),out _),Is.False);
            model.Stop();Assert.That(model.IsPlaying,Is.False);Assert.That(RetainedFor(id,RoomRetentionReason.Animation),Is.False);yield return null;
        }
    }
}
