// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UniGLTF;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        sealed class ModelLoadBarrier:IAwaitCaller
        {
            readonly TaskCompletionSource<bool> release=new();
            internal bool Entered;
            internal void Open()=>release.TrySetResult(true);
            internal void Fail()=>release.TrySetException(new IOException("Synthetic importer failure"));
            async Task Wait(){Entered=true;await release.Task;}
            public Task NextFrame()=>Wait();
            public Task NextFrameIfTimedOut()=>Wait();
            public async Task Run(Action action){await Wait();action();}
            public async Task<T> Run<T>(Func<T> action){await Wait();return action();}
        }
        JObject ReservationFact(int index)
        {
            bool found=BehaviourCatalog.TryRead("runtime.modelReservation",1,new JObject{["index"]=index},new BehaviourCatalog.FactContext(editor:editor),out var value);
            if(!found)return null;Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);
        }
        ImportedModel ResidencyModel(string name="reservation model")
        {
            var child=new GameObject(name);child.transform.SetParent(root.transform,false);return child.AddComponent<ImportedModel>();
        }
        [UnityTest] public IEnumerator ModelResidencyRejectsConcurrentLoadAndKeepsTheFirstOwner()
        {
            root.AddComponent<RuntimeDiagnostics>();var before=ImportedModel.LiveBudget;var model=ResidencyModel();var barrier=new ModelLoadBarrier();
            var identity=editor.WorldIdentity;var target=new string('c',32);model.ConfigureResourceOwner(identity,target,"object");
            var asset=ModelLibrary.Inspect("triangle.glb",ModelFixture.Create());var first=model.LoadAsync(asset,barrier);
            var duplicate=model.LoadAsync(asset,barrier);
            try{
                yield return new WaitUntil(()=>barrier.Entered||first.IsCompleted);
                Assert.That(barrier.Entered,Is.True,first.Exception?.ToString());
                Assert.That(duplicate.IsFaulted,Is.True);Assert.That(duplicate.Exception.InnerException,Is.TypeOf<InvalidOperationException>());
                Assert.Throws<InvalidOperationException>(()=>model.ConfigureResourceOwner(RoomWorldIdentity.Create(),target,"object"));
                var current=ReservationFact(before.Models);Assert.That((string)current["worldId"],Is.EqualTo(identity.worldId));
                Assert.That((string)current["regionId"],Is.EqualTo(identity.regionId));Assert.That((string)current["target"],Is.EqualTo(target));
                Assert.That((string)current["state"],Is.EqualTo("loading"));Assert.That((string)current["modelHash"],Is.EqualTo(asset.Hash));
                var loading=current.DeepClone();current["vertices"]=-1;Assert.That((int)ReservationFact(before.Models)["vertices"],Is.EqualTo(asset.Inspection.Vertices));
                barrier.Open();yield return new WaitUntil(()=>first.IsCompleted);Assert.That(first.Exception,Is.Null);
                Assert.That(model.Ready,Is.True);Assert.That((string)ReservationFact(before.Models)["state"],Is.EqualTo("ready"));
                Assert.That(ImportedModel.LiveBudget.Models,Is.EqualTo(before.Models+1));
                var evidence=Environment.GetEnvironmentVariable("MAESTRO_MODEL_RESIDENCY_EVIDENCE");
                if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(Path.GetDirectoryName(evidence));File.WriteAllText(evidence,new JObject{["loading"]=loading,["ready"]=ReservationFact(before.Models)}.ToString());}
            }finally{barrier.Open();model.Dispose();}
            yield return null;Assert.That(ImportedModel.LiveBudget,Is.EqualTo(before));Assert.That(ReservationFact(before.Models),Is.Null);
        }
        [UnityTest] public IEnumerator ModelResidencyRetainsAbandonedInFlightCostsUntilImporterDrains()
        {
            root.AddComponent<RuntimeDiagnostics>();var before=ImportedModel.LiveBudget;var model=ResidencyModel();var barrier=new ModelLoadBarrier();
            model.ConfigureResourceOwner(editor.WorldIdentity,"","preview");
            var asset=ModelLibrary.Inspect("triangle.glb",ModelFixture.Create());var load=model.LoadAsync(asset,barrier);
            try{
                yield return new WaitUntil(()=>barrier.Entered||load.IsCompleted);Assert.That(barrier.Entered,Is.True,load.Exception?.ToString());
                var reserved=ImportedModel.LiveBudget;model.Dispose();model.Dispose();
                Assert.That(ImportedModel.LiveBudget,Is.EqualTo(reserved),"Outstanding native allocations still own their admission budget");
                Assert.That((string)ReservationFact(before.Models)["state"],Is.EqualTo("retiring"));
                Assert.That((string)ReservationFact(before.Models)["role"],Is.EqualTo("preview"));Assert.That(model.Ready,Is.False);
                barrier.Open();yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Exception,Is.Null);
                Assert.That(model.Ready,Is.False);Assert.That(ImportedModel.LiveBudget,Is.EqualTo(before));
            }finally{barrier.Open();model.Dispose();}
        }
        [UnityTest] public IEnumerator ModelResidencyDestroyedQueuedRequestCannotAllocateOrBlockTheNextLoad()
        {
            var before=ImportedModel.LiveBudget;var first=ResidencyModel();var queued=ResidencyModel();var next=ResidencyModel();var barrier=new ModelLoadBarrier();
            var asset=ModelLibrary.Inspect("triangle.glb",ModelFixture.Create());var load=first.LoadAsync(asset,barrier);
            try{
                yield return new WaitUntil(()=>barrier.Entered||load.IsCompleted);Assert.That(barrier.Entered,Is.True,load.Exception?.ToString());
                var skipped=queued.LoadAsync(asset);var following=next.LoadAsync(asset);queued.Dispose();
                Assert.That(ImportedModel.LiveBudget.Models,Is.EqualTo(before.Models+1));
                barrier.Open();yield return new WaitUntil(()=>load.IsCompleted&&skipped.IsCompleted&&following.IsCompleted);
                Assert.That(load.Exception,Is.Null);Assert.That(skipped.Exception,Is.Null);Assert.That(following.Exception,Is.Null);
                Assert.That(queued.Ready,Is.False);Assert.That(next.Ready,Is.True);Assert.That(ImportedModel.LiveBudget.Models,Is.EqualTo(before.Models+2));
            }finally{barrier.Open();first.Dispose();queued.Dispose();next.Dispose();}
            yield return null;Assert.That(ImportedModel.LiveBudget,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator ModelResidencyFailedImporterReleasesItsLeaseAndAllowsFreshRetry()
        {
            root.AddComponent<RuntimeDiagnostics>();var before=ImportedModel.LiveBudget;var model=ResidencyModel();var barrier=new ModelLoadBarrier();
            var asset=ModelLibrary.Inspect("triangle.glb",ModelFixture.Create());var load=model.LoadAsync(asset,barrier);string failedId=null;
            try{
                yield return new WaitUntil(()=>barrier.Entered||load.IsCompleted);Assert.That(barrier.Entered,Is.True,load.Exception?.ToString());
                failedId=(string)ReservationFact(before.Models)["reservationId"];barrier.Fail();yield return new WaitUntil(()=>load.IsCompleted);
                Assert.That(load.IsFaulted,Is.True);Assert.That(load.Exception.ToString(),Does.Contain("Synthetic importer failure"));
                Assert.That(model.Ready,Is.False);Assert.That(ImportedModel.LiveBudget,Is.EqualTo(before));
                var retry=model.LoadAsync(asset);yield return new WaitUntil(()=>retry.IsCompleted);Assert.That(retry.Exception,Is.Null);Assert.That(model.Ready,Is.True);
                Assert.That((string)ReservationFact(before.Models)["reservationId"],Is.Not.EqualTo(failedId));
            }finally{barrier.Open();model.Dispose();}
            yield return null;Assert.That(ImportedModel.LiveBudget,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator ModelResidencyRealPreviewAndSavedObjectKeepDistinctScopedLeases()
        {
            root.AddComponent<RuntimeDiagnostics>();var before=ImportedModel.LiveBudget;var workshop=root.AddComponent<ImportWorkshop>();workshop.Initialize(editor);
            var prepare=workshop.PrepareAsync("triangle.glb",ModelFixture.Create());yield return new WaitUntil(()=>prepare.IsCompleted);Assert.That(workshop.HasPreview,Is.True,workshop.Status);
            var preview=ReservationFact(before.Models);Assert.That((string)preview["role"],Is.EqualTo("preview"));Assert.That((string)preview["target"],Is.Empty);
            Assert.That((string)preview["worldId"],Is.EqualTo(editor.WorldIdentity.worldId));
            var accept=workshop.AcceptAsync();yield return new WaitUntil(()=>accept.IsCompleted);Assert.That(accept.Result,Is.True,workshop.Status);
            var data=editor.Read(editor.SelectedId);var created=editor.Find(data.id).GetComponent<CreatedRoomObject>();
            yield return new WaitUntil(()=>created.Model&&created.Model.Ready||created.ModelStatus!="Loading local model…");
            Assert.That(created.Model&&created.Model.Ready,Is.True,created.ModelStatus);yield return null;
            var entry=Enumerable.Range(0,ImportedModel.LiveBudget.Models).Select(ReservationFact).Single(x=>(string)x["target"]==data.id);
            Assert.That((string)entry["reservationId"],Is.Not.EqualTo((string)preview["reservationId"]));Assert.That((string)entry["modelHash"],Is.EqualTo(data.modelHash));
            Assert.That((string)entry["role"],Is.EqualTo("object"));Assert.That((string)entry["regionId"],Is.EqualTo(editor.WorldIdentity.regionId));
            Assert.That(ImportedModel.LiveBudget.Models,Is.EqualTo(before.Models+1));
        }
    }
}
