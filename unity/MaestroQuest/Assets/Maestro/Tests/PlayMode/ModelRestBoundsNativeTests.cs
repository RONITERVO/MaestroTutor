// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class ModelRestBoundsNativeTests
    {
        GameObject root;
        [UnityTearDown] public IEnumerator Cleanup(){if(root)Object.Destroy(root);yield return null;yield return null;}
        IEnumerator Check(byte[] bytes,bool known=true){
            var asset=ModelLibrary.Inspect("rest.glb",bytes);var rest=asset.Inspection.RestBounds;Assert.That(rest.Known,Is.EqualTo(known),rest.Reason);if(known)Assert.That(rest.HasBounds,Is.True);
            root=new GameObject("Rest bounds parity");root.transform.SetPositionAndRotation(new Vector3(3,1,-2),Quaternion.Euler(15,35,10));root.transform.localScale=Vector3.one*1.3f;
            var model=root.AddComponent<ImportedModel>();var load=model.LoadAsync(asset);yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Exception,Is.Null);
            Assert.That(model.IsPlaying,Is.False);if(known)Assert.That(model.SourceBounds,Is.EqualTo(rest.SourceBounds));
            foreach(string scale in new[]{"fitted","source"})foreach(string pivot in new[]{"center","base","source"}){
                var settings=new RoomModelGeometry{scaleMode=scale,pivot=pivot,metresPerUnit=scale=="source"?.5f:1};
                Assert.That(model.ApplyGeometry(settings,out var error),Is.True,error);
                Assert.That(ModelGeometryLayout.TryCreate(model.SourceBounds,Quaternion.identity,settings,out var layout,out error),Is.True,error);
                Assert.That(model.LocalBounds,Is.EqualTo(layout.Bounds));var bounds=layout.Bounds;bounds.Expand(.001f);
                var measured=new Bounds();bool found=false;
                foreach(var renderer in model.Instance.Renderers){
                    Mesh baked=null;var mesh=renderer is SkinnedMeshRenderer skin?(baked=new Mesh()):renderer.GetComponent<MeshFilter>().sharedMesh;
                    try{
                        // Compensate inherited scale before applying the native Transform.
                        if(renderer is SkinnedMeshRenderer s)s.BakeMesh(baked,true);
                        Assert.That(mesh.vertexCount,Is.GreaterThan(0));
                        foreach(var vertex in mesh.vertices){
                            var point=root.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                            Assert.That(bounds.Contains(point),Is.True,$"Actual Unity rest vertex {point} outside {bounds} ({scale}/{pivot})");
                            if(found)measured.Encapsulate(point);else {measured=new Bounds(point,Vector3.zero);found=true;}
                        }
                    }finally{if(baked)Object.Destroy(baked);}
                }
                Assert.That(found,Is.True);
                if(!known){
                    Assert.That(Vector3.Distance(measured.min,layout.Bounds.min),Is.LessThan(.001f),"Native fallback measures the actual default shape");
                    Assert.That(Vector3.Distance(measured.max,layout.Bounds.max),Is.LessThan(.001f));
                }
            }
            Object.Destroy(root);root=null;yield return null;yield return null;
        }
        [UnityTest] public IEnumerator RestBoundsNativeWeightedSkinAndInverseBindsMatchActualDeformedVertices(){yield return Check(RestModelFixture.Skin());}
        [UnityTest] public IEnumerator RestBoundsNativeQuantizedWeightsMatchActualDeformedVertices(){yield return Check(RestModelFixture.Skin(quantized:true));}
        [UnityTest] public IEnumerator RestBoundsNativeMissingInverseBindsMatchImporterFallback(){yield return Check(RestModelFixture.Skin(missingBind:true));}
        [UnityTest] public IEnumerator RestBoundsNativeMorphDefaultsAndNodeOverridesMatchActualDeformedVertices(){yield return Check(RestModelFixture.Morph(.4f,-2));yield return Check(RestModelFixture.Morph(multiple:true));}
        [UnityTest] public IEnumerator RestBoundsNativeAnimatedRigidSourceAndHierarchyMatchActualVertices(){yield return Check(ModelFixture.Create());yield return Check(StaticModelFixture.Hierarchy());}
        [UnityTest] public IEnumerator RestBoundsNativeCombinedSkinMorphAndNonuniformJointFramesMatchActualVertices(){
            var bytes=RestModelFixture.Edit(RestModelFixture.Skin(),(json,binary)=>{
                json["nodes"][1]["scale"]=new Newtonsoft.Json.Linq.JArray(1.2,.8,1.1);
                var q=Quaternion.Euler(15,35,-10);json["nodes"][5]["rotation"]=new Newtonsoft.Json.Linq.JArray(q.x,q.y,q.z,q.w);
                json["nodes"][5]["scale"]=new Newtonsoft.Json.Linq.JArray(.7,1.3,1.1);
                var mesh=json["meshes"][0];mesh["primitives"][0]["targets"]=new Newtonsoft.Json.Linq.JArray(new Newtonsoft.Json.Linq.JObject{["POSITION"]=1});
                mesh["extras"]=new Newtonsoft.Json.Linq.JObject{["targetNames"]=new Newtonsoft.Json.Linq.JArray("Rest offset")};mesh["weights"]=new Newtonsoft.Json.Linq.JArray(.6);
            });
            yield return Check(bytes);
        }
        [UnityTest] public IEnumerator RestBoundsNativeUnknownMatrixMorphStillFitsActualGeometry(){
            foreach(var source in new[]{RestModelFixture.Morph(.4f,1.2f),RestModelFixture.Morph(.4f,-2),RestModelFixture.Skin(),StaticModelFixture.Hierarchy()}){
                var bytes=RestModelFixture.Edit(source,(json,binary)=>{
                    var node=(Newtonsoft.Json.Linq.JObject)json["nodes"][0];
                    node.Remove("translation");node.Remove("rotation");node.Remove("scale");
                    node["matrix"]=new Newtonsoft.Json.Linq.JArray(1.2,0,0,0,0,.8,0,0,0,0,1.1,0,.2,.3,.4,1);
                });
                yield return Check(bytes,false);
            }
        }
        [UnityTest] public IEnumerator RestBoundsNativeStopRestoresDefaultMorphAfterSamplingAnimation(){
            var asset=ModelLibrary.Inspect("morph.glb",RestModelFixture.Morph(.4f,.75f));root=new GameObject("Rest weights");var model=root.AddComponent<ImportedModel>();
            var load=model.LoadAsync(asset);yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Exception,Is.Null);
            var skin=model.Instance.SkinnedMeshRenderers.Single();Assert.That(skin.GetBlendShapeWeight(0),Is.EqualTo(75).Within(.001));
            Assert.That(model.SampleClip(0,1,false),Is.True);Assert.That(skin.GetBlendShapeWeight(0),Is.EqualTo(100).Within(.001));
            model.Stop();Assert.That(skin.GetBlendShapeWeight(0),Is.EqualTo(75).Within(.001));
            root.SetActive(false);root.SetActive(true);Assert.That(skin.GetBlendShapeWeight(0),Is.EqualTo(75).Within(.001));yield return null;
        }
    }
}
