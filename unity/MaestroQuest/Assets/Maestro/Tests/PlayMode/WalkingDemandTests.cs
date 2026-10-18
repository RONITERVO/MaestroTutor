// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        [UnityTest]public IEnumerator WalkingDemandLoadsUpperFloorWithoutDroppingToResidentLowerGround()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;
            var target=TravelPoint();string upper=PlacementTerrain(editor,target.position,out var area);
            PlacementTerrain(editor,target.position-Vector3.up*.12f,out _);
            string error;yield return PlaceWorld(editor,null,target,true);
            // This test supplies movement directly; do not also run the neutral hardware loop.
            host.Current.Controls.enabled=false;
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);int revision=editor.Revision;
            var frame=room.transform.position;var step=room.transform.TransformDirection(Vector3.right*.02f);
            Assert.That(view.Move(step),Is.False);Assert.That(view.MovementError,Does.Contain("Preparing"));Assert.That(room.transform.position,Is.EqualTo(frame));
            yield return PlacementLoadingDone(editor);Assert.That(room.transform.position,Is.EqualTo(frame),"Loading never queues a displacement");
            Assert.That(view.Move(step),Is.True,view.MovementError);Assert.That(view.ReadViewpoint(out var after),Is.True);
            Assert.That(after.position.y,Is.EqualTo(0).Within(.001));Assert.That(after.position.x,Is.EqualTo(target.position.x+.02f).Within(.001));
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.RetireNativeArea(area,out error),Is.False);
            Assert.That(editor.ReadRetention().Reasons(upper)&RoomRetentionReason.Ownership,Is.Not.EqualTo(RoomRetentionReason.None));
            view.Move(Vector3.zero);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
        }
        [UnityTest]public IEnumerator WalkingDemandLoadsDormantWallBeforeTestingActualClearance()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;var target=TravelPoint();
            PlacementTerrain(editor,target.position,out _);string error;yield return PlaceWorld(editor,null,target,true);
            // This test supplies movement directly; do not also run the neutral hardware loop.
            host.Current.Controls.enabled=false;
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Wall across walking body",target.position+Vector3.up,2,Color.red,out var id,out error),Is.True,error);
            string area=PlacementArea(editor,id);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            var frame=room.transform.position;var step=room.transform.TransformDirection(Vector3.right*.02f);
            Assert.That(view.Move(step),Is.False);Assert.That(view.MovementError,Does.Contain("Preparing"));
            yield return PlacementLoadingDone(editor);Assert.That(editor.Find(id),Is.Not.Null);
            Assert.That(view.Move(step),Is.False);Assert.That(view.MovementError,Does.Contain("occupied"));Assert.That(room.transform.position,Is.EqualTo(frame));
            view.EndWalking(editor);
        }
        [UnityTest]public IEnumerator WalkingDemandNeutralControlsCancelPendingWorkWithoutLateMovement()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;var target=TravelPoint();
            string id=PlacementTerrain(editor,target.position,out var area);string error;yield return PlaceWorld(editor,null,target,true);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);var frame=room.transform.position;
            Assert.That(view.Move(room.transform.TransformDirection(Vector3.right*.02f)),Is.False);Assert.That(editor.NativeActivationPending,Is.True);
            // Real controller-loop neutral input, not disposal of a test-only owner.
            host.Current.Controls.Tick(.01f);yield return PlacementLoadingDone(editor);yield return null;
            Assert.That(room.transform.position,Is.EqualTo(frame));Assert.That(editor.Find(id),Is.Null);
            Assert.That(editor.ReadRetention().Reasons(id)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));
        }
        [UnityTest]public IEnumerator WalkingDemandInactiveGeometryCannotProveClearanceAndNeedsFreshInput()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;var target=TravelPoint();
            string id=PlacementTerrain(editor,target.position,out _);string error;yield return PlaceWorld(editor,null,target,true);
            Assert.That(view.Enter(),Is.True);
            var item=editor.Find(id);item.gameObject.SetActive(false);var step=room.transform.TransformDirection(Vector3.right*.02f);
            Assert.That(view.Move(step),Is.False);Assert.That(view.MovementError,Does.Contain("unavailable"));
            Assert.That(WorldViewpointCapability.StartPlacement(editor,null,target,true,out _,out error),Is.False);Assert.That(error,Does.Contain("unavailable"));
            item.gameObject.SetActive(true);Assert.That(view.Move(step),Is.False,"Holding a failed input must not silently retry");
            view.Move(Vector3.zero);Assert.That(view.Move(step),Is.True,view.MovementError);
        }
        [UnityTest]public IEnumerator WalkingDemandCoversRaisedBodyCorridorButLeavesKnownDistantAreaDormant()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;var target=TravelPoint();
            PlacementTerrain(editor,target.position,out _);string error;yield return PlaceWorld(editor,null,target,true);
            // This test supplies movement directly; do not also run the neutral hardware loop.
            host.Current.Controls.enabled=false;
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Possible raised-step obstacle",target.position+Vector3.up*2.15f,1,Color.red,out var overhead,out error),Is.True,error);
            string near=PlacementArea(editor,overhead),farId=PlacementTerrain(editor,new Vector3(-10,0,-10),out var far);
            Assert.That(editor.RetireNativeArea(near,out error),Is.True,error);Assert.That(editor.RetireNativeArea(far,out error),Is.True,error);
            var step=room.transform.TransformDirection(Vector3.right*.09f);Assert.That(view.Move(step),Is.False);
            yield return PlacementLoadingDone(editor);Assert.That(editor.Find(overhead),Is.Not.Null);Assert.That(editor.Find(farId),Is.Null);
            Assert.That(view.Move(step),Is.True,"Exact ground/body query after conservative preparation: "+view.MovementError);view.EndWalking(editor);
        }
        [UnityTest]public IEnumerator WalkingDemandFocusLossCancelsLoadingWithoutLateMovement()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;var target=TravelPoint();
            string id=PlacementTerrain(editor,target.position,out var area);string error;yield return PlaceWorld(editor,null,target,true);
            host.Current.Controls.enabled=false;
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);var frame=room.transform.position;
            var step=room.transform.TransformDirection(Vector3.right*.02f);Assert.That(view.Move(step),Is.False);
            editor.GetComponent<WorkspaceViewpoint>().SendMessage("OnApplicationFocus",false);
            yield return PlacementLoadingDone(editor);yield return null;
            Assert.That(view.Move(step),Is.False);Assert.That(room.transform.position,Is.EqualTo(frame));Assert.That(editor.Find(id),Is.Null);
            Assert.That(editor.ReadRetention().Reasons(id)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));
        }
        [UnityTest]public IEnumerator WalkingDemandInvalidInputReleasesResidentGeometry()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;room.Viewer.GetComponent<Camera>().enabled=false;var target=TravelPoint();
            string id=PlacementTerrain(editor,target.position,out var area);string error;yield return PlaceWorld(editor,null,target,true);
            Assert.That(view.Move(room.transform.TransformDirection(Vector3.right*.02f)),Is.True,view.MovementError);
            Assert.That(editor.RetireNativeArea(area,out error),Is.False);
            Assert.That(view.Move(new Vector3(float.NaN,0,0)),Is.False);
            Assert.That(editor.ReadRetention().Reasons(id)&RoomRetentionReason.Ownership,Is.EqualTo(RoomRetentionReason.None));
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
        }
        [UnityTest]public IEnumerator WalkingDemandUnboundPersistentViewCannotUseStandaloneFallback()
        {
            SetupTravel();yield return ReadyHost();var editor=host.Current.Editor;var target=TravelPoint();
            PlacementTerrain(editor,target.position,out _);yield return PlaceWorld(editor,null,target,true);
            view.UnbindWalking(editor);var frame=room.transform.position;
            Assert.That(view.Move(room.transform.TransformDirection(Vector3.right*.02f)),Is.False);Assert.That(view.MovementError,Does.Contain("workspace"));Assert.That(room.transform.position,Is.EqualTo(frame));
        }
    }
    public sealed partial class RoomRulesTests
    {
        [UnityTest]public IEnumerator WalkingDemandTimeoutCancelsAcquisitionAndDoesNotRetryHeldInput()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);var at=block.transform.localPosition;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);float now=0;
            using var walking=new RoomWalkingPreparation(editor,()=>true,()=>now);
            Assert.That(walking.Ready(at,at+Vector3.right*.02f,.2f,1.7f,out error),Is.False);Assert.That(editor.NativeActivationPending,Is.True);
            now=31;Assert.That(walking.Ready(at,at+Vector3.right*.02f,.2f,1.7f,out error),Is.False);Assert.That(error,Does.Contain("timed out"));
            float deadline=Time.realtimeSinceStartup+8;while(editor.NativeActivationPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(id),Is.Null);
            Assert.That(walking.Ready(at,at+Vector3.right*.02f,.2f,1.7f,out error),Is.False);Assert.That(editor.NativeActivationPending,Is.False);
        }
        [UnityTest]public IEnumerator WalkingDemandFailedImportRetriesOnlyAfterInputEnds()
        {
            yield return GeometryModel(BuildingModelFixture.Create());string id=geometryTarget,area=NativeAreaFor(id);
            var at=editor.ReadSpatialBounds(id,out _).Collision.Bounds.center;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            string path=Path.Combine(directory,"models",editor.Read(id).modelHash+".glb"),parked=path+".walking-test-missing";File.Move(path,parked);
            using var walking=new RoomWalkingPreparation(editor,()=>true);
            try {
                Assert.That(walking.Ready(at,at+Vector3.right*.02f,.2f,1.7f,out error),Is.False);
                float deadline=Time.realtimeSinceStartup+8;while(editor.NativeActivationPending&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(editor.NativeActivationPending,Is.False);Assert.That(walking.Ready(at,at+Vector3.right*.02f,.2f,1.7f,out error),Is.False);Assert.That(error,Does.Contain("release"));
                File.Move(parked,path);
                for(int i=0;i<3;i++){Assert.That(walking.Ready(at,at+Vector3.right*.02f,.2f,1.7f,out error),Is.False);Assert.That(editor.NativeActivationPending,Is.False);yield return null;}
                walking.End();walking.Ready(at,at+Vector3.right*.02f,.2f,1.7f,out error);
                // Activation completion can precede the demand continuation and
                // narrowing of the conservative dependency set by a frame.
                deadline=Time.realtimeSinceStartup+8;bool ready=false;
                while(!(ready=walking.Ready(at,at+Vector3.right*.02f,.2f,1.7f,out error))&&Time.realtimeSinceStartup<deadline){
                    Assert.That(error,Does.Not.Contain("release the movement stick"),error);yield return null;
                }
                Assert.That(ready,Is.True,error);Assert.That(editor.Find(id),Is.Not.Null);
            } finally {if(File.Exists(parked))File.Move(parked,path);}
        }
    }
}
