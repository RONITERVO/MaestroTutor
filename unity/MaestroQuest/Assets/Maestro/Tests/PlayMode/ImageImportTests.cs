// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [UnityTest] public IEnumerator ImagePickerCreatesExactAppearanceWithoutBindingAndUndoKeepsFile(){
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(ImageImportCapability.Feature));
            using var picker=new SoundPicker();var workshop=editor.GetComponent<ImageImportWorkshop>();workshop.SetPickerForTests(picker);string id=workshop.Select();picker.Pick(ImageFiles.Png());
            double until=Time.realtimeSinceStartupAsDouble+5;while((string)workshop.Observe()["phase"]!="preview"&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            var preview=workshop.Observe();Assert.AreEqual("preview",(string)preview["phase"],preview.ToString());Assert.IsEmpty(editor.Appearances());string hash=(string)preview["preview"]["imageHash"];Assert.IsFalse(workshop.CanAccept(id,new string('a',64),out _));
            var accept=workshop.Accept(id,hash,CancellationToken.None);while(!accept.IsCompleted)yield return null;Assert.IsFalse(accept.IsFaulted,accept.Exception?.ToString());string appearance=(string)accept.Result["appearanceId"];
            Assert.AreEqual(hash,editor.ReadAppearance(appearance).style.imageHash);Assert.IsTrue(editor.Snapshot().objects.All(o=>o.appearanceBindings.Length==0));Assert.IsFalse(workshop.CanAccept(id,hash,out _));
            Assert.IsTrue(BehaviourCatalog.TryRead("appearance.definition",2,new JObject{["id"]=appearance},new BehaviourCatalog.FactContext(editor:editor),out var value));Assert.AreEqual(hash,(string)((JObject)value.Value)["source"]["imageHash"]);
            editor.Undo();Assert.IsNull(editor.ReadAppearance(appearance));var read=editor.Images.ReadAsync(hash);while(!read.IsCompleted)yield return null;Assert.IsFalse(read.IsFaulted);Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
            var refresh=workshop.Refresh(CancellationToken.None);while(!refresh.IsCompleted)yield return null;Assert.IsFalse(refresh.IsFaulted,refresh.Exception?.ToString());Assert.AreEqual(hash,(string)refresh.Result["library"]["entries"][0]["id"]);Assert.IsTrue(CapabilityArguments.Validate(refresh.Result,new ImageImportCapability().OutputSchema,out var error),error);Assert.DoesNotThrow(()=>ProgramValue.Literal(refresh.Result));
        }
        [UnityTest] public IEnumerator ImageTextureLeasesSharePixelsReleaseAndRecoverMissingExactFile(){
            var asset=ImageLibrary.Inspect("Tiles",ImageFiles.Png());var cache=editor.ImageTextures;var first=cache.Acquire(asset.Hash);var second=cache.Acquire(asset.Hash);Assert.AreEqual(1,cache.Count);
            double until=Time.realtimeSinceStartupAsDouble+5;while(first.State=="loading"&&Time.realtimeSinceStartupAsDouble<until)yield return null;Assert.AreEqual("failed",first.State);Assert.IsTrue(cache.Error(asset.Hash).Contains("missing"));Assert.AreEqual(0,cache.ResidentBytes);
            var saving=editor.Images.SaveAsync(asset);while(!saving.IsCompleted)yield return null;Assert.IsFalse(saving.IsFaulted);cache.Retry(asset.Hash);until=Time.realtimeSinceStartupAsDouble+5;while(first.State=="loading"&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            Assert.AreEqual("ready",first.State,cache.Error(asset.Hash));Assert.AreSame(first.Texture,second.Texture);Assert.AreEqual(3,first.Texture.width);Assert.IsFalse(((Texture2D)first.Texture).isReadable);Assert.Greater(((Texture2D)first.Texture).mipmapCount,1);Assert.AreEqual(28,cache.ResidentBytes);
            first.Dispose();Assert.AreEqual(1,cache.Count);second.Dispose();Assert.AreEqual(0,cache.Count);Assert.AreEqual(0,cache.ResidentBytes);
        }
        [UnityTest] public IEnumerator ImageTextureReadWaitsForPreservationAndLastLeaseCancelsPendingRead(){
            var asset=ImageLibrary.Inspect("Tiles",ImageFiles.Png());var saving=editor.Images.SaveAsync(asset);while(!saving.IsCompleted)yield return null;Assert.IsFalse(saving.IsFaulted);
            var cache=editor.ImageTextures;AppearanceImages.Lease lease;
            using(var hold=editor.WriteGate.TryFreeze(out var error)){
                Assert.IsNotNull(hold,error);lease=cache.Acquire(asset.Hash);yield return null;yield return null;
                Assert.AreEqual("loading",lease.State);Assert.AreEqual(0,cache.ResidentBytes);
            }
            double until=Time.realtimeSinceStartupAsDouble+5;while(lease.State=="loading"&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            Assert.AreEqual("ready",lease.State,cache.Error(asset.Hash));lease.Dispose();
            Assert.IsTrue(editor.Images.TryCaptureArchive(out var capture));
            try{
                lease=cache.Acquire(asset.Hash);yield return null;yield return null;
                Assert.IsFalse(editor.WriteGate.CanFreeze(out _));lease.Dispose();
                until=Time.realtimeSinceStartupAsDouble+5;while(!editor.WriteGate.CanFreeze(out _)&&Time.realtimeSinceStartupAsDouble<until)yield return null;
                Assert.IsTrue(editor.WriteGate.CanFreeze(out _));Assert.AreEqual(0,cache.Count);Assert.AreEqual(0,cache.ResidentBytes);
            }finally{capture.Dispose();}
            yield return null;Assert.AreEqual(0,cache.ResidentBytes);
        }
        static Color32[] ImagePixels(Texture texture){var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);Texture2D copy=null;try{Graphics.Blit(texture,target);RenderTexture.active=target;copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);return copy.GetPixels32();}finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);if(copy)UnityEngine.Object.DestroyImmediate(copy);}}
        [Test] public void ImageDecodeNormalizesAllOrientationsAndPreservesAlpha(){
            var original=ImageFiles.Png();var upright=AppearanceImages.Decode(ImageLibrary.Inspect("Original",original));Color32[] reference;try{reference=ImagePixels(upright);}finally{UnityEngine.Object.DestroyImmediate(upright);}
            foreach(int orientation in Enumerable.Range(1,8)){var asset=ImageLibrary.Inspect("Oriented",ImageFiles.OrientPng(original,orientation));var texture=AppearanceImages.Decode(asset);try{int w=asset.Inspection.DisplayWidth,h=asset.Inspection.DisplayHeight;Assert.AreEqual(w,texture.width);Assert.AreEqual(h,texture.height);Assert.IsFalse(texture.isReadable);var actual=ImagePixels(texture);for(int y=0;y<2;y++)for(int x=0;x<3;x++){var d=SurfaceImage.Upright(x,y,3,2,orientation);Assert.AreEqual(reference[(1-y)*3+x],actual[(h-1-d.Y)*w+d.X],"orientation "+orientation+" pixel "+x+","+y);}}finally{UnityEngine.Object.DestroyImmediate(texture);}}
        }
        [UnityTest] public IEnumerator ImageBindingsShareTextureKeepOpacityAndReturnOriginalMaterial(){
            var asset=ImageLibrary.Inspect("Tiles",ImageFiles.Png());var save=editor.Images.SaveAsync(asset);while(!save.IsCompleted)yield return null;Assert.IsFalse(save.IsFaulted);
            var definition=new RoomAppearance{id=Guid.NewGuid().ToString("N"),name="Tiles",style=new(){patternMode="image",imageHash=asset.Hash,renderMode="blend",opacity=.4f}};
            Assert.IsTrue(editor.EditAppearance(definition,definition.id,0,Array.Empty<string>(),out var error),error);
            var objectId=editor.Snapshot().objects.First(o=>o.kind==RoomObjectKind.Block).id;var renderer=editor.Find(objectId).GetComponentInChildren<Renderer>();var original=renderer.sharedMaterial;
            var binding=new AppearanceBinding{appearanceId=definition.id};Assert.IsTrue(editor.BindAppearance(objectId,editor.ObjectRevision(objectId),binding,editor.AppearanceRevision(definition.id),false,false,out error),error);
            double until=Time.realtimeSinceStartupAsDouble+5;while(editor.AppearanceRenderState(objectId,binding)!="active"&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            Assert.AreEqual("active",editor.AppearanceRenderState(objectId,binding),editor.ImageTextures.Error(asset.Hash));Assert.AreEqual(3,renderer.sharedMaterial.mainTexture.width);Assert.AreEqual(.4f,renderer.sharedMaterial.GetFloat("_SurfaceOpacity"));Assert.AreEqual(1,editor.ImageTextures.Count);
            var first=renderer.sharedMaterial;editor.Find(objectId).GetComponent<RoomAppearanceView>().Refresh();Assert.AreSame(first,renderer.sharedMaterial);Assert.AreEqual(1,editor.ImageTextures.Count);
            Assert.IsTrue(editor.BindAppearance(objectId,editor.ObjectRevision(objectId),binding,editor.AppearanceRevision(definition.id),true,false,out error),error);yield return null;Assert.IsNull(renderer.sharedMaterial.mainTexture);Assert.AreEqual(0,editor.ImageTextures.Count);Assert.AreEqual(0,editor.ImageTextures.ResidentBytes);
        }
        [UnityTest] public IEnumerator ImageSelectionCancelledOrDisabledCannotAcceptStalePreview(){using var picker=new SoundPicker();var workshop=editor.GetComponent<ImageImportWorkshop>();workshop.SetPickerForTests(picker);string id=workshop.Select();workshop.SendMessage("OnApplicationFocus",false);picker.Pick(ImageFiles.Png());yield return new WaitForSecondsRealtime(.2f);Assert.AreEqual("selecting",(string)workshop.Observe()["phase"]);workshop.enabled=false;workshop.enabled=true;workshop.SendMessage("OnApplicationFocus",true);Assert.IsFalse(workshop.CanAccept(id,new string('a',64),out _));Assert.IsTrue(editor.WriteGate.CanFreeze(out _));Assert.IsTrue(workshop.CanStart(true,out var error),error);}
    }
}
