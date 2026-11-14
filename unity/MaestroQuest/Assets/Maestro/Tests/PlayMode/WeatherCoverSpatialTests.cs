// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        string SavedWeatherRoof(Vector3 position,float scale=1) {
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Saved rain roof",position,scale,Color.white,out var id,out var error),Is.True,error);
            var data=editor.Read(id);data.physics=ItemPhysics.Fixed;
            data.collision=new CollisionRecipe{shapes=new[]{new CollisionShape{id="roof",size=new Vector3(2,.025f,2)}}};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            Physics.SyncTransforms();return id;
        }
        JObject SavedWeatherExposure(Vector3 roomPoint,string target="") {
            var args=new JObject{["position"]=new JObject{["x"]=roomPoint.x,["y"]=roomPoint.y,["z"]=roomPoint.z},["target"]=target};
            Assert.That(WorldWeatherCapability.Exposure().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,args,out var value),Is.True);
            return JObject.FromObject(value.Value);
        }
        [UnityTest]public IEnumerator WeatherCoverSpatialRoofLifecycleKeepsPassiveExposureTruthful() {
            physics.SetSurfaces(true,"Aligned test room");var point=new Vector3(8,1,8);var cover=new WeatherCover();
            string roof=SavedWeatherRoof(point+Vector3.up*3),area=NativeAreaFor(roof);
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Covered));
            Assert.That((string)SavedWeatherExposure(point)["state"],Is.EqualTo("covered"));
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;Physics.SyncTransforms();
            Assert.That(editor.DormantCollisionGuard(roof).Known,Is.True,"Establish retained bounds rather than an absent roof");
            int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
            for(int i=0;i<4;i++) {
                Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Unknown),"A conservative dormant box is neither proven open sky nor an exact solid roof");
                Assert.That((string)SavedWeatherExposure(point)["state"],Is.EqualTo("unknown"));
            }
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(roof),Is.Null);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Physics.SyncTransforms();
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Covered));
            Assert.That((string)SavedWeatherExposure(point)["state"],Is.EqualTo("covered"));
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{roof},out error),Is.True,error);
            Physics.SyncTransforms();Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Open));
            Assert.That((string)SavedWeatherExposure(point)["state"],Is.EqualTo("open"));
        }
        [UnityTest]public IEnumerator WeatherCoverSpatialDistantKnownDormantRoofDoesNotBlockOpenSky() {
            physics.SetSurfaces(true,"Aligned test room");var cover=new WeatherCover();var point=new Vector3(8,1,8);
            string roof=SavedWeatherRoof(new Vector3(-8,4,-8)),area=NativeAreaFor(roof);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;Physics.SyncTransforms();
            var extent=editor.ReadCollisionBounds(roof);Assert.That(extent.Known&&extent.HasBounds,Is.True);
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Open));
            Assert.That((string)SavedWeatherExposure(point)["state"],Is.EqualTo("open"));
            Assert.That(cover.Segment(editor,point,point+Vector3.down, true),Is.EqualTo(RainExposure.Open));
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(roof),Is.Null);
        }
        [UnityTest]public IEnumerator WeatherCoverSpatialUnknownImportedBoundsCannotCertifyDistantOpenSky() {
            physics.SetSurfaces(true,"Aligned test room");string imported=null;
            yield return ResidentModelAt(new Vector3(-8,4,-8),id=>imported=id);
            string area=NativeAreaFor(imported);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{imported},out error),Is.True,error);
            editor.Undo();Assert.That(editor.Read(imported).kind,Is.EqualTo(RoomObjectKind.ImportedModel));
            Assert.That(editor.NativeEntityDormant(imported),Is.True);Assert.That(editor.ReadCollisionBounds(imported).Known,Is.False,"Undo must not reuse the deleted native import's old collision envelope");
            var point=new Vector3(8,1,8);var cover=new WeatherCover();int revision=editor.Revision;
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Unknown));
            Assert.That((string)SavedWeatherExposure(point)["state"],Is.EqualTo("unknown"));
            Assert.That(cover.Segment(editor,point,point+Vector3.down,true),Is.EqualTo(RainExposure.Unknown));
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(imported),Is.Null);Assert.That(editor.Revision,Is.EqualTo(revision));
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{imported},out error),Is.True,error);
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Open));
        }
        [UnityTest]public IEnumerator WeatherCoverSpatialRealRoomOptOutStillNeedsVirtualCoverCompleteness() {
            physics.SetSurfaces(true,"Aligned test room");var point=new Vector3(8,1,8);var cover=new WeatherCover();
            string actor=editor.Identity(block),profile=NewEnvironment();Assert.That(AssignEnvironment(actor,profile,out var error),Is.True,error);
            var scanned=RainRoof(point+Vector3.up*2,true);
            Assert.That(cover.Exposure(editor,point,Vector3.down*6,editor.Find("maestro")),Is.EqualTo(RainExposure.Covered));
            Assert.That(cover.Exposure(editor,point,Vector3.down*6,block),Is.EqualTo(RainExposure.Open));
            Assert.That((bool)SavedWeatherExposure(point,actor)["realRoom"],Is.False);
            UnityEngine.Object.DestroyImmediate(scanned);
            string roof=SavedWeatherRoof(point+Vector3.up*3),area=NativeAreaFor(roof);
            Assert.That(cover.Exposure(editor,point,Vector3.down*6,block),Is.EqualTo(RainExposure.Covered));
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;Physics.SyncTransforms();
            Assert.That(cover.Exposure(editor,point,Vector3.down*6,block),Is.EqualTo(RainExposure.Unknown));
            Assert.That(cover.Exposure(editor,point,Vector3.down*6,editor.Find("maestro")),Is.EqualTo(RainExposure.Unknown));
            physics.SetSurfaces(false,"Scan unavailable");Assert.That((string)SavedWeatherExposure(point,actor)["state"],Is.EqualTo("unknown"));
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{roof},out error),Is.True,error);
            Assert.That(cover.Exposure(editor,point,Vector3.down*6,block),Is.EqualTo(RainExposure.Open));
            Assert.That(cover.Exposure(editor,point,Vector3.down*6,editor.Find("maestro")),Is.EqualTo(RainExposure.Unknown));
        }
        [UnityTest]public IEnumerator WeatherCoverSpatialTransformsUpstreamBoundsWithTheWorldFrame() {
            physics.SetSurfaces(true,"Aligned test room");var roomPoint=new Vector3(8,1,8);var cover=new WeatherCover();
            string roof=SavedWeatherRoof(roomPoint+Vector3.up*3),area=NativeAreaFor(roof);
            root.transform.SetPositionAndRotation(new Vector3(2,1,-3),Quaternion.Euler(20,40,15));root.transform.localScale=Vector3.one*1.5f;Physics.SyncTransforms();
            Assert.That(editor.Frame.Valid,Is.True);var point=editor.Frame.PointToWorld(roomPoint);var velocity=WeatherCover.Velocity(editor);
            Assert.That(cover.Exposure(editor,point,velocity),Is.EqualTo(RainExposure.Covered));
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;Physics.SyncTransforms();
            Assert.That(cover.Exposure(editor,point,velocity),Is.EqualTo(RainExposure.Unknown));
            Assert.That((string)SavedWeatherExposure(roomPoint)["state"],Is.EqualTo("unknown"));
            var moved=editor.Read(roof);moved.position=new Vector3(-8,4,-8);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{moved},Array.Empty<string>(),out error),Is.True,error);Physics.SyncTransforms();
            Assert.That(cover.Exposure(editor,point,velocity),Is.EqualTo(RainExposure.Open));
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(roof),Is.Null);
        }
        [UnityTest]public IEnumerator WeatherCoverSpatialPartiallyDisabledRegisteredRoofCannotProveCover() {
            physics.SetSurfaces(true,"Aligned test room");var point=new Vector3(8,1,8);var cover=new WeatherCover();
            string roof=SavedWeatherRoof(point+Vector3.up*3);var data=editor.Read(roof);
            data.collision.shapes=new[]{data.collision.shapes[0],new CollisionShape{id="second_roof",size=new Vector3(2,.025f,2)}};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);Physics.SyncTransforms();
            var solids=editor.Find(roof).Grab.colliders.Where(c=>!c.isTrigger).ToArray();Assert.That(solids.Length,Is.EqualTo(2));
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Covered));
            solids[0].enabled=false;Physics.SyncTransforms();
            Assert.That(solids[1].Raycast(new Ray(point,Vector3.up),out _,WeatherCover.Distance),Is.True,"Retain a real hit to exercise the early Covered path");
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Unknown));
            Assert.That((string)SavedWeatherExposure(point)["state"],Is.EqualTo("unknown"));
            solids[1].enabled=false;Physics.SyncTransforms();
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Unknown),"Removing all native hits cannot establish Open either");
            foreach(var solid in solids)solid.enabled=true;Physics.SyncTransforms();
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Covered));
            Assert.That(editor.NativeActivationPending,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator WeatherCoverSpatialUnreadyRigidCannotUseItsRemainingNativeHitAsCover() {
            physics.SetSurfaces(true,"Aligned test room");var point=new Vector3(8,1,8);var cover=new WeatherCover();
            string roof=SavedWeatherRoof(point+Vector3.up*3);var item=editor.Find(roof);var rigid=item.GetComponent<RigidRoomItem>();
            var solid=item.Grab.colliders.First(c=>!c.isTrigger);Assert.That(rigid.GeometryReady,Is.True);
            rigid.SetGeometryReady(false);Physics.SyncTransforms();
            Assert.That(solid.enabled&&solid.gameObject.activeInHierarchy,Is.True);
            Assert.That(solid.Raycast(new Ray(point,Vector3.up),out _,WeatherCover.Distance),Is.True,"The hit exists but is not authoritative geometry");
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Unknown));
            Assert.That(cover.Segment(editor,point,point+Vector3.up*4,true),Is.EqualTo(RainExposure.Unknown));
            Assert.That((string)SavedWeatherExposure(point)["state"],Is.EqualTo("unknown"));
            rigid.SetGeometryReady(true);Physics.SyncTransforms();
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Covered));
            Assert.That(editor.NativeActivationPending,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator WeatherCoverSpatialLostImportedGeometryCannotUseStaleNativeHit() {
            physics.SetSurfaces(true,"Aligned test room");string imported=null;
            yield return ResidentModelAt(new Vector3(8,3,8),id=>imported=id);
            var item=editor.Find(imported);var model=item.GetComponent<CreatedRoomObject>();var rigid=item.GetComponent<RigidRoomItem>();
            var solid=item.Grab.colliders.First(c=>!c.isTrigger);Physics.SyncTransforms();
            var bounds=solid.bounds;var point=bounds.center-Vector3.up*(bounds.extents.y+1);var cover=new WeatherCover();
            Assert.That(model.isActiveAndEnabled&&model.ModelGeometryReady,Is.True);
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Covered));
            Assert.That(editor.ResidentCollisionGuard(imported),Is.Null);
            model.enabled=false;
            // Deliberately do not publish a replacement guard: the passive read
            // must reject the stale native hit on its own in this same frame.
            Assert.That(rigid.GeometryReady,Is.True);
            Assert.That(solid.enabled&&solid.gameObject.activeInHierarchy,Is.True);
            Assert.That(solid.Raycast(new Ray(point,Vector3.up),out _,WeatherCover.Distance),Is.True);
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Unknown));
            Assert.That((string)SavedWeatherExposure(editor.Frame.PointToRoom(point))["state"],Is.EqualTo("unknown"));
            Assert.That(editor.NativeActivationPending,Is.False);
            Assert.That(editor.ResidentCollisionGuard(imported),Is.Null,"Passive exposure must not repair or publish native collision state");
            model.enabled=true;
            Assert.That(model.isActiveAndEnabled&&model.ModelGeometryReady,Is.True);
            Assert.That(cover.Exposure(editor,point,Vector3.down*6),Is.EqualTo(RainExposure.Covered));
            Assert.That(editor.NativeActivationPending,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator WeatherCoverSpatialCollectionWaitsForDisabledRoofAndRecoversWithoutRainBacklog() {
            string cup=RainCup();physics.PausePhysics();yield return LiquidSaved();
            var cupData=editor.Read(cup);cupData.physics=ItemPhysics.Fixed;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{cupData},Array.Empty<string>(),out var error),Is.True,error);
            var item=editor.Find(cup);var point=item.transform.position+Vector3.up*.3f;
            string roof=SavedWeatherRoof(editor.Frame.PointToRoom(point)+Vector3.up*2);
            var solid=editor.Find(roof).Grab.colliders.First(c=>!c.isTrigger);solid.enabled=false;Physics.SyncTransforms();
            Assert.That(new WeatherCover().Exposure(editor,point,Vector3.down*6,item),Is.EqualTo(RainExposure.Unknown));
            physics.StartPhysics();for(int i=0;i<10;i++)editor.Liquids.Tick(.1f);
            var waiting=editor.Liquids.ObserveRain(cup);
            Assert.That((string)waiting["phase"],Is.EqualTo("waiting"),waiting.ToString());
            Assert.That((bool)waiting["ready"],Is.False);Assert.That((double)waiting["exposure"],Is.Zero);
            Assert.That(FlowAmount(cup),Is.Zero);Assert.That(editor.Liquids.Active,Is.False);
            solid.enabled=true;Physics.SyncTransforms();editor.Liquids.Tick(.05f);
            Assert.That(new WeatherCover().Exposure(editor,point,Vector3.down*6,item),Is.EqualTo(RainExposure.Covered));
            var recovered=editor.Liquids.ObserveRain(cup);Assert.That((string)recovered["reason"],Does.Contain("covered"),recovered.ToString());
            Assert.That(FlowAmount(cup),Is.Zero,"Recovery under a complete roof remains dry");
            physics.PausePhysics();yield return LiquidSaved();
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{roof},out error),Is.True,error);
            Physics.SyncTransforms();physics.StartPhysics();editor.Liquids.Tick(.05f);
            double expected=60*cupData.containers[0].FootprintArea*.05*1000/3600;
            Assert.That(FlowAmount(cup),Is.EqualTo(expected).Within(.000001),"Only the newly exposed active step may collect rain");
            physics.PausePhysics();yield return LiquidSaved();
        }
        [UnityTest]public IEnumerator WeatherCoverSpatialRainPresentationStaysBoundedAndHidesUnderDormantRoof() {
            physics.SetSurfaces(true,"Aligned test room");
            Assert.That(editor.SetWeather(editor.WeatherRevision,42,new(){rainMmPerHour=120},0,out var error),Is.True,error);
            var view=root.AddComponent<WorldRainView>();view.Initialize(editor,leftAnchor.transform);view.enabled=false;
            for(int i=0;i<12;i++)view.Sample(i*.05f);Assert.That(view.VisibleDrops,Is.InRange(1,WorldRainView.MaximumDrops));
            var mesh=view.GetComponentsInChildren<MeshFilter>().Last(x=>x.sharedMesh?.name=="Bounded rain streaks").sharedMesh;
            Assert.That(mesh.vertexCount,Is.EqualTo(WorldRainView.MaximumDrops*4));
            string roof=SavedWeatherRoof(editor.Frame.PointToRoom(leftAnchor.transform.position)+Vector3.up*7,4),area=NativeAreaFor(roof);
            for(int i=0;i<12;i++)view.Sample(10+i*.05f);Assert.That(view.VisibleDrops,Is.Zero);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;Physics.SyncTransforms();int revision=editor.Revision;
            for(int i=0;i<24;i++)view.Sample(20+i*.05f);
            Assert.That(view.VisibleDrops,Is.Zero,"Missing saved roof geometry must not reveal rain indoors");
            Assert.That(mesh.vertexCount,Is.EqualTo(WorldRainView.MaximumDrops*4));Assert.That(view.GetComponentsInChildren<Collider>().Any(x=>x.gameObject.name=="Local rain"),Is.False);
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(roof),Is.Null);Assert.That(editor.Revision,Is.EqualTo(revision));
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{roof},out error),Is.True,error);
            for(int i=0;i<12;i++)view.Sample(30+i*.05f);Assert.That(view.VisibleDrops,Is.InRange(1,WorldRainView.MaximumDrops));
        }
    }
}
