// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static Bounds CollisionPoint(Vector3 point)=>new(point,Vector3.one*.01f);
        void CaptureCollisionLookup(string name,RoomEditor.MovementCandidates snapshot){
            string folder=Environment.GetEnvironmentVariable("MAESTRO_COLLISION_DIRECTORY_EVIDENCE");if(string.IsNullOrEmpty(folder))return;
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,name+".json"),new JObject{
                ["objects"]=editor.SpatialObjectCount,["boundsReads"]=snapshot.BoundsReads,["dormantBoundsReads"]=snapshot.DormantBoundsReads,
                ["indexBuildWork"]=snapshot.IndexBuildWork,["candidates"]=snapshot.Visits,["nodes"]=snapshot.NodeVisits,
                ["nativePending"]=editor.NativeActivationPending}.ToString());
        }
        [UnityTest] public IEnumerator PersistentCollisionLookupReusesDormantGeometryWhileLiveObjectsAndTheWorldMove()
        {
            var ids=new List<string>();string area=NewRegion();
            for(int i=0;i<18;i++){
                Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Unloaded query fixture",new(5+i*.75f,1,5),.2f,Color.white,out var id,out var error),Is.True,error);
                Assert.That(AssignRegion(id,area,out error),Is.True,error);ids.Add(id);
            }
            Assert.That(editor.RetireNativeArea(area,out var issue),Is.True,issue);yield return null;
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            var first=editor.CaptureMovementCandidates(false,repeatedQueries:false);
            var expected=editor.ReadCollisionBounds(ids[8]).Bounds;var query=CollisionPoint(expected.center);
            Assert.That(first.Targets(query,null),Does.Contain(ids[8]));
            var warm=editor.CaptureMovementCandidates(false,repeatedQueries:false);
            Assert.That(warm.DormantBoundsReads,Is.Zero,"An unchanged dormant partition must not read every retired object's bounds again");
            Assert.That(warm.BoundsReads,Is.LessThanOrEqualTo(editor.SpatialObjectCount-ids.Count));
            Assert.That(warm.IndexBuildWork,Is.Zero,"The retained tree must not be rebuilt for a one-off query");
            Assert.That(warm.Targets(query,null),Does.Contain(ids[8]));CaptureCollisionLookup("warm-dormant-query",warm);
            string moving=editor.Identity(block);var before=editor.ReadCollisionBounds(moving).Bounds;
            block.transform.localPosition+=Vector3.right*3;var after=editor.ReadCollisionBounds(moving).Bounds;
            var moved=editor.CaptureMovementCandidates(false,repeatedQueries:false);
            Assert.That(moved.DormantBoundsReads,Is.Zero);Assert.That(moved.Targets(CollisionPoint(after.center),null),Does.Contain(moving));
            Assert.That(moved.Targets(CollisionPoint(before.center),null),Does.Not.Contain(moving));
            Assert.That(first.Targets(CollisionPoint(before.center),null),Does.Contain(moving),"An older synchronous capture owns its previous live values");
            root.transform.SetPositionAndRotation(new(2,0,-3),Quaternion.Euler(0,37,0));root.transform.localScale=Vector3.one*1.25f;
            var relocated=editor.CaptureMovementCandidates(false,repeatedQueries:false);
            Assert.That(relocated.DormantBoundsReads,Is.Zero);Assert.That(relocated.Targets(query,null),Does.Contain(ids[8]));
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            Assert.That(editor.NativeActivationPending,Is.False);CaptureCollisionLookup("moved-live-and-world",relocated);
        }
        [UnityTest] public IEnumerator PersistentCollisionLookupAcceptedEditsDeleteAndUndoReplaceTheCapturedPartition()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);block.transform.localPosition+=Vector3.right*3;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var oldBounds=editor.ReadCollisionBounds(id).Bounds;var old=editor.CaptureMovementCandidates(false);
            var changed=editor.Read(id);changed.position+=Vector3.left*4;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            var replacement=editor.ReadCollisionBounds(id).Bounds;var current=editor.CaptureMovementCandidates(false);
            Assert.That(current.Targets(CollisionPoint(oldBounds.center),null),Does.Not.Contain(id));
            Assert.That(current.Targets(CollisionPoint(replacement.center),null),Does.Contain(id));
            Assert.That(old.Targets(CollisionPoint(oldBounds.center),null),Does.Contain(id));
            Assert.That(editor.CaptureMovementCandidates(false).DormantBoundsReads,Is.Zero);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{id},out error),Is.True,error);
            Assert.That(editor.SpatialMovementTargets(CollisionPoint(replacement.center),null),Does.Not.Contain(id));
            editor.Undo();Assert.That(editor.HasSavedObject(id),Is.True);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeEntityDormant(id),Is.True);
            Assert.That(editor.SpatialMovementTargets(CollisionPoint(replacement.center),null),Does.Contain(id),"Restored cold geometry remains a dependency until ready");
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));var restored=editor.Find(id);Assert.That(restored,Is.Not.Null);
            restored.transform.localPosition+=Vector3.forward*3;var live=editor.ReadCollisionBounds(id).Bounds;
            Assert.That(editor.SpatialMovementTargets(CollisionPoint(live.center),null),Does.Contain(id));
            Assert.That(editor.SpatialMovementTargets(CollisionPoint(replacement.center),null),Does.Not.Contain(id));yield return null;
        }
        [UnityTest] public IEnumerator PersistentCollisionLookupCancellationAndActivationKeepTheCorrectPartition()
        {
            string id=editor.Identity(block),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            var before=editor.ReadCollisionBounds(id).Bounds;editor.CaptureMovementCandidates(false);
            using(var cancel=new CancellationTokenSource()){
                var opening=editor.ActivateNativeArea(area,cancel.Token);cancel.Cancel();yield return NativeAreaCompletion(opening,false);
            }
            Assert.That(editor.Find(id),Is.Null);Assert.That(editor.SpatialMovementTargets(CollisionPoint(before.center),null),Does.Contain(id));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));var item=editor.Find(id);item.transform.localPosition+=Vector3.forward*3;
            var after=editor.ReadCollisionBounds(id).Bounds;var current=editor.CaptureMovementCandidates(false);
            Assert.That(current.Targets(CollisionPoint(after.center),null),Does.Contain(id));Assert.That(current.Targets(CollisionPoint(before.center),null),Does.Not.Contain(id));
        }
        [UnityTest] public IEnumerator PersistentCollisionLookupGeometryChangeNeverUsesOldClearanceOrVisualPreparation()
        {
            string id=RecipeTarget(),area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var distant=CollisionPoint(new(100,100,100));Assert.That(editor.SpatialMovementTargets(distant,null),Does.Not.Contain(id));
            var changed=editor.Read(id);changed.recipe.parts[0].size*=2;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            Assert.That(editor.ReadCollisionBounds(id).Known,Is.False);Assert.That(editor.SpatialMovementTargets(distant,null),Does.Contain(id));
            float deadline=Time.realtimeSinceStartup+5;
            while(!(bool)editor.ObserveAuthoredBounds(id)["prepared"]&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That((bool)editor.ObserveAuthoredBounds(id)["prepared"],Is.True);
            Assert.That(editor.ReadCollisionBounds(id).Known,Is.False);Assert.That(editor.SpatialMovementTargets(distant,null),Does.Contain(id));
            Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator PersistentCollisionLookupTemporaryDiscardCannotKeepTheForksPlacement()
        {
            yield return WaitForModuleLibrary();string id=editor.Identity(block);var original=editor.ReadCollisionBounds(id).Bounds;
            editor.CaptureMovementCandidates(false);Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            string area=NativeAreaFor(id);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
            var changed=editor.Read(id);changed.position+=Vector3.right*5;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
            var fork=editor.ReadCollisionBounds(id).Bounds;Assert.That(editor.SpatialMovementTargets(CollisionPoint(fork.center),null),Does.Contain(id));
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.ReadCollisionBounds(id).Known,Is.False,"Temporary bounds cannot survive journal replacement");
            Assert.That(editor.SpatialMovementTargets(CollisionPoint(fork.center),null),Does.Contain(id),"Unknown cold geometry remains a conservative candidate until prepared");
            yield return NativeAreaCompletion(editor.AcquireNativeEntities(new[]{id},()=>true,CancellationToken.None));
            var restored=editor.CaptureMovementCandidates(false);Assert.That(restored.Targets(CollisionPoint(original.center),null),Does.Contain(id));
            Assert.That(restored.Targets(CollisionPoint(fork.center),null),Does.Not.Contain(id));
        }
    }
    public sealed partial class AvatarSpatialTests
    {
        [UnityTest] public IEnumerator PersistentCollisionLookupResolvesDormantScanAnchorsOnEveryCapture()
        {
            var scan=SharedEnvironment(out var platform);var wall=InkWall();var source=new LayoutSource{Entries=new[]{wall}};yield return LoadedLayout(scan,platform,source);
            string id=MakeLayer(out _);yield return InkAdd(id);yield return ScanBoundsPrepared(id);string area=ScanBoundsArea(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var far=new Bounds(new(100,100,100),Vector3.one*.01f);var saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
            Assert.That(editor.ReadCollisionBounds(id).Known,Is.True);Assert.That(editor.SpatialMovementTargets(far,null),Does.Not.Contain(id));
            source.Entries=Array.Empty<ScannedSurface>();yield return null;
            Assert.That(editor.ReadCollisionBounds(id).Known,Is.False);Assert.That(editor.SpatialMovementTargets(far,null),Does.Contain(id));
            source.Entries=new[]{wall};yield return null;
            Assert.That(editor.ReadCollisionBounds(id).Known,Is.True);Assert.That(editor.SpatialMovementTargets(far,null),Does.Not.Contain(id));
            Assert.That(editor.Find(id),Is.Null);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
    }
}
