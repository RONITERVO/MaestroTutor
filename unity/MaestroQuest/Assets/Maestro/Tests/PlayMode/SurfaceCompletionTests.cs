// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
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
    public sealed partial class AvatarSpatialTests
    {
        sealed class HeldSurfacePublication:IDisposable
        {
            readonly ManualResetEventSlim release=new(false);
            internal volatile bool Entered;
            internal void Check(string phase){
                if(phase!="publishing")return;Entered=true;
                if(!release.Wait(TimeSpan.FromSeconds(20)))throw new TimeoutException("Surface publication was not released");
            }
            internal void Release()=>release.Set();
            public void Dispose()=>Release();
        }
        PhysicsTools SurfaceCompletionTray(ScannedRoom scan,string target)
        {
            var tray=new GameObject("Surface completion tray");tray.transform.SetParent(root.transform,false);
            var tools=tray.AddComponent<PhysicsTools>();tools.Build(editor,world,scan,room);editor.Select(editor.Find(target));return tools;
        }
        IEnumerator ArmSurfaceCompletion(PhysicsTools tools)
        {
            tools.GetComponentsInChildren<RuleToolAction>().Single(x=>x.AccessibleName=="Place surface").Command();
            yield return Until(()=>tools.Placing);
        }
        [UnityTest] public IEnumerator SurfaceCompletionPhysicalWaitsForPublication()
        {
            var scan=PlacementRoom(out _,out var target);var tools=SurfaceCompletionTray(scan,target);
            yield return ArmSurfaceCompletion(tools);
            using var hold=new HeldSurfacePublication();editor.ManualCreationCheckpoint=hold.Check;
            try{
                tools.Place(new Ray(new Vector3(2,1.5f,2),Vector3.down));
                Assert.That(editor.ManualCreationPending,Is.True,"Physical placement must wait for shared saved completion");
                yield return PhysicalCreationTestWait.Until(()=>hold.Entered,"Placement never reached the publication worker");
                editor.Select(editor.Find("book"));hold.Release();yield return PhysicalCreationTestWait.Finish(editor);
                Assert.That(editor.SelectedId,Is.EqualTo("book"));
                Physics.SyncTransforms();ScannedRoom.Bounds(editor.Find(target),out var bounds);
                Assert.That(bounds.min.y,Is.EqualTo(.01f).Within(.001f));Assert.That(bounds.center.x,Is.EqualTo(2).Within(.001f));
            }finally{hold.Release();editor.ManualCreationCheckpoint=null;}
        }
        [UnityTest] public IEnumerator SurfaceCompletionPhysicalLeavesUnrelatedProgramRunning()
        {
            var scan=PlacementRoom(out _,out var target);var tools=SurfaceCompletionTray(scan,target);yield return ArmSurfaceCompletion(tools);
            var wait=new JObject{["id"]="time.wait",["version"]=1,["arguments"]=new JObject{["seconds"]=20}};
            Assert.That(modeRules.Scheduler.Invoke(wait,Time.unscaledTime,out var run,out var error),Is.True,error);
            tools.Place(new Ray(new Vector3(2,1.5f,2),Vector3.down));
            Assert.That((string)modeRules.Scheduler.Invocation(run)["phase"],Is.EqualTo("running"),"Placing one object must not stop another program");
            yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That((string)modeRules.Scheduler.Invocation(run)["phase"],Is.EqualTo("running"));
        }
        [UnityTest] public IEnumerator SurfaceCompletionExactScanQueuesWithoutApplyingWhileEarlierEditOwnsPreparation()
        {
            var scan=SharedEnvironment(out var platform);var floor=PlacementFloor();
            yield return LoadedLayout(scan,platform,new LayoutSource{Entries=new[]{floor}});
            string target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;
            Assert.That(editor.MoveObject(target,new Vector3(1,1.2f,1),out var error),Is.True,error);
            var before=editor.Read(target).position;
            Assert.That(editor.BeginCreatePrimitive(RoomObjectKind.Ball,"earlier edit",Vector3.up,1,Color.white,out _,out var blocker,out error),Is.True,error);
            using(blocker){
                Assert.That(modeActions.Execute(ScanPlacementRequest(target,floor),out error),Is.True,error);
                yield return null;yield return null;
                Assert.That(editor.Read(target).position,Is.EqualTo(before),"Queued scan placement cannot apply before its turn");
            }
            yield return PlacementFinished();
            Assert.That(editor.Read(target).position,Is.Not.EqualTo(before));
        }
        IEnumerator SurfaceOperationReady(CapabilityOperation operation)
        {
            yield return PhysicalCreationTestWait.Until(()=>operation.State(out _)!=RuleActionState.Preparing,"Surface operation did not reach an outcome");
            Assert.That(operation.State(out var error),Is.EqualTo(RuleActionState.Ready),error);
        }
        IEnumerator SurfacePublicationEntered(CapabilityOperation operation,HeldSurfacePublication hold)
        {
            yield return PhysicalCreationTestWait.Until(()=>{
                Assert.That(operation.State(out var error),Is.Not.EqualTo(RuleActionState.Failed),error);
                return hold.Entered;
            },"Surface publication did not start");
        }
        [UnityTest] public IEnumerator SurfaceCompletionLiveQueueCapturesOneRayAndCancellationNeverQueriesIt()
        {
            var scan=PlacementRoom(out var source,out var target);var before=editor.Read(target).position;
            var ray=new Ray(new Vector3(2,1.5f,2),Vector3.down);
            Assert.That(editor.BeginCreatePrimitive(RoomObjectKind.Ball,"earlier edit",Vector3.up,1,Color.white,out _,out var blocker,out var error),Is.True,error);
            CapabilityOperation cancelled,placement;
            using(blocker){
                Assert.That(editor.StartSurfacePlacement(scan,target,editor.ObjectRevision(target),scan.SetupIdentity,ray,out cancelled,out error,true),Is.True,error);
                Assert.That(cancelled.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                cancelled.Stop(true);Assert.That(source.Rays,Is.Zero);
                Assert.That(editor.StartSurfacePlacement(scan,target,editor.ObjectRevision(target),scan.SetupIdentity,ray,out placement,out error,true),Is.True,error);
                Assert.That(placement.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                ray=new Ray(Vector3.one,Vector3.right);editor.Select(editor.Find("book"));yield return null;
                Assert.That(source.Rays,Is.Zero);Assert.That(editor.Read(target).position,Is.EqualTo(before));
            }
            yield return SurfaceOperationReady(placement);Assert.That(placement.Complete(out error),Is.True,error);
            Assert.That(source.Rays,Is.EqualTo(1));Assert.That(source.LastRay.origin,Is.EqualTo(new Vector3(2,1.5f,2)));
            Assert.That(source.LastRay.direction,Is.EqualTo(Vector3.down));Assert.That(editor.SelectedId,Is.EqualTo("book"));
            Assert.That(cancelled.State(out _),Is.EqualTo(RuleActionState.Failed));placement.Stop(false);
        }
        [UnityTest] public IEnumerator SurfaceCompletionScanRevalidatesLayoutAndFrameBeforeAcceptance()
        {
            var scan=SharedEnvironment(out var platform);var floor=PlacementFloor();
            yield return LoadedLayout(scan,platform,new LayoutSource{Entries=new[]{floor}});
            string target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;
            var before=editor.Read(target).position;int revision=editor.ObjectRevision(target);
            foreach(bool relocate in new[]{false,true}){
                Assert.That(editor.StartScanPlacement(scan,target,revision,(string)ScanStatus()["stateId"],floor.Id,0,0,out var operation,out var error),Is.True,error);
                var origin=editor.transform.position;var plane=floor.Position;
                if(relocate)editor.transform.position+=Vector3.right;else floor.Position+=Vector3.up;
                yield return null;
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Failed),error);operation.Stop(true);
                Assert.That(editor.Read(target).position,Is.EqualTo(before));Assert.That(editor.ObjectRevision(target),Is.EqualTo(revision));
                editor.transform.position=origin;floor.Position=plane;
                yield return PhysicalCreationTestWait.Until(()=>!editor.PendingEditSave,"Rejected placement did not release its preparation");
                yield return null;
            }
        }
        [UnityTest] public IEnumerator SurfaceCompletionScanReceiptRetainsAcceptedPositionAndRevisionAfterAnotherEdit()
        {
            var scan=SharedEnvironment(out var platform);var floor=PlacementFloor();
            yield return LoadedLayout(scan,platform,new LayoutSource{Entries=new[]{floor}});
            string target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;
            Assert.That(editor.StartScanPlacement(scan,target,editor.ObjectRevision(target),(string)ScanStatus()["stateId"],floor.Id,.4f,.3f,out var operation,out var error),Is.True,error);
            yield return SurfaceOperationReady(operation);var accepted=editor.Read(target).position;int revision=editor.ObjectRevision(target);
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==target).position,Is.EqualTo(accepted));
            Assert.That(editor.MoveObject(target,new Vector3(1,2,1),out error),Is.True,error);
            Assert.That(operation.Complete(out error),Is.True,error);var result=operation.Result;
            Assert.That((int)result["revision"],Is.EqualTo(revision));Assert.That(editor.ObjectRevision(target),Is.GreaterThan(revision));
            Assert.That(JToken.DeepEquals(result["position"],ScannedRoom.Triple(accepted)),Is.True);
            result["revision"]=0;Assert.That((int)operation.Result["revision"],Is.EqualTo(revision));operation.Stop(false);
        }
        [UnityTest] public IEnumerator SurfaceCompletionAcceptedLiveStopDrainsWithoutAnotherRayOrMove()
        {
            var scan=PlacementRoom(out var source,out var target);var before=editor.Read(target).position;
            using var hold=new HeldSurfacePublication();
            Assert.That(editor.StartSurfacePlacement(scan,target,editor.ObjectRevision(target),scan.SetupIdentity,
                new Ray(new Vector3(2,1.5f,2),Vector3.down),out var operation,out var error,true,hold.Check),Is.True,error);
            try{
                yield return SurfacePublicationEntered(operation,hold);var accepted=editor.Read(target).position;
                Assert.That(accepted,Is.Not.EqualTo(before));Assert.That(editor.PendingEditSave,Is.True);
                operation.Stop(true);Assert.That(operation.InterruptionStatus,Does.Contain("accepted"));
                scan.ToggleSurfaces();hold.Release();
                yield return PhysicalCreationTestWait.Until(()=>!editor.PendingEditSave,"Accepted placement did not drain after Stop");
                Assert.That(editor.Read(target).position,Is.EqualTo(accepted));Assert.That(source.Rays,Is.EqualTo(1));
                Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==target).position,Is.EqualTo(accepted));
                Assert.That(operation.State(out _),Is.EqualTo(RuleActionState.Failed));
            }finally{hold.Release();operation.Stop(true);}
        }
        [UnityTest] public IEnumerator SurfaceCompletionAcceptedScanFinishesWithOriginalAnchorAfterScanChanges()
        {
            var scan=SharedEnvironment(out var platform);var floor=PlacementFloor();
            yield return LoadedLayout(scan,platform,new LayoutSource{Entries=new[]{floor}});
            string target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;string identity=(string)ScanStatus()["stateId"];
            using var hold=new HeldSurfacePublication();
            Assert.That(editor.StartScanPlacement(scan,target,editor.ObjectRevision(target),identity,floor.Id,.3f,.4f,out var operation,out var error,hold.Check),Is.True,error);
            try{
                yield return SurfacePublicationEntered(operation,hold);var accepted=editor.Read(target).position;int revision=editor.ObjectRevision(target);
                floor.Position+=Vector3.up;yield return null;Assert.That((string)ScanStatus()["stateId"],Is.Not.EqualTo(identity));
                hold.Release();yield return SurfaceOperationReady(operation);Assert.That(operation.Complete(out error),Is.True,error);
                Assert.That((string)operation.Result["anchorId"],Is.EqualTo(floor.Id));Assert.That((int)operation.Result["revision"],Is.EqualTo(revision));
                Assert.That(JToken.DeepEquals(operation.Result["position"],ScannedRoom.Triple(accepted)),Is.True);
                Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==target).position,Is.EqualTo(accepted));
            }finally{hold.Release();operation.Stop(true);}
        }
        [UnityTest] public IEnumerator SurfaceCompletionNoOpKeepsRevisionAndAddsNoUndo()
        {
            var scan=SharedEnvironment(out var platform);var floor=PlacementFloor();
            yield return LoadedLayout(scan,platform,new LayoutSource{Entries=new[]{floor}});
            string target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;
            var before=editor.Read(target).position;int accepted=0;RoomObjectData first=null;
            for(int i=0;i<2;i++){
                Assert.That(editor.StartScanPlacement(scan,target,editor.ObjectRevision(target),(string)ScanStatus()["stateId"],floor.Id,0,0,out var operation,out var error),Is.True,error);
                yield return SurfaceOperationReady(operation);Assert.That(operation.Complete(out error),Is.True,error);
                if(i==0){accepted=editor.ObjectRevision(target);first=editor.Read(target);}
                var current=editor.Read(target);
                Assert.That(editor.ObjectRevision(target),Is.EqualTo(accepted),
                    "First position "+first.position.ToString("R")+" rotation "+first.rotation.ToString("R")+" scale "+first.scale.ToString("R")+
                    "; current position "+current.position.ToString("R")+" rotation "+current.rotation.ToString("R")+" scale "+current.scale.ToString("R"));
                Assert.That((int)operation.Result["revision"],Is.EqualTo(accepted));operation.Stop(false);
            }
            editor.Undo();Assert.That(editor.Read(target).position,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator SurfaceCompletionFailedAcceptedPhysicalSaveRetainsPlacementAndRequiresRecovery()
        {
            var scan=PlacementRoom(out var source,out var target);var tools=SurfaceCompletionTray(scan,target);yield return ArmSurfaceCompletion(tools);
            Assert.That(editor.TryFlush(out var error),Is.True,error);
            var before=editor.Read(target).position;var path=Path.Combine(directory,RoomStorage.FileName);var saved=File.ReadAllBytes(path);
            editor.ManualCreationCheckpoint=phase=>{if(phase=="publishing")throw new IOException("Injected surface publication failure");};
            try{
                tools.Place(new Ray(new Vector3(2,1.5f,2),Vector3.down));yield return PhysicalCreationTestWait.Finish(editor);
                Assert.That(editor.Read(target).position,Is.Not.EqualTo(before));Assert.That(File.ReadAllBytes(path),Is.EqualTo(saved));
                Assert.That(editor.Status,Does.Contain("unconfirmed"));Assert.That(editor.Status,Does.Contain("Surface placement"));
                int rays=source.Rays;
                Assert.That(editor.PlaceOnSurfaceManually(scan,target,editor.ObjectRevision(target),scan.SetupIdentity,new Ray(Vector3.up,Vector3.down)),Is.False);
                Assert.That(source.Rays,Is.EqualTo(rays));Assert.That(editor.TryFlush(out _),Is.False);
            }finally{editor.ManualCreationCheckpoint=null;}
        }
        [UnityTest] public IEnumerator SurfaceCompletionSmallIntentionalPlacementStillCreatesAnEdit()
        {
            var scan=SharedEnvironment(out var platform);var floor=PlacementFloor();
            yield return LoadedLayout(scan,platform,new LayoutSource{Entries=new[]{floor}});
            string target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;
            int prior=0;
            foreach(float x in new[]{0f,.0001f}){
                Assert.That(editor.StartScanPlacement(scan,target,editor.ObjectRevision(target),(string)ScanStatus()["stateId"],floor.Id,x,0,out var operation,out var error),Is.True,error);
                yield return SurfaceOperationReady(operation);Assert.That(operation.Complete(out error),Is.True,error);
                Assert.That(editor.Read(target).position.x,Is.EqualTo(x).Within(.000001f));
                Assert.That(editor.ObjectRevision(target),Is.GreaterThan(prior));prior=editor.ObjectRevision(target);operation.Stop(false);
            }
        }
        [UnityTest] public IEnumerator SurfaceCompletionNoiseToleranceCannotAccumulateAcrossLiveAndSavedOrigins()
        {
            PlacementRoom(out _,out var target);var item=editor.Find(target);var saved=editor.Read(target).position;
            var live=saved+Vector3.right*.000009f;var requested=saved+Vector3.right*.000018f;
            item.transform.position=editor.transform.TransformPoint(live);
            Assert.That(ScannedRoom.StableSurfacePosition(editor,target,item,requested),Is.EqualTo(requested),
                "Two nearby pairs must not combine to discard intent outside the canonical tolerance");
            yield return null;
        }
    }
}
