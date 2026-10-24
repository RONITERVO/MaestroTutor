// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
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
        string PlacementArea(RoomEditor editor,string target)
        {
            string error;string area=RegionFixture.Create(editor);
            Assert.That(editor.BindRegion(target,editor.ObjectRevision(target),area,editor.RegionRevision(area),out error),Is.True,error);
            return area;
        }
        string PlacementTerrain(RoomEditor editor,Vector3 at,out string area)
        {
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Travel ground",at,1,Color.green,out var id,out var error),Is.True,error);
            var data=editor.Read(id);data.physics=ItemPhysics.Fixed;data.motion=null;data.position-=Vector3.up*.1f;
            data.heightFields=new[]{new RoomHeightField{cells=4,width=2,depth=2,maxHeight=.5f,heights=Enumerable.Repeat(.1f,25).ToArray()}};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            area=PlacementArea(editor,id);return id;
        }
        IEnumerator PlacementLoadingDone(RoomEditor editor)
        {
            float deadline=Time.realtimeSinceStartup+12;
            while(editor.NativeActivationPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.NativeActivationPending,Is.False);
        }
        IEnumerator PlacementReceipt(RoomExecutions actions,string phase)
        {
            float deadline=Time.realtimeSinceStartup+12;
            while((string)actions.Observe()["selected"]?["phase"] is "preparing" or "running"&&Time.realtimeSinceStartup<deadline)yield return null;
            var result=actions.Observe();Assert.That((string)result["selected"]?["phase"],Is.EqualTo(phase),result.ToString());
        }
        [UnityTest]public IEnumerator PlacementDemandLoadsTerrainThroughSharedExecutionWithoutAuthoredEdits()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;
            var target=TravelPoint();string id=PlacementTerrain(editor,target.position,out var area);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            Assert.That(view.Enter(),Is.True);int revision=editor.Revision;var objects=editor.Snapshot().objects.Select(JsonUtility.ToJson).ToArray();
            var frame=room.transform.position;var actions=new RoomExecutions(editor,host);var request=TravelRequest(actions,target);
            Assert.That(actions.Execute(request,out error),Is.True,error);Assert.That((string)actions.Observe()["selected"]["phase"],Is.EqualTo("preparing"));
            Assert.That(room.transform.position,Is.EqualTo(frame));
            yield return PlacementReceipt(actions,"completed");
            Assert.That(editor.Find(id),Is.Not.Null);Assert.That(view.ReadViewpoint(out var actual),Is.True);Assert.That(Vector3.Distance(actual.position,target.position),Is.LessThan(.001));
            Assert.That(editor.Revision,Is.EqualTo(revision));CollectionAssert.AreEqual(objects,editor.Snapshot().objects.Select(JsonUtility.ToJson));
            Assert.That(editor.ReadRetention().Reasons(id)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));
            Assert.That(actions.Execute(request,out error),Is.True,error);Assert.That(editor.Revision,Is.EqualTo(revision));
        }
        [UnityTest]public IEnumerator PlacementDemandDirectCompletionPreparesDormantGroundBeforeSaving()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;
            var target=TravelPoint();string id=PlacementTerrain(editor,target.position,out var area);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);Assert.That(view.Enter(),Is.True);
            var frame=room.transform.position;int revision=editor.Revision;
            Assert.That(WorldViewpointCapability.StartPlacement(editor,null,target,true,out var operation,out error),Is.True,error);
            try {
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                Assert.That(room.transform.position,Is.EqualTo(frame));
                yield return WaitPlacement(operation,RuleActionState.Ready);Assert.That(operation.Complete(out error),Is.True,error);
                Assert.That(editor.Find(id),Is.Not.Null);Assert.That(view.ReadViewpoint(out var actual),Is.True);
                Assert.That(Vector3.Distance(actual.position,target.position),Is.LessThan(.001));Assert.That(editor.Revision,Is.EqualTo(revision));
                Assert.That(editor.ReadRetention().Reasons(id)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));
            }finally{operation.Stop(false);}
        }
        [UnityTest]public IEnumerator PlacementDemandLoadsDormantObstacleAndRefusesOccupiedDestination()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;
            var target=TravelPoint();Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Unloaded wall",target.position+Vector3.up,2,Color.blue,out var id,out var error),Is.True,error);
            string area=PlacementArea(editor,id);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            var frame=room.transform.position;var saved=JsonUtility.ToJson(editor.Snapshot().viewpoint);int revision=editor.Revision;
            Assert.That(editor.CanSetWorldViewpoint(null,target,true,out error),Is.False);Assert.That(error,Does.Contain("Prepare"));
            var actions=new RoomExecutions(editor,host);Assert.That(actions.Execute(TravelRequest(actions,target),out error),Is.True,error);
            yield return PlacementReceipt(actions,"failed");Assert.That((string)actions.Observe()["selected"]["status"],Does.Contain("occupied"));
            Assert.That(editor.Find(id),Is.Not.Null);Assert.That(room.transform.position,Is.EqualTo(frame));Assert.That(editor.Revision,Is.EqualTo(revision));
            Assert.That(JsonUtility.ToJson(editor.Snapshot().viewpoint),Is.EqualTo(saved));
        }
        [UnityTest]public IEnumerator PlacementDemandManualOriginPreparesGroundAndRecoveryCancelsPendingPlacement()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;
            string id=PlacementTerrain(editor,Vector3.zero,out var area);var target=TravelPoint();
            string error;yield return PlaceWorld(editor,null,target,true);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);Assert.That(view.Enter(),Is.True);
            var controls=host.Current.Controls;var frame=room.transform.position;
            controls.ReturnToWorldOrigin();Assert.That(editor.NativeActivationPending,Is.True);controls.Recover();string recovered=controls.Status;
            yield return PlacementLoadingDone(editor);yield return null;Assert.That(room.transform.position,Is.EqualTo(frame));
            Assert.That(controls.Status,Is.EqualTo(recovered),"Cancelled preparation must not overwrite newer recovery feedback");
            Assert.That(view.Enter(),Is.True);controls.ReturnToWorldOrigin();
            yield return PlacementLoadingDone(editor);yield return WaitOrigin(controls);
            Assert.That(editor.Find(id),Is.Not.Null);Assert.That(view.ReadViewpoint(out var origin),Is.True);Assert.That(origin.position.magnitude,Is.LessThan(.001),controls.Status);
        }
        [UnityTest]public IEnumerator PlacementDemandStaleGuardDoesNotLoadDestination()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;
            var target=TravelPoint();string id=PlacementTerrain(editor,target.position,out var area);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            var actions=new RoomExecutions(editor,host);var request=TravelRequest(actions,target);request["call"]["arguments"]["stateId"]=new string('0',32);
            Assert.That(actions.Execute(request,out error),Is.False);Assert.That(error,Does.Contain("changed"));
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest]public IEnumerator PlacementDemandRechecksNewObstacleAfterLoadingBeforeEffect()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;
            var target=TravelPoint();string id=PlacementTerrain(editor,target.position,out var area);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            Assert.That(view.Enter(),Is.True);
            var actions=new RoomExecutions(editor,host);var call=(JObject)TravelRequest(actions,target)["call"];
            var scheduler=new RuleScheduler(new RoomRuleActions(editor,null));var frame=room.transform.position;
            try {
                Assert.That(scheduler.Invoke(call,Time.unscaledTime,out var run,out error),Is.True,error);
                yield return PlacementLoadingDone(editor);
                Assert.That(editor.ReadRetention().Reasons(id)&RoomRetentionReason.Ownership,Is.Not.EqualTo(RoomRetentionReason.None));
                Assert.That(editor.RetireNativeArea(area,out error),Is.False,"Preparation retains geometry until the effect or cancellation");
                Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"New obstruction",target.position+Vector3.up,2,Color.red,out _,out error),Is.True,error);
                scheduler.Tick(Time.unscaledTime);Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));
                Assert.That((string)scheduler.Invocation(run)["status"],Does.Contain("changed before"));Assert.That(room.transform.position,Is.EqualTo(frame));
                Assert.That(editor.ReadRetention().Reasons(id)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));
            } finally {scheduler.StopAll();}
        }
        [UnityTest]public IEnumerator PlacementDemandLeavesKnownDistantAreaDormant()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;
            string far=PlacementTerrain(editor,new Vector3(-10,0,-10),out var farArea);
            string near=PlacementTerrain(editor,TravelPoint().position,out var nearArea);
            Assert.That(editor.RetireNativeArea(farArea,out var error),Is.True,error);Assert.That(editor.RetireNativeArea(nearArea,out error),Is.True,error);
            Assert.That(view.Enter(),Is.True);var actions=new RoomExecutions(editor,host);
            Assert.That(actions.Execute(TravelRequest(actions,TravelPoint()),out error),Is.True,error);yield return PlacementReceipt(actions,"completed");
            Assert.That(editor.Find(near),Is.Not.Null);Assert.That(editor.Find(far),Is.Null);
        }
    }
    public sealed partial class RoomRulesTests
    {
        [UnityTest]public IEnumerator NativeDemandRetainsResidentInputsUntilLastOwnerReleasesWithoutLockingEdits()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);var targets=new[]{id};
            var first=editor.DemandNativeEntities(targets,()=>targets,()=>true);var second=editor.DemandNativeEntities(targets,()=>targets,()=>true);
            try {
                Assert.That(first.Preparation.IsCompleted&&second.Preparation.IsCompleted,Is.True);Assert.That(first.Error,Is.Null);
                Assert.That(editor.RetireNativeArea(area,out var error),Is.False,error);first.Dispose();Assert.That(editor.RetireNativeArea(area,out error),Is.False);
                var data=editor.Read(id);data.name="Human can still edit";Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
                Assert.That(second.Error,Does.Contain("changed before"));
            } finally {first.Dispose();second.Dispose();}
            Assert.That(editor.RetireNativeArea(area,out var finalError),Is.True,finalError);yield return null;
        }
    }
}
