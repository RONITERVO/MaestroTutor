// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        IEnumerator SkinBoundsImport(byte[] bytes,Action<string,ImportedModel> completed)
        {
            var asset=ModelLibrary.Inspect("skin.glb",bytes);var save=editor.Models.SaveAsync(asset);
            yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var load=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);
            yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Exception,Is.Null);
            completed(load.Result,editor.Find(load.Result).GetComponent<CreatedRoomObject>().Model);
        }
        static void SkinBoundsContainBaked(SkinnedMeshRenderer skin,RoomSpatialExtent extent,Matrix4x4 inverse)
        {
            var baked=new Mesh();
            try{skin.BakeMesh(baked,true);foreach(var point in baked.vertices)SpatialContains(extent,inverse.MultiplyPoint3x4(skin.transform.TransformPoint(point)));}
            finally{UnityEngine.Object.Destroy(baked);}
        }
        [UnityTest] public IEnumerator SkinBoundsFollowActualJointPosesAndNonuniformTransforms()
        {
            ImportedModel model=null;yield return SkinBoundsImport(ModelFixture.Mixamo(),(_,value)=>model=value);
            var skin=model.Instance.SkinnedMeshRenderers.Single();var owned=skin.GetComponent<ImportedSkinBounds>();Assert.That(owned,Is.Not.Null);
            var head=model.Humanoid.GetBoneTransform(HumanBodyBones.Head);var hips=model.Humanoid.GetBoneTransform(HumanBodyBones.Hips);
            for(int i=0;i<12;i++){
                model.Stop();hips.localPosition+=new Vector3(i*.2f,0,-.05f*i);head.localRotation=Quaternion.Euler(i*13,i*19,i*7);head.localScale=new Vector3(1,1+i*.1f,.6f);
                model.transform.SetLocalPositionAndRotation(new Vector3(2,1,3),Quaternion.Euler(10,60,20));model.transform.localScale=new Vector3(1.4f,.7f,2);
                SkinBoundsContainBaked(skin,owned.Read(Matrix4x4.identity),Matrix4x4.identity);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator SkinBoundsIncludePositiveNegativeAndExtrapolatedMorphWeights()
        {
            ImportedModel model=null;yield return SkinBoundsImport(ModelFixture.MorphMotion(),(_,value)=>model=value);
            var skin=model.Instance.SkinnedMeshRenderers.Single();var owned=skin.GetComponent<ImportedSkinBounds>();
            foreach(float weight in new[]{-200f,-25,0,30,100,250}){
                skin.SetBlendShapeWeight(0,weight);var bounds=owned.Read(Matrix4x4.identity);SkinBoundsContainBaked(skin,bounds,Matrix4x4.identity);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator SkinBoundsObserveActualGeometryEvenWhenRendererBoundsAreStale()
        {
            ImportedModel model=null;yield return SkinBoundsImport(ModelFixture.Mixamo(),(_,value)=>model=value);
            var skin=model.Instance.SkinnedMeshRenderers.Single();var camera=NativeObservationCamera(false);
            var hips=model.Humanoid.GetBoneTransform(HumanBodyBones.Hips);hips.position=new Vector3(0,0,3);
            skin.bounds=new Bounds(new Vector3(0,0,-30),Vector3.one*.01f);
            Assert.That(new RoomObservationView(camera).Includes(skin),Is.True);
            skin.GetComponent<ImportedSkinBounds>().RefreshRendererBounds();
            SkinBoundsContainBaked(skin,new RoomSpatialExtent{Known=true,HasBounds=true,Bounds=skin.bounds},Matrix4x4.identity);
            hips.position=new Vector3(0,0,-3);Assert.That(new RoomObservationView(camera).Includes(skin),Is.False);
            camera.cullingMask=0;Assert.That(new RoomObservationView(camera).Includes(skin),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator SkinBoundsRetirementCoversCapturedAndResetGeometryWithoutLoading()
        {
            ImportedModel model=null;string id=null;yield return SkinBoundsImport(ModelFixture.Mixamo(),(target,value)=>{id=target;model=value;});
            var skin=model.Instance.SkinnedMeshRenderers.Single();model.Humanoid.GetBoneTransform(HumanBodyBones.Head).localPosition+=Vector3.right*3;
            var before=editor.ReadSpatialBounds(id,out _);SkinBoundsContainBaked(skin,before.Visual,editor.transform.worldToLocalMatrix);
            string area=NativeAreaFor(id);var snapshot=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var dormant=editor.ReadSpatialBounds(id,out var source);Assert.That(source,Is.EqualTo("retained"));Assert.That(dormant.Visual.Known,Is.True);Assert.That(dormant.Visual.Bounds,Is.EqualTo(before.Visual.Bounds));
            Assert.That(editor.Find(id),Is.Null);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(snapshot));Assert.That(editor.Revision,Is.EqualTo(revision));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            skin=editor.Find(id).GetComponent<CreatedRoomObject>().Model.Instance.SkinnedMeshRenderers.Single();SkinBoundsContainBaked(skin,dormant.Visual,editor.transform.worldToLocalMatrix);
        }
        [UnityTest] public IEnumerator SkinBoundsReadsArePassiveAndReusePreparedMeshData()
        {
            ImportedModel model=null;yield return SkinBoundsImport(ModelFixture.Mixamo(),(_,value)=>model=value);
            var skin=model.Instance.SkinnedMeshRenderers.Single();var owned=skin.GetComponent<ImportedSkinBounds>();
            var before=skin.bounds;var pose=model.Humanoid.GetBoneTransform(HumanBodyBones.Head).localRotation;
            for(int i=0;i<8;i++)owned.Read(Matrix4x4.identity,true);
            long allocated=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<512;i++)owned.Read(Matrix4x4.identity,true);
            Assert.That(GC.GetAllocatedBytesForCurrentThread()-allocated,Is.LessThan(4096));
            Assert.That(skin.bounds,Is.EqualTo(before));Assert.That(model.Humanoid.GetBoneTransform(HumanBodyBones.Head).localRotation,Is.EqualTo(pose));Assert.That(model.IsPlaying,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator SkinBoundsChangedMeshOrQualityRemainUnknownAndReleaseCustomCulling()
        {
            ImportedModel model=null;yield return SkinBoundsImport(ModelFixture.Mixamo(),(_,value)=>model=value);
            var skin=model.Instance.SkinnedMeshRenderers.Single();var owned=skin.GetComponent<ImportedSkinBounds>();var original=skin.sharedMesh;
            skin.quality=SkinQuality.Bone2;Assert.That(owned.Read(Matrix4x4.identity).Known,Is.False);owned.RefreshRendererBounds();Assert.That(skin.updateWhenOffscreen,Is.True);
            skin.quality=SkinQuality.Bone4;var other=UnityEngine.Object.Instantiate(original);skin.sharedMesh=other;
            Assert.That(owned.Read(Matrix4x4.identity).Known,Is.False);skin.sharedMesh=original;UnityEngine.Object.Destroy(other);
            owned.RefreshRendererBounds();Assert.That(skin.updateWhenOffscreen,Is.False);Assert.That(owned.Read(Matrix4x4.identity).Known,Is.True);yield return null;
        }
        [UnityTest] public IEnumerator SkinBoundsRejectUnsupportedFramesAndInvalidWeightsConservatively()
        {
            ImportedModel model=null;yield return SkinBoundsImport(ModelFixture.MorphMotion(),(_,value)=>model=value);
            var source=model.Instance.SkinnedMeshRenderers.Single();var go=new GameObject("Unsupported skin");go.transform.SetParent(root.transform,false);
            var skin=go.AddComponent<SkinnedMeshRenderer>();skin.quality=SkinQuality.Bone4;skin.bones=source.bones;var mesh=UnityEngine.Object.Instantiate(source.sharedMesh);skin.sharedMesh=mesh;
            var delta=new Vector3[mesh.vertexCount];mesh.AddBlendShapeFrame(mesh.GetBlendShapeName(0),200,delta,delta,delta);
            var owned=go.AddComponent<ImportedSkinBounds>();owned.Initialize(skin,root.transform);Assert.That(owned.Read(Matrix4x4.identity).Known,Is.False);
            var camera=NativeObservationCamera(false);go.transform.localPosition=new Vector3(0,0,-30);Assert.That(new RoomObservationView(camera).Includes(skin),Is.True);
            mesh.ClearBlendShapes();var weights=mesh.boneWeights;weights[0].weight0=-1;mesh.boneWeights=weights;owned.Initialize(skin,root.transform);Assert.That(owned.Read(Matrix4x4.identity).Known,Is.False);
            UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(mesh);yield return null;
        }
        [UnityTest] public IEnumerator SkinBoundsContainBlendedJointInfluencesFromActualGlb()
        {
            byte[] bytes=ModelFixture.Mixamo();int length=BitConverter.ToInt32(bytes,12);
            var json=Newtonsoft.Json.Linq.JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes,20,length));
            var binary=new byte[(int)json["buffers"][0]["byteLength"]];Array.Copy(bytes,28+length,binary,0,binary.Length);
            var attributes=json["meshes"][0]["primitives"][0]["attributes"];
            int Offset(string name){var a=json["accessors"][(int)attributes[name]];return (int)json["bufferViews"][(int)a["bufferView"]]["byteOffset"]+((int?)a["byteOffset"]??0);}
            for(int i=0;i<3;i++){
                int j=Offset("JOINTS_0")+i*8,w=Offset("WEIGHTS_0")+i*16;
                Array.Copy(BitConverter.GetBytes((ushort)0),0,binary,j,2);Array.Copy(BitConverter.GetBytes((ushort)4),0,binary,j+2,2);
                Array.Copy(BitConverter.GetBytes(.25f),0,binary,w,4);Array.Copy(BitConverter.GetBytes(.75f),0,binary,w+4,4);
            }
            ImportedModel model=null;yield return SkinBoundsImport(ModelFixture.Pack(json,binary),(_,value)=>model=value);
            var skin=model.Instance.SkinnedMeshRenderers.Single();var owned=skin.GetComponent<ImportedSkinBounds>();
            for(int i=0;i<6;i++){
                model.Stop();model.Humanoid.GetBoneTransform(HumanBodyBones.Head).localPosition+=new Vector3(i*.4f,i*.1f,-i*.2f);
                SkinBoundsContainBaked(skin,owned.Read(Matrix4x4.identity),Matrix4x4.identity);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator SkinBoundsOneOffRoomCaptureRefreshesCurrentPoseBeforeLateUpdate()
        {
            ImportedModel model=null;yield return SkinBoundsImport(ModelFixture.Mixamo(),(_,value)=>model=value);
            var skin=model.Instance.SkinnedMeshRenderers.Single();NativeObservationCamera();
            model.Humanoid.GetBoneTransform(HumanBodyBones.Hips).position=new Vector3(0,0,2);
            skin.bounds=new Bounds(new Vector3(0,0,-30),Vector3.one*.01f);
            var saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision,layer=skin.gameObject.layer;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))if(renderer!=skin)renderer.forceRenderingOff=true;
            Assert.That(editor.CaptureCameraView(out var image,out var error),Is.True,error);
            SkinBoundsContainBaked(skin,new RoomSpatialExtent{Known=true,HasBounds=true,Bounds=skin.bounds},Matrix4x4.identity);
            var pixels=new Texture2D(2,2);
            try{
                Assert.That(pixels.LoadImage(Convert.FromBase64String((string)image["data"])),Is.True);
                var values=pixels.GetPixels32();var background=values[0];
                Assert.That(values.Count(c=>Math.Abs(c.r-background.r)+Math.Abs(c.g-background.g)+Math.Abs(c.b-background.b)>40),Is.GreaterThan(64),"The freshly posed skin must actually appear in the captured pixels");
            }finally{UnityEngine.Object.Destroy(pixels);}
            Assert.That(skin.gameObject.layer,Is.EqualTo(layer));
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));yield return null;
        }
        [UnityTest] public IEnumerator SkinBoundsVrmAvatarUsesTheSameOwnedGeometry()
        {
            ImportedModel model=null;yield return SkinBoundsImport(ModelFixture.Create(avatar:true),(_,value)=>model=value);
            var skin=model.Instance.SkinnedMeshRenderers.Single();model.Humanoid.GetBoneTransform(HumanBodyBones.Head).localRotation=Quaternion.Euler(30,45,20);
            SkinBoundsContainBaked(skin,skin.GetComponent<ImportedSkinBounds>().Read(Matrix4x4.identity),Matrix4x4.identity);yield return null;
        }
    }
}
