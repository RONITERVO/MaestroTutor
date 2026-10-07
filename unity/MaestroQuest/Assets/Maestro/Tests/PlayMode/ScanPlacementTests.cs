// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        static ScannedSurface PlacementFloor(int id=1,float height=.35f)=>new(){Id=id.ToString("x32"),Label="FLOOR",Position=new Vector3(0,height,0),Rotation=Quaternion.Euler(-90,0,0),Plane=new Rect(-2,-2,4,4)};
        JObject ScanPlacementRequest(string target,ScannedSurface surface,float x=0,float y=0)=>SettingsRequest("object.scan.place",new JObject{["target"]=target,["stateId"]=ScanStatus()["stateId"].DeepClone(),["anchorId"]=surface.Id,["x"]=x,["y"]=y});
        [UnityTest] public IEnumerator ExactScannedFloorPlacementUsesBoundsNotTabletopAndSavesOneUndo()
        {
            var scan=SharedEnvironment(out var platform);var floor=PlacementFloor();var table=PlacementFloor(2,1.1f);table.Label="TABLE";
            yield return LoadedLayout(scan,platform,new LayoutSource{Entries=new[]{floor,table}});
            var target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;var item=editor.Find(target);
            item.GetComponentInChildren<BoxCollider>().center=new Vector3(.2f,.3f,0);Assert.That(editor.MoveObject(target,new Vector3(1,1.6f,1),out var error),Is.True,error);
            int revision=editor.ObjectRevision(target);var before=editor.Read(target).position;var other=editor.Read("maestro").position;var status=ScanStatus();var request=ScanPlacementRequest(target,floor,.5f,-.4f);
            Assert.That(modeActions.Execute(request,out error),Is.True,error);yield return PlacementFinished();var receipt=modeActions.Observe().DeepClone();
            Physics.SyncTransforms();ScannedRoom.Bounds(item,out var bounds);Assert.That(bounds.min.y,Is.EqualTo(.36f).Within(.001f));Assert.That(bounds.center.x,Is.EqualTo(.5f).Within(.001f));Assert.That(bounds.center.z,Is.EqualTo(.4f).Within(.001f));
            Assert.That((string)receipt["selected"]["output"]["anchorId"],Is.EqualTo(floor.Id));Assert.That(world.Running,Is.False);Assert.That(editor.Read("maestro").position,Is.EqualTo(other));
            var saved=new RoomStorage(directory).Load(out error);Assert.That(error,Is.Null);Assert.That(saved.objects.Single(x=>x.id==target).position,Is.EqualTo(editor.Read(target).position));
            editor.Undo();Assert.That(editor.Read(target).position,Is.EqualTo(before));Assert.That(modeActions.Execute(request,out error),Is.True,error);Assert.That(editor.Read(target).position,Is.EqualTo(before),"Receipt replay cannot repeat a saved edit after Undo");
            string output=Environment.GetEnvironmentVariable("MAESTRO_SCAN_PLACEMENT");if(!string.IsNullOrWhiteSpace(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"scan-placement.json"),new JObject{["scan"]=status,["request"]=request,["receipt"]=receipt,["before"]=ScannedRoom.Triple(before),["beforeRevision"]=revision,["boundary"]="synthetic scan source; real native placement, persistence, Undo and receipt"}.ToString());}
        }
        [UnityTest] public IEnumerator ScannedPlacementRejectsStaleMissingSteepAndOverhangingPlanesWithoutChanges()
        {
            var scan=SharedEnvironment(out var platform);var floor=PlacementFloor();var source=new LayoutSource{Entries=new[]{floor}};yield return LoadedLayout(scan,platform,source);
            var target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;var before=editor.Read(target).position;int revision=editor.ObjectRevision(target);var stale=ScanPlacementRequest(target,floor);
            floor.Position+=Vector3.up;yield return null;Assert.That(modeActions.Execute(stale,out _),Is.False);
            Assert.That(modeActions.Execute(ScanPlacementRequest(target,PlacementFloor(99)),out _),Is.False);
            Assert.That(modeActions.Execute(ScanPlacementRequest(target,floor,2,0),out _),Is.False);
            floor.Rotation=Quaternion.identity;yield return null;Assert.That(modeActions.Execute(ScanPlacementRequest(target,floor),out _),Is.False);
            floor.Rotation=Quaternion.Euler(-90,0,0);floor.Boundary=new[]{new Vector2(-2,-2),new Vector2(2,-2),new Vector2(2,2),new Vector2(.02f,2),new Vector2(.02f,-.02f),new Vector2(-.02f,-.02f),new Vector2(-.02f,2),new Vector2(-2,2)};yield return null;
            Assert.That(modeActions.Execute(ScanPlacementRequest(target,floor),out _),Is.False,"A small concave notch blocks the whole projected footprint");
            Assert.That(editor.Read(target).position,Is.EqualTo(before));Assert.That(editor.ObjectRevision(target),Is.EqualTo(revision));
        }
        [UnityTest] public IEnumerator ScannedPlacementUsesRoomTransformAndRespectsTemporaryAndLifecycleBoundaries()
        {
            var scan=SharedEnvironment(out var platform);var floor=PlacementFloor();yield return LoadedLayout(scan,platform,new LayoutSource{Entries=new[]{floor}});
            var target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;var before=editor.Read(target).position;
            editor.transform.SetPositionAndRotation(new Vector3(2,.6f,-1),Quaternion.Euler(0,37,0));Physics.SyncTransforms();yield return null;
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            var request=ScanPlacementRequest(target,floor,.3f,-.2f);Assert.That(modeActions.Execute(request,out error),Is.True,error);yield return PlacementFinished();
            Physics.SyncTransforms();ScannedRoom.Bounds(editor.Find(target),out var bounds);Assert.That(bounds.min.y,Is.EqualTo(.96f).Within(.001f));
            Assert.That((bool)modeActions.Observe()["selected"]["output"]["temporary"],Is.True);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(target).position,Is.EqualTo(before));
            request=ScanPlacementRequest(target,floor);scan.SendMessage("OnApplicationFocus",false);scan.SendMessage("OnApplicationFocus",true);Assert.That(modeActions.Execute(request,out _),Is.False);Assert.That(editor.Read(target).position,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator ScannedPlacementDoesNotOverrideTargetOwnershipOrFrozenStorage()
        {
            var scan=SharedEnvironment(out var platform);var floor=PlacementFloor();yield return LoadedLayout(scan,platform,new LayoutSource{Entries=new[]{floor}});
            var target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;var before=editor.Read(target).position;
            using(editor.WriteGate.TryFreeze(out var error)){Assert.That(modeActions.Execute(ScanPlacementRequest(target,floor),out _),Is.False);}
            scan.SetSurfaceSourceForTests(new SurfaceSource{Pending=new()});Assert.That(modeActions.Execute(PlacementRequest(target),out var failure),Is.True,failure);
            Assert.That(modeActions.Execute(ScanPlacementRequest(target,floor),out _),Is.False,"A pending live placement owns the same target");Assert.That(editor.Read(target).position,Is.EqualTo(before));
        }
    }
}
