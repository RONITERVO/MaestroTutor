// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        (string lamp,string caster,string distant) LightObservationScene(string shadows="hard"){
            var values=editor.Snapshot().objects.Where(x=>!x.IsBuiltIn).Take(3).ToArray();Assert.That(values.Length,Is.EqualTo(3));
            var positions=new[]{new Vector3(6,0,4),new Vector3(4,0,4),new Vector3(-18,0,-10)};
            for(int i=0;i<3;i++){values[i].position=positions[i];values[i].scale=1;}
            Assert.That(editor.ApplyAgentEdit(editor.Revision,values,Array.Empty<string>(),out var error),Is.True,error);
            foreach(var data in values)NativeAreaFor(data.id);
            string lamp=values[0].id;Assert.That(editor.EditLight(lamp,editor.ObjectRevision(lamp),LampArguments(lamp,new RoomLight{range=6,shadows=shadows}),out error),Is.True,error);
            return(lamp,values[1].id,values[2].id);
        }
        Camera LightObservationCamera(){var camera=NativeObservationCamera();camera.orthographic=true;camera.orthographicSize=1;return camera;}
        void RetireLightInput(string id){string area=editor.Snapshot().regions.Single(x=>x.members.Contains(id)).id;Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);}
        [UnityTest]public IEnumerator LightObservationRetainsOffscreenLampAndCasterWithoutChangingOccupiedBoundsOrSavedState(){
            var(lamp,caster,distant)=LightObservationScene();var spatial=editor.ObserveAuthoredBounds(lamp);var camera=LightObservationCamera();
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;int count=root.GetComponentsInChildren<Light>().Length;
            Assert.That(RetainedFor(lamp,RoomRetentionReason.Observation),Is.False);Assert.That(RetainedFor(caster,RoomRetentionReason.Observation),Is.False);
            for(int i=0;i<3;i++){Assert.That(RetainedFor(lamp,RoomRetentionReason.Lighting),Is.True);Assert.That(RetainedFor(caster,RoomRetentionReason.Lighting),Is.True);Assert.That(RetainedFor(distant,RoomRetentionReason.Lighting),Is.False);}
            string area=editor.Snapshot().regions.Single(x=>x.members.Contains(lamp)).id;
            Assert.That(editor.ObserveRegionRetention(area)["reasons"].Values<string>(),Does.Contain("lighting"));Assert.That((bool)editor.ObserveRegionRetention(area)["unloadingSupported"],Is.False);
            Assert.That(editor.RetireNativeArea(area,out _),Is.False);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            Assert.That(JToken.DeepEquals(spatial,editor.ObserveAuthoredBounds(lamp)),Is.True);Assert.That(root.GetComponentsInChildren<Light>().Length,Is.EqualTo(count));
            camera.cullingMask=1<<RoomPhysicsLayers.Scanned;Assert.That(RetainedFor(lamp,RoomRetentionReason.Lighting),Is.False);camera.cullingMask=-1;
            root.transform.SetPositionAndRotation(new Vector3(20,4,-10),Quaternion.Euler(0,65,0));Assert.That(RetainedFor(lamp,RoomRetentionReason.Lighting),Is.True);yield return null;
        }
        [UnityTest]public IEnumerator LightObservationHonoursEffectiveControlsShadowSettingsAndCasterVisibility(){
            var(lamp,caster,_)=LightObservationScene();LightObservationCamera();
            var args=LampControl(lamp);Assert.That(editor.ControlLight(lamp,"Lamp",(string)args["stateId"],args,out var error),Is.True,error);Assert.That(RetainedFor(lamp,RoomRetentionReason.Lighting),Is.False);Assert.That(RetainedFor(caster,RoomRetentionReason.Lighting),Is.False);
            args=LampControl(lamp,enabled:true);args["intensity"]=0;Assert.That(editor.ControlLight(lamp,"Lamp",(string)args["stateId"],args,out error),Is.True,error);Assert.That(RetainedFor(lamp,RoomRetentionReason.Lighting),Is.False);
            args=LampControl(lamp,enabled:true);Assert.That(editor.ControlLight(lamp,"Lamp",(string)args["stateId"],args,out error),Is.True,error);Assert.That(RetainedFor(caster,RoomRetentionReason.Lighting),Is.True);
            foreach(var renderer in editor.Find(caster).GetComponentsInChildren<Renderer>())renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            Assert.That(RetainedFor(caster,RoomRetentionReason.Lighting),Is.False);
            Assert.That(editor.EditLight(lamp,editor.ObjectRevision(lamp),LampArguments(lamp,new RoomLight{range=6}),out error),Is.True,error);
            Assert.That(RetainedFor(lamp,RoomRetentionReason.Lighting),Is.True);Assert.That(RetainedFor(caster,RoomRetentionReason.Lighting),Is.False);yield return null;
        }
        [UnityTest]public IEnumerator LightObservationCaptureLeaseAndViewerGraceHaveTheSameIndirectDependencies(){
            var(lamp,caster,_)=LightObservationScene();var capture=NativeObservationCamera(false);capture.orthographic=true;capture.orthographicSize=1;
            Assert.That(RetainedFor(lamp,RoomRetentionReason.Lighting),Is.False);
            using(editor.ObserveNativeCamera(capture)){Assert.That(RetainedFor(lamp,RoomRetentionReason.Lighting),Is.True);Assert.That(RetainedFor(caster,RoomRetentionReason.Lighting),Is.True);}
            Assert.That(RetainedFor(lamp,RoomRetentionReason.Lighting),Is.False);Assert.That(RetainedFor(lamp,RoomRetentionReason.RecentObservation),Is.False);
            var viewer=LightObservationCamera();SampleViewerDemand();viewer.transform.rotation=Quaternion.Euler(0,180,0);SampleViewerDemand();
            // Moving entirely away, rather than merely turning within the light's
            // envelope, makes current illumination demand disappear.
            viewer.transform.position=new Vector3(-15,0,-15);SampleViewerDemand();
            Assert.That(RetainedFor(lamp,RoomRetentionReason.Lighting),Is.False);Assert.That(RetainedFor(lamp,RoomRetentionReason.RecentObservation),Is.True);
            viewer.enabled=false;Assert.That(RetainedFor(lamp,RoomRetentionReason.RecentObservation),Is.False);yield return null;
        }
        [UnityTest]public IEnumerator LightObservationDisplayLoadsOffscreenInputsAndLeavesUnrelatedAreaDormant(){
            var(lamp,caster,distant)=LightObservationScene();RetireLightInput(lamp);RetireLightInput(caster);RetireLightInput(distant);yield return null;
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;LightObservationCamera();
            yield return DisplayAppears(lamp);yield return DisplayAppears(caster);yield return DisplayFrames();Assert.That(editor.Find(distant),Is.Null);
            Assert.That(editor.Find(lamp).GetComponent<RoomLightView>().Active("Lamp"),Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest]public IEnumerator LightObservationPreparedCaptureLoadsOffscreenInputsAndSynchronousCaptureCannotSkipThem(){
            var(lamp,caster,distant)=LightObservationScene();RetireLightInput(lamp);RetireLightInput(caster);RetireLightInput(distant);yield return null;PreparedCaptureViewer();
            string saved=JsonUtility.ToJson(editor.Snapshot());Assert.That(editor.CaptureCameraView(out _,out var error),Is.False);Assert.That(error,Does.Contain("loading"));yield return new WaitForSecondsRealtime(1.05f);
            using var capture=editor.BeginViewCapture(out error);Assert.That(capture,Is.Not.Null,error);yield return PreparedCaptureDone(capture);Assert.That(capture.Read(out var image,out error),Is.True,error);
            Assert.That(image["data"].Value<string>().Length,Is.GreaterThan(0));Assert.That(editor.Find(lamp).GetComponent<RoomLightView>().Active("Lamp"),Is.True);Assert.That(editor.Find(caster),Is.Not.Null);Assert.That(editor.Find(distant),Is.Null);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(editor.ViewCaptureMetadata,Is.Null);
        }
        [UnityTest]public IEnumerator LightObservationMissingOffscreenShadowModelFailsWithoutReplacingPreviousCapture(){
            LightObservationScene();yield return GeometryModel(StaticModelFixture.Create());string id=geometryTarget;NativeAreaFor(id);editor.Find(id).transform.position=new Vector3(4,0,4);PreparedCaptureViewer();
            Assert.That(editor.CaptureView(out var previous,out var error),Is.True,error);RetireLightInput(id);yield return null;
            string path=Path.Combine(directory,"models",editor.Read(id).modelHash+".glb"),parked=path+".light-missing";File.Move(path,parked);
            try{
                yield return new WaitForSecondsRealtime(1.05f);using var capture=editor.BeginViewCapture(out error);Assert.That(capture,Is.Not.Null,error);yield return PreparedCaptureDone(capture);
                Assert.That(capture.Read(out var image,out error),Is.False);Assert.That(image,Is.Null);Assert.That(error,Is.Not.Empty);Assert.That(JToken.DeepEquals(previous,editor.ViewCaptureMetadata),Is.True);
            }finally{File.Move(parked,path);}
        }
        [UnityTest]public IEnumerator LightObservationUnknownOffscreenImportWaitsForRealGeometryThenCaptureCompletes(){
            LightObservationScene();var asset=Maestro.Quest.Imports.ModelLibrary.Inspect("shadow.glb",StaticModelFixture.Create());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var barrier=new ModelLoadBarrier();var blocker=ResidencyModel();var blocked=blocker.LoadAsync(asset,barrier);VirtualRoomCapture capture=null;
            try{
                yield return new WaitUntil(()=>barrier.Entered||blocked.IsCompleted);Assert.That(barrier.Entered,Is.True);Assert.That(editor.CreateImportedModel(asset.Hash,out var id,out var error),Is.True,error);
                editor.Find(id).transform.position=new Vector3(4,0,4);PreparedCaptureViewer();capture=editor.BeginViewCapture(out error);Assert.That(capture,Is.Not.Null,error);for(int i=0;i<5;i++)yield return null;
                Assert.That(capture.Done,Is.False,"Off-screen loading geometry may cast a visible shadow");barrier.Open();blocker.Dispose();yield return new WaitUntil(()=>blocked.IsCompleted);
                yield return PreparedCaptureDone(capture);Assert.That(capture.Read(out _,out error),Is.True,error);Assert.That(editor.Find(id).GetComponent<CreatedRoomObject>().ModelGeometryReady,Is.True);
            }finally{capture?.Dispose();barrier.Open();blocker.Dispose();}
        }
        [UnityTest]public IEnumerator LightObservationRecipeAttachmentCoversRotatedAncestorAndUpdatesReachWithoutResettingControl(){
            var recipe=new RoomRecipe{parts=new[]{new RecipePart{id="arm",position=new Vector3(2,0,0)},new RecipePart{id="tip",parent="arm",position=new Vector3(.5f,0,0)}}};
            Assert.That(editor.CreateRecipe("Sweeping lamp",new Vector3(6,0,3),1,recipe,out var id,out var error),Is.True,error);NativeAreaFor(id);
            var source=new RoomLight{part="tip",position=new Vector3(4,0,0),range=.2f};Assert.That(editor.EditLight(id,editor.ObjectRevision(id),LampArguments(id,source),out error),Is.True,error);
            LightObservationCamera();var item=editor.Find(id);item.GetComponent<RecipeObject>().Part("arm").localRotation=Quaternion.Euler(0,180,0);item.GetComponent<RoomLightView>().Refresh();
            Assert.That(RetainedFor(id,RoomRetentionReason.Lighting),Is.True,"The rotating offset can illuminate the observed region even while its root stays outside");
            var args=LampControl(id,enabled:true);Assert.That(editor.ControlLight(id,"Lamp",(string)args["stateId"],args,out error),Is.True,error);string state=(string)editor.ObserveLight(id,"Lamp")["stateId"];
            var changed=editor.Read(id);changed.recipe.parts[0].position=new Vector3(.2f,0,0);changed.recipe.parts[1].position=new Vector3(.2f,0,0);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            Assert.That((string)editor.ObserveLight(id,"Lamp")["stateId"],Is.EqualTo(state));Assert.That(RetainedFor(id,RoomRetentionReason.Lighting),Is.False,"Reduced ancestor reach must replace the previous envelope");yield return null;
        }
        [UnityTest]public IEnumerator LightObservationUnavailableNativeLampFailsCapture(){
            var(lamp,_,_)=LightObservationScene();PreparedCaptureViewer();UnityEngine.Object.DestroyImmediate(editor.Find(lamp).GetComponent<RoomLightView>());
            Assert.That(editor.CaptureCameraView(out var image,out var error),Is.False);Assert.That(image,Is.Null);Assert.That(error,Does.Contain("lamp"));Assert.That(editor.ViewCaptureMetadata,Is.Null);yield return null;
        }
    }
    public sealed partial class AvatarSpatialTests
    {
        void ScanObservationLamp(string id){
            var settings=JObject.Parse(JsonUtility.ToJson(new RoomLight{range=1,shadows="hard"}));settings.Remove("id");
            var args=new JObject{["operation"]="save",["target"]=id,["revision"]=editor.ObjectRevision(id),["light"]="Lamp",["settings"]=settings};
            Assert.That(editor.EditLight(id,editor.ObjectRevision(id),args,out var error),Is.True,error);
        }
        [UnityTest]public IEnumerator LightObservationScanLampUsesExactAnchorAfterRetirementAndWorldRelocation(){
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{InkWall()},WorldCoordinates=true};yield return LoadedLayout(scan,platform,source);
            string id=MakeLayer(out _);ScanObservationLamp(id);string area=ScanBoundsArea(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            root.transform.SetPositionAndRotation(new Vector3(12,0,-8),Quaternion.Euler(0,70,0));source.Entries[0].Position+=Vector3.right*4;yield return null;
            var camera=ScanBoundsCamera(new Vector3(0,1.3f,3),Vector3.back);camera.orthographic=true;camera.orthographicSize=.3f;
            using(editor.ObserveNativeCamera(camera)){
                Assert.That((editor.ReadRetention().Reasons(id)&RoomRetentionReason.Lighting)==0,Is.True,"The old plane cannot retain its moved lamp");
                camera.transform.position=new Vector3(4,1.3f,3);Assert.That((editor.ReadRetention().Reasons(id)&RoomRetentionReason.Lighting)!=0,Is.True);
                Assert.That(editor.Find(id),Is.Null,"Observation alone never creates the lamp");
            }
        }
        [UnityTest]public IEnumerator LightObservationHiddenScanLampAndCasterDoNotBlockPreparedCapture(){
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{InkWall()}};yield return LoadedLayout(scan,platform,source);
            string id=MakeLayer(out _);InkAdd(id);ScanObservationLamp(id);string area=ScanBoundsArea(id);yield return ScanBoundsPrepared(id);
            // Establish dormant input without the fixture's primary viewer
            // correctly retaining it through predictive/grace demand.
            var camera=viewer.GetComponent<Camera>();bool enabled=camera&&camera.enabled;if(camera)camera.enabled=false;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            // A separate lamp also must not demand this intentionally hidden
            // layer as unknown shadow geometry.
            var other=editor.Snapshot().objects.First(x=>!x.IsBuiltIn&&x.id!=id);var settings=JObject.Parse(JsonUtility.ToJson(new RoomLight{kind="spot",range=8,shadows="none"}));settings.Remove("id");
            // The hidden point already reserves six faces; first remove its shadows.
            var data=editor.Read(id);data.lights[0].shadows="none";Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            settings["shadows"]="hard";Assert.That(editor.EditLight(other.id,editor.ObjectRevision(other.id),new JObject{["operation"]="save",["target"]=other.id,["revision"]=editor.ObjectRevision(other.id),["light"]="Lamp",["settings"]=settings},out error),Is.True,error);
            yield return ScanBoundsPrepared(id);source.Entries=Array.Empty<ScannedSurface>();yield return null;if(camera)camera.enabled=enabled;
            using var capture=editor.BeginViewCapture(out error);Assert.That(capture,Is.Not.Null,error);yield return Until(()=>capture.Done);Assert.That(capture.Read(out _,out error),Is.True,error);Assert.That(editor.Find(id),Is.Null);
        }
    }
}
