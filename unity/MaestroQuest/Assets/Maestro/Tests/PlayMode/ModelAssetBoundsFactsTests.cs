// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Threading;
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
        JObject ModelAssetFact(string id)
        {
            Assert.That(BehaviourCatalog.TryRead("object.model.assetBounds",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);
            return (JObject)value.Value;
        }
        [UnityTest] public IEnumerator ModelAssetBoundsFactSurvivesRetirementAndProjectsCurrentGeometryWithoutLoadingOrWriting()
        {
            var asset=ModelLibrary.Inspect("Static.glb",StaticModelFixture.Create());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var create=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);yield return new WaitUntil(()=>create.IsCompleted);Assert.That(create.Exception,Is.Null);
            string id=create.Result,area=NativeAreaFor(id);var fact=ModelAssetFact(id);Assert.That((bool)fact["inspected"]&&(bool)fact["known"]&&(bool)fact["hasBounds"],Is.True);
            Assert.That((string)fact["coordinates"],Is.EqualTo("object"));Assert.That((string)fact["modelHash"],Is.EqualTo(asset.Hash));
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;yield return null;
            var data=editor.Read(id);data.position=new Vector3(8,4,-5);data.scale=2;data.modelGeometry=new RoomModelGeometry{scaleMode="source",pivot="base",metresPerUnit=1};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;var budget=ImportedModel.LiveBudget;
            // Metadata describes verified content, not current availability; the
            // pure fact must not touch the removed backing file or instantiate it.
            File.Delete(Path.Combine(directory,"models",asset.Hash+".glb"));
            for(int i=0;i<3;i++){
                fact=ModelAssetFact(id);Assert.That((bool)fact["known"],Is.True);Assert.That((float)fact["min"]["y"],Is.EqualTo(0).Within(.0001));
                Assert.That((float)fact["max"]["y"],Is.EqualTo(3).Within(.0001));Assert.That((int)fact["revision"],Is.EqualTo(editor.ObjectRevision(id)));
                Assert.That(editor.Find(id),Is.Null);
            }
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(ImportedModel.LiveBudget,Is.EqualTo(budget));
            Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Known,Is.False,"Asset-only metadata must not silently claim whole-object or decorated bounds");
        }
        [UnityTest] public IEnumerator ModelAssetBoundsFactFollowsExactHashAndReportsAnimationAndUnavailableMetadataAsUnknown()
        {
            yield return GeometryModel(StaticModelFixture.Create());string id=geometryTarget,area=NativeAreaFor(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            yield return GeometryModel(ModelFixture.Create());string animatedId=geometryTarget;
            var fact=ModelAssetFact(animatedId);Assert.That((bool)fact["inspected"],Is.True);Assert.That((bool)fact["known"],Is.False);Assert.That((bool)fact["hasBounds"],Is.False);Assert.That((string)fact["reason"],Does.Contain("Animated"));
            Assert.That((string)ModelAssetFact(id)["modelHash"],Is.Not.EqualTo((string)fact["modelHash"]));Assert.That((bool)ModelAssetFact(id)["known"],Is.True);
            string hash=editor.Read(id).modelHash;File.Delete(Path.Combine(directory,"models",hash+".glb"));
            var missing=editor.Models.ReadAsync(hash);yield return new WaitUntil(()=>missing.IsCompleted);Assert.That(missing.IsFaulted,Is.True);
            fact=ModelAssetFact(id);Assert.That((bool)fact["inspected"],Is.False);Assert.That((bool)fact["known"],Is.False);Assert.That(editor.Find(id),Is.Null);
            Assert.That((bool)ModelAssetFact(animatedId)["inspected"],Is.True);
            Assert.That(BehaviourCatalog.TryRead("object.model.assetBounds",1,new JObject{["target"]=editor.Identity(block)},new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
        }
    }
}
