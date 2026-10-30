// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class RoomAuthoredBoundsNativeTests
    {
        GameObject root,managerRoot;HeightFieldGeometry terrain;int priorManagers;
        [UnitySetUp] public IEnumerator Setup()
        {
            priorManagers=UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            managerRoot=new GameObject("Owned bounds-test interaction manager");managerRoot.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            terrain?.Dispose();if(root)UnityEngine.Object.Destroy(root);yield return null;
            if(managerRoot)UnityEngine.Object.Destroy(managerRoot);yield return null;
            Assert.That(UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,Is.EqualTo(priorManagers),"Geometry verification must not leave an automatic XR manager in the next test scene");
        }
        void ContainsNative(RoomSpatialExtent envelope)
        {
            Assert.That(envelope.Known&&envelope.HasBounds,Is.True);var bounds=envelope.Bounds;bounds.Expand(.00001f);
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))for(int i=0;i<8;i++){
                var b=renderer.localBounds;var point=root.transform.InverseTransformPoint(renderer.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                Assert.That(bounds.Contains(point),Is.True,$"{renderer.name}: {point} outside {bounds}");
            }
        }
        static RoomObjectData Data(RoomObjectKind kind)=>new(){id=new string('a',32),name="Canonical envelope fixture",kind=kind};
        [UnityTest] public IEnumerator AuthoredBoundsContainNativePrimitiveAndDegenerateDrawingSelection()
        {
            foreach(var kind in new[]{RoomObjectKind.Block,RoomObjectKind.Ball,RoomObjectKind.Cylinder,RoomObjectKind.Drawing}){
                var data=Data(kind);if(kind==RoomObjectKind.Drawing)data.points=new[]{Vector3.one*2,Vector3.one*2};
                root=new GameObject("Envelope fixture");root.AddComponent<CreatedRoomObject>().Build(data);ContainsNative(RoomAuthoredBounds.Derive(data).Visual);UnityEngine.Object.Destroy(root);yield return null;
            }
        }
        [UnityTest] public IEnumerator AuthoredBoundsMatchNativeNormalizationOfAdmittedNearlyUnitRotations()
        {
            var data=Data(RoomObjectKind.Assembly);var rotation=Quaternion.Euler(65,135,20);rotation=new(rotation.x*.996f,rotation.y*.996f,rotation.z*.996f,rotation.w*.996f);Assert.That(MotionFrame.ValidRotation(rotation),Is.True);
            data.recipe=new(){parts=new[]{new RecipePart{id="root",size=new(1,.1f,.1f),rotation=rotation}}};
            data.surfaces=new[]{new DrawingSurface{id="ink",part="root",position=Vector3.right*1.3f,rotation=rotation,strokes=new[]{new SurfaceStroke{id="line",points=new[]{Vector3.left*.1f,Vector3.right*.1f}}}}};
            root=new GameObject("Nearly unit rotation fixture");root.AddComponent<CreatedRoomObject>().Build(data);root.AddComponent<DrawingSurfaceView>().Apply(data.surfaces);
            ContainsNative(RoomAuthoredBounds.Derive(data).Visual);yield return null;
        }
        [UnityTest] public IEnumerator AuthoredBoundsContainNativeRecipeRotationsCurvedInkTerrainLiquidAndCarriedMaterial()
        {
            var data=Data(RoomObjectKind.Assembly);data.recipe=new(){parts=new[]{new RecipePart{id="arm",position=new(.4f,.2f,0),size=new(.1f,.3f,.1f)},new RecipePart{id="hand",parent="arm",position=Vector3.up*.4f,size=Vector3.one*.1f}},tracks=new[]{new RecipeTrack{part="arm",keys=new[]{new RecipeKey{time=0},new RecipeKey{time=2,rotation=Quaternion.Euler(0,0,150)}}}}};
            data.surfaces=new[]{new DrawingSurface{id="ink",part="hand",shape="sphere",curvatureRadius=.2f,width=.4f,height=.3f,position=new(.2f,0,0),rotation=Quaternion.Euler(0,60,0),strokes=new[]{new SurfaceStroke{id="one",points=new[]{new Vector3(-.15f,-.1f,0),new Vector3(.15f,.1f,0)}}}}};
            data.heightFields=new[]{new RoomHeightField{cells=4,heights=Enumerable.Range(0,25).Select(i=>i*.01f).ToArray(),frame=new(){position=new(0,-.5f,0),rotation=Quaternion.Euler(0,25,0)}}};
            data.containers=new[]{new RoomContainer{version=2,rectangle=new(){width=.4f,depth=.2f},height=.3f,amountMl=200,frame=new(){position=new(-.7f,0,0),rotation=Quaternion.Euler(30,20,10)}}};
            data.sculptTips=new[]{new SculptTip{version=2,mode="scoop",part="hand",position=new(0,0,.3f),radius=.12f,height=0,amountLitres=.1}};data.materialStores=new[]{new RoomMaterialStore{amountLitres=.7}};
            root=new GameObject("Decorated envelope fixture");root.AddComponent<CreatedRoomObject>().Build(data);root.AddComponent<DrawingSurfaceView>().Apply(data.surfaces);root.AddComponent<ContainerFillView>().Apply(data.containers);root.AddComponent<MaterialToolContentsView>().Apply(data.sculptTips[0],data.materialStores[0]);
            terrain=new HeightFieldGeometry(data.heightFields[0],new RoomResourceOwner(null,data.id,"collision"));terrain.Attach(root.transform,Color.white);
            var envelope=RoomAuthoredBounds.Derive(data);Assert.That(envelope.Collision.Known,Is.False);var recipe=root.GetComponent<RecipeObject>();
            foreach(float angle in new[]{0f,35f,100f,170f,250f,359f}){recipe.Part("arm").localRotation=Quaternion.Euler(angle*.5f,angle,angle);ContainsNative(envelope.Visual);yield return null;}
        }
    }
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator AuthoredBoundsReplaceStaleDormantGeometryAndDriveSharedObservationWithoutLoading()
        {
            string id=NewStroke(),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var changed=editor.Read(id);changed.radius*=2;changed.position=new Vector3(0,0,-3);Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            Assert.That((bool)editor.ObserveAuthoredBounds(id)["prepared"],Is.False);var camera=NativeObservationCamera();Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);
            for(int i=0;i<40&&!(bool)editor.ObserveAuthoredBounds(id)["prepared"];i++)yield return null;
            var fact=editor.ObserveSpatialBounds(id);Assert.That((string)fact["source"],Is.EqualTo("authored"));Assert.That((bool)fact["visual"]["known"],Is.True);Assert.That((bool)fact["collision"]["known"],Is.False);
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);camera.transform.localRotation=Quaternion.Euler(0,180,0);Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);
            Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            var bounds=editor.ReadSpatialBounds(id,out _);yield return NativeAreaCompletion(editor.ActivateNativeArea(area));SpatialContainsRenderers(id,bounds.Visual);
        }
        [UnityTest] public IEnumerator AuthoredBoundsDisableCancelsPreparationAndEnableRebuildsWithoutChangingWorld()
        {
            string id=editor.Identity(block);for(int i=0;i<40&&!(bool)editor.ObserveAuthoredBounds(id)["prepared"];i++)yield return null;
            Assert.That((bool)editor.ObserveAuthoredBounds(id)["prepared"],Is.True);string saved=JsonUtility.ToJson(editor.Snapshot());editor.enabled=false;
            Assert.That((bool)editor.ObserveAuthoredBounds(id)["prepared"],Is.False);editor.enabled=true;
            for(int i=0;i<40&&!(bool)editor.ObserveAuthoredBounds(id)["prepared"];i++)yield return null;
            Assert.That((bool)editor.ObserveAuthoredBounds(id)["prepared"],Is.True);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
    }
}
