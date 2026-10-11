// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class ModelAssetBoundsNativeTests
    {
        GameObject root;
        [UnityTearDown] public IEnumerator Cleanup(){if(root)Object.Destroy(root);yield return null;yield return null;}
        static void EqualBounds(Bounds left,Bounds right)
        {
            Assert.That(Vector3.Distance(left.min,right.min),Is.LessThan(.0002f),$"min {left.min} != {right.min}");
            Assert.That(Vector3.Distance(left.max,right.max),Is.LessThan(.0002f),$"max {left.max} != {right.max}");
        }
        [UnityTest] public IEnumerator ModelAssetBoundsMatchActualUniGltfForStrideSceneInstancesRotationsAndReflection()
        {
            foreach(var bytes in new[]{StaticModelFixture.Create(),StaticModelFixture.Hierarchy(),BuildingModelFixture.Create()}){
                var asset=ModelLibrary.Inspect("Static.glb",bytes);var metadata=asset.Inspection.AssetBounds;Assert.That(metadata.Known&&metadata.HasBounds,Is.True,metadata.Reason);
                root=new GameObject("Asset envelope parity");root.transform.SetPositionAndRotation(new Vector3(4,2,-1),Quaternion.Euler(10,40,-20));root.transform.localScale=Vector3.one*1.2f;
                var model=root.AddComponent<ImportedModel>();var load=model.LoadAsync(asset);yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Exception,Is.Null);
                EqualBounds(metadata.SourceBounds,model.SourceBounds);
                foreach(string scale in new[]{"fitted","source"})foreach(string pivot in new[]{"center","base","source"}){
                    var settings=new RoomModelGeometry{scaleMode=scale,metresPerUnit=scale=="source"?.5f:1,pivot=pivot};
                    Assert.That(ModelGeometryLayout.TryCreate(metadata.SourceBounds,Quaternion.identity,settings,out var layout,out var error),Is.True,error);
                    Assert.That(model.ApplyGeometry(settings,out error),Is.True,error);EqualBounds(layout.Bounds,model.LocalBounds);
                    Assert.That(Vector3.Distance(layout.Position,model.Instance.transform.localPosition),Is.LessThan(.0002f));
                    var bounds=layout.Bounds;bounds.Expand(.0004f);
                    foreach(var renderer in model.Instance.Renderers){
                        var local=renderer.localBounds;
                        for(int i=0;i<8;i++){
                            var p=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                            p=root.transform.InverseTransformPoint(renderer.transform.TransformPoint(p));Assert.That(bounds.Contains(p),Is.True,$"Native mesh corner {p} outside {bounds}");
                        }
                    }
                }
                Object.Destroy(root);yield return null;yield return null;root=null;
            }
        }
        [UnityTest] public IEnumerator ModelAssetBoundsUnknownMatrixModelsStillLoadThroughOrdinaryImporter()
        {
            var asset=ModelLibrary.Inspect("Matrix.glb",StaticModelFixture.Create(data=>data["nodes"][0]["matrix"]=new Newtonsoft.Json.Linq.JArray(1,0,0,0,0,1,0,0,0,0,1,0,2,0,0,1)));
            Assert.That(asset.Inspection.AssetBounds.Known,Is.False);root=new GameObject("Unknown envelope model");var model=root.AddComponent<ImportedModel>();
            var load=model.LoadAsync(asset);yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Exception,Is.Null);Assert.That(model.Ready,Is.True);
        }
    }
}
