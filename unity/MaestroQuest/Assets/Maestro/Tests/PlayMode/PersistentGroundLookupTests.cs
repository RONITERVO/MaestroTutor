// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class AvatarSpatialTests {
        [UnityTest] public IEnumerator GroundFootProbeRejectsMissingUpperSurfaceAfterWorldRelocation(){
            WalkingScene();Terrain(new(4,-.2f,4),0,out _);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Upper foot surface",new(4,0,4),1,Color.white,out var id,out var error),Is.True,error);
            var data=editor.Read(id);data.physics=ItemPhysics.Fixed;
            data.heightFields=new[]{new RoomHeightField{cells=4,width=2,depth=2,maxHeight=.5f,heights=Enumerable.Repeat(.1f,25).ToArray()}};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            yield return ScanBoundsPrepared(id);string area=ScanBoundsArea(id);
            root.transform.SetPositionAndRotation(new(3,1,2),Quaternion.Euler(0,37,0));Physics.SyncTransforms();
            var point=root.transform.TransformPoint(new Vector3(4,.1f,4));var actor=editor.Find("maestro");
            Assert.That(editor.SampleFootGround(actor,point,.4f,out _),Is.True,"Resident upper support is available before retirement");
            world.PausePhysics();yield return null;
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);world.StartPhysics();Physics.SyncTransforms();
            Assert.That(editor.GroundComplete(new Bounds(new(4,.1f,4),new(.05f,.8f,.05f))),Is.False);
            var query=new RoomGroundQuery();query.Capture(physicalRoot.transform);
            Assert.That(query.Sample(point,.4f,.4f,out var lower),Is.True,"A lower resident surface makes incorrect fallback observable");
            Assert.That(root.transform.InverseTransformPoint(lower.point).y,Is.EqualTo(-.2f).Within(.001));
            Assert.That(world.SimulationActive&&world.EnvironmentReady(actor)&&editor.Frame.Valid&&!editor.RuntimeGate.Held,Is.True);
            Assert.That(editor.SampleFootGround(actor,point,.4f,out _),Is.False,"World-space foot probes cannot bypass unloaded room-space ground");
        }
    }
    public sealed partial class RoomRulesTests {
        string[] GroundLookupGroup(){
            string area=NewRegion();var ids=new string[4];
            for(int i=0;i<ids.Length;i++){
                var position=i==0?new Vector3(4,0,4):new Vector3(10+i*2,0,-6);
                Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Ground lookup fixture",position,1,Color.white,out var id,out var error),Is.True,error);
                var data=editor.Read(id);data.physics=ItemPhysics.Fixed;
                data.heightFields=new[]{new RoomHeightField{cells=4,width=2,depth=2,maxHeight=.5f,heights=Enumerable.Repeat(.1f,25).ToArray()}};
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
                Assert.That(AssignRegion(id,area,out error),Is.True,error);ids[i]=id;
            }
            Assert.That(editor.RetireNativeArea(area,out var issue),Is.True,issue);return ids;
        }
        string[] ExhaustiveGroundLookup(string[] ids,Bounds envelope)=>ids.Where(id=>{
            var spatial=editor.ReadSpatialBounds(id,out var source);var extent=spatial.Visual;
            if(source!="authored"){
                if(!spatial.Collision.Known)extent.Known=false;
                else if(spatial.Collision.HasBounds)extent.Include(spatial.Collision.Bounds,Matrix4x4.identity);
            }
            return !extent.Known||!extent.HasBounds||extent.Bounds.Intersects(envelope);
        }).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        void AssertGroundLookup(string[] ids,Bounds envelope){
            var expected=ExhaustiveGroundLookup(ids,envelope);var actual=editor.GroundDependencies(envelope);
            CollectionAssert.AreEqual(expected,actual.Select(x=>(string)x["target"]));Assert.That(editor.GroundComplete(envelope),Is.EqualTo(expected.Length==0));
        }
        [UnityTest] public IEnumerator PersistentGroundLookupReusesDormantGeometryWithoutWarmProbeAllocations(){
            var ids=GroundLookupGroup();yield return VisualLookupPrepared();
            var envelope=new Bounds(GroundPoint,new Vector3(.05f,.8f,.05f));AssertGroundLookup(ids,envelope);
            var before=editor.GroundQueryWork;int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
            for(int i=0;i<30;i++){yield return null;Assert.That(editor.GroundComplete(envelope),Is.False);}
            var warm=editor.GroundQueryWork;Assert.That(warm.reads,Is.EqualTo(before.reads));Assert.That(warm.build,Is.EqualTo(before.build));
            Assert.That(warm.candidates-before.candidates,Is.EqualTo(30));
            for(int i=0;i<8;i++)editor.GroundComplete(envelope);
            long allocated=GC.GetAllocatedBytesForCurrentThread();int complete=0;
            for(int i=0;i<256;i++)if(editor.GroundComplete(envelope))complete++;
            allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Assert.That(complete,Is.Zero);Assert.That(allocated,Is.Zero,"Warm boolean support queries must not allocate lists or captured callbacks");
            root.transform.SetPositionAndRotation(new(2,1,-3),Quaternion.Euler(0,47,0));yield return null;AssertGroundLookup(ids,envelope);
            Assert.That(editor.GroundQueryWork.reads,Is.EqualTo(before.reads));Assert.That(editor.GroundQueryWork.build,Is.EqualTo(before.build));
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(ids.All(id=>editor.Find(id)==null),Is.True);
            string folder=Environment.GetEnvironmentVariable("MAESTRO_GROUND_LOOKUP_EVIDENCE");
            if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"warm-ground-query.json"),new JObject{
                ["dormantSurfaces"]=ids.Length,["frames"]=30,["boundsReads"]=warm.reads-before.reads,["indexBuildWork"]=warm.build-before.build,
                ["queryCandidatesPerFrame"]=1,["directWarmCalls"]=256,["managedBytes"]=allocated,["worldRelocated"]=true,
                ["boundary"]="Editor ground-query path only; JSON facts, live native geometry and whole-frame/Quest costs are separate."}.ToString());}
        }
        [UnityTest] public IEnumerator PersistentGroundLookupMatchesExhaustiveAfterEditsUndoDeletionAndReload(){
            var ids=GroundLookupGroup();yield return VisualLookupPrepared();
            var random=new System.Random(735);
            for(int i=0;i<80;i++)AssertGroundLookup(ids,new Bounds(new((float)random.NextDouble()*22-2,(float)random.NextDouble()*3-1,(float)random.NextDouble()*18-9),new(3,2,3)));
            var near=new Bounds(GroundPoint,Vector3.one);AssertGroundLookup(ids,near);
            var data=editor.Read(ids[0]);data.position=Vector3.up*3;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);AssertGroundLookup(ids,near);
            editor.Undo();AssertGroundLookup(ids,near);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{ids[0]},out error),Is.True,error);AssertGroundLookup(ids.Skip(1).ToArray(),near);
            editor.Undo();Assert.That(editor.Find(ids[0]),Is.Not.Null);Assert.That(editor.GroundDependencies(near),Is.Empty,"Restored native support is ready, not a stale dormant dependency");
            editor.enabled=false;editor.enabled=true;Assert.That(editor.GroundComplete(near),Is.True);
        }
        [UnityTest] public IEnumerator PersistentGroundLookupKeepsNativeAvailabilityAndPoseFreshWithoutEdits(){
            string id=AvailabilityTerrain();yield return VisualLookupPrepared();var item=editor.Find(id);
            var old=new Bounds(GroundPoint,Vector3.one);Assert.That(editor.GroundComplete(old),Is.True);int revision=editor.Revision;
            item.gameObject.SetActive(false);Assert.That(editor.GroundComplete(old),Is.False);
            item.transform.localPosition=Vector3.zero;
            Assert.That(editor.GroundComplete(old),Is.True);Assert.That(editor.GroundComplete(new Bounds(Vector3.zero,Vector3.one)),Is.False);
            item.gameObject.SetActive(true);Assert.That(editor.GroundComplete(new Bounds(Vector3.zero,Vector3.one)),Is.True);
            Assert.That(editor.Revision,Is.EqualTo(revision));
        }
    }

}
