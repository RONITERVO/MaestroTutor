// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
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
    public sealed partial class RoomRulesTests
    {
        static readonly Vector3 GroundPoint=new(4,.1f,4);
        string AvailabilityTerrain()
        {
            string id=editor.Identity(block);var data=editor.Read(id);
            data.position=new Vector3(4,0,4);data.rotation=Quaternion.identity;data.scale=1;data.motion=null;data.physics=ItemPhysics.Fixed;
            data.heightFields=new[]{new RoomHeightField{cells=4,width=2,depth=2,maxHeight=.5f,heights=Enumerable.Repeat(.1f,25).ToArray()}};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            return id;
        }
        JObject AvailabilityFact(Vector3 point)
        {
            Assert.That(BehaviourCatalog.TryRead("world.ground",1,new JObject{["position"]=WorkspaceViewpoint.Point(point),["radius"]=.2},
                new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);
            Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);
        }
        [UnityTest] public IEnumerator GroundAvailabilityRetirementAndReloadAreTruthfulAndPassive()
        {
            string id=AvailabilityTerrain(),area=NativeAreaFor(id);
            var supported=AvailabilityFact(GroundPoint);Assert.That((bool)supported["found"],Is.True);Assert.That((string)supported["availability"],Is.EqualTo("ready"));
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            Assert.That(editor.TryFlush(out error),Is.True,error);int revision=editor.Revision;
            string saved=JsonUtility.ToJson(editor.Snapshot()),path=Path.Combine(editor.SaveDirectory,RoomStorage.FileName),bytes=File.ReadAllText(path);
            for(int i=0;i<4;i++){
                var fact=AvailabilityFact(GroundPoint);Assert.That((bool)fact["found"],Is.False);
                Assert.That((string)fact["availability"],Is.EqualTo("unavailable"));Assert.That((int)fact["dependencyCount"],Is.EqualTo(1));
                Assert.That((string)fact["dependencies"][0]["target"],Is.EqualTo(id));Assert.That((bool)fact["dependencies"][0]["boundsKnown"],Is.True);
                Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeActivationPending,Is.False);Assert.That(physics.Running,Is.False);
            }
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(File.ReadAllText(path),Is.EqualTo(bytes));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_WORLD_GROUND");
            if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"unavailable.json"),AvailabilityFact(GroundPoint).ToString());}
            var distant=AvailabilityFact(Vector3.zero);Assert.That((string)distant["availability"],Is.EqualTo("ready"));Assert.That((bool)distant["found"],Is.False);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            Assert.That(JToken.DeepEquals(supported,AvailabilityFact(GroundPoint)),Is.True);
        }
        [UnityTest] public IEnumerator GroundAvailabilityDoesNotFallBackThroughMissingUpperTerrain()
        {
            string id=AvailabilityTerrain(),area=NativeAreaFor(id);
            Assert.That(editor.CopyObject(id,editor.ObjectRevision(id),"Lower floor",new Vector3(4,-.2f,4),out var lower,out var error),Is.True,error);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            var query=new RoomGroundQuery();Physics.SyncTransforms();query.Capture(root.transform);
            Assert.That(query.Support(GroundPoint,.2f,.5f,.5f,out var hit,out _,out _),Is.True);Assert.That(hit.y,Is.EqualTo(-.1f).Within(.001));
            var fact=AvailabilityFact(GroundPoint);Assert.That((bool)fact["found"],Is.False);Assert.That((string)fact["availability"],Is.EqualTo("unavailable"));
            Assert.That(editor.Find(lower),Is.Not.Null);
        }
        [UnityTest] public IEnumerator GroundAvailabilityTracksDormantPlacementWithoutNativeActivation()
        {
            string id=AvailabilityTerrain(),area=NativeAreaFor(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var moved=editor.Read(id);moved.position=Vector3.zero;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{moved},Array.Empty<string>(),out error),Is.True,error);
            Assert.That((string)AvailabilityFact(GroundPoint)["availability"],Is.EqualTo("ready"));
            var atOrigin=AvailabilityFact(Vector3.zero);Assert.That((string)atOrigin["availability"],Is.EqualTo("unavailable"));Assert.That((bool)atOrigin["dependencies"][0]["boundsKnown"],Is.True);
            moved=editor.Read(id);moved.position=Vector3.up*3;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{moved},Array.Empty<string>(),out error),Is.True,error);
            Assert.That((string)AvailabilityFact(Vector3.zero)["availability"],Is.EqualTo("ready"),"Vertical separation excludes unrelated terrain too");
            Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator GroundAvailabilityUnknownEditedBoundsStayConservativeUntilPrepared()
        {
            string id=AvailabilityTerrain(),area=NativeAreaFor(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var data=editor.Read(id);data.heightFields[0].heights[0]+=.01f;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            var unknown=AvailabilityFact(Vector3.zero);Assert.That((string)unknown["availability"],Is.EqualTo("unavailable"));Assert.That((bool)unknown["dependencies"][0]["boundsKnown"],Is.False);
            float deadline=Time.realtimeSinceStartup+5;
            while((string)AvailabilityFact(Vector3.zero)["availability"]!="ready"&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That((string)AvailabilityFact(Vector3.zero)["availability"],Is.EqualTo("ready"));
            Assert.That((string)AvailabilityFact(GroundPoint)["availability"],Is.EqualTo("unavailable"));Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator GroundAvailabilityFailedImportedSurfaceNeverBecomesEmptyGround()
        {
            yield return GeometryModel(BuildingModelFixture.Create());string id=geometryTarget;
            ContainerRun(new RoomAgentExecutor(editor),GeometryCall(BuildingSettings()));
            var extent=editor.ReadSpatialBounds(id,out _).Visual;Assert.That(extent.Known&&extent.HasBounds,Is.True);
            var point=extent.Bounds.center;string area=NativeAreaFor(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            string path=Path.Combine(directory,"models",editor.Read(id).modelHash+".glb"),parked=path+".test-missing";
            File.Move(path,parked);
            try{
                yield return NativeAreaCompletion(editor.ActivateNativeArea(area),false);
                Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Failed));
                var fact=AvailabilityFact(point);Assert.That((bool)fact["found"],Is.False);Assert.That((string)fact["availability"],Is.EqualTo("unavailable"));
                Assert.That((string)fact["dependencies"][0]["target"],Is.EqualTo(id));Assert.That(editor.Find(id),Is.Null);
            }finally{File.Move(parked,path);}
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            Assert.That((string)AvailabilityFact(point)["availability"],Is.EqualTo("ready"));
        }
        [UnityTest] public IEnumerator GroundAvailabilityIncludesFootprintEdgesAndBoundsItsSharedResult()
        {
            string id=AvailabilityTerrain(),area=NativeAreaFor(id);
            var preview=editor.Read(id).heightFields[0].Copy();preview.width=preview.depth=.1f;
            editor.Find(id).GetComponent<HeightFieldView>().Preview(preview);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            // The centre misses the surface bounds, but a footprint edge overlaps.
            Assert.That((string)AvailabilityFact(new Vector3(5.15f,.1f,4))["availability"],Is.EqualTo("unavailable"));
            Assert.That((string)AvailabilityFact(new Vector3(5.5f,.1f,4))["availability"],Is.EqualTo("ready"));
            var envelope=RoomGroundQuery.SupportEnvelope(Vector3.zero,.2f,.5f,.5f);
            Assert.That(envelope.Contains(new Vector3(.2f,.69f,0)),Is.True,"Footprint rays extend above the centre search band");
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            for(int i=0;i<3;i++){
                Assert.That(editor.CopyObject(id,editor.ObjectRevision(id),"Dormant ground "+i,new Vector3(4,0,4),out var copy,out error),Is.True,error);
                string extra=NativeAreaFor(copy);Assert.That(editor.RetireNativeArea(extra,out error),Is.True,error);yield return null;
            }
            // Keep the existing four-heightfield budget. Other ground can be
            // imported rigid geometry; result truncation must cover both kinds.
            for(int i=0;i<2;i++){
                yield return GeometryModel(StaticModelFixture.Create());
                ContainerRun(new RoomAgentExecutor(editor),GeometryCall(BuildingSettings()));
                var imported=editor.Read(geometryTarget);imported.position=new Vector3(4,0,4);
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{imported},Array.Empty<string>(),out error),Is.True,error);
                string extra=NativeAreaFor(geometryTarget);Assert.That(editor.RetireNativeArea(extra,out error),Is.True,error);yield return null;
            }
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            var fact=AvailabilityFact(GroundPoint);Assert.That((int)fact["dependencyCount"],Is.EqualTo(6));Assert.That(((JArray)fact["dependencies"]).Count,Is.EqualTo(4));
            var targets=fact["dependencies"].Select(x=>(string)x["target"]).ToArray();Assert.That(targets,Is.EqualTo(targets.OrderBy(x=>x,StringComparer.Ordinal).ToArray()));
        }
    }
}
