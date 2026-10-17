// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomSpatialIndexTests
    {
        static RoomObjectData Item(char id='a',RoomObjectKind kind=RoomObjectKind.Block)=>new(){id=new string(id,32),kind=kind,name="Fixture"};
        static RoomDocument Document(params RoomObjectData[] data)=>new(){objects=data};
        static string Source(RoomObjectData data)=>RoomEditor.SpatialSource(data.Copy());
        static void Drain(RoomSpatialIndex index){for(int i=0;i<100&&index.Pending;i++)index.Tick();Assert.That(index.Pending,Is.False);}
        [Test] public void SpatialIndexReadDoesNotPrepareAndDetachedSourceCannotBeMutated()
        {
            int reads=0;using var index=new RoomSpatialIndex((h,c)=>{reads++;return Task.FromResult(default(ModelAssetBounds));});
            var data=Item();string source=Source(data);index.Synchronize(Document(data));data.radius=.05f;
            for(int i=0;i<10;i++)Assert.That(index.TryRead(data.id,source,out _),Is.False);
            Assert.That(reads,Is.Zero);Drain(index);Assert.That(index.TryRead(data.id,source,out var bounds),Is.True);Assert.That(bounds.Visual.Known,Is.True);Assert.That(bounds.Collision.Known,Is.False);
        }
        [Test] public void SpatialIndexGeometryEditsInvalidateWhileRootPlacementReusesValues()
        {
            using var index=new RoomSpatialIndex((h,c)=>throw new Exception("No asset needed"));var data=Item();index.Synchronize(Document(data));Drain(index);
            data.position=Vector3.one*9;data.scale=2;data.rotation=Quaternion.Euler(20,50,0);data.name="Renamed";
            Assert.That(index.TryRead(data.id,Source(data),out _),Is.True);index.Synchronize(Document(data));Assert.That(index.Pending,Is.False);
            data.kind=RoomObjectKind.Drawing;data.points=new[]{Vector3.zero,Vector3.up*3};index.Synchronize(Document(data));Assert.That(index.TryRead(data.id,Source(data),out _),Is.False);
            Drain(index);Assert.That(index.TryRead(data.id,Source(data),out var bounds),Is.True);Assert.That(bounds.Visual.Bounds.max.y,Is.GreaterThan(3));
        }
        [Test] public void SpatialIndexBoundsWorkPerTickAndRemovesDeletedEntries()
        {
            using var index=new RoomSpatialIndex((h,c)=>throw new Exception());var data=Enumerable.Range(0,20).Select(i=>new RoomObjectData{id=i.ToString("x32"),kind=RoomObjectKind.Block}).ToArray();index.Synchronize(Document(data));index.Tick();
            int ready=data.Count(d=>index.TryRead(d.id,Source(d),out _));Assert.That(ready,Is.InRange(1,RoomPreparationBudget.MaximumStepsPerFrame));Drain(index);
            index.Synchronize(Document(data[0]));Assert.That(index.Count,Is.EqualTo(1));Assert.That(index.TryRead(data[1].id,Source(data[1]),out _),Is.False);
        }
        [Test] public void SpatialIndexSharesHashInspectionAndDoesNotBlockPrimitivePreparation()
        {
            var completion=new TaskCompletionSource<ModelAssetBounds>();int calls=0;using var index=new RoomSpatialIndex((h,c)=>{calls++;return completion.Task;});
            var a=Item('a',RoomObjectKind.ImportedModel);a.modelHash=new string('1',64);var b=a.Copy();b.id=new string('b',32);var plain=Item('c');index.Synchronize(Document(a,b,plain));
            for(int i=0;i<10;i++)index.Tick();Assert.That(calls,Is.EqualTo(1));Assert.That(index.TryRead(plain.id,Source(plain),out _),Is.True);
            completion.SetResult(ModelInspection.Inspect(StaticModelFixture.Create()).AssetBounds);Drain(index);
            Assert.That(calls,Is.EqualTo(1));Assert.That(index.TryRead(a.id,Source(a),out var value)&&value.Visual.Known,Is.True);Assert.That(index.TryRead(b.id,Source(b),out _),Is.True);
        }
        [Test] public void SpatialIndexDisposalCancelsOwnedInspectionAndLateResultsCannotPublish()
        {
            var completion=new TaskCompletionSource<ModelAssetBounds>();CancellationToken captured=default;var index=new RoomSpatialIndex((h,c)=>{captured=c;return completion.Task;});
            var data=Item('a',RoomObjectKind.ImportedModel);data.modelHash=new string('1',64);index.Synchronize(Document(data));index.Tick();index.Dispose();
            Assert.That(captured.IsCancellationRequested,Is.True);completion.SetResult(ModelInspection.Inspect(StaticModelFixture.Create()).AssetBounds);index.Tick();Assert.That(index.TryRead(data.id,Source(data),out _),Is.False);Assert.That(index.Count,Is.Zero);
            Assert.Throws<ObjectDisposedException>(()=>index.Synchronize(Document(data)));
        }
        [Test] public void SpatialIndexLateInspectionUsesCurrentGeometryAndCannotResurrectRemovedObject()
        {
            var completion=new TaskCompletionSource<ModelAssetBounds>();using var index=new RoomSpatialIndex((h,c)=>completion.Task);var a=Item('a',RoomObjectKind.ImportedModel);a.modelHash=new string('1',64);
            index.Synchronize(Document(a));index.Tick();var b=a.Copy();b.id=new string('b',32);b.modelGeometry=new(){scaleMode="source",pivot="base",metresPerUnit=1};index.Synchronize(Document(b));
            completion.SetResult(ModelInspection.Inspect(StaticModelFixture.Create()).AssetBounds);Drain(index);
            Assert.That(index.TryRead(a.id,Source(a),out _),Is.False);Assert.That(index.TryRead(b.id,Source(b),out var bounds),Is.True);Assert.That(bounds.Visual.Bounds.size.y,Is.GreaterThan(3));
        }
        [Test] public void SpatialIndexFailedInspectionIsUnknownAndAReimportCanRepairIt()
        {
            ModelAssetBounds? cached=null;using var index=new RoomSpatialIndex((h,c)=>Task.FromException<ModelAssetBounds>(new IOException("private path")),h=>cached);
            var data=Item('a',RoomObjectKind.ImportedModel);data.modelHash=new string('1',64);index.Synchronize(Document(data));Drain(index);Assert.That(index.TryRead(data.id,Source(data),out var before),Is.True);Assert.That(before.Visual.Known,Is.False);
            cached=ModelInspection.Inspect(StaticModelFixture.Create()).AssetBounds;index.Synchronize(Document(data));Drain(index);Assert.That(index.TryRead(data.id,Source(data),out var after)&&after.Visual.Known,Is.True);
        }
        [Test] public void SpatialIndexPreparationReadsColdModelWithoutNativeObjectsOrWrites()
        {
            string directory=Path.Combine(Path.GetTempPath(),"maestro-spatial-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try{
                var asset=ModelLibrary.Inspect("Static.glb",StaticModelFixture.Create());File.WriteAllBytes(Path.Combine(directory,asset.Hash+".glb"),asset.Bytes);var library=new ModelLibrary(directory);
                var result=library.PrepareBoundsAsync(asset.Hash,CancellationToken.None).GetAwaiter().GetResult();Assert.That(result.Known,Is.True);Assert.That(Directory.GetFiles(directory).Length,Is.EqualTo(1));Assert.That(library.TryReadBounds(asset.Hash,out _),Is.True);
                var cancelled=new CancellationToken(true);Assert.Throws<OperationCanceledException>(()=>library.PrepareBoundsAsync(asset.Hash,cancelled).GetAwaiter().GetResult());
            }finally{Directory.Delete(directory,true);}
        }
        [Test] public void SpatialIndexIncludedContentInspectionNeverMaterializesPrivateLibraryFiles()
        {
            string directory=Path.Combine(Path.GetTempPath(),"maestro-spatial-bundle-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try{
                var bundled=BundledAvatarFixture.Write(directory,StaticModelFixture.Create());string privateLibrary=Path.Combine(directory,"private");
                var library=new ModelLibrary(privateLibrary,includedAvatar:bundled);var result=library.PrepareBoundsAsync(bundled.Hash,CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(result.Known,Is.True);Assert.That(Directory.Exists(privateLibrary),Is.False);Assert.That(library.TryReadBounds(bundled.Hash,out _),Is.True);
            }finally{Directory.Delete(directory,true);}
        }
        [Test] public void SpatialIndexColdInspectionRejectsContentHashMismatchWithoutPublishing()
        {
            string directory=Path.Combine(Path.GetTempPath(),"maestro-spatial-damage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try{
                string hash=new string('1',64);File.WriteAllBytes(Path.Combine(directory,hash+".glb"),StaticModelFixture.Create());var library=new ModelLibrary(directory);
                Assert.Throws<ModelImportException>(()=>library.PrepareBoundsAsync(hash,CancellationToken.None).GetAwaiter().GetResult());Assert.That(library.TryReadBounds(hash,out _),Is.False);Assert.That(Directory.GetFiles(directory).Length,Is.EqualTo(1));
            }finally{Directory.Delete(directory,true);}
        }
        [Test] public void SpatialIndexCancelledQueuedInspectionNeverReadsOrPublishesAndReleasesItsWaiter()
        {
            string directory=Path.Combine(Path.GetTempPath(),"maestro-spatial-queue-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try{
                var asset=ModelLibrary.Inspect("Static.glb",StaticModelFixture.Create());File.WriteAllBytes(Path.Combine(directory,asset.Hash+".glb"),asset.Bytes);var library=new ModelLibrary(directory);
                // Occupy the real library admission slot to exercise cancellation
                // deterministically, independently of disk/validation speed.
                var slot=(SemaphoreSlim)typeof(ModelLibrary).GetField("boundsPreparation",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(library);
                slot.Wait();using var cancellation=new CancellationTokenSource();var waiting=library.PrepareBoundsAsync(asset.Hash,cancellation.Token);Assert.That(waiting.IsCompleted,Is.False);cancellation.Cancel();
                Assert.Catch<OperationCanceledException>(()=>waiting.GetAwaiter().GetResult());Assert.That(library.TryReadBounds(asset.Hash,out _),Is.False);slot.Release();
                Assert.That(library.PrepareBoundsAsync(asset.Hash,CancellationToken.None).GetAwaiter().GetResult().Known,Is.True);
            }finally{Directory.Delete(directory,true);}
        }
        [Test] public void SpatialIndexAnimatedImportsAndScanAnchorsNeverBecomeEmptySpace()
        {
            var imported=Item('a',RoomObjectKind.ImportedModel);Assert.That(RoomAuthoredBounds.Derive(imported,ModelInspection.Inspect(ModelFixture.Create()).AssetBounds).Visual.Known,Is.False);
            var anchored=Item();anchored.scanAnchors=new[]{new ScanDrawingAnchor()};Assert.That(RoomAuthoredBounds.Derive(anchored).Visual.Known,Is.False);Assert.That(RoomAuthoredBounds.Derive(new(){kind=RoomObjectKind.Maestro}).Visual.Known,Is.False);
        }
        [Test] public void SpatialIndexGeometryCoverageRequiresReviewForNewCanonicalFields()
        {
            // New components must classify their geometry and add native parity
            // coverage before the broad-phase can safely reject them.
            var expected="id name recipe environmentProfile visibilityLayer waterTraversal modelGeometry appearanceBindings scanAnchors collision lights windows surfaces drawingTips connections snapPoints containers heightFields sculptTips materialStores audioEmitters kind position rotation scale color points radius joints motion modelHash physics collisionShape mass followDistance walkSpeed walkClip walkMotionId".Split(' ');
            CollectionAssert.AreEquivalent(expected,typeof(RoomObjectData).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public).Select(f=>f.Name));
        }
    }
}
