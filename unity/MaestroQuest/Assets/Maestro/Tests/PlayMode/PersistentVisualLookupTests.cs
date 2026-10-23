// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        string[] VisualLookupGroup(int count,out string area){
            area=NewRegion();var ids=new List<string>();
            for(int i=0;i<count;i++){
                var position=i==0?new Vector3(0,0,6):new Vector3(14+i%3*2,0,-6-i*.25f);
                Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Visual lookup fixture",position,.2f,Color.white,out var id,out var error),Is.True,error);
                Assert.That(AssignRegion(id,area,out error),Is.True,error);ids.Add(id);
            }
            Assert.That(editor.RetireNativeArea(area,out var issue),Is.True,issue);return ids.ToArray();
        }
        IEnumerator VisualLookupPrepared(){
            for(int i=0;i<80&&editor.Snapshot().objects.Any(x=>!(bool)editor.ObserveAuthoredBounds(x.id)["prepared"]);i++)yield return null;
            Assert.That(editor.Snapshot().objects.All(x=>(bool)editor.ObserveAuthoredBounds(x.id)["prepared"]),Is.True);
        }
        string[] VisualLookupVisible(Camera camera){
            var view=new RoomObservationView(camera);
            return editor.QueryDormantView(new[]{view}).Where(x=>view.Includes(x.Spatial.Transform(root.transform.localToWorldMatrix))).Select(x=>x.Id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        void VisualLookupMatches(string[] ids,Camera camera){
            var view=new RoomObservationView(camera);
            var expected=ids.Where(id=>view.Includes(editor.ReadSpatialBounds(id,out _).Transform(root.transform.localToWorldMatrix))).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected,VisualLookupVisible(camera));
        }
        [UnityTest] public IEnumerator PersistentVisualLookupReusesColdGeometryAcrossFramesAndWorldRelocation(){
            var ids=VisualLookupGroup(18,out _);yield return VisualLookupPrepared();var camera=NativeObservationCamera(false);
            CollectionAssert.AreEqual(new[]{ids[0]},VisualLookupVisible(camera));var before=editor.VisualQueryWork;
            int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
            for(int i=0;i<30;i++){yield return null;CollectionAssert.AreEqual(new[]{ids[0]},VisualLookupVisible(camera));}
            var warm=editor.VisualQueryWork;Assert.That(warm.reads,Is.EqualTo(before.reads));Assert.That(warm.build,Is.EqualTo(before.build));
            var candidates=editor.QueryDormantView(new[]{new RoomObservationView(camera)});
            Assert.That(candidates.Length,Is.LessThan(ids.Length/2));VisualLookupMatches(ids,camera);
            camera.transform.SetParent(null,true);
            try{
                root.transform.SetPositionAndRotation(new(2,1,-3),Quaternion.Euler(0,37,0));root.transform.localScale=Vector3.one*1.25f;
                yield return null;VisualLookupMatches(ids,camera);
                Assert.That(editor.VisualQueryWork.reads,Is.EqualTo(before.reads));Assert.That(editor.VisualQueryWork.build,Is.EqualTo(before.build));
            }finally{camera.transform.SetParent(root.transform,true);}
            Assert.That(ids.All(id=>editor.Find(id)==null),Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            string folder=Environment.GetEnvironmentVariable("MAESTRO_VISUAL_LOOKUP_EVIDENCE");
            if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"warm-visual-query.json"),new JObject{
                ["dormantObjects"]=ids.Length,["frames"]=30,["boundsReads"]=warm.reads-before.reads,["indexBuildWork"]=warm.build-before.build,
                ["queryCandidates"]=candidates.Length,["worldRelocated"]=true,["nativePending"]=editor.NativeActivationPending,
                ["boundary"]="Directory work in the Editor; current native renderers, lamps, metadata synchronization and Quest frame time are separate costs."}.ToString());}
        }
        [UnityTest] public IEnumerator PersistentVisualLookupTracksEditsUndoDeletionAndNativeMembership(){
            var ids=VisualLookupGroup(3,out var area);yield return VisualLookupPrepared();var camera=NativeObservationCamera(false);
            CollectionAssert.AreEqual(new[]{ids[0]},VisualLookupVisible(camera));
            var changed=editor.Read(ids[0]);changed.position=new(15,0,-5);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out var error),Is.True,error);Assert.That(VisualLookupVisible(camera),Is.Empty);
            editor.Undo();CollectionAssert.AreEqual(new[]{ids[0]},VisualLookupVisible(camera));
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{ids[0]},out error),Is.True,error);Assert.That(VisualLookupVisible(camera),Is.Empty);
            editor.Undo();Assert.That(editor.Find(ids[0]),Is.Not.Null);Assert.That(VisualLookupVisible(camera),Is.Empty,"Restored live objects cannot remain in the dormant tree");
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);CollectionAssert.AreEqual(new[]{ids[0]},VisualLookupVisible(camera));
            editor.enabled=false;editor.enabled=true;CollectionAssert.AreEqual(new[]{ids[0]},VisualLookupVisible(camera));
        }
        [UnityTest] public IEnumerator PersistentVisualLookupKeepsExplicitDependenciesAndUnknownFrameWithoutLoading(){
            var ids=VisualLookupGroup(18,out _);yield return VisualLookupPrepared();var camera=NativeObservationCamera(false);var view=new RoomObservationView(camera);
            var candidates=editor.QueryDormantView(new[]{view},new[]{ids[17],ids[17],"missing",ids[0]});
            CollectionAssert.AreEquivalent(new[]{ids[0],ids[17]},candidates.Select(x=>x.Id));
            root.transform.localScale=new(1,2,1);Assert.That(editor.Frame.Valid,Is.False);
            CollectionAssert.AreEquivalent(ids,editor.QueryDormantView(new[]{view}).Select(x=>x.Id));
            root.transform.localScale=Vector3.one;CollectionAssert.AreEqual(new[]{ids[0]},VisualLookupVisible(camera));
            Assert.That(ids.All(id=>editor.Find(id)==null),Is.True);
        }
    }
    public sealed partial class AvatarSpatialTests {
        [UnityTest] public IEnumerator PersistentVisualLookupResolvesExactAnchorsFreshWithoutRereadingColdNeighbors(){
            var scan=SharedEnvironment(out var platform);var wall=InkWall();var source=new LayoutSource{Entries=new[]{wall}};yield return LoadedLayout(scan,platform,source);
            string id=MakeLayer(out _);yield return InkAdd(id);yield return ScanBoundsPrepared(id);string area=ScanBoundsArea(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Cold visual neighbor",new(8,1,8),.2f,Color.white,out var cold,out error),Is.True,error);
            yield return ScanBoundsPrepared(cold);Assert.That(editor.RetireNativeArea(ScanBoundsArea(cold),out error),Is.True,error);yield return null;
            var go=new GameObject("Anchor query camera",typeof(Camera));go.transform.SetParent(root.transform,false);var camera=go.GetComponent<Camera>();camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;
            RoomEditor.DormantVisual ReadAnchor()=>editor.QueryDormantView(new[]{new RoomObservationView(camera)}).Single(x=>x.Id==id);
            Assert.That(ReadAnchor().Placed,Is.True);var before=editor.VisualQueryWork;Assert.That(ReadAnchor().Placed,Is.True);
            Assert.That(editor.VisualQueryWork.reads-before.reads,Is.EqualTo(1));Assert.That(editor.VisualQueryWork.build,Is.EqualTo(before.build));
            source.Entries=Array.Empty<ScannedSurface>();yield return null;yield return null;
            var lost=ReadAnchor();Assert.That(lost.Placed,Is.False);Assert.That(lost.Spatial.Visual.Known,Is.False);
            source.Entries=new[]{wall};yield return null;yield return null;Assert.That(ReadAnchor().Placed,Is.True);
            Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Find(cold),Is.Null);
        }
    }
}
