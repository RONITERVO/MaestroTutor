// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        JObject lastPlacementReceipt;
        string lastPlacementError;
        IEnumerator PlaceWorld(RoomEditor editor,string expected,RoomViewpoint target,bool manual)
        {
            Assert.That(WorldViewpointCapability.StartPlacement(editor,expected,target,manual,out var operation,out var error),Is.True,error);
            try {
                yield return WaitPlacement(operation,RuleActionState.Ready);
                Assert.That(operation.Complete(out error),Is.True,error);lastPlacementReceipt=operation.Result;
            }finally{operation.Stop(false);}
        }
        IEnumerator WaitPlacement(CapabilityOperation operation,RuleActionState expected)
        {
            float deadline=Time.realtimeSinceStartup+12;var state=operation.State(out lastPlacementError);
            while(state==RuleActionState.Preparing&&Time.realtimeSinceStartup<deadline){
                yield return null;state=operation.State(out lastPlacementError);
            }
            Assert.That(state,Is.EqualTo(expected),lastPlacementError);
        }
        IEnumerator WaitOrigin(MovementControls controls)
        {
            float deadline=Time.realtimeSinceStartup+12;
            while(controls.OriginPlacementPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(controls.OriginPlacementPending,Is.False,controls.Status);
        }
        sealed class PlacementPublicationHold:IDisposable
        {
            readonly ManualResetEventSlim release=new(false);
            readonly string stopAt;
            internal PlacementPublicationHold(string stopAt="queued"){this.stopAt=stopAt;}
            readonly TaskCompletionSource<bool> entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal bool Entered=>entered.Task.IsCompleted;
            internal void Checkpoint(string stage){
                if(stage!=stopAt)return;
                entered.TrySetResult(true);
                if(!release.Wait(10000))throw new TimeoutException("Placement publication watchdog expired");
            }
            internal void Release()=>release.Set();
            public void Dispose()=>Release();
        }
        sealed class PlacementModelBarrier:UniGLTF.IAwaitCaller
        {
            readonly TaskCompletionSource<bool> release=new();
            internal bool Entered;
            internal void Open()=>release.TrySetResult(true);
            async Task Wait(){Entered=true;await release.Task;}
            public Task NextFrame()=>Wait();
            public Task NextFrameIfTimedOut()=>Wait();
            public async Task Run(Action action){await Wait();action();}
            public async Task<T> Run<T>(Func<T> action){await Wait();return action();}
        }
        [UnityTest]public IEnumerator WorldPlacementWaitsForResidentImportThenRejectsItsOccupiedDestination()=>PlacementResidentImport(false);
        [UnityTest]public IEnumerator WorldPlacementCancelledDuringResidentImportCannotMoveAfterGeometryArrives()=>PlacementResidentImport(true);
        IEnumerator PlacementResidentImport(bool cancel)
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;
            var asset=Maestro.Quest.Imports.ModelLibrary.Inspect("placement.glb",StaticModelFixture.Create());
            var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var child=new GameObject("Held placement import");child.transform.SetParent(root.transform,false);
            var blocker=child.AddComponent<Maestro.Quest.Imports.ImportedModel>();var barrier=new PlacementModelBarrier();
            var blocked=blocker.LoadAsync(asset,barrier);
            try {
                float deadline=Time.realtimeSinceStartup+12;
                while(!barrier.Entered&&!blocked.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(barrier.Entered,Is.True);
                var target=TravelPoint();Assert.That(editor.CreateImportedModel(asset.Hash,out var id,out var error),Is.True,error);
                var data=editor.Read(id);data.position=target.position+Vector3.up;data.scale=2;
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
                var model=editor.Find(id).GetComponent<CreatedRoomObject>();Assert.That(model.ModelLoading,Is.True);
                CollectionAssert.Contains(editor.WorldPlacementTargets(target),id);
                var frame=room.transform.position;var bookmark=JsonUtility.ToJson(editor.Snapshot().viewpoint);int revision=editor.Revision;
                var actions=new RoomExecutions(editor,host);var request=TravelRequest(actions,target);
                Assert.That(actions.Execute(request,out error),Is.True,error);
                for(int i=0;i<5;i++){yield return null;Assert.That((string)actions.Observe()["selected"]["phase"],Is.EqualTo("preparing"));}
                Assert.That(room.transform.position,Is.EqualTo(frame));Assert.That(JsonUtility.ToJson(editor.Snapshot().viewpoint),Is.EqualTo(bookmark));
                if(cancel)Assert.That(actions.Execute(new JObject {["operation"]="cancel",["runId"]=request["runId"].DeepClone()},out error),Is.True,error);
                barrier.Open();blocker.Dispose();yield return new WaitUntil(()=>blocked.IsCompleted);Assert.That(blocked.Exception,Is.Null);
                deadline=Time.realtimeSinceStartup+12;while(model.ModelLoading&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(model.ModelGeometryReady,Is.True,model.ModelGeometryIssue);
                yield return PlacementReceipt(actions,cancel?"cancelled":"failed");
                if(!cancel)Assert.That((string)actions.Observe()["selected"]["status"],Does.Contain("occupied"));
                Assert.That(room.transform.position,Is.EqualTo(frame));Assert.That(editor.Revision,Is.EqualTo(revision));
                Assert.That(JsonUtility.ToJson(editor.Snapshot().viewpoint),Is.EqualTo(bookmark));
                Assert.That(editor.ReadRetention().Reasons(id)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));
            }finally{barrier.Open();blocker.Dispose();}
        }
        [UnityTest]public IEnumerator WorldPlacementWaitsForEarlierSaveAndUsesLatestPhysicalHeadWithoutChangingGeometry()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;Assert.That(editor.TryFlush(out var error),Is.True,error);
            using var earlier=new DelayedAcceptedSave(editor);var target=TravelPoint();var frame=room.transform.position;int revision=editor.Revision;
            Assert.That(WorldViewpointCapability.StartPlacement(editor,null,target,true,out var operation,out error),Is.True,error);
            try {
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                Assert.That(editor.LiquidCheckpointWait,Does.Contain("preparing"));
                yield return null;Assert.That(earlier.Released,Is.False);Assert.That(room.transform.position,Is.EqualTo(frame));
                var physical=new Vector3(-1,1.8f,1.5f);var heading=Quaternion.Euler(0,80,0);room.Viewer.SetPositionAndRotation(physical,heading);heading=room.Viewer.rotation;
                earlier.Release();yield return WaitPlacement(operation,RuleActionState.Ready);
                Assert.That(operation.Complete(out error),Is.True,error);
                Assert.That(room.Viewer.position,Is.EqualTo(physical));Assert.That(room.Viewer.rotation,Is.EqualTo(heading));
                Assert.That(view.ReadViewpoint(out var actual),Is.True);Assert.That(Vector3.Distance(actual.position,target.position),Is.LessThan(.001));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(actual.yaw,target.yaw)),Is.LessThan(.001));Assert.That(editor.Revision,Is.EqualTo(revision));
                Assert.That(JsonUtility.ToJson(new RoomStorage(editor.SaveDirectory).Load(out error).viewpoint),Is.EqualTo(JsonUtility.ToJson(target)),error);
            }finally{operation.Stop(false);}
        }
        [UnityTest]public IEnumerator WorldPlacementCancelledBeforeAcceptanceNeverMovesAfterEarlierSaveFinishes()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;Assert.That(editor.TryFlush(out var error),Is.True,error);
            using var earlier=new DelayedAcceptedSave(editor);var frame=room.transform.position;var bookmark=JsonUtility.ToJson(editor.Snapshot().viewpoint);
            Assert.That(WorldViewpointCapability.StartPlacement(editor,null,TravelPoint(),true,out var operation,out error),Is.True,error);
            Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);operation.Stop(false);earlier.Release();yield return null;
            Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(operation.InterruptionStatus,Is.Null);
            Assert.That(room.transform.position,Is.EqualTo(frame));Assert.That(JsonUtility.ToJson(editor.Snapshot().viewpoint),Is.EqualTo(bookmark));
            Assert.That(editor.PendingEditSave,Is.False);using var frozen=editor.WriteGate.TryFreeze(out error);Assert.That(frozen,Is.Not.Null,error);
        }
        [UnityTest]public IEnumerator WorldPlacementTrackingLossWhileWaitingFailsWithoutLateMovement()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;Assert.That(editor.TryFlush(out var error),Is.True,error);
            using var earlier=new DelayedAcceptedSave(editor);var frame=room.transform.position;
            Assert.That(WorldViewpointCapability.StartPlacement(editor,null,TravelPoint(),true,out var operation,out error),Is.True,error);
            try {
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);recoveryHeadTracked=false;
                yield return WaitPlacement(operation,RuleActionState.Failed);earlier.Release();recoveryHeadTracked=true;yield return null;
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(room.transform.position,Is.EqualTo(frame));
            }finally{operation.Stop(false);}
        }
        [UnityTest]public IEnumerator WorldPlacementChangedDestinationWhileWaitingFailsBeforeAcceptance()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;var target=TravelPoint();string id=PlacementTerrain(editor,target.position,out _);
            Assert.That(editor.TryFlush(out var error),Is.True,error);using var earlier=new DelayedAcceptedSave(editor);var frame=room.transform.position;
            Assert.That(WorldViewpointCapability.StartPlacement(editor,null,target,true,out var operation,out error),Is.True,error);
            try {
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                Assert.That(editor.MoveObject(id,target.position+Vector3.up,out error),Is.True,error);
                yield return WaitPlacement(operation,RuleActionState.Failed);earlier.Release();yield return null;
                Assert.That(room.transform.position,Is.EqualTo(frame));Assert.That(operation.InterruptionStatus,Is.Null);
            }finally{operation.Stop(false);}
        }
        [UnityTest]public IEnumerator WorldPlacementAcceptedSaveContinuesAfterStopAndDoesNotOverwriteLaterWalking()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;Assert.That(editor.TryFlush(out var error),Is.True,error);
            using var hold=new PlacementPublicationHold();var target=TravelPoint();
            Assert.That(WorldViewpointCapability.StartPlacement(editor,null,target,true,out var operation,out error,hold.Checkpoint),Is.True,error);
            try {
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                float deadline=Time.realtimeSinceStartup+5;while(!hold.Entered&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(hold.Entered,Is.True);Assert.That(operation.InterruptionStatus,Does.Contain("accepted"));Assert.That(editor.LiquidCheckpointWait,Is.Null);
                Assert.That(view.ReadViewpoint(out var actual),Is.True);Assert.That(Vector3.Distance(actual.position,target.position),Is.LessThan(.001));
                Assert.That(editor.WriteGate.TryFreeze(out _),Is.Null,"Accepted publication keeps its write lease");
                operation.Stop(false);Assert.That(editor.PendingEditSave,Is.True);
                // Real physical movement changes the bookmark without another world relocation.
                room.Viewer.position+=Vector3.right*.3f;
                editor.GetComponent<WorkspaceViewpoint>().SendMessage("OnApplicationFocus",false);
                var later=editor.Snapshot().viewpoint.Copy();Assert.That(Vector3.Distance(later.position,target.position),Is.GreaterThan(.1f));
                hold.Release();deadline=Time.realtimeSinceStartup+10;
                while(editor.PendingEditSave&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(editor.PendingEditSave,Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot().viewpoint),Is.EqualTo(JsonUtility.ToJson(later)));
                Assert.That(editor.TryFlush(out error),Is.True,error);
                Assert.That(JsonUtility.ToJson(new RoomStorage(editor.SaveDirectory).Load(out error).viewpoint),Is.EqualTo(JsonUtility.ToJson(later)),error);
            }finally{hold.Release();operation.Stop(false);}
        }
        [UnityTest]public IEnumerator WorldPlacementCompletionReturnsAcceptanceLocationAfterPhysicalMovement()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;Assert.That(editor.TryFlush(out var error),Is.True,error);
            using var hold=new PlacementPublicationHold();var target=TravelPoint();var requested=target.Copy();
            Assert.That(WorldViewpointCapability.StartPlacement(editor,null,target,true,out var operation,out error,hold.Checkpoint),Is.True,error);
            try {
                target.position=Vector3.one;target.yaw=-45;
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                float deadline=Time.realtimeSinceStartup+5;while(!hold.Entered&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(hold.Entered,Is.True);
                room.Viewer.position+=Vector3.right*.4f;var physical=room.Viewer.position;
                Assert.That(view.ReadViewpoint(out var walking),Is.True);Assert.That(Vector3.Distance(walking.position,requested.position),Is.GreaterThan(.1f));
                hold.Release();yield return WaitPlacement(operation,RuleActionState.Ready);Assert.That(operation.Complete(out error),Is.True,error);
                Assert.That(JToken.DeepEquals(operation.Result["position"],WorkspaceViewpoint.Point(requested.position)),Is.True);
                Assert.That((float)operation.Result["yaw"],Is.EqualTo(requested.yaw));Assert.That(room.Viewer.position,Is.EqualTo(physical));
                var frame=room.transform.position;Assert.That(operation.Complete(out error),Is.True,error);Assert.That(room.transform.position,Is.EqualTo(frame));
            }finally{hold.Release();operation.Stop(false);}
        }
        [UnityTest]public IEnumerator WorldPlacementWaitsBehindAcceptedObjectEditAndSavesBothResults()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Retained paint",new Vector3(-2,1,3),1,Color.red,out var id,out var error),Is.True,error);
            Assert.That(editor.TryFlush(out error),Is.True,error);using var hold=new PlacementPublicationHold("publishing");
            Assert.That(PersistedEditOperation.Start(editor,id,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>editor.BeginPaint(id,Color.blue,out edit,out issue,hold.Checkpoint),
                out var paint,out error),Is.True,error);
            CapabilityOperation placement=null;
            try {
                float deadline=Time.realtimeSinceStartup+8;while(!hold.Entered&&Time.realtimeSinceStartup<deadline){Assert.That(paint.State(out error),Is.EqualTo(RuleActionState.Preparing),error);yield return null;}
                Assert.That(hold.Entered,Is.True);var frame=room.transform.position;
                Assert.That(WorldViewpointCapability.StartPlacement(editor,null,TravelPoint(),true,out placement,out error),Is.True,error);
                Assert.That(placement.State(out error),Is.EqualTo(RuleActionState.Preparing),error);Assert.That(room.transform.position,Is.EqualTo(frame));
                hold.Release();yield return WaitPlacement(paint,RuleActionState.Ready);Assert.That(paint.Complete(out error),Is.True,error);
                yield return WaitPlacement(placement,RuleActionState.Ready);Assert.That(placement.Complete(out error),Is.True,error);
                var saved=new RoomStorage(editor.SaveDirectory).Load(out error);Assert.That(error,Is.Null);
                Assert.That(JsonUtility.ToJson(saved.viewpoint),Is.EqualTo(JsonUtility.ToJson(TravelPoint())));
                Assert.That(System.Array.Find(saved.objects,x=>x.id==id).color,Is.EqualTo(editor.Read(id).color));
            }finally{hold.Release();paint.Stop(false);placement?.Stop(false);}
        }
        [UnityTest]public IEnumerator WorldPlacementRetirementOwnsAcceptedSaveAfterUnityOwnerIsDestroyed()
        {
            SetupTravel();yield return ReadyHost();var content=host.Current;var editor=content.Editor;Assert.That(editor.TryFlush(out var error),Is.True,error);
            string directory=editor.SaveDirectory;using var hold=new PlacementPublicationHold();
            Assert.That(WorldViewpointCapability.StartPlacement(editor,null,TravelPoint(),true,out var operation,out error,hold.Checkpoint),Is.True,error);
            try {
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                float deadline=Time.realtimeSinceStartup+5;while(!hold.Entered&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(hold.Entered,Is.True);
                var retired=content.Retire();for(int i=0;i<3;i++)yield return null;
                Assert.That((bool)editor,Is.False);Assert.That(retired.IsCompleted,Is.False,"Retiring workspace still owns its accepted bookmark");
                hold.Release();yield return TestLifecycleDrain.Wait(retired);
                Assert.That(JsonUtility.ToJson(new RoomStorage(directory).Load(out error).viewpoint),Is.EqualTo(JsonUtility.ToJson(TravelPoint())),error);
            }finally{hold.Release();operation.Stop(false);}
        }
    }
}
