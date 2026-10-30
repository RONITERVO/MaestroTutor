// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class ModelRestBoundsInspectionTests
    {
        [Test] public void RestBoundsAreSeparateFromStaticAndFutureAnimationCoverage(){
            var budget=ImportedModel.LiveBudget;
            foreach(var bytes in new[]{ModelFixture.Create(),RestModelFixture.Skin(),RestModelFixture.Morph()}){
                var info=Task.Run(()=>ModelInspection.Inspect(bytes)).GetAwaiter().GetResult();
                Assert.That(info.AssetBounds.Known,Is.False);Assert.That(info.RestBounds.Known&&info.RestBounds.HasBounds,Is.True,info.RestBounds.Reason);
            }
            Assert.That(ImportedModel.LiveBudget,Is.EqualTo(budget));
        }
        [Test] public void RestBoundsUseNodeMorphDefaultsBeforeMeshDefaultsIncludingExtrapolation(){
            var mesh=ModelInspection.Inspect(RestModelFixture.Morph()).RestBounds;Assert.That(mesh.SourceBounds.min.z,Is.EqualTo(-.4f).Within(.00001));
            var node=ModelInspection.Inspect(RestModelFixture.Morph(.4f,-2)).RestBounds;Assert.That(node.SourceBounds.max.z,Is.EqualTo(2).Within(.00001));
            Assert.That(ModelInspection.Inspect(RestModelFixture.Morph(.4f,0)).RestBounds.SourceBounds.size.z,Is.EqualTo(0));
        }
        [Test] public void RestBoundsNormalizeMixedWeightsAndHandleQuantizedWeightsAndMissingInverseBinds(){
            var normal=ModelInspection.Inspect(RestModelFixture.Skin()).RestBounds;var quantized=ModelInspection.Inspect(RestModelFixture.Skin(quantized:true)).RestBounds;
            Assert.That(normal.Known&&quantized.Known,Is.True);Assert.That(Vector3.Distance(normal.SourceBounds.min,quantized.SourceBounds.min),Is.LessThan(.0001));
            var fallback=ModelInspection.Inspect(RestModelFixture.Skin(missingBind:true)).RestBounds;
            Assert.That(fallback.Known,Is.True,fallback.Reason);Assert.That(Vector3.Distance(fallback.SourceBounds.center,new Vector3(.7f,.3f,-.4f)),Is.LessThan(.0001));
        }
        [Test] public void RestBoundsSelectOnlyTheActiveSceneAndKeepEmptyDistinctFromUnknown(){
            var empty=ModelInspection.Inspect(ModelFixture.Create(r=>r["scenes"][0]["nodes"]=new JArray())).RestBounds;
            Assert.That(empty.Known,Is.True);Assert.That(empty.HasBounds,Is.False);
            var outside=ModelInspection.Inspect(ModelFixture.Mixamo(r=>r["scenes"][0]["nodes"]=new JArray(0))).RestBounds;
            Assert.That(outside.Known,Is.False);Assert.That(outside.Reason,Does.Contain("outside"));
            Assert.That(ModelInspection.Inspect(ModelFixture.Create(avatar:true)).RestBounds.Known,Is.False);
        }
        [Test] public void RestBoundsRefuseSharedSkinAndUnprovenMatrixMathWithoutInventingAnEnvelope(){
            var shared=ModelInspection.Inspect(ModelFixture.Mixamo(r=>{((JArray)r["nodes"]).Add(new JObject{["mesh"]=0,["skin"]=0});((JArray)r["scenes"][0]["nodes"]).Add(((JArray)r["nodes"]).Count-1);})).RestBounds;
            Assert.That(shared.Known,Is.False);Assert.That(shared.Reason,Does.Contain("Shared"));
            var matrix=ModelInspection.Inspect(ModelFixture.Create(r=>r["nodes"][0]["matrix"]=new JArray(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1))).RestBounds;
            Assert.That(matrix.Known,Is.False);Assert.That(matrix.HasBounds,Is.False);
        }
        [Test] public void RestBoundsKeepCancellationMarginSeparateFromFitDimensions(){
            var bytes=RestModelFixture.Edit(RestModelFixture.Skin(),(r,b)=>{
                r["nodes"][1]["translation"]=new JArray(8000,1,0);
                int bind=RestModelFixture.Offset(r,(int)r["skins"][0]["inverseBindMatrices"]);
                for(int j=0;j<17;j++)RestModelFixture.Float(b,bind+j*64+12*4,-8000);
            });
            var result=ModelInspection.Inspect(bytes).RestBounds;Assert.That(result.Known,Is.True,result.Reason);
            Assert.That(result.SourceBounds.size.x,Is.LessThan(2));Assert.That(result.Margin,Is.GreaterThan(1));
        }
        [Test] public void RestBoundsZeroAndNegativeSkinWeightsStayUnknown(){
            foreach(float weight in new[]{0f,-1f}){
                var bytes=RestModelFixture.Edit(ModelFixture.Mixamo(),(r,b)=>{int w=RestModelFixture.Offset(r,(int)r["meshes"][0]["primitives"][0]["attributes"]["WEIGHTS_0"]);for(int i=0;i<3;i++)RestModelFixture.Float(b,w+i*16,weight);});
                Assert.That(ModelInspection.Inspect(bytes).RestBounds.Known,Is.False);
            }
        }
        [Test] public void RestBoundsMalformedMorphDefaultsAreRejectedBeforeNativeAllocation(){
            var bytes=RestModelFixture.Edit(RestModelFixture.Morph(),(r,b)=>r["nodes"][0]["weights"]=new JArray(.5,.5));
            Assert.Throws<ModelImportException>(()=>ModelInspection.Inspect(bytes));
        }
        [Test] public void RestBoundsColdRoomPreparationUsesRestScopeWithoutCreatingNativeModels(){
            string directory=Path.Combine(Path.GetTempPath(),"MaestroColdRest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try{
                var asset=ModelLibrary.Inspect("skin.glb",RestModelFixture.Skin());File.WriteAllBytes(Path.Combine(directory,asset.Hash+".glb"),asset.Bytes);
                var data=new Maestro.Quest.Creation.RoomObjectData{id=new string('a',32),kind=Maestro.Quest.Creation.RoomObjectKind.ImportedModel,modelHash=asset.Hash};
                using var index=new Maestro.Quest.Creation.RoomSpatialIndex(new ModelLibrary(directory));
                var budget=ImportedModel.LiveBudget;index.Synchronize(new Maestro.Quest.Creation.RoomDocument{objects=new[]{data}});
                var deadline=DateTime.UtcNow.AddSeconds(10);while(index.Pending&&DateTime.UtcNow<deadline){index.Tick();Thread.Sleep(1);}
                Assert.That(index.Pending,Is.False);Assert.That(index.TryRead(data.id,Maestro.Quest.Creation.RoomEditor.SpatialSource(data.Copy()),out var bounds),Is.True);
                Assert.That(bounds.Visual.Known&&bounds.Visual.HasBounds,Is.True);Assert.That(bounds.Collision.Known,Is.False);Assert.That(ImportedModel.LiveBudget,Is.EqualTo(budget));
            }finally{Directory.Delete(directory,true);}
        }
        [Test] public void RestBoundsShareTheContentCacheAndDamagedBytesInvalidateBothScopes(){
            string directory=Path.Combine(Path.GetTempPath(),"MaestroRest-"+Guid.NewGuid().ToString("N"));
            try{
                var asset=ModelLibrary.Inspect("skin.glb",RestModelFixture.Skin());var library=new ModelLibrary(directory);library.SaveAsync(asset).GetAwaiter().GetResult();
                var cold=new ModelLibrary(directory);Assert.That(cold.TryReadBounds(asset.Hash,out _,true),Is.False);
                var rest=cold.PrepareRestBoundsAsync(asset.Hash,CancellationToken.None).GetAwaiter().GetResult();Assert.That(rest.Known,Is.True,rest.Reason);
                Assert.That(cold.TryReadBounds(asset.Hash,out var stillStatic),Is.True);Assert.That(stillStatic.Known,Is.False);
                File.WriteAllBytes(Path.Combine(directory,asset.Hash+".glb"),new byte[32]);Assert.Throws<ModelImportException>(()=>cold.ReadAsync(asset.Hash).GetAwaiter().GetResult());
                Assert.That(cold.TryReadBounds(asset.Hash,out _,true),Is.False);Assert.That(cold.TryReadBounds(asset.Hash,out _),Is.False);
            }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}
