// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
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
        string ScanBoundsArea(string target)
        {
            var capability=new RegionSaveCapability();
            Assert.That(capability.Start(new CapabilityContext(editor,authoring),"scan-area",capability.Example,out var operation,out var error),Is.True,error);
            string area=(string)operation.Result["id"];
            Assert.That(editor.BindRegion(target,editor.ObjectRevision(target),area,editor.RegionRevision(area),out error),Is.True,error);return area;
        }
        IEnumerator ScanBoundsPrepared(string target)
        {
            yield return Until(()=>(bool)editor.ObserveAuthoredBounds(target)["prepared"]);
            Assert.That((bool)editor.ObserveAuthoredBounds(target)["visual"]["known"],Is.True);
        }
        void ScanBoundsContainMeshes(string target,RoomSpatialExtent envelope,bool local=false)
        {
            Assert.That(envelope.Known&&envelope.HasBounds,Is.True);var bounds=envelope.Bounds;bounds.Expand(.0002f);
            var item=editor.Find(target);Assert.That(item,Is.Not.Null);
            foreach(var filter in item.GetComponentsInChildren<MeshFilter>(true))
                foreach(var vertex in filter.sharedMesh.vertices){
                    var worldPoint=filter.transform.TransformPoint(vertex);
                    var point=local?item.transform.InverseTransformPoint(worldPoint):editor.Frame.PointToRoom(worldPoint);
                    Assert.That(bounds.Contains(point),Is.True,$"{point} outside {envelope.Bounds} from {filter.name}");
                }
        }
        Camera ScanBoundsCamera(Vector3 position,Vector3 forward)
        {
            var go=new GameObject("Scan bounds observer",typeof(Camera));go.transform.SetParent(physicalRoot.transform,false);go.transform.SetPositionAndRotation(position,Quaternion.LookRotation(forward));
            var camera=go.GetComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=15;camera.fieldOfView=50;camera.aspect=1;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.enabled=false;return camera;
        }
        [UnityTest] public IEnumerator ScanSpatialBoundsPrepareLocalGeometryWithoutRequiringScanOrChangingTheRoom()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{InkWall()}};yield return LoadedLayout(scan,platform,source);
            string id=MakeLayer(out _);InkAdd(id);yield return ScanBoundsPrepared(id);
            var local=RoomAuthoredBounds.Derive(editor.Read(id));ScanBoundsContainMeshes(id,local.Visual,true);Assert.That(local.Collision.Known,Is.False);
            source.Available=false;yield return null;var saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision,loads=platform.Loads,permissions=platform.Permissions;
            for(int i=0;i<20;i++){
                Assert.That(ScanFact("object.authoredBounds",new JObject{["target"]=id},out var fact),Is.True);
                Assert.That((bool)fact["prepared"]&&(bool)fact["visual"]["known"],Is.True);
                Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Known,Is.False,"An unavailable physical anchor is not the origin or the previous pose");
            }
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(platform.Loads,Is.EqualTo(loads));Assert.That(platform.Permissions,Is.EqualTo(permissions));Assert.That(world.Running,Is.False);
        }
        [UnityTest] public IEnumerator ScanSpatialBoundsNativeReadUsesCurrentAnchorWithoutMovingTheInstance()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{InkWall()},WorldCoordinates=true};yield return LoadedLayout(scan,platform,source);
            string id=MakeLayer(out _);InkAdd(id);var item=editor.Find(id);var initial=editor.ReadSpatialBounds(id,out var origin);Assert.That(origin,Is.EqualTo("native"));ScanBoundsContainMeshes(id,initial.Visual);Assert.That(initial.Collision.Known&&!initial.Collision.HasBounds,Is.True);
            var saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;var before=source.Reads;
            root.transform.SetPositionAndRotation(new Vector3(2,0,-3),Quaternion.Euler(0,55,0));var unchanged=item.transform.position;
            var current=editor.ReadSpatialBounds(id,out _);Assert.That(source.Reads,Is.EqualTo(before+1),"A same-frame authored-root move invalidates the scan-coordinate snapshot");
            Assert.That(item.transform.position,Is.EqualTo(unchanged),"Reading bounds cannot sync or teleport native objects");
            Assert.That(Vector3.Distance(editor.Frame.PointToWorld(current.Visual.Bounds.center),initial.Visual.Bounds.center),Is.LessThan(.04f));
            item.GetComponent<ScannedDrawingView>().Sync();ScanBoundsContainMeshes(id,current.Visual);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest] public IEnumerator ScanSpatialBoundsRetirementReprojectsExactAnchorAndUsesOwnedCameraWithoutLoading()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{InkWall()},WorldCoordinates=true};yield return LoadedLayout(scan,platform,source);
            string id=MakeLayer(out _);InkAdd(id);string area=ScanBoundsArea(id);var original=editor.ReadSpatialBounds(id,out _);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            source.Entries[0].Position+=Vector3.right*4;source.Entries[0].Rotation=Quaternion.Euler(0,65,20);yield return null;
            var moved=editor.ReadSpatialBounds(id,out var kind);Assert.That(kind,Is.EqualTo("retained"));Assert.That(moved.Visual.Known,Is.True);Assert.That(Vector3.Distance(moved.Visual.Bounds.center,original.Visual.Bounds.center),Is.GreaterThan(3));
            var camera=ScanBoundsCamera(new Vector3(0,1.3f,3),Vector3.back);
            using(editor.ObserveNativeCamera(camera)){
                Assert.That((editor.ReadRetention().Reasons(id)&RoomRetentionReason.Observation)==0,Is.True,"Old physical location cannot retain a moved anchor");
                camera.transform.LookAt(editor.Frame.PointToWorld(moved.Visual.Bounds.center));
                Assert.That((editor.ReadRetention().Reasons(id)&RoomRetentionReason.Observation)!=0,Is.True);
            }
            Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            var activation=editor.ActivateNativeArea(area);yield return Until(()=>activation.IsCompleted);Assert.That(activation.GetAwaiter().GetResult(),Is.Null);ScanBoundsContainMeshes(id,moved.Visual);
        }
        [UnityTest] public IEnumerator ScanSpatialBoundsNeverReuseOldPoseForMissingWrongOrNonfittingAnchors()
        {
            var scan=SharedEnvironment(out var platform);var wall=InkWall();var source=new LayoutSource{Entries=new[]{wall}};yield return LoadedLayout(scan,platform,source);
            string id=MakeLayer(out _);InkAdd(id);yield return ScanBoundsPrepared(id);string area=ScanBoundsArea(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var saved=JsonUtility.ToJson(editor.Snapshot());string roomId=source.RoomId;
            foreach(string problem in new[]{"missing","wrong-anchor","wrong-room","too-small","not-plane","invalid","unavailable"}){
                source.Entries=new[]{wall.Copy()};source.RoomId=roomId;source.Available=true;
                switch(problem){
                    case "missing":source.Entries=Array.Empty<ScannedSurface>();break;
                    case "wrong-anchor":source.Entries[0].Id=Guid.NewGuid().ToString("N");break;
                    case "wrong-room":source.RoomId=Guid.NewGuid().ToString("N");break;
                    case "too-small":source.Entries[0].Plane=new Rect(-.02f,-.02f,.04f,.04f);break;
                    case "not-plane":source.Entries[0].Plane=null;source.Entries[0].Volume=new Bounds(Vector3.zero,Vector3.one);break;
                    case "invalid":source.Entries[0].Position=Vector3.one*float.NaN;break;
                    case "unavailable":source.Available=false;break;
                }
                yield return null;var value=editor.ReadSpatialBounds(id,out _);Assert.That(value.Visual.Known||value.Collision.Known,Is.False,problem);
                Assert.That(editor.Find(id),Is.Null);Assert.That((bool)editor.ObserveAuthoredBounds(id)["visual"]["known"],Is.True);
            }
            source.Entries=new[]{wall};source.RoomId=roomId;source.Available=true;yield return null;Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Known,Is.True);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest] public IEnumerator ScanSpatialBoundsHoldScaledFrameAndVirtualModeRemainUnknownUntilExactRecovery()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{InkWall()}};yield return LoadedLayout(scan,platform,source);string id=MakeLayer(out _);InkAdd(id);
            string area=ScanBoundsArea(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            root.transform.localScale=Vector3.one*2;Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Known,Is.False);root.transform.localScale=Vector3.one;
            scan.SetVirtualView(true);Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Known,Is.False);scan.SetVirtualView(false);world.SetSurfaces(true,"Restored synthetic scan");
            using(editor.RuntimeGate.Hold("Workspace review"))Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Known,Is.False);
            world.SetSurfaces(true,"Restored synthetic scan");Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Known,Is.True);Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator ScanSpatialBoundsAuthoredIndexSupportsColdStartWithoutNativeObjectsOrSdkReads()
        {
            var data=new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Drawing,physics=ItemPhysics.Fixed,points=Array.Empty<Vector3>(),surfaces=new[]{new DrawingSurface{id="Canvas",width=.6f,height=.4f,strokes=new[]{new SurfaceStroke{id="ink",radius=.01f,points=new[]{new Vector3(-.2f,-.1f,0),new Vector3(.2f,.1f,0)}}}}},scanAnchors=new[]{new ScanDrawingAnchor{roomId=new string('b',32),anchorId=new string('c',32)}}};
            Assert.That(ScanDrawingAnchor.ValidateOwner(data,out var error),Is.True,error);int reads=0;
            using var index=new RoomSpatialIndex((hash,cancel)=>{reads++;throw new InvalidOperationException("Ink needs no asset read");});
            string key=RoomEditor.SpatialSource(data.Copy());index.Synchronize(new RoomDocument{objects=new[]{data}});Assert.That(index.TryRead(data.id,key,out _),Is.False);
            for(int i=0;i<10&&index.Pending;i++)index.Tick();Assert.That(index.TryRead(data.id,key,out var value),Is.True);Assert.That(value.Visual.Known&&value.Visual.HasBounds,Is.True);Assert.That(value.Collision.Known,Is.False);Assert.That(reads,Is.Zero);
            Assert.That(value.Visual.Bounds.Contains(new Vector3(.3f,.2f,0)),Is.True);Assert.That(UnityEngine.Object.FindObjectsByType<ScannedDrawingView>(FindObjectsSortMode.None),Is.Empty);yield return null;
        }
        [UnityTest] public IEnumerator ScanSpatialBoundsDormantSavedInkEditAndUndoInvalidateOnlyTheDerivedEnvelope()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{InkWall()}};yield return LoadedLayout(scan,platform,source);
            string id=MakeLayer(out _);InkAdd(id);string area=ScanBoundsArea(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var original=editor.ReadSpatialBounds(id,out _);var edited=editor.Read(id);edited.surfaces[0].width=1.2f;edited.scanAnchors[0].x=-.3f;edited.scanAnchors[0].angle=35;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{edited},Array.Empty<string>(),out error),Is.True,error);Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Known,Is.False);
            yield return ScanBoundsPrepared(id);var changed=editor.ReadSpatialBounds(id,out var kind);Assert.That(kind,Is.EqualTo("authored"));Assert.That(changed.Visual.Bounds.size.sqrMagnitude,Is.GreaterThan(original.Visual.Bounds.size.sqrMagnitude));Assert.That(changed.Collision.Known,Is.False);Assert.That(editor.Find(id),Is.Null);
            editor.Undo();Assert.That(editor.ReadSpatialBounds(id,out kind).Visual.Bounds,Is.EqualTo(original.Visual.Bounds));Assert.That(kind,Is.EqualTo("retained"));Assert.That(editor.Find(id),Is.Null);
            var activation=editor.ActivateNativeArea(area);yield return Until(()=>activation.IsCompleted);Assert.That(activation.GetAwaiter().GetResult(),Is.Null);ScanBoundsContainMeshes(id,original.Visual);
        }
        [UnityTest] public IEnumerator ScanSpatialBoundsObservedDormantInkReappearsAtCurrentPlaneThroughSharedDisplayAdmission()
        {
            var scan=SharedEnvironment(out var platform);var source=new LayoutSource{Entries=new[]{InkWall()},WorldCoordinates=true};yield return LoadedLayout(scan,platform,source);
            string id=MakeLayer(out _);InkAdd(id);string area=ScanBoundsArea(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            source.Entries[0].Position+=Vector3.right*3;yield return null;
            var expected=editor.ReadSpatialBounds(id,out _);string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            var camera=ScanBoundsCamera(new Vector3(3,1.3f,3),Vector3.back);var exact=source.Entries[0];source.Entries=new[]{InkWall(2)};
            using(editor.ObserveNativeCamera(camera)){
                for(int i=0;i<8;i++){yield return null;editor.TickNativeDisplay();Assert.That(editor.Find(id),Is.Null,"An unrelated wall cannot trigger ink reconstruction");}
                source.Entries=new[]{exact};yield return null;editor.TickNativeDisplay();yield return Until(()=>editor.Find(id));
            }
            ScanBoundsContainMeshes(id,expected.Visual);Assert.That(editor.NativeEntityDormant(id),Is.False);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(world.Running,Is.False);
        }
    }
}
