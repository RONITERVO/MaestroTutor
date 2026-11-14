// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using NUnit.Framework;
using UniGLTF;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class ModelAssetOwnershipTests
    {
        sealed class ImportBarrier:IAwaitCaller
        {
            readonly TaskCompletionSource<bool> release=new();
            internal bool Entered;
            internal void Open()=>release.TrySetResult(true);
            internal void Fail()=>release.TrySetException(new IOException("Synthetic owned importer failure"));
            async Task Wait(){Entered=true;await release.Task;}
            public Task NextFrame()=>Wait();
            public Task NextFrameIfTimedOut()=>Wait();
            public async Task Run(Action action){await Wait();action();}
            public async Task<T> Run<T>(Func<T> action){await Wait();return action();}
        }
        GameObject root;string directory,hash;WorkspaceWriteGate workspace;ModelLibrary library;
        readonly List<ImportedModel> models=new();readonly List<ImportBarrier> barriers=new();
        readonly List<Task> loads=new();readonly List<Task<ModelAsset>> reads=new();
        [UnitySetUp] public IEnumerator SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"MaestroModelAssetOwnership-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            byte[] bytes=ModelFixture.Create();hash=ModelLibrary.Hash(bytes);File.WriteAllBytes(Path.Combine(directory,hash+".glb"),bytes);
            workspace=new WorkspaceWriteGate();library=new ModelLibrary(directory,workspace);root=new GameObject("Model asset ownership fixture");yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach(var barrier in barriers)barrier.Open();foreach(var model in models)if(model)model.Dispose();
            double deadline=Time.realtimeSinceStartupAsDouble+15;
            while((loads.Any(t=>!t.IsCompleted)||reads.Any(t=>!t.IsCompleted))&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            bool drained=loads.All(t=>t.IsCompleted)&&reads.All(t=>t.IsCompleted);
            foreach(var task in reads){if(task.Status==TaskStatus.RanToCompletion)task.Result.Dispose();else if(task.IsFaulted)_=task.Exception;}
            foreach(var task in loads)if(task.IsFaulted)_=task.Exception;
            foreach(var model in models)if(model)model.Dispose();
            if(root)UnityEngine.Object.Destroy(root);yield return null;yield return null;
            models.Clear();barriers.Clear();loads.Clear();reads.Clear();
            Assert.That(drained,Is.True,"Owned native work did not drain; preserve its fixture files");
            Assert.That(workspace.Assets.Pinned(ImportedAssetKind.Model,hash),Is.Zero,"The fixture must release every source owner");
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
        ImportedModel NewModel(ModelLibrary source=null,bool inactive=false)
        {
            var child=new GameObject("Owned imported model");child.transform.SetParent(root.transform,false);if(inactive)child.SetActive(false);
            var model=child.AddComponent<ImportedModel>();model.ConfigureResourceOwner(RoomWorldIdentity.Create(),Guid.NewGuid().ToString("N"),inactive?"preview":"object");
            model.ConfigureAssetSource(source??library);models.Add(model);return model;
        }
        ImportBarrier Barrier(){var barrier=new ImportBarrier();barriers.Add(barrier);return barrier;}
        Task<ModelAsset> Read(){var task=library.ReadAsync(hash);reads.Add(task);return task;}
        Task Load(ImportedModel model,ModelAsset asset,IAwaitCaller caller=null){var task=model.LoadAsync(asset,caller);loads.Add(task);return task;}
        static IEnumerator Until(Func<bool> complete,string message)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+10;while(!complete()&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(complete(),Is.True,message);
        }
        static IEnumerator Succeeded(Task task)
        {
            yield return Until(()=>task.IsCompleted,"The owned model task did not finish");Assert.That(task.IsCanceled,Is.False);Assert.That(task.Exception,Is.Null);
        }
        int Pins=>workspace.Assets.Pinned(ImportedAssetKind.Model,hash);
        [UnityTest] public IEnumerator ReadyNativeModelRetainsSourceAfterReadOwnerClosesAndReleasesOnDispose()
        {
            var before=ImportedModel.LiveBudget;var read=Read();yield return Succeeded(read);var asset=read.Result;
            Assert.That(Pins,Is.EqualTo(1));var model=NewModel();var load=Load(model,asset);asset.Dispose();
            Assert.That(Pins,Is.EqualTo(1),"Native admission must retain its own source before the read owner closes");
            yield return Succeeded(load);Assert.That(model.Ready,Is.True);Assert.That(model.Instance.Renderers.Count,Is.GreaterThan(0));
            var native=model.Instance;Assert.That(Pins,Is.EqualTo(1));Assert.That(ImportedModel.LiveBudget.Models,Is.EqualTo(before.Models+1));
            using(var blocked=workspace.Assets.TryRetire(ImportedAssetKind.Model,hash,out var error)){Assert.That(blocked,Is.Null);Assert.That(error,Does.Contain("in use"));}
            var closing=workspace.Retire();Assert.That(closing.IsCompleted,Is.False);model.Dispose();model.Dispose();
            Assert.That(model.Ready,Is.False);Assert.That(Pins,Is.Zero);yield return Succeeded(closing);yield return null;
            Assert.That(native==null,Is.True,"The actual imported native instance must be destroyed");Assert.That(ImportedModel.LiveBudget,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator SameHashFromAnotherWorkspaceIsRefusedBeforeImporterOrNativeBudgetAdmission()
        {
            var before=ImportedModel.LiveBudget;var read=Read();yield return Succeeded(read);var asset=read.Result;
            var otherWorkspace=new WorkspaceWriteGate();var otherLibrary=new ModelLibrary(directory,otherWorkspace);var model=NewModel(otherLibrary);var never=Barrier();
            var rejected=Load(model,asset,never);yield return Until(()=>rejected.IsCompleted,"Wrong-workspace admission did not finish");
            Assert.That(rejected.IsFaulted,Is.True);Assert.That(rejected.Exception.GetBaseException(),Is.TypeOf<InvalidOperationException>());
            Assert.That(rejected.Exception.GetBaseException().Message,Does.Contain("workspace"));Assert.That(never.Entered,Is.False);Assert.That(model.Ready,Is.False);
            Assert.That(ImportedModel.LiveBudget,Is.EqualTo(before));Assert.That(Pins,Is.EqualTo(1));Assert.That(otherWorkspace.Assets.Pinned(ImportedAssetKind.Model,hash),Is.Zero);
            using(var independent=otherWorkspace.Assets.TryRetire(ImportedAssetKind.Model,hash,out var error))Assert.That(independent,Is.Not.Null,error);
            asset.Dispose();Assert.That(Pins,Is.Zero);
        }
        [UnityTest] public IEnumerator CancelledNeverActiveQueuedModelReleasesOnlyItsOwnPinBeforeActiveImporterDrains()
        {
            var before=ImportedModel.LiveBudget;var read=Read();yield return Succeeded(read);var asset=read.Result;
            var first=NewModel();var queued=NewModel(inactive:true);var blocker=Barrier();var never=Barrier();var active=Load(first,asset,blocker);
            yield return Until(()=>blocker.Entered||active.IsCompleted,"The active importer did not reach its barrier");Assert.That(blocker.Entered,Is.True,active.Exception?.ToString());
            var waiting=Load(queued,asset,never);Assert.That(Pins,Is.EqualTo(3));asset.Dispose();Assert.That(Pins,Is.EqualTo(2));
            queued.Dispose();queued.Dispose();Assert.That(Pins,Is.EqualTo(1),"A queued candidate must release only its native source owner");
            yield return Succeeded(waiting);Assert.That(never.Entered,Is.False);Assert.That(active.IsCompleted,Is.False);Assert.That(queued.Ready,Is.False);
            Assert.That(ImportedModel.LiveBudget.Models,Is.EqualTo(before.Models+1));
            first.Dispose();Assert.That(Pins,Is.EqualTo(1));blocker.Open();yield return Succeeded(active);
            Assert.That(Pins,Is.Zero);Assert.That(ImportedModel.LiveBudget,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator DisposedActiveImporterRetainsSourceAndWorkspaceUntilRealNativeDrain()
        {
            var before=ImportedModel.LiveBudget;var read=Read();yield return Succeeded(read);var asset=read.Result;
            var model=NewModel(inactive:true);var barrier=Barrier();var load=Load(model,asset,barrier);asset.Dispose();
            yield return Until(()=>barrier.Entered||load.IsCompleted,"The real importer did not reach its barrier");Assert.That(barrier.Entered,Is.True,load.Exception?.ToString());
            model.Dispose();model.Dispose();Assert.That(Pins,Is.EqualTo(1));Assert.That(model.Ready,Is.False);Assert.That(load.IsCompleted,Is.False);
            Assert.That((string)ModelReservations.Observe(before.Models)["state"],Is.EqualTo("retiring"));
            var closing=workspace.Retire();Assert.That(closing.IsCompleted,Is.False);
            using(var refused=workspace.Assets.TryRetire(ImportedAssetKind.Model,hash,out var error)){Assert.That(refused,Is.Null);Assert.That(error,Does.Contain("in use"));}
            barrier.Open();yield return Succeeded(load);yield return Succeeded(closing);
            Assert.That(model.Ready,Is.False);Assert.That(Pins,Is.Zero);Assert.That(ImportedModel.LiveBudget,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator FailedRealImporterReleasesNativeSourceOwnerWithoutChangingThePayload()
        {
            var before=ImportedModel.LiveBudget;byte[] bytes=File.ReadAllBytes(Path.Combine(directory,hash+".glb"));var read=Read();yield return Succeeded(read);var asset=read.Result;
            var model=NewModel();var barrier=Barrier();var load=Load(model,asset,barrier);asset.Dispose();
            yield return Until(()=>barrier.Entered||load.IsCompleted,"The real importer did not reach its failure boundary");Assert.That(barrier.Entered,Is.True,load.Exception?.ToString());
            barrier.Fail();yield return Until(()=>load.IsCompleted,"The failed importer did not drain");Assert.That(load.IsFaulted,Is.True);Assert.That(load.Exception.ToString(),Does.Contain("Synthetic owned importer failure"));
            Assert.That(Pins,Is.Zero);Assert.That(model.Ready,Is.False);Assert.That(ImportedModel.LiveBudget,Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(Path.Combine(directory,hash+".glb")),Is.EqualTo(bytes));
            using(var retirement=workspace.Assets.TryRetire(ImportedAssetKind.Model,hash,out var error))Assert.That(retirement,Is.Not.Null,error);
            var reread=Read();yield return Succeeded(reread);Assert.That(reread.Result.Hash,Is.EqualTo(hash));reread.Result.Dispose();Assert.That(Pins,Is.Zero);
        }
    }
}
