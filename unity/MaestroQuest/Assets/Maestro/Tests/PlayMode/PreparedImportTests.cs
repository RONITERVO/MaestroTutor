// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UniGLTF;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        sealed class ImportAdmissionBarrier:IAwaitCaller
        {
            readonly TaskCompletionSource<bool> release=new();internal bool Entered;
            internal void Open()=>release.TrySetResult(true);
            async Task Wait(){Entered=true;await release.Task;}
            public Task NextFrame()=>Wait();public Task NextFrameIfTimedOut()=>Wait();
            public async Task Run(Action action){await Wait();action();}
            public async Task<T> Run<T>(Func<T> action){await Wait();return action();}
        }
        ImportedModel AdmissionModel(bool inactive=false){var go=new GameObject("Import admission blocker");go.SetActive(!inactive);go.transform.SetParent(root.transform,false);return go.AddComponent<ImportedModel>();}
        [UnityTest] public IEnumerator PreparedImportBudgetRefusalKeepsPreviewAndPublishesNoObject()
        {
            ImportRuntime();yield return ChooseModel(ModelFixture.Create());var before=editor.Snapshot().objects.Length;var asset=ModelLibrary.Inspect("blocker.glb",ModelFixture.Create());var barrier=new ImportAdmissionBarrier();var models=new List<ImportedModel>();var loads=new List<Task>();Task<bool> accept=null;bool completed=false,accepted=false,preview=false;int count=-1;
            try {
                var first=AdmissionModel();models.Add(first);loads.Add(first.LoadAsync(asset,barrier));yield return new WaitUntil(()=>barrier.Entered||loads[0].IsCompleted);Assert.IsTrue(barrier.Entered,loads[0].Exception?.ToString());
                while(ImportedModel.LiveBudget.Models<ImportedModel.MaximumLiveModels){var model=AdmissionModel();models.Add(model);loads.Add(model.LoadAsync(asset));}
                accept=imports.AcceptAsync();float deadline=Time.realtimeSinceStartup+5;while(!accept.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
                completed=accept.IsCompleted;accepted=completed&&accept.Result;preview=imports.HasPreview;count=editor.Snapshot().objects.Length;
            }finally{barrier.Open();foreach(var model in models)model.Dispose();}
            yield return new WaitUntil(()=>loads.All(t=>t.IsCompleted)&&(accept==null||accept.IsCompleted));foreach(var t in loads)Assert.IsNull(t.Exception);
            Assert.IsTrue(completed,"Budget refusal must not wait behind a native importer");Assert.IsFalse(accepted,"A failed object activation cannot report successful import");Assert.IsTrue(preview);Assert.AreEqual(before,count,"A refused import must not save a placeholder");
        }
        [UnityTest] public IEnumerator PreparedImportWaitsForNativeGeometryAndCancelsBeforePlacement()
        {
            ImportRuntime();yield return ChooseModel(ModelFixture.Create());var before=editor.Snapshot().objects.Length;var asset=ModelLibrary.Inspect("blocker.glb",ModelFixture.Create());var barrier=new ImportAdmissionBarrier();var first=AdmissionModel();var active=first.LoadAsync(asset,barrier);Task<JObject> accept=null;using var cancel=new CancellationTokenSource();bool early=false,cancelledBeforeRelease=false,preview=false;int count=-1;
            try {
                yield return new WaitUntil(()=>barrier.Entered||active.IsCompleted);Assert.IsTrue(barrier.Entered,active.Exception?.ToString());var args=ImportAccept("object");Assert.IsTrue(imports.BeginAcceptSelection((string)args["requestId"],(string)args["modelHash"],"object",0,cancel.Token,out accept,out var error),error);
                float deadline=Time.realtimeSinceStartup+5;while(ImportedModel.LiveBudget.Models<3&&!accept.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
                for(int i=0;i<5;i++)yield return null;early=accept.IsCompleted;count=editor.Snapshot().objects.Length;cancel.Cancel();deadline=Time.realtimeSinceStartup+2;while(!accept.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
                cancelledBeforeRelease=accept.IsCompleted&&accept.Result==null;preview=imports.HasPreview;
            }finally{cancel.Cancel();barrier.Open();first.Dispose();}
            yield return new WaitUntil(()=>active.IsCompleted&&(accept==null||accept.IsCompleted));Assert.IsNull(active.Exception);Assert.IsNull(accept?.Exception);
            Assert.IsFalse(early,"Acceptance must remain pending while its geometry waits for native import");Assert.AreEqual(before,count);Assert.IsTrue(cancelledBeforeRelease);Assert.IsTrue(preview);Assert.AreEqual(before,editor.Snapshot().objects.Length);
        }
        [UnityTest] public IEnumerator PreparedImportNeverActiveModelDisposesItsPencilMeshesAndMaterials()
        {
            var model=AdmissionModel(true);var load=model.LoadAsync(ModelLibrary.Inspect("inactive.glb",ModelFixture.Create()));yield return new WaitUntil(()=>load.IsCompleted);Assert.IsNull(load.Exception);Assert.IsTrue(model.Ready);
            var renderer=model.Instance.Renderers[0];var pigment=renderer.sharedMaterial;var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;Assert.IsTrue(model.Instance.GetComponent<PencilModelStyle>());
            model.Dispose();UnityEngine.Object.DestroyImmediate(model.gameObject);yield return null;
            Assert.IsFalse(mesh,"The never-active style owner must release its cloned mesh explicitly");Assert.IsFalse(pigment,"The never-active style owner must release its cloned material explicitly");
        }
        [UnityTest] public IEnumerator PreparedImportSaveFailureReleasesCandidateAndKeepsPreviewAndUndo()
        {
            ImportRuntime();yield return ChooseModel(ModelFixture.Create());Assert.IsTrue(editor.TryFlush(out var error),error);int count=editor.Snapshot().objects.Length;bool undo=editor.CanUndo;var budget=ImportedModel.LiveBudget;
            string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);Task<bool> accept=null;
            try {accept=imports.AcceptAsync();yield return new WaitUntil(()=>accept.IsCompleted);Assert.IsNull(accept.Exception);Assert.IsFalse(accept.Result,imports.Status);}
            finally {Directory.Delete(pending);}
            yield return null;Assert.AreEqual(count,editor.Snapshot().objects.Length);Assert.AreEqual(undo,editor.CanUndo);Assert.IsTrue(imports.HasPreview);Assert.AreEqual(budget,ImportedModel.LiveBudget);
            Assert.IsFalse(Resources.FindObjectsOfTypeAll<ImportedModel>().Any(x=>x&&x.gameObject.name=="Prepared imported object"));
            var retry=imports.AcceptAsync();yield return new WaitUntil(()=>retry.IsCompleted);Assert.IsTrue(retry.Result,imports.Status);Assert.IsTrue(editor.Find(editor.SelectedId).GetComponent<CreatedRoomObject>().ModelGeometryReady);
        }
        [UnityTest] public IEnumerator PreparedImportAgentCompletionPublishesUsableGeometryAndPreservesConcurrentEdits()
        {
            ImportRuntime();yield return ChooseModel(ModelFixture.Create());var before=editor.Snapshot().objects.Length;var asset=ModelLibrary.Inspect("blocker.glb",ModelFixture.Create());var barrier=new ImportAdmissionBarrier();var first=AdmissionModel();var active=first.LoadAsync(asset,barrier);JObject request=null;string other=null;
            try {
                yield return new WaitUntil(()=>barrier.Entered||active.IsCompleted);Assert.IsTrue(barrier.Entered);request=ImportRequest(ImportAccept("object"));Assert.IsTrue(authorActions.Execute(request,out var error),error);
                float deadline=Time.realtimeSinceStartup+5;while(ImportedModel.LiveBudget.Models<3&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.AreEqual("accepting",(string)ImportFact()["phase"]);Assert.AreEqual(before,editor.Snapshot().objects.Length);
                Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Ball,"While loading",Vector3.one,1,Color.blue,out other,out error),error);barrier.Open();yield return ModelFinished();
                var accepted=ImportFact();Assert.AreEqual("completed",(string)accepted["phase"],imports.Status);string id=(string)accepted["accepted"]["objectId"];var created=editor.Find(id).GetComponent<CreatedRoomObject>();
                Assert.IsTrue(created.ModelGeometryReady,"Completion must mean usable geometry without another wait");Assert.Greater(created.CollisionPieces,0);Assert.IsTrue(created.GetComponent<RoomItem>().Grab.colliders.All(x=>x&&x.enabled));Assert.IsFalse(created.Model.IsPlaying);Assert.IsNotNull(editor.Read(other));
                Assert.IsTrue(authorActions.Execute(request,out error),error);Assert.AreEqual(before+2,editor.Snapshot().objects.Length,"Receipt replay cannot place another object");
                var saved=new RoomStorage(directory).Load(out error);Assert.IsNotNull(saved,error);Assert.IsTrue(saved.objects.Any(x=>x.id==other));Assert.IsTrue(saved.objects.Any(x=>x.id==id));
                editor.Undo();Assert.IsNull(editor.Read(id));Assert.IsNotNull(editor.Read(other));yield return null;editor.Redo();created=editor.Find(id).GetComponent<CreatedRoomObject>();yield return new WaitUntil(()=>created.ModelGeometryReady||created.ModelStatus!="Loading local model…");Assert.IsTrue(created.ModelGeometryReady,created.ModelStatus);Assert.IsFalse(created.Model.IsPlaying);
            }finally {barrier.Open();first.Dispose();}
            yield return new WaitUntil(()=>active.IsCompleted);Assert.IsNull(active.Exception);
        }
        [UnityTest] public IEnumerator PreparedImportRuntimeHoldCancelsAndCannotRestartPlacementAfterRelease()
        {
            ImportRuntime();yield return ChooseModel(ModelFixture.Create());int count=editor.Snapshot().objects.Length;var barrier=new ImportAdmissionBarrier();var first=AdmissionModel();var active=first.LoadAsync(ModelLibrary.Inspect("blocker.glb",ModelFixture.Create()),barrier);Task<bool> accept=null;IDisposable hold=null;
            try {
                yield return new WaitUntil(()=>barrier.Entered||active.IsCompleted);Assert.IsTrue(barrier.Entered);accept=imports.AcceptAsync();float deadline=Time.realtimeSinceStartup+5;while(ImportedModel.LiveBudget.Models<3&&!accept.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
                hold=editor.RuntimeGate.Hold("Testing a brief room pause");hold.Dispose();hold=null;deadline=Time.realtimeSinceStartup+2;while(!accept.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(accept.IsCompleted);Assert.IsFalse(accept.Result);Assert.AreEqual(count,editor.Snapshot().objects.Length);Assert.IsTrue(imports.HasPreview);
            }finally{hold?.Dispose();barrier.Open();first.Dispose();}
            yield return new WaitUntil(()=>active.IsCompleted&&(accept==null||accept.IsCompleted));yield return null;Assert.AreEqual(count,editor.Snapshot().objects.Length);Assert.IsNull(active.Exception);
        }
        [UnityTest] public IEnumerator PreparedImportTransferRequiresExactSourceAndKeepsItsOwnerAfterAdoption()
        {
            var before=ImportedModel.LiveBudget;var asset=ModelLibrary.Inspect("candidate.glb",ModelFixture.Create());var data=new RoomObjectData{id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.ImportedModel,modelHash=asset.Hash};
            var loading=PreparedImportedModel.Load(asset,data,editor.WorldIdentity,CancellationToken.None,()=>true);yield return new WaitUntil(()=>loading.IsCompleted);Assert.IsNull(loading.Exception);using var candidate=loading.Result;
            var changed=new RoomObjectData{id=data.id,kind=data.kind,modelHash=data.modelHash};changed.modelGeometry.pivot="base";Assert.Throws<ModelImportException>(()=>candidate.Validate(changed));candidate.Validate(data);
            var holder=new GameObject("Adopted candidate owner");holder.transform.SetParent(root.transform,false);candidate.Adopt(holder.transform,out var imported,out var collision);Assert.IsNull(collision);candidate.Dispose();Assert.IsTrue(imported.Ready);Assert.AreEqual(before.Models+1,ImportedModel.LiveBudget.Models);Assert.Throws<ModelImportException>(()=>candidate.Validate(data));
            imported.Dispose();UnityEngine.Object.DestroyImmediate(holder);yield return null;Assert.AreEqual(before,ImportedModel.LiveBudget);
        }
        [UnityTest] public IEnumerator PreparedImportInvalidGeometryReleasesLoadedCandidateWithoutPublishingIt()
        {
            var before=ImportedModel.LiveBudget;var asset=ModelLibrary.Inspect("candidate.glb",ModelFixture.Create());var data=new RoomObjectData{id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.ImportedModel,modelHash=asset.Hash};data.modelGeometry.scaleMode="source";data.modelGeometry.metresPerUnit=100;
            int count=editor.Snapshot().objects.Length;var loading=PreparedImportedModel.Load(asset,data,editor.WorldIdentity,CancellationToken.None,()=>true);yield return new WaitUntil(()=>loading.IsCompleted);Assert.IsTrue(loading.IsFaulted);Assert.That(loading.Exception.InnerException,Is.TypeOf<ModelImportException>());yield return null;
            Assert.AreEqual(count,editor.Snapshot().objects.Length);Assert.AreEqual(before,ImportedModel.LiveBudget);Assert.IsFalse(Resources.FindObjectsOfTypeAll<ImportedModel>().Any(x=>x&&x.gameObject.name=="Prepared imported object"));
        }
    }
}
