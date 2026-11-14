// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        [UnityTest] public IEnumerator PhysicalAvatarCompletionKeepsUnrelatedProgram()
        {
            AuthorRuntime();
            Assert.That(authorRuntime.Scheduler.Invoke(new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=30}},Time.unscaledTime,out var waiting,out var error),Is.True,error);
            Assert.That(editor.SetMaestroModel(null),Is.True,editor.Status);
            yield return new WaitUntil(()=>!avatar.ModelBusy);
            Assert.That(authorRuntime.Scheduler.RunningCount,Is.EqualTo(1),"Changing Maestro must not stop another action");
            Assert.That((string)authorRuntime.Scheduler.Invocation(waiting)["phase"],Is.EqualTo("running"));
        }
        [UnityTest] public IEnumerator PhysicalAvatarCompletionKeepsOtherTargetRecording()
        {
            AuthorRuntime();editor.Select(editor.Find("book"));workshop.ToggleRecord();yield return new WaitForSeconds(.12f);
            Assert.That(workshop.IsRecording,Is.True);
            Assert.That(editor.SetMaestroModel(null),Is.True,editor.Status);
            yield return new WaitUntil(()=>!avatar.ModelBusy);
            Assert.That(workshop.IsRecording,Is.True,"Another target's recording was stopped");
            Assert.That((string)workshop.ObserveRecording()["target"],Is.EqualTo("book"));
            workshop.DiscardRecording(workshop.RecordingSessionId,out _,out _);
        }
        [UnityTest] public IEnumerator AvatarCompletionReceiptKeepsAcceptedRevisionAcrossObserverEdit()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Receipt.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);
            int accepted=0;bool changed=false;
            void AfterReady(){
                if(changed||avatar.ModelBusy||avatar.ModelHash!=asset.Hash)return;
                changed=true;accepted=editor.ObjectRevision("maestro");
                Assert.That(editor.MoveObject("maestro",Vector3.right,out var error),Is.True,error);
            }
            avatar.ModelChanged+=AfterReady;
            try{
                Assert.That(authorActions.Execute(ModelRequest(asset.Hash),out var error),Is.True,error);
                yield return ModelFinished();
                Assert.That(changed,Is.True);
                Assert.That(editor.ObjectRevision("maestro"),Is.GreaterThan(accepted));
                Assert.That((int)authorActions.Observe()["selected"]["output"]["revision"],Is.EqualTo(accepted));
            }finally{avatar.ModelChanged-=AfterReady;}
        }

        sealed class HeldAvatarPublication:IDisposable
        {
            readonly ManualResetEventSlim release=new(false);readonly string phase;
            int entered,exited;internal int ThreadId;
            internal HeldAvatarPublication(string phase="publishing"){this.phase=phase;}
            internal bool Entered=>Volatile.Read(ref entered)==1;
            internal void Check(string value){
                if(value!=phase)return;ThreadId=Thread.CurrentThread.ManagedThreadId;Volatile.Write(ref entered,1);
                try{if(!release.Wait(TimeSpan.FromSeconds(25)))throw new IOException("Held avatar publication timed out");}
                finally{Volatile.Write(ref exited,1);}
            }
            internal void Release()=>release.Set();
            public void Dispose(){
                Release();
                if(!Entered||Volatile.Read(ref exited)==1)release.Dispose();
                else _=Task.Run(()=>{if(SpinWait.SpinUntil(()=>Volatile.Read(ref exited)==1,30000))release.Dispose();});
            }
        }
        IEnumerator AvatarDrained()=>PhysicalCreationTestWait.Until(()=>!editor.AvatarSelectionPending&&!avatar.ModelBusy,"Avatar selection did not drain");
        [UnityTest] public IEnumerator AvatarCompletionKeepsOldAvatarUntilBackgroundPublicationConfirms()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Background.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);
            Assert.That(editor.TryFlush(out var error),Is.True,error);
            var path=Path.Combine(directory,RoomStorage.FileName);var before=File.ReadAllBytes(path);int thread=Thread.CurrentThread.ManagedThreadId;
            using var hold=new HeldAvatarPublication();editor.AvatarSelectionCheckpoint=hold.Check;
            try{
                Assert.That(editor.SetMaestroModel(asset.Hash),Is.True,editor.Status);var completion=editor.AvatarSelectionCompletion;
                yield return PhysicalCreationTestWait.Until(()=>hold.Entered,"Avatar did not reach publication");
                Assert.That(hold.ThreadId,Is.Not.EqualTo(thread));Assert.That(completion.IsCompleted,Is.False);
                Assert.That(editor.Read("maestro").modelHash,Is.Null.Or.Empty);Assert.That(avatar.ModelHash,Is.Empty);Assert.That(avatar.CustomModel,Is.Null);
                Assert.That(File.ReadAllBytes(path),Is.EqualTo(before));
                Assert.That(editor.MoveObject("book",Vector3.right,out error),Is.False);Assert.That(error,Does.Contain("publishing"));
                editor.Select(editor.Find("book"));hold.Release();yield return AvatarDrained();
                Assert.That(completion.Result,Is.True);Assert.That(editor.SelectedId,Is.EqualTo("book"));Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash));
                Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").modelHash,Is.EqualTo(asset.Hash),error);
                editor.Undo();yield return new WaitUntil(()=>!avatar.ModelBusy);Assert.That(editor.Read("maestro").modelHash,Is.Null.Or.Empty);Assert.That(avatar.ModelHash,Is.Empty);
                editor.Redo();yield return new WaitUntil(()=>!avatar.ModelBusy);Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash));
            }finally{hold.Release();editor.AvatarSelectionCheckpoint=null;}
        }
        [UnityTest] public IEnumerator AvatarCompletionStopAfterPublicationStillAppliesExactlyOnce()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Accepted.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);
            using var hold=new HeldAvatarPublication();editor.AvatarSelectionCheckpoint=hold.Check;
            try{
                var request=ModelRequest(asset.Hash);Assert.That(authorActions.Execute(request,out var error),Is.True,error);
                yield return PhysicalCreationTestWait.Until(()=>hold.Entered,"Avatar did not reach publication");
                Assert.That(authorActions.Execute(new JObject {["operation"]="cancel",["runId"]=request["runId"].DeepClone()},out error),Is.True,error);
                Assert.That(avatar.ModelHash,Is.Empty);hold.Release();yield return AvatarDrained();
                Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash));int revision=editor.ObjectRevision("maestro");
                for(int i=0;i<5;i++)yield return null;
                Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));
                Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").modelHash,Is.EqualTo(asset.Hash),error);
                Assert.That((string)authorActions.Observe()["selected"]["phase"],Is.EqualTo("cancelled"));
            }finally{hold.Release();editor.AvatarSelectionCheckpoint=null;}
        }
        [UnityTest] public IEnumerator AvatarCompletionCancelBeforePublicationKeepsBothPriorIdentities()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Cancelled.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);
            Assert.That(editor.TryFlush(out var error),Is.True,error);var before=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            using var hold=new HeldAvatarPublication("ready");editor.AvatarSelectionCheckpoint=hold.Check;
            try{
                var request=ModelRequest(asset.Hash);Assert.That(authorActions.Execute(request,out error),Is.True,error);
                yield return PhysicalCreationTestWait.Until(()=>hold.Entered,"Avatar did not reach preflight");
                Assert.That(authorActions.Execute(new JObject {["operation"]="cancel",["runId"]=request["runId"].DeepClone()},out error),Is.True,error);
                hold.Release();yield return AvatarDrained();
                Assert.That(avatar.ModelHash,Is.Empty);Assert.That(editor.Read("maestro").modelHash,Is.Null.Or.Empty);
                Assert.That(File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)),Is.EqualTo(before));
            }finally{hold.Release();editor.AvatarSelectionCheckpoint=null;}
        }
        [UnityTest] public IEnumerator AvatarCompletionPauseCannotCheckpointOldAvatarOverAcceptedSelection()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Paused.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);
            using var hold=new HeldAvatarPublication();editor.AvatarSelectionCheckpoint=hold.Check;
            try{
                Assert.That(editor.SetMaestroModel(asset.Hash),Is.True);
                yield return PhysicalCreationTestWait.Until(()=>hold.Entered,"Avatar did not reach publication");
                editor.SendMessage("OnApplicationPause",true);var lifecycle=editor.LifecycleCompletion;
                Assert.That(lifecycle.IsCompleted,Is.False);Assert.That(avatar.ModelHash,Is.Empty);
                hold.Release();yield return PhysicalCreationTestWait.Until(()=>lifecycle.IsCompleted,"Avatar lifecycle did not drain");yield return AvatarDrained();
                editor.SendMessage("OnApplicationPause",false);
                Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash));
                Assert.That(new RoomStorage(directory).Load(out var error).objects.Single(x=>x.id=="maestro").modelHash,Is.EqualTo(asset.Hash),error);
            }finally{hold.Release();editor.AvatarSelectionCheckpoint=null;editor.SendMessage("OnApplicationPause",false);}
        }
        [UnityTest] public IEnumerator AvatarCompletionUncertainPublicationPreservesLiveAvatarAndBlocksReplay()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Unconfirmed.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);
            editor.AvatarSelectionCheckpoint=value=>{if(value=="published")throw new IOException("Injected after-publication fault");};
                try{
                    Assert.That(authorActions.Execute(ModelRequest(asset.Hash),out var error),Is.True,error);yield return ModelFinished("failed");yield return AvatarDrained();
                    Assert.That(editor.Read("maestro").modelHash,Is.Null.Or.Empty);Assert.That(avatar.ModelHash,Is.Empty);
                    Assert.That(avatar.ModelStatus,Does.Contain("not confirmed"));Assert.That(editor.CanSaveRoom,Is.False);Assert.That((string)ModelFact()["phase"],Is.EqualTo("unavailable"));
                    Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").modelHash,Is.EqualTo(asset.Hash),error);
                    Assert.That(editor.SetMaestroModel(asset.Hash),Is.False);
                    for(int i=0;i<5;i++)yield return null;
                    Assert.That(avatar.ModelHash,Is.Empty);
                }finally{editor.AvatarSelectionCheckpoint=null;}
        }
        [UnityTest] public IEnumerator AvatarCompletionQueuedPhysicalRequestWaitsForItsOwnResult()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Queued.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);
            Assert.That(editor.BeginCreatePrimitive(RoomObjectKind.Block,"blocker",Vector3.up,1,Color.white,out _,out var blocker,out var error),Is.True,error);
            Task<bool> result;
            using(blocker){
                Assert.That(editor.SetMaestroModel(asset.Hash),Is.True,editor.Status);result=editor.AvatarSelectionCompletion;
                Assert.That(result.IsCompleted,Is.False);Assert.That(avatar.ModelBusy,Is.False);Assert.That((bool)ModelFact()["busy"],Is.True);
                Assert.That(editor.SetMaestroModel(null),Is.False);
                editor.Select(editor.Find("book"));yield return null;
            }
            yield return AvatarDrained();Assert.That(result.Result,Is.True);
            Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash));Assert.That(editor.SelectedId,Is.EqualTo("book"));
        }
        [UnityTest] public IEnumerator AvatarCompletionNoOpReceiptRetainsExistingRevision()
        {
            AuthorRuntime();int expected=editor.ObjectRevision("maestro");
            Assert.That(authorActions.Execute(ModelRequest(""),out var error),Is.True,error);yield return ModelFinished();
            Assert.That((int)authorActions.Observe()["selected"]["output"]["revision"],Is.EqualTo(expected));
            Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(expected));
        }

        [UnityTest] public IEnumerator AvatarCompletionDestroyedOwnerDrainsAcceptedPublicationAndLatestCheckpoint()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Closing.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);
            var budgetBefore=ImportedModel.LiveBudget;
            using var hold=new HeldAvatarPublication();editor.AvatarSelectionCheckpoint=hold.Check;
            var gate=editor.WriteGate;var rules=root.GetComponent<Maestro.Quest.Rules.RuleWorkshop>();
            try{
                Assert.That(editor.SetMaestroModel(asset.Hash),Is.True);
                yield return PhysicalCreationTestWait.Until(()=>hold.Entered,"Avatar did not reach publication");
                UnityEngine.Object.Destroy(root);yield return null;
                var retired=gate.Retire();var lifecycle=Task.WhenAll(editor.LifecycleCompletion,rules.LifecycleCompletion);
                Assert.That(retired.IsCompleted,Is.False);Assert.That(lifecycle.IsCompleted,Is.False);
                hold.Release();yield return TestLifecycleDrain.Wait(Task.WhenAll(retired,lifecycle));
                Assert.That(ImportedModel.LiveBudget,Is.EqualTo(budgetBefore),"Destroyed hidden avatar candidate must release its native reservation before workspace retirement");
                Assert.That(new RoomStorage(directory).Load(out var error).objects.Single(x=>x.id=="maestro").modelHash,Is.EqualTo(asset.Hash),error);
            }finally{hold.Release();editor.AvatarSelectionCheckpoint=null;}
        }
        [UnityTest] public IEnumerator AvatarCompletionImportKeepsAcceptedReceiptAndNewerUserSelection()
        {
            ImportRuntime();yield return ChooseModel(ModelFixture.Mixamo());
            using var hold=new HeldAvatarPublication();editor.AvatarSelectionCheckpoint=hold.Check;
            int accepted=0;
            void AfterReady(){
                if(avatar.ModelBusy||avatar.ModelHash.Length==0||accepted!=0)return;
                accepted=editor.ObjectRevision("maestro");
                Assert.That(editor.MoveObject("maestro",Vector3.right,out var error),Is.True,error);
            }
            avatar.ModelChanged+=AfterReady;
            try{
                Assert.That(authorActions.Execute(ImportRequest(ImportAccept("maestro")),out var error),Is.True,error);
                yield return PhysicalCreationTestWait.Until(()=>hold.Entered,"Imported avatar did not reach publication");
                editor.Select(editor.Find("book"));hold.Release();yield return ModelFinished();
                Assert.That(editor.SelectedId,Is.EqualTo("book"));
                Assert.That(accepted,Is.GreaterThan(0));
                Assert.That(editor.ObjectRevision("maestro"),Is.GreaterThan(accepted));
                Assert.That((int)authorActions.Observe()["selected"]["output"]["revision"],Is.EqualTo(accepted));
            }finally{hold.Release();editor.AvatarSelectionCheckpoint=null;avatar.ModelChanged-=AfterReady;}
        }
        [UnityTest] public IEnumerator AvatarCompletionPhysicalPreviewKeepsOtherTargetRecording()
        {
            ImportRuntime();yield return ChooseModel(ModelFixture.Mixamo());
            editor.Select(editor.Find("book"));workshop.ToggleRecord();yield return new WaitForSeconds(.12f);
            var use=imports.UseMaestroAsync();
            yield return PhysicalCreationTestWait.Until(()=>use.IsCompleted,"Imported avatar did not complete");
            Assert.That(use.Result,Is.True,imports.Status);
            Assert.That(workshop.IsRecording,Is.True);Assert.That(workshop.ControlsTarget("book"),Is.True);
            Assert.That(avatar.ModelHash,Is.Not.Empty);
            workshop.ToggleRecord();yield return FinishAuthoringSave();
        }
    }
}
