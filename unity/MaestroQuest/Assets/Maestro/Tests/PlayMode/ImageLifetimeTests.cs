// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
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
        static IEnumerator ImageTask(Task task){float end=Time.realtimeSinceStartup+8;while(!task.IsCompleted&&Time.realtimeSinceStartup<end)yield return null;Assert.That(task.IsCompleted,Is.True,"Image operation timed out");Assert.That(task.Exception,Is.Null);Assert.That(task.IsCanceled,Is.False);}
        static IEnumerator ImageState(AppearanceImages.Lease lease){float end=Time.realtimeSinceStartup+8;while(lease.State=="loading"&&Time.realtimeSinceStartup<end)yield return null;Assert.That(lease.State,Is.Not.EqualTo("loading"));}
        JObject SharedImageFact(string id,JObject args=null){Assert.That(BehaviourCatalog.TryRead(id,1,args,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);}
        JObject SharedImageReservation(string id){var budget=SharedImageFact("runtime.imageResources");int count=(int)budget["payloadReads"]+(int)budget["textureInstances"];return Enumerable.Range(0,count).Select(i=>SharedImageFact("runtime.imageResource",new JObject{["index"]=i})).SingleOrDefault(v=>(string)v["reservationId"]==id);}
        [UnityTest] public IEnumerator ImageLifetimeSharedCapacityPreservesOtherTexturesAndStillValidatesEachWorkspace(){
            var asset=ImageLibrary.Inspect("Shared image",ImageFiles.Png());yield return ImageTask(editor.Images.SaveAsync(asset));
            using var first=editor.ImageTextures.Acquire(asset.Hash,new RoomResourceOwner(editor.WorldIdentity,"book","appearance"));yield return ImageState(first);Assert.That(first.State,Is.EqualTo("ready"));var texture=first.Texture;
            var otherRoot=new GameObject("Other image workspace");var missingRoot=new GameObject("Missing image workspace");var held=new List<ImageResources.TextureLease>();AppearanceImages.Lease other=null,missing=null;
            try{
                var library=new ImageLibrary(Path.Combine(directory,"second-images"));yield return ImageTask(library.SaveAsync(asset));var cache=otherRoot.AddComponent<AppearanceImages>();cache.Initialize(library);
                int count=ImageResources.MaximumTextureInstances-(int)ImageResources.ObserveBudget()["textureInstances"];
                for(int i=0;i<count;i++)held.Add(ImageResources.BeginTexture(asset.Inspection,asset.Content.Length,new RoomResourceOwner(RoomWorldIdentity.Create(),null,"preview"),asset.Hash));
                Assert.That(editor.TryFlush(out var error),Is.True,error);var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
                other=cache.Acquire(asset.Hash,new RoomResourceOwner(RoomWorldIdentity.Create(),"book","appearance"));yield return ImageState(other);Assert.That(other.State,Is.EqualTo("failed"));StringAssert.Contains("shared image texture budget",cache.Error(asset.Hash));Assert.That(first.Texture,Is.SameAs(texture));Assert.That(first.State,Is.EqualTo("ready"));
                Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                foreach(var lease in held)lease.Dispose();held.Clear();cache.Retry(asset.Hash);yield return ImageState(other);Assert.That(other.State,Is.EqualTo("ready"));Assert.That(other.Texture,Is.Not.SameAs(texture),"Workspace validation and physical cache ownership stay independent");
                var unavailable=missingRoot.AddComponent<AppearanceImages>();unavailable.Initialize(new ImageLibrary(Path.Combine(directory,"absent-images")));missing=unavailable.Acquire(asset.Hash);yield return ImageState(missing);Assert.That(missing.State,Is.EqualTo("failed"));StringAssert.Contains("missing",unavailable.Error(asset.Hash));Assert.That(first.Texture,Is.SameAs(texture));
            }finally{foreach(var lease in held)lease.Dispose();other?.Dispose();missing?.Dispose();UnityEngine.Object.Destroy(otherRoot);UnityEngine.Object.Destroy(missingRoot);}yield return null;
        }
        [UnityTest] public IEnumerator ImageLifetimeTextureBytesRemainBoundedIndependentlyOfDecoderScratch(){
            yield return null;var info=new SurfaceImage.Info{Format="png",Width=2048,Height=2048};var tiny=new SurfaceImage.Info{Format="png",Width=1,Height=1};var owner=new RoomResourceOwner(editor.WorldIdentity,null,"preview");var held=new List<ImageResources.TextureLease>();string hash=new string('a',64);
            Assert.That((int)ImageResources.ObserveBudget()["textureInstances"],Is.Zero);
            try{
                // Exercise retained native capacity directly; this is not a decode
                // throughput or measured-memory benchmark.
                for(int i=0;i<6;i++){
                    var reservation=ImageResources.BeginTexture(info,24,owner,hash);held.Add(reservation);var texture=new Texture2D(2048,2048,TextureFormat.RGBA32,true,false);reservation.Track(texture);texture.Apply(true,true);reservation.Inspect(texture);reservation.Ready(texture);yield return null;
                }
                var full=ImageResources.ObserveBudget();Assert.That((long)full["textureBytes"],Is.EqualTo(6L*22369620));Assert.That((long)full["decodeScratchBytes"],Is.Zero);Assert.That((int)full["textureInstances"],Is.EqualTo(6));
                Assert.Throws<RoomResourceLimitException>(()=>ImageResources.BeginTexture(tiny,24,owner,hash));held[0].Dispose();Assert.Throws<RoomResourceLimitException>(()=>ImageResources.BeginTexture(tiny,24,owner,hash));
                yield return null;using var retry=ImageResources.BeginTexture(tiny,24,owner,hash);Assert.That((long)ImageResources.ObserveBudget()["textureBytes"],Is.LessThan((long)full["textureBytes"]));
            }finally{foreach(var lease in held)lease.Dispose();}yield return null;Assert.That((int)ImageResources.ObserveBudget()["textureInstances"],Is.Zero);
        }
        [UnityTest] public IEnumerator ImageLifetimeOrientationScratchAndReleasedOutputStayChargedUntilDestroyed(){
            root.AddComponent<RuntimeDiagnostics>();yield return null;var asset=ImageLibrary.Inspect("Oriented",ImageFiles.OrientPng(ImageFiles.Png(),6));
            using var payload=ImageResources.BeginPayload(new RoomResourceOwner(editor.WorldIdentity,"book","appearance"),SurfaceImage.MaximumBytes,asset.Hash);
            var pending=ImageResources.DecodeAsync(asset,payload);Assert.That(pending.IsCompleted,Is.True,"The first decoder of a fresh frame starts immediately");Assert.That(pending.Exception,Is.Null);var native=pending.Result;
            try{
                var ready=SharedImageReservation(native.Id);Assert.That((string)ready["phase"],Is.EqualTo("ready"));Assert.That((int)ready["textureCount"],Is.EqualTo(2));Assert.That((long)ready["textureBytes"],Is.EqualTo(56));Assert.That((long)ready["decodeScratchBytes"],Is.EqualTo(28));
                var held=SharedImageReservation(payload.Id);var output=native.Texture;var budget=SharedImageFact("runtime.imageResources");native.Dispose();var retiring=SharedImageReservation(native.Id);Assert.That(output,Is.Not.Null);Assert.That((string)retiring["phase"],Is.EqualTo("retiring"));Assert.That((long)retiring["textureBytes"],Is.EqualTo(56));
                string evidence=Environment.GetEnvironmentVariable("MAESTRO_IMAGE_LIFETIME_EVIDENCE");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(evidence,new JObject{["budget"]=budget,["payload"]=held,["ready"]=ready,["retiring"]=retiring}.ToString());
                yield return null;Assert.That(output==null,Is.True);Assert.That(SharedImageReservation(native.Id),Is.Null);Assert.That(SharedImageReservation(payload.Id),Is.Not.Null);
            }finally{native.Dispose();}
        }
        [UnityTest] public IEnumerator ImageLifetimeQueuedDecodeCancellationRetainsPayloadUntilDrainAndDoesNotAllocate(){
            yield return null;var asset=ImageLibrary.Inspect("Queued",ImageFiles.Png());var owner=new RoomResourceOwner(editor.WorldIdentity,null,"preview");using var a=ImageResources.BeginPayload(owner,SurfaceImage.MaximumBytes);using var b=ImageResources.BeginPayload(owner,SurfaceImage.MaximumBytes);using var cancel=new CancellationTokenSource();
            var first=ImageResources.DecodeAsync(asset,a);Assert.That(first.IsCompleted,Is.True);Assert.That(first.Exception,Is.Null);using var native=first.Result;int before=(int)ImageResources.ObserveBudget()["textureInstances"];
            var waiting=ImageResources.DecodeAsync(asset,b,cancel.Token);Assert.That(waiting.IsCompleted,Is.False);b.Dispose();cancel.Cancel();Assert.That((int)ImageResources.ObserveBudget()["payloadReads"],Is.EqualTo(2));
            float end=Time.realtimeSinceStartup+5;while(!waiting.IsCompleted&&Time.realtimeSinceStartup<end)yield return null;Assert.That(waiting.IsCanceled,Is.True);Assert.That((int)ImageResources.ObserveBudget()["textureInstances"],Is.EqualTo(before));Assert.That((int)ImageResources.ObserveBudget()["payloadReads"],Is.EqualTo(1));Assert.That((int)ImageResources.ObserveBudget()["queuedDecodes"],Is.Zero);
            using var c=ImageResources.BeginPayload(owner,SurfaceImage.MaximumBytes);var retry=ImageResources.DecodeAsync(asset,c);yield return ImageTask(retry);using var accepted=retry.Result;Assert.That(accepted.Texture,Is.Not.Null);
        }
        [UnityTest] public IEnumerator ImageLifetimeNativeCostMismatchRetainsActualOutputThroughRollback(){
            root.AddComponent<RuntimeDiagnostics>();yield return null;var asset=ImageLibrary.Inspect("Mismatch",ImageFiles.Png());asset.Inspection.Width=2;asset.Inspection.Height=2;
            using var payload=ImageResources.BeginPayload(new RoomResourceOwner(editor.WorldIdentity,null,"preview"),SurfaceImage.MaximumBytes);int before=(int)ImageResources.ObserveBudget()["textureInstances"];
            var failed=ImageResources.DecodeAsync(asset,payload);Assert.That(failed.IsCompleted,Is.True);Assert.That(failed.Exception?.GetBaseException(),Is.TypeOf<InvalidDataException>());Assert.That((int)ImageResources.ObserveBudget()["textureInstances"],Is.EqualTo(before+1));
            int count=(int)ImageResources.ObserveBudget()["payloadReads"]+(int)ImageResources.ObserveBudget()["textureInstances"];var retained=Enumerable.Range(0,count).Select(ImageResources.Observe).Single(r=>(string)r["kind"]=="texture"&&(string)r["imageHash"]==asset.Hash);
            Assert.That((string)retained["phase"],Is.EqualTo("retiring"));Assert.That((long)retained["textureBytes"],Is.EqualTo(28));yield return null;Assert.That((int)ImageResources.ObserveBudget()["textureInstances"],Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator ImageLifetimeCacheClosureDrainsBlockedReadWithoutPublishingLateTexture(){
            var asset=ImageLibrary.Inspect("Pending",ImageFiles.Png());yield return ImageTask(editor.Images.SaveAsync(asset));int before=(int)ImageResources.ObserveBudget()["payloadReads"];
            Assert.That(editor.Images.TryCaptureArchive(out var capture),Is.True);var cache=editor.ImageTextures;AppearanceImages.Lease lease=null;
            try{
                lease=cache.Acquire(asset.Hash);yield return null;yield return null;Assert.That((int)ImageResources.ObserveBudget()["payloadReads"],Is.EqualTo(before+1));Assert.That(lease.State,Is.EqualTo("loading"));UnityEngine.Object.Destroy(cache);yield return null;
                float end=Time.realtimeSinceStartup+5;while((int)ImageResources.ObserveBudget()["payloadReads"]!=before&&Time.realtimeSinceStartup<end)yield return null;
                Assert.That((int)ImageResources.ObserveBudget()["payloadReads"],Is.EqualTo(before));Assert.That(lease.State,Is.EqualTo("released"));Assert.That(lease.Texture,Is.Null);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
            }finally{lease?.Dispose();capture.Dispose();}yield return null;
        }
        [UnityTest] public IEnumerator ImageLifetimePreviewAdmissionFailureDoesNotHoldWorkspaceAndCanRetry(){
            var held=new List<ImageResources.PayloadLease>();using var picker=new SoundPicker();var workshop=editor.GetComponent<ImageImportWorkshop>();workshop.SetPickerForTests(picker);string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            try{
                for(int i=0;i<4;i++)held.Add(ImageResources.BeginPayload(new RoomResourceOwner(RoomWorldIdentity.Create(),null,"preview"),SurfaceImage.MaximumBytes));
                Assert.Throws<RoomResourceLimitException>(()=>workshop.Select());Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);Assert.That((string)workshop.Observe()["phase"],Is.EqualTo("idle"));Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
                held[0].Dispose();held[1].Dispose();string id=workshop.Select();picker.Pick(ImageFiles.Png());float end=Time.realtimeSinceStartup+5;while((string)workshop.Observe()["phase"]!="preview"&&Time.realtimeSinceStartup<end)yield return null;Assert.That((string)workshop.Observe()["phase"],Is.EqualTo("preview"),workshop.Observe().ToString());workshop.Cancel(id);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
            }finally{foreach(var lease in held)lease.Dispose();}yield return null;
        }
        [UnityTest] public IEnumerator ImageLifetimeCompletedPreviewRetainsMetadataWithoutRetainingEncodedPayload(){
            using var picker=new SoundPicker();var workshop=editor.GetComponent<ImageImportWorkshop>();workshop.SetPickerForTests(picker);int before=(int)ImageResources.ObserveBudget()["payloadReads"];string id=workshop.Select();picker.Pick(ImageFiles.Png());
            float end=Time.realtimeSinceStartup+5;while((string)workshop.Observe()["phase"]!="preview"&&Time.realtimeSinceStartup<end)yield return null;Assert.That((string)workshop.Observe()["phase"],Is.EqualTo("preview"));string hash=(string)workshop.Observe()["preview"]["imageHash"];Assert.That((int)ImageResources.ObserveBudget()["payloadReads"],Is.EqualTo(before+1));
            yield return ImageTask(workshop.Accept(id,hash,CancellationToken.None));Assert.That((int)ImageResources.ObserveBudget()["payloadReads"],Is.EqualTo(before));Assert.That((string)workshop.Observe()["preview"]["imageHash"],Is.EqualTo(hash));Assert.That((string)workshop.Receipt()["imageHash"],Is.EqualTo(hash));
            var saved=editor.Images.ReadAsync(hash);yield return ImageTask(saved);Assert.That(saved.Result.Hash,Is.EqualTo(hash));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);yield return null;
        }
    }
}
