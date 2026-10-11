// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator ModelQueueReservesWaitingSourceCostsAndRejectsOverflowBeforeImporterAccess()
        {
            root.AddComponent<RuntimeDiagnostics>();var before=ImportedModel.LiveBudget;var asset=ModelLibrary.Inspect("queued.glb",ModelFixture.Create());var barrier=new ModelLoadBarrier();
            var models=new List<ImportedModel>();var loads=new List<Task>();int reserved=0,vertices=0;bool rejected=false;string queuedPhase=null;
            try {
                var first=ResidencyModel();models.Add(first);loads.Add(first.LoadAsync(asset,barrier));yield return new WaitUntil(()=>barrier.Entered||loads[0].IsCompleted);Assert.IsTrue(barrier.Entered,loads[0].Exception?.ToString());
                for(int i=before.Models+1;i<ImportedModel.MaximumLiveModels;i++){var model=ResidencyModel();models.Add(model);loads.Add(model.LoadAsync(asset));}
                var budget=ImportedModel.LiveBudget;reserved=budget.Models;vertices=budget.Vertices;queuedPhase=(string)ReservationFact(before.Models+1)?["state"];
                var excess=ResidencyModel();models.Add(excess);var denied=excess.LoadAsync(asset);loads.Add(denied);rejected=denied.IsFaulted&&denied.Exception.InnerException is ModelImportException;
            }finally{barrier.Open();foreach(var model in models)model.Dispose();}
            yield return new WaitUntil(()=>loads.All(t=>t.IsCompleted));foreach(var load in loads)_=load.Exception;
            Assert.AreEqual(ImportedModel.MaximumLiveModels,reserved,"Waiting loads must own budget before entering the importer");
            Assert.AreEqual(before.Vertices+(ImportedModel.MaximumLiveModels-before.Models)*asset.Inspection.Vertices,vertices);Assert.AreEqual("queued",queuedPhase);Assert.IsTrue(rejected,"A full queue must refuse before waiting behind native import");Assert.AreEqual(before,ImportedModel.LiveBudget);
        }
        [UnityTest] public IEnumerator ModelQueueDisposedWaiterCompletesWithoutWaitingForActiveImporter()
        {
            var before=ImportedModel.LiveBudget;var asset=ModelLibrary.Inspect("queued.glb",ModelFixture.Create());var blocker=new ModelLoadBarrier();var never=new ModelLoadBarrier();var first=ResidencyModel();var queued=ResidencyModel();
            var active=first.LoadAsync(asset,blocker);Task waiting=null;bool completedBeforeRelease=false,entered=false;int afterCancel=0;
            try {
                yield return new WaitUntil(()=>blocker.Entered||active.IsCompleted);Assert.IsTrue(blocker.Entered,active.Exception?.ToString());waiting=queued.LoadAsync(asset,never);queued.Dispose();queued.Dispose();
                for(int i=0;i<5&&!waiting.IsCompleted;i++)yield return null;
                completedBeforeRelease=waiting.IsCompleted;entered=never.Entered;afterCancel=ImportedModel.LiveBudget.Models;
            }finally{blocker.Open();never.Open();first.Dispose();queued.Dispose();}
            yield return new WaitUntil(()=>active.IsCompleted&&(waiting==null||waiting.IsCompleted));Assert.IsNull(active.Exception);Assert.IsNull(waiting?.Exception);
            Assert.IsTrue(completedBeforeRelease,"A cancelled waiter must drain while the native importer is still blocked");Assert.IsFalse(entered);Assert.AreEqual(before.Models+1,afterCancel);Assert.AreEqual(before,ImportedModel.LiveBudget);
        }
        [UnityTest] public IEnumerator ModelQueueKeepsOneReservationIdentityAcrossQueuedLoadingAndReady()
        {
            root.AddComponent<RuntimeDiagnostics>();var before=ImportedModel.LiveBudget;var asset=ModelLibrary.Inspect("queued.glb",ModelFixture.Create());var blocker=new ModelLoadBarrier();var secondBarrier=new ModelLoadBarrier();var first=ResidencyModel();var second=ResidencyModel();
            string target=new string('d',32);second.ConfigureResourceOwner(editor.WorldIdentity,target,"object");var active=first.LoadAsync(asset,blocker);Task next=null;string queuedId=null,loadingId=null,readyId=null,queuedWorld=null,queuedTarget=null,loadingState=null,readyState=null;JObject observed=null;
            try {
                yield return new WaitUntil(()=>blocker.Entered||active.IsCompleted);Assert.IsTrue(blocker.Entered,active.Exception?.ToString());next=second.LoadAsync(asset,secondBarrier);
                var queued=ReservationFact(before.Models+1);queuedId=(string)queued?["reservationId"];queuedWorld=(string)queued?["worldId"];queuedTarget=(string)queued?["target"];
                blocker.Open();yield return new WaitUntil(()=>secondBarrier.Entered||next.IsCompleted);Assert.IsTrue(secondBarrier.Entered,next.Exception?.ToString());
                var loading=ReservationFact(before.Models+1);loadingId=(string)loading?["reservationId"];loadingState=(string)loading?["state"];
                secondBarrier.Open();yield return new WaitUntil(()=>next.IsCompleted);Assert.IsNull(next.Exception);Assert.IsTrue(second.Ready);
                var ready=ReservationFact(before.Models+1);readyId=(string)ready?["reservationId"];readyState=(string)ready?["state"];
                observed=new JObject{["queued"]=queued,["loading"]=loading,["ready"]=ready};
            }finally{blocker.Open();secondBarrier.Open();first.Dispose();second.Dispose();}
            yield return new WaitUntil(()=>active.IsCompleted&&(next==null||next.IsCompleted));Assert.IsNull(active.Exception);Assert.IsNull(next?.Exception);
            Assert.That(queuedId,Is.Not.Null.And.Not.Empty,"A queued request needs an observable reservation");Assert.AreEqual(queuedId,loadingId);Assert.AreEqual(queuedId,readyId);Assert.AreEqual("loading",loadingState);Assert.AreEqual("ready",readyState);Assert.AreEqual(editor.WorldIdentity.worldId,queuedWorld);Assert.AreEqual(target,queuedTarget);Assert.AreEqual(before,ImportedModel.LiveBudget);
            var evidence=Environment.GetEnvironmentVariable("MAESTRO_MODEL_QUEUE_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(Path.GetDirectoryName(evidence));File.WriteAllText(evidence,observed.ToString());}
        }
        [UnityTest] public IEnumerator ModelQueueBudgetRefusalCanRetryTheSameLoaderAfterAWaitingSlotIsReleased()
        {
            var before=ImportedModel.LiveBudget;var asset=ModelLibrary.Inspect("queued.glb",ModelFixture.Create());var barrier=new ModelLoadBarrier();var models=new List<ImportedModel>();var loads=new List<Task>();
            try {
                var first=ResidencyModel();models.Add(first);var active=first.LoadAsync(asset,barrier);loads.Add(active);yield return new WaitUntil(()=>barrier.Entered||active.IsCompleted);Assert.IsTrue(barrier.Entered,active.Exception?.ToString());
                for(int i=before.Models+1;i<ImportedModel.MaximumLiveModels;i++){var queued=ResidencyModel();models.Add(queued);loads.Add(queued.LoadAsync(asset));}
                var retrying=ResidencyModel();models.Add(retrying);var rejected=retrying.LoadAsync(asset);loads.Add(rejected);Assert.IsTrue(rejected.IsFaulted);Assert.That(rejected.Exception.InnerException,Is.TypeOf<ModelImportException>());
                models[1].Dispose();var retry=retrying.LoadAsync(asset);loads.Add(retry);Assert.IsFalse(retry.IsCompleted);Assert.AreEqual(ImportedModel.MaximumLiveModels,ImportedModel.LiveBudget.Models);
                foreach(var queued in models.Skip(1).Take(models.Count-2))queued.Dispose();barrier.Open();yield return new WaitUntil(()=>active.IsCompleted&&retry.IsCompleted);Assert.IsNull(active.Exception);Assert.IsNull(retry.Exception);Assert.IsTrue(retrying.Ready);
                Assert.AreEqual(before.Models+2,ImportedModel.LiveBudget.Models,"A refused load must neither release an unowned semaphore slot nor block its retry");
            }finally{barrier.Open();foreach(var model in models)model.Dispose();}
            yield return new WaitUntil(()=>loads.All(t=>t.IsCompleted));foreach(var load in loads)_=load.Exception;Assert.AreEqual(before,ImportedModel.LiveBudget);
        }
        [UnityTest] public IEnumerator ModelQueueNeverActiveCandidateCanBeExplicitlyDisposedWhileAnotherImportRuns()
        {
            var before=ImportedModel.LiveBudget;var asset=ModelLibrary.Inspect("queued.glb",ModelFixture.Create());var barrier=new ModelLoadBarrier();var first=ResidencyModel();var inactive=new GameObject("Never-active model candidate");inactive.SetActive(false);inactive.transform.SetParent(root.transform,false);var candidate=inactive.AddComponent<ImportedModel>();
            var active=first.LoadAsync(asset,barrier);Task waiting=null;bool completed=false;
            try {
                yield return new WaitUntil(()=>barrier.Entered||active.IsCompleted);Assert.IsTrue(barrier.Entered,active.Exception?.ToString());waiting=candidate.LoadAsync(asset);Assert.AreEqual(before.Models+2,ImportedModel.LiveBudget.Models);
                candidate.Dispose();UnityEngine.Object.DestroyImmediate(inactive);for(int i=0;i<5&&!waiting.IsCompleted;i++)yield return null;completed=waiting.IsCompleted;
                Assert.AreEqual(before.Models+1,ImportedModel.LiveBudget.Models);
            }finally{barrier.Open();first.Dispose();if(candidate)candidate.Dispose();if(inactive)UnityEngine.Object.DestroyImmediate(inactive);}
            yield return new WaitUntil(()=>active.IsCompleted&&(waiting==null||waiting.IsCompleted));Assert.IsNull(active.Exception);Assert.IsNull(waiting?.Exception);Assert.IsTrue(completed);Assert.AreEqual(before,ImportedModel.LiveBudget);
        }
    }
}
