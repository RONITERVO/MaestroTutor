// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        sealed class SurfaceSource:IRoomSurfaceSource
        {
            public bool Supported {get;set;}=true;
            public bool Miss,Steep,Throw;
            public int Preparations,Rays;
            public Ray LastRay;
            public TaskCompletionSource<bool> Pending;
            public Task<bool> Prepare(){Preparations++;return Throw?Task.FromException<bool>(new IOException("Surface provider failed")):Pending?.Task??Task.FromResult(true);}
            public bool Raycast(Ray ray,out Vector3 point,out Vector3 normal)
            {
                Rays++;LastRay=ray;point=normal=default;
                if(Miss||!Physics.Raycast(ray,out var hit,4,1<<RoomPhysicsLayers.Scanned,QueryTriggerInteraction.Ignore))return false;
                point=hit.point;normal=Steep?Vector3.right:hit.normal;return true;
            }
        }
        ScannedRoom PlacementRoom(out SurfaceSource source,out string target)
        {
            var scan=SharedEnvironment(out _);world.PausePhysics();source=new SurfaceSource();scan.SetSurfaceSourceForTests(source);
            target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;
            Assert.That(editor.MoveObject(target,new Vector3(1,1.2f,1),out var error),Is.True,error);Physics.SyncTransforms();return scan;
        }
        JObject PlacementRequest(string target,string direction="below")=>SettingsRequest("object.surface.place",new JObject {["target"]=target,["stateId"]=EnvironmentFact()["stateId"].DeepClone(),["direction"]=direction});
        IEnumerator PlacementFinished(string phase="completed")
        {
            yield return Until(()=>new[]{"completed","failed","cancelled"}.Contains((string)modeActions.Observe()["selected"]?["phase"]));
            Assert.That((string)modeActions.Observe()["selected"]["phase"],Is.EqualTo(phase),modeActions.Observe().ToString());
        }
        [UnityTest] public IEnumerator SurfacePlacementSavesActualSupportOffsetAndReplaysWithoutAnotherRayOrEdit()
        {
            var scan=PlacementRoom(out var source,out var target);var before=editor.Read(target);int beforeRevision=editor.ObjectRevision(target);var environment=EnvironmentFact();var request=PlacementRequest(target);
            Assert.That(modeActions.Execute(request,out var error),Is.True,error);yield return PlacementFinished();var receipt=modeActions.Observe().DeepClone();var result=(JObject)receipt["selected"]["output"];
            Physics.SyncTransforms();Assert.That(ScannedRoom.Bounds(editor.Find(target),out var bounds),Is.True);Assert.That(bounds.min.y,Is.EqualTo(.01f).Within(.001f));Assert.That(world.Running,Is.False);
            var saved=new RoomStorage(directory).Load(out error);Assert.That(error,Is.Null);Assert.That(saved.objects.Single(x=>x.id==target).position,Is.EqualTo(editor.Read(target).position));
            Assert.That((string)result["target"],Is.EqualTo(target));Assert.That((int)result["revision"],Is.EqualTo(editor.ObjectRevision(target)));Assert.That(source.Rays,Is.EqualTo(1));
            editor.Undo();Assert.That(editor.Read(target).position,Is.EqualTo(before.position));Assert.That(modeActions.Execute(request,out error),Is.True,error);Assert.That(source.Rays,Is.EqualTo(1));Assert.That(editor.Read(target).position,Is.EqualTo(before.position),"Receipt replay cannot undo the user's Undo");
            string output=Environment.GetEnvironmentVariable("MAESTRO_SURFACE_PLACEMENT");if(!string.IsNullOrWhiteSpace(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"surface-placement.json"),new JObject {["environment"]=environment,["request"]=request,["receipt"]=receipt,["before"]=ScannedRoom.Triple(before.position),["beforeRevision"]=beforeRevision,["boundary"]="synthetic surface provider; real native placement, journal and receipt"}.ToString());}
        }
        [UnityTest] public IEnumerator GazePlacementSamplesOnceAndSupportsOffsetCollisionBounds()
        {
            PlacementRoom(out var source,out var target);source.Pending=new();var item=editor.Find(target);var box=item.GetComponentInChildren<BoxCollider>();Assert.That(box,Is.Not.Null);box.center=new Vector3(.1f,.2f,0);Physics.SyncTransforms();
            viewer.transform.position=new Vector3(0,1.5f,0);viewer.transform.LookAt(new Vector3(0,0,1));var expected=new Ray(viewer.transform.position,viewer.transform.forward);var request=PlacementRequest(target,"gaze");
            Assert.That(modeActions.Execute(request,out var error),Is.True,error);Assert.That(source.Rays,Is.Zero);viewer.transform.LookAt(new Vector3(3,1.5f,0));source.Pending.SetResult(true);yield return PlacementFinished();
            Assert.That(Vector3.Distance(source.LastRay.origin,expected.origin),Is.LessThan(.001f));Assert.That(Vector3.Distance(source.LastRay.direction,expected.direction),Is.LessThan(.001f));
            Physics.SyncTransforms();ScannedRoom.Bounds(item,out var bounds);Assert.That(bounds.min.y,Is.EqualTo(.01f).Within(.001f));Assert.That(bounds.center.z,Is.EqualTo(1).Within(.01f));
        }
        [UnityTest] public IEnumerator CancelledPlacementAndLifecycleLossCannotApplyLatePreparation()
        {
            var scan=PlacementRoom(out var source,out var target);var before=editor.Read(target).position;source.Pending=new();var request=PlacementRequest(target);
            Assert.That(modeActions.Execute(request,out var error),Is.True,error);Assert.That(modeActions.Execute(new JObject {["operation"]="cancel",["runId"]=request["runId"].DeepClone()},out error),Is.True,error);
            source.Pending.SetResult(true);yield return null;yield return null;Assert.That(editor.Read(target).position,Is.EqualTo(before));Assert.That(source.Rays,Is.Zero);
            source.Pending=new();Assert.That(modeActions.Execute(PlacementRequest(target),out error),Is.True,error);scan.SendMessage("OnApplicationFocus",false);scan.SendMessage("OnApplicationFocus",true);source.Pending.SetResult(true);yield return PlacementFinished("cancelled");
            Assert.That(editor.Read(target).position,Is.EqualTo(before));Assert.That(source.Rays,Is.Zero);
        }
        [UnityTest] public IEnumerator SurfacePlacementRejectsChangedRoomOrObjectAndRespectsPreservation()
        {
            var scan=PlacementRoom(out var source,out var target);var request=PlacementRequest(target);scan.ToggleSurfaces();Assert.That(modeActions.Execute(request,out _),Is.False);Assert.That(source.Preparations,Is.Zero);
            using(editor.WriteGate.TryFreeze(out var error)){Assert.That(modeActions.Execute(PlacementRequest(target),out _),Is.False);}
            source.Pending=new();Assert.That(modeActions.Execute(PlacementRequest(target),out var failure),Is.True,failure);Assert.That(editor.PaintObject(target,Color.red,out failure),Is.True,failure);var before=editor.Read(target).position;source.Pending.SetResult(true);yield return PlacementFinished("failed");Assert.That(source.Rays,Is.Zero);Assert.That(editor.Read(target).position,Is.EqualTo(before));
            source.Pending=null;scan.SetVirtualView(true);Assert.That(modeActions.Execute(PlacementRequest(target),out _),Is.False);scan.SetVirtualView(false);source.Supported=false;Assert.That(modeActions.Execute(PlacementRequest(target),out _),Is.False);
        }
        [UnityTest] public IEnumerator MissSteepSurfacePreparationFailureAndWriteFailureLeavePlacementUnchanged()
        {
            PlacementRoom(out var source,out var target);var before=editor.Read(target).position;int revision=editor.ObjectRevision(target);
            foreach(var failure in new[]{"miss","steep","prepare","write"}){
                source.Miss=failure=="miss";source.Steep=failure=="steep";source.Throw=false;source.Pending=new();
                Assert.That(modeActions.Execute(PlacementRequest(target),out var error),Is.True,error);
                IDisposable frozen=null;if(failure=="write"){frozen=editor.WriteGate.TryFreeze(out error);Assert.That(frozen,Is.Not.Null);}
                if(failure=="prepare")source.Pending.SetException(new IOException("Surface provider failed"));else source.Pending.SetResult(true);
                yield return PlacementFinished("failed");frozen?.Dispose();Assert.That(editor.Read(target).position,Is.EqualTo(before));Assert.That(editor.ObjectRevision(target),Is.EqualTo(revision));
            }
        }
        [UnityTest] public IEnumerator PhysicalSurfaceControlUsesTheSharedPlacementAndIgnoresRetiredArming()
        {
            var scan=PlacementRoom(out var source,out var target);var tray=new GameObject("Surface controls");tray.transform.SetParent(root.transform,false);var tools=tray.AddComponent<PhysicsTools>();tools.Build(editor,world,scan,room);editor.Select(editor.Find(target));source.Pending=new();
            var button=tray.GetComponentsInChildren<Maestro.Quest.Rules.RuleToolAction>().Single(x=>x.AccessibleName=="Place surface");button.Command();tools.CancelPlacement();source.Pending.SetResult(true);yield return null;Assert.That(tools.Placing,Is.False);
            source.Pending=null;button.Command();yield return Until(()=>tools.Placing);tools.Place(new Ray(new Vector3(2,1.5f,2),Vector3.down));Physics.SyncTransforms();ScannedRoom.Bounds(editor.Find(target),out var bounds);Assert.That(bounds.min.y,Is.EqualTo(.01f).Within(.001f));Assert.That(bounds.center.x,Is.EqualTo(2).Within(.001f));Assert.That(source.Rays,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator TemporarySurfacePlacementDoesNotOverwriteSavedRoomAndCannotStealOwnedTarget()
        {
            PlacementRoom(out var source,out var target);var before=editor.Read(target).position;Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            var wait=new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=20}};Assert.That(modeRules.Scheduler.Invoke(wait,Time.unscaledTime,out var waiting,out error),Is.True,error);
            source.Pending=new();Assert.That(modeActions.Execute(PlacementRequest(target),out error),Is.True,error);
            var move=new JObject {["id"]="object.position.set",["version"]=1,["arguments"]=new JObject {["target"]=target,["x"]=0,["y"]=2,["z"]=0}};Assert.That(modeRules.Scheduler.Invoke(move,Time.unscaledTime,out _,out _),Is.False);
            source.Pending.SetResult(true);yield return PlacementFinished();Assert.That((bool)modeActions.Observe()["selected"]["output"]["temporary"],Is.True);Assert.That((string)modeRules.Scheduler.Invocation(waiting)["phase"],Is.EqualTo("running"));
            var saved=new RoomStorage(directory).Load(out error);Assert.That(error,Is.Null);Assert.That(saved.objects.Single(x=>x.id==target).position,Is.EqualTo(before));Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(target).position,Is.EqualTo(before));
        }
    }
}
