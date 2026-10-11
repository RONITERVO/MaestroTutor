// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class ModelAssetBoundsInspectionTests
    {
        [Test] public void DenseStridedPositionsOverrideUntrustedAccessorHintsOnWorker()
        {
            var budget=ImportedModel.LiveBudget;
            var value=Task.Run(()=>ModelInspection.Inspect(StaticModelFixture.Create()).AssetBounds).GetAwaiter().GetResult();
            Assert.That(value.Known&&value.HasBounds,Is.True,value.Reason);
            Assert.That(value.SourceBounds.min,Is.EqualTo(new Vector3(-2,-1,-4)));
            Assert.That(value.SourceBounds.max,Is.EqualTo(new Vector3(3,2,1)));
            Assert.That(ImportedModel.LiveBudget,Is.EqualTo(budget));
        }
        [Test] public void SelectedSceneAndAllReachableMeshInstancesDetermineEnvelope()
        {
            var value=ModelInspection.Inspect(StaticModelFixture.Create(root=>{
                root["scene"]=1;root["scenes"]=new JArray(new JObject{["nodes"]=new JArray(0)},new JObject{["nodes"]=new JArray(1,2)});
                root["nodes"]=new JArray(new JObject{["mesh"]=0,["translation"]=new JArray(100,0,0)},new JObject{["mesh"]=0,["translation"]=new JArray(2,0,0)},new JObject{["mesh"]=0,["translation"]=new JArray(-3,0,0)});
            })).AssetBounds;
            Assert.That(value.Known,Is.True,value.Reason);Assert.That(value.SourceBounds.min.x,Is.EqualTo(-5));Assert.That(value.SourceBounds.max.x,Is.EqualTo(5));
        }
        [Test] public void EmptySelectedSceneIsDistinctFromUnknown()
        {
            var value=ModelInspection.Inspect(StaticModelFixture.Create(root=>root["scenes"][0]["nodes"]=new JArray())).AssetBounds;
            Assert.That(value.Known,Is.True);Assert.That(value.HasBounds,Is.False);Assert.That(value.Reason,Is.Empty);
        }
        [Test] public void AnimationSkinVrmAndMorphAreNeverStaticEnvelopes()
        {
            Assert.That(ModelInspection.Inspect(ModelFixture.Create()).AssetBounds.Known,Is.False);
            Assert.That(ModelInspection.Inspect(ModelFixture.Create(root=>root.Remove("animations"),avatar:true)).AssetBounds.Known,Is.False);
            var morph=ModelInspection.Inspect(StaticModelFixture.Create(root=>root["meshes"][0]["primitives"][0]["targets"]=new JArray(new JObject{["POSITION"]=0}))).AssetBounds;
            Assert.That(morph.Known,Is.False);Assert.That(morph.HasBounds,Is.False);Assert.That(morph.Reason,Does.Contain("deformable"));
        }
        [Test] public void UnsupportedNodeMathStaysUnknownWithoutRejectingAnOtherwiseValidImport()
        {
            var matrix=ModelInspection.Inspect(StaticModelFixture.Create(root=>root["nodes"][0]["matrix"]=new JArray(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1))).AssetBounds;
            Assert.That(matrix.Known,Is.False);Assert.That(matrix.Reason,Does.Contain("Matrix"));
            var zero=ModelInspection.Inspect(StaticModelFixture.Create(root=>root["nodes"][0]["rotation"]=new JArray(0,0,0,0))).AssetBounds;
            Assert.That(zero.Known,Is.False);Assert.That(zero.Reason,Does.Contain("degenerate"));
        }
        [Test] public void OversizedAndOverflowingTransformsNeverPublishFiniteLookingZeroBounds()
        {
            var value=ModelInspection.Inspect(StaticModelFixture.Create(root=>{
                var nodes=new JArray();for(int i=0;i<16;i++){var n=new JObject{["scale"]=new JArray(10000,10000,10000)};if(i==15)n["mesh"]=0;else n["children"]=new JArray(i+1);nodes.Add(n);}root["nodes"]=nodes;
            })).AssetBounds;
            Assert.That(value.Known,Is.False);Assert.That(value.HasBounds,Is.False);Assert.That(value.Reason,Is.Not.Empty);
            Assert.That(ModelInspection.Inspect(StaticModelFixture.Create(root=>root["nodes"][0]["scale"]=new JArray(10000,10000,10000))).AssetBounds.Known,Is.False);
        }
        [Test] public void FarOriginCancellationRemainsUnknownEvenWhenSourceDimensionsAreSmall()
        {
            var value=ModelInspection.Inspect(StaticModelFixture.Create(root=>{
                root["nodes"]=new JArray(new JObject{["translation"]=new JArray(10000,0,0),["children"]=new JArray(1)},new JObject{["translation"]=new JArray(10000,0,0),["mesh"]=0});
            })).AssetBounds;
            Assert.That(value.Known,Is.False);Assert.That(value.HasBounds,Is.False);Assert.That(value.Reason,Does.Contain("precision"));
        }
        [Test] public void SharedLayoutRejectsInvalidSourceAndOversizedConfiguredGeometry()
        {
            var source=new Bounds(new Vector3(2,1,-1),new Vector3(4,2,6));
            Assert.That(ModelGeometryLayout.TryCreate(source,Quaternion.identity,new RoomModelGeometry{scaleMode="source",metresPerUnit=3},out _,out _),Is.False);
            source.center=new Vector3(float.PositiveInfinity,0,0);Assert.That(ModelGeometryLayout.TryCreate(source,Quaternion.identity,new RoomModelGeometry(),out _,out _),Is.False);
            source=new Bounds(new Vector3(100,0,0),Vector3.one);Assert.That(ModelGeometryLayout.TryCreate(source,Quaternion.identity,new RoomModelGeometry{scaleMode="source",pivot="source"},out _,out _),Is.False);
        }
        [Test] public void VerifiedLibraryReadsPopulateColdMetadataWithoutNativeInstancesAndDamageInvalidatesIt()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroBounds-"+Guid.NewGuid().ToString("N"));
            try {
                var library=new ModelLibrary(directory);var asset=ModelLibrary.Inspect("Static.glb",StaticModelFixture.Create());
                Assert.That(library.TryReadBounds(asset.Hash,out _),Is.False);Assert.That(Directory.Exists(directory),Is.False);
                // Persistence must inspect bytes itself, not trust a substituted inspection object.
                asset.Inspection=ModelInspection.Inspect(ModelFixture.Create());library.SaveAsync(asset).GetAwaiter().GetResult();
                Assert.That(library.TryReadBounds(asset.Hash,out var warm),Is.True);Assert.That(warm.Known,Is.True);
                var cold=new ModelLibrary(directory);Assert.That(cold.TryReadBounds(asset.Hash,out _),Is.False);var budget=ImportedModel.LiveBudget;
                cold.ReadAsync(asset.Hash).GetAwaiter().GetResult();Assert.That(cold.TryReadBounds(asset.Hash,out var read),Is.True);Assert.That(read.SourceBounds,Is.EqualTo(warm.SourceBounds));Assert.That(ImportedModel.LiveBudget,Is.EqualTo(budget));
                File.WriteAllBytes(Path.Combine(directory,asset.Hash+".glb"),new byte[30]);
                Assert.Throws<ModelImportException>(()=>cold.ReadAsync(asset.Hash).GetAwaiter().GetResult());Assert.That(cold.TryReadBounds(asset.Hash,out _),Is.False);
            } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void MetadataCacheIsBoundedAndNeverRehydratesEvictedEntriesDuringRead()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroBounds-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try {
                var library=new ModelLibrary(directory);string first=null,last=null;
                for(int i=0;i<33;i++){
                    var asset=ModelLibrary.Inspect("Static.glb",StaticModelFixture.Create(root=>root["asset"]["copyright"]="Fixture "+i));
                    first??=asset.Hash;last=asset.Hash;File.WriteAllBytes(Path.Combine(directory,asset.Hash+".glb"),asset.Bytes);library.ReadAsync(asset.Hash).GetAwaiter().GetResult();
                }
                Assert.That(library.TryReadBounds(first,out _),Is.False);Assert.That(library.TryReadBounds(last,out _),Is.True);Assert.That(library.TryReadBounds("../file",out _),Is.False);
                Assert.That(File.Exists(Path.Combine(directory,first+".glb")),Is.True);Assert.That(library.TryReadBounds(first,out _),Is.False);
            } finally {Directory.Delete(directory,true);}
        }
    }
}
