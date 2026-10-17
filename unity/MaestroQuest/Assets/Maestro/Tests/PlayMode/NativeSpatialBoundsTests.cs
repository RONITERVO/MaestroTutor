// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static void SpatialContains(RoomSpatialExtent envelope,Vector3 point)
        {
            Assert.That(envelope.Known&&envelope.HasBounds,Is.True);
            var expanded=envelope.Bounds;expanded.Expand(.0001f);
            Assert.That(expanded.Contains(point),Is.True,$"{point} outside {envelope.Bounds}");
        }
        void SpatialContainsRenderers(string id,RoomSpatialExtent envelope)
        {
            foreach(var renderer in editor.Find(id).GetComponentsInChildren<MeshRenderer>(true)){
                var bounds=renderer.localBounds;
                for(int i=0;i<8;i++)SpatialContains(envelope,editor.Frame.PointToRoom(renderer.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))));
            }
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsSurviveRetirementAndUseActualIdlePoseWithoutWriting()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);block.transform.SetLocalPositionAndRotation(new Vector3(3,2,-1),Quaternion.Euler(10,65,20));block.transform.localScale=Vector3.one*1.7f;
            var before=editor.ReadSpatialBounds(id,out var source);Assert.That(source,Is.EqualTo("native"));SpatialContainsRenderers(id,before.Visual);
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            for(int i=0;i<3;i++){
                var retained=editor.ReadSpatialBounds(id,out source);Assert.That(source,Is.EqualTo("retained"));
                Assert.That(retained.Visual.Bounds.center,Is.EqualTo(before.Visual.Bounds.center));Assert.That(retained.Visual.Bounds.size,Is.EqualTo(before.Visual.Bounds.size));
                Assert.That(retained.Collision.Known&&retained.Collision.HasBounds,Is.True);Assert.That(editor.Find(id),Is.Null);
                Assert.That(SpatialBoundsFact.Definition().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["target"]=id},out var fact),Is.True);
                Assert.That((string)((JObject)fact.Value)["coordinates"],Is.EqualTo("room"));Assert.That((bool)((JObject)fact.Value)["visual"]["known"],Is.True);
            }
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));SpatialContainsRenderers(id,before.Visual);
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsReprojectPlacementWhileRenameAndUnrelatedEditsPreserveGeometry()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);block.transform.localPosition+=Vector3.up;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var before=editor.ReadSpatialBounds(id,out _);var renamed=editor.Read(id);renamed.name="Still the same block";
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{renamed},Array.Empty<string>(),out error),Is.True,error);
            Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Bounds,Is.EqualTo(before.Visual.Bounds));
            var peer=editor.Read("book");peer.name="Unrelated edit";Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{peer},Array.Empty<string>(),out error),Is.True,error);
            Assert.That(editor.ReadSpatialBounds(id,out _).Visual.Bounds,Is.EqualTo(before.Visual.Bounds));
            var moved=editor.Read(id);moved.position=new Vector3(-3,1,2);moved.rotation=Quaternion.Euler(15,30,40);moved.scale=2;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{moved},Array.Empty<string>(),out error),Is.True,error);
            var projected=editor.ReadSpatialBounds(id,out var source);Assert.That(source,Is.EqualTo("retained"));Assert.That(projected.Visual.Known,Is.True);
            Assert.That((projected.Visual.Bounds.center-moved.position).magnitude,Is.LessThan(.001f));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));SpatialContainsRenderers(id,projected.Visual);
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsInvalidateChangedDrawingAndUndoRecoversWithoutLoading()
        {
            string id=NewStroke(),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var before=editor.ReadSpatialBounds(id,out _);Assert.That(before.Visual.Known&&before.Visual.HasBounds,Is.True);
            var edited=editor.Read(id);edited.radius*=2;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{edited},Array.Empty<string>(),out error),Is.True,error);
            var stale=editor.ObserveSpatialBounds(id);Assert.That((string)stale["source"],Is.EqualTo("unknown"));Assert.That((bool)stale["visual"]["known"],Is.False);Assert.That((bool)stale["visual"]["hasBounds"],Is.False);Assert.That(editor.Find(id),Is.Null);
            editor.Undo();Assert.That(editor.ReadSpatialBounds(id,out var source).Visual.Bounds,Is.EqualTo(before.Visual.Bounds));Assert.That(source,Is.EqualTo("retained"));Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsKeepStoppedRecipePartPose()
        {
            string id=RecipeTarget(true),area=NativeAreaFor(id);var recipe=editor.Find(id).GetComponent<RecipeObject>();yield return null;recipe.Stop();
            var part=recipe.Part(editor.Read(id).recipe.tracks[0].part);part.localRotation=Quaternion.Euler(20,45,60);
            var before=editor.ReadSpatialBounds(id,out _);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var after=editor.ReadSpatialBounds(id,out _);Assert.That(after.Visual.Bounds,Is.EqualTo(before.Visual.Bounds));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));SpatialContainsRenderers(id,after.Visual);
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsImportedGeometryInvalidatesWhenPivotChanges()
        {
            var asset=ModelLibrary.Inspect("bounds.glb",ModelFixture.Create());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var placement=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);yield return new WaitUntil(()=>placement.IsCompleted);Assert.That(placement.Exception,Is.Null);
            string id=placement.Result,area=NativeAreaFor(id);var before=editor.ReadSpatialBounds(id,out _);Assert.That(before.Collision.Known,Is.True);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;Assert.That(editor.ReadSpatialBounds(id,out var source).Collision.Bounds,Is.EqualTo(before.Collision.Bounds));Assert.That(source,Is.EqualTo("retained"));
            var data=editor.Read(id);data.modelGeometry.pivot="base";Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            Assert.That(editor.ReadSpatialBounds(id,out source).Collision.Known,Is.False);Assert.That(source,Is.EqualTo("unknown"));Assert.That(editor.Find(id),Is.Null);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.ReadSpatialBounds(id,out source).Collision.Known,Is.True);Assert.That(source,Is.EqualTo("native"));
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsDormantObservationTracksViewsRoomFrameAndSourceLayerOverride()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);block.transform.localPosition=new Vector3(0,0,3);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var camera=NativeObservationCamera();Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);Assert.That(editor.Find(id),Is.Null);
            camera.transform.localRotation=Quaternion.Euler(0,180,0);Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);
            camera.transform.localRotation=Quaternion.identity;root.transform.SetPositionAndRotation(new Vector3(20,4,-10),Quaternion.Euler(0,65,0));root.transform.localScale=Vector3.one*1.2f;
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);camera.enabled=false;
            var capture=NativeObservationCamera(false);capture.cullingMask=1<<31;
            using(editor.ObserveNativeCamera(capture))Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);
            using(editor.ObserveNativeCamera(capture,sourceLayers:-1))Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True);
            Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsUnknownDormantGeometryRemainsAnObservationCandidate()
        {
            string id=NewStroke(),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var changed=editor.Read(id);changed.radius*=2;changed.position=new Vector3(0,0,-3);Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            var camera=NativeObservationCamera();Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.True,"Unknown must not imply outside the view");
            camera.cullingMask=0;Assert.That(RetainedFor(id,RoomRetentionReason.Observation),Is.False);Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsSeparateMissingEmptyUnsupportedAndScanAnchoredContent()
        {
            var missing=editor.ObserveSpatialBounds(new string('0',32));Assert.That((string)missing["source"],Is.EqualTo("missing"));Assert.That((bool)missing["visual"]["known"],Is.False);
            var empty=editor.ReadSpatialBounds("book",out _);Assert.That(empty.Visual.Known,Is.True);Assert.That(empty.Visual.HasBounds,Is.False);Assert.That(empty.Collision.HasBounds,Is.True);
            var skin=block.gameObject.AddComponent<SkinnedMeshRenderer>();var unsupported=editor.ReadSpatialBounds(editor.Identity(block),out _);Assert.That(unsupported.Visual.Known,Is.False);Assert.That(unsupported.Collision.Known,Is.True);UnityEngine.Object.Destroy(skin);
            var anchored=editor.Read(editor.Identity(block));anchored.scanAnchors=new[]{new ScanDrawingAnchor()};Assert.That(RoomSpatialBounds.Capture(block,anchored).Visual.Known,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsCollisionUsesDisabledAcceptedGeometryAndConservativeRoundScale()
        {
            string id=editor.Identity(block);var collider=block.Grab.colliders[0];collider.enabled=false;
            Assert.That(editor.ReadSpatialBounds(id,out _).Collision.HasBounds,Is.True);
            var child=new GameObject("Nonuniform sphere");child.transform.SetParent(block.transform,false);child.transform.localScale=new Vector3(1,3,.5f);
            var sphere=child.AddComponent<SphereCollider>();sphere.radius=.5f;sphere.center=Vector3.right;block.Grab.colliders.Add(sphere);
            var bounds=editor.ReadSpatialBounds(id,out _);var centre=editor.Frame.PointToRoom(child.transform.TransformPoint(sphere.center));
            foreach(var direction in new[]{Vector3.up,Vector3.down,Vector3.right,Vector3.left,Vector3.forward,Vector3.back})SpatialContains(bounds.Collision,centre+direction*1.5f);
            sphere.isTrigger=true;var triggers=editor.ReadSpatialBounds(id,out _);Assert.That(triggers.Collision.Bounds.size.magnitude,Is.LessThan(bounds.Collision.Bounds.size.magnitude));yield return null;
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsDeletedAndReplacedJournalsCannotReuseOldEnvelope()
        {
            yield return WaitForModuleLibrary();string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            editor.ReadSpatialBounds(id,out var source);Assert.That(source,Is.EqualTo("native"));
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{id},out error),Is.True,error);
            Assert.That((string)editor.ObserveSpatialBounds(id)["source"],Is.EqualTo("missing"));Assert.That(editor.NativeEntityDormant(id),Is.False);
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsReadActualTextRenderersWithoutRequiringMeshFilters()
        {
            var child=new GameObject("Generated text geometry");child.transform.SetParent(editor.Find("book").transform,false);
            var text=child.AddComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text="Room audio state";text.characterSize=.02f;
            yield return null;Assert.That(child.TryGetComponent<MeshFilter>(out _),Is.False);Assert.That(child.GetComponent<MeshRenderer>(),Is.Not.Null);
            var bounds=editor.ReadSpatialBounds("book",out var source);Assert.That(source,Is.EqualTo("native"));Assert.That(bounds.Visual.Known&&bounds.Visual.HasBounds,Is.True);SpatialContainsRenderers("book",bounds.Visual);
            Assert.That(SpatialBoundsFact.Definition().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["target"]="book"},out _),Is.True);
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsOverflowAndNonfiniteGeometryRemainUnknown()
        {
            var extent=RoomSpatialExtent.Empty;extent.Include(new Bounds(Vector3.one*float.MaxValue,Vector3.zero),Matrix4x4.identity);
            extent.Include(new Bounds(Vector3.one*-float.MaxValue,Vector3.zero),Matrix4x4.identity);Assert.That(extent.Known,Is.False);
            var invalid=RoomSpatialExtent.Empty;var mapping=Matrix4x4.identity;mapping.m00=float.NaN;invalid.Include(new Bounds(Vector3.zero,Vector3.one),mapping);Assert.That(invalid.Known,Is.False);
            Assert.That(default(RoomSpatialExtent).Transform(Matrix4x4.identity).Known,Is.False);Assert.That(RoomSpatialExtent.Empty.Transform(Matrix4x4.identity).Known,Is.True);yield return null;
        }
        [UnityTest] public IEnumerator NativeSpatialBoundsInvalidFrameDoesNotTurnUnknownGeometryIntoEmptySpace()
        {
            string id=editor.Identity(block);root.transform.localScale=new Vector3(1,2,1);
            var value=editor.ReadSpatialBounds(id,out var source);Assert.That(source,Is.EqualTo("unknown"));Assert.That(value.Visual.Known,Is.False);Assert.That(value.Collision.Known,Is.False);yield return null;
        }
    }
}
