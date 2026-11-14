// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        static object MovementPrivate(object owner,string field)=>owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
        delegate void MovementRefresh(out int reads,out int build,out int updates,out int compactions);
        void MovementWarmDirectoryDoesNotAllocate(){
            // Bind the private production method once, outside the measured loop.
            // Dynamic snapshot allocation belongs to CaptureMovementCandidates,
            // so this proves only the unchanged directory-refresh fast path.
            var method=typeof(RoomEditor).GetMethod("RefreshMovementDirectory",BindingFlags.Instance|BindingFlags.NonPublic);
            var refresh=(MovementRefresh)method.CreateDelegate(typeof(MovementRefresh),editor);
            for(int i=0;i<8;i++)refresh(out _,out _,out _,out _);
            long before=GC.GetAllocatedBytesForCurrentThread();int work=0;
            for(int i=0;i<256;i++){refresh(out int reads,out int build,out int updates,out int compactions);work+=reads+build+updates+compactions;}
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(work,Is.Zero);Assert.That(allocated,Is.Zero,"An unchanged directory refresh must not allocate cursors or delta collections");
        }
        string[] IncrementalMovementGroup(int count,out string area,bool independent=false,bool fixedPhysics=false){
            area=NewRegion();var ids=new string[count];
            if(independent){var region=editor.ReadRegion(area);region.loading="independent";Assert.That(editor.EditRegion(region,area,editor.RegionRevision(area),region.members,out var error),Is.True,error);}
            for(int i=0;i<count;i++){
                Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Incremental movement fixture",new(5+i%8*.75f,1,5+i/8*.75f),.2f,Color.white,out ids[i],out var error),Is.True,error);
                Assert.That(AssignRegion(ids[i],area,out error),Is.True,error);
                if(fixedPhysics){var data=editor.Read(ids[i]);data.physics=ItemPhysics.Fixed;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);}
            }
            Assert.That(editor.RetireNativeArea(area,out var issue),Is.True,issue);return ids;
        }
        RoomEditor.MovementCandidates.Candidate[] MovementOracleValues()=>editor.Snapshot().objects.Where(data=>{
            var item=editor.Find(data.id);return !item||item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame!=true;
        }).Select(data=>new RoomEditor.MovementCandidates.Candidate(data.id,editor.ReadCollisionBounds(data.id),!data.IsBuiltIn&&data.physics==ItemPhysics.Fixed&&(data.heightFields.Length>0||data.kind==RoomObjectKind.ImportedModel&&data.modelGeometry.walkable&&data.modelGeometry.meshCollision))).ToArray();
        static string[] MovementOracle(RoomEditor.MovementCandidates.Candidate[] values,Bounds body,Bounds? support,string actor=null)=>values.Where(value=>value.Id!=actor&&(!value.Collision.Known||value.Collision.HasBounds&&value.Collision.Bounds.Intersects(body)||support.HasValue&&value.Ground&&(!value.Collision.HasBounds||value.Collision.Bounds.Intersects(support.Value)))).Select(value=>value.Id).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        static void MovementOracleMatches(RoomEditor.MovementCandidates capture,RoomEditor.MovementCandidates.Candidate[] values){
            var random=new System.Random(3117);
            for(int i=0;i<24;i++){
                var body=new Bounds(new((float)random.NextDouble()*15,(float)random.NextDouble()*4,(float)random.NextDouble()*15),Vector3.one*1.5f);
                Bounds? support=i%2==0?new Bounds(body.center-Vector3.up,new Vector3(2,.25f,2)):null;
                string actor=values.Length>0&&i%3==0?values[i%values.Length].Id:null;
                CollectionAssert.AreEqual(MovementOracle(values,body,support,actor),capture.Targets(body,support,actor),"Exhaustive probe "+i);
            }
        }
        static void MovementNoColdWork(RoomEditor.MovementCandidates capture){
            Assert.That(capture.DormantBoundsReads,Is.Zero);Assert.That(capture.IndexBuildWork,Is.Zero);
            Assert.That(capture.DormantOverlayUpdates,Is.Zero);Assert.That(capture.DormantCompactions,Is.Zero);
        }
        [UnityTest] public IEnumerator IncrementalMovementLiveAndMultiObjectEditsReadOnlyChangedDormantIdentities(){
            var ids=IncrementalMovementGroup(64,out _);yield return VisualLookupPrepared();
            var old=editor.CaptureMovementCandidates(true,false);var oldValues=MovementOracleValues();MovementWarmDirectoryDoesNotAllocate();
            var journal=(RoomJournal)MovementPrivate(editor,"journal");var oldGround=MovementPrivate(journal,"groundCandidates");
            var live=editor.Read(editor.Identity(block));live.name="Renamed live actor";live.color=Color.blue;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{live},Array.Empty<string>(),out var error),Is.True,error);
            var renamed=editor.CaptureMovementCandidates(true,false);MovementNoColdWork(renamed);MovementOracleMatches(renamed,MovementOracleValues());
            yield return VisualLookupPrepared();MovementNoColdWork(editor.CaptureMovementCandidates(true,false));
            var edits=ids.Take(3).Select(editor.Read).ToArray();foreach(var value in edits)value.position+=Vector3.up*2;
            live=editor.Read(live.id);live.name="Second live name";
            Assert.That(editor.ApplyAgentEdit(editor.Revision,edits.Append(live).ToArray(),Array.Empty<string>(),out error),Is.True,error);
            var changed=editor.CaptureMovementCandidates(true,false);
            Assert.That(changed.DormantBoundsReads,Is.EqualTo(3));Assert.That(changed.DormantOverlayUpdates,Is.EqualTo(3));Assert.That(changed.DormantCompactions,Is.Zero);Assert.That(changed.IndexBuildWork,Is.Zero);
            MovementOracleMatches(changed,MovementOracleValues());MovementOracleMatches(old,oldValues);MovementNoColdWork(editor.CaptureMovementCandidates(true,false));
            Assert.That(MovementPrivate(journal,"groundCandidates"),Is.SameAs(oldGround),"Incremental classification must not run the whole ground enumeration");
            var source=editor.Read(ids[4]);source.color=Color.magenta;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{source},Array.Empty<string>(),out error),Is.True,error);
            var pending=editor.CaptureMovementCandidates(true,false);Assert.That(pending.DormantBoundsReads,Is.EqualTo(1));Assert.That(pending.IndexBuildWork,Is.Zero);
            Assert.That(pending.Targets(CollisionPoint(new(100,100,100)),null),Does.Contain(source.id),"A replacement unknown extent is considered even when its old base bounds miss the query");
            yield return VisualLookupPrepared();var published=editor.CaptureMovementCandidates(true,false);Assert.That(published.DormantBoundsReads,Is.EqualTo(1));Assert.That(published.IndexBuildWork,Is.Zero);
            MovementOracleMatches(published,MovementOracleValues());MovementOracleMatches(old,oldValues);
        }
        [UnityTest] public IEnumerator IncrementalMovementRepeatedSameIdentityEditsAndHistoryKeepOldCapturesImmutable(){
            var ids=IncrementalMovementGroup(64,out _);yield return VisualLookupPrepared();
            var original=editor.CaptureMovementCandidates(true,false);var originalValues=MovementOracleValues();string id=ids[0];var origin=editor.Read(id).position;
            for(int i=0;i<300;i++){
                var value=editor.Read(id);value.position=origin+Vector3.up*(i%2==0?2:3);
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{value},Array.Empty<string>(),out var error),Is.True,error);
                var changed=editor.CaptureMovementCandidates(true,false);Assert.That(changed.DormantBoundsReads,Is.EqualTo(1));Assert.That(changed.DormantOverlayUpdates,Is.EqualTo(1));Assert.That(changed.DormantOverrideCount,Is.EqualTo(1));Assert.That(changed.DormantCompactions,Is.Zero);Assert.That(changed.IndexBuildWork,Is.Zero);
                Assert.That(changed.Targets(CollisionPoint(origin),null),Does.Not.Contain(id));Assert.That(changed.Targets(CollisionPoint(value.position),null),Does.Contain(id));
            }
            var beforeDelete=editor.CaptureMovementCandidates(true,false);var beforeValues=MovementOracleValues();
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{id},out var issue),Is.True,issue);
            var removed=editor.CaptureMovementCandidates(true,false);Assert.That(removed.DormantBoundsReads,Is.Zero);Assert.That(removed.DormantOverlayUpdates,Is.EqualTo(1));Assert.That(removed.IndexBuildWork,Is.Zero);MovementOracleMatches(removed,MovementOracleValues());
            editor.Undo();Assert.That(editor.HasSavedObject(id)&&editor.NativeEntityDormant(id),Is.True);
            var restored=editor.CaptureMovementCandidates(true,false);Assert.That(restored.DormantBoundsReads,Is.EqualTo(1));Assert.That(restored.DormantOverrideCount,Is.EqualTo(1));MovementOracleMatches(restored,MovementOracleValues());
            editor.Redo();Assert.That(editor.HasSavedObject(id),Is.False);var redone=editor.CaptureMovementCandidates(true,false);Assert.That(redone.DormantBoundsReads,Is.Zero);Assert.That(redone.DormantOverrideCount,Is.EqualTo(1));MovementOracleMatches(redone,MovementOracleValues());
            MovementOracleMatches(original,originalValues);MovementOracleMatches(beforeDelete,beforeValues);
            MovementNoColdWork(editor.CaptureMovementCandidates(true,false));
        }
        [UnityTest] public IEnumerator IncrementalMovementCompactsDistinctOverridesIncludingTombstonesWithoutRereadingNeighbors(){
            var ids=IncrementalMovementGroup(40,out _);yield return VisualLookupPrepared();
            var original=editor.CaptureMovementCandidates(true,false);var originalValues=MovementOracleValues();RoomEditor.MovementCandidates atLimit=null;RoomEditor.MovementCandidates.Candidate[] atLimitValues=null;
            for(int i=0;i<33;i++){
                bool remove=i%3==0;var changed=editor.Read(ids[i]);changed.position+=Vector3.up*3;
                Assert.That(editor.ApplyAgentEdit(editor.Revision,remove?Array.Empty<RoomObjectData>():new[]{changed},remove?new[]{ids[i]}:Array.Empty<string>(),out var error),Is.True,error);
                var capture=editor.CaptureMovementCandidates(true,false);Assert.That(capture.DormantBoundsReads,Is.EqualTo(remove?0:1));Assert.That(capture.DormantOverlayUpdates,Is.EqualTo(1));
                Assert.That(capture.DormantCompactions,Is.EqualTo(i==32?1:0));Assert.That(capture.DormantOverrideCount,Is.EqualTo(i==32?0:i+1));
                if(i<32)Assert.That(capture.IndexBuildWork,Is.Zero);else Assert.That(capture.IndexBuildWork,Is.GreaterThan(0));
                if(i==31){atLimit=capture;atLimitValues=MovementOracleValues();}
            }
            var current=editor.CaptureMovementCandidates(true,false);MovementNoColdWork(current);MovementOracleMatches(current,MovementOracleValues());MovementOracleMatches(original,originalValues);MovementOracleMatches(atLimit,atLimitValues);
        }
        [UnityTest] public IEnumerator IncrementalMovementEmptyPartitionAdmitsNewUnknownIdentityAndMetadataDoesNotDeclareCollisionReady(){
            yield return VisualLookupPrepared();editor.CaptureMovementCandidates(true,false);
            string area=NewRegion();Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"New dormant identity",new(7,1,7),.2f,Color.white,out var id,out var error),Is.True,error);
            Assert.That(AssignRegion(id,area,out error),Is.True,error);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            var value=editor.Read(id);value.color=Color.magenta;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{value},Array.Empty<string>(),out error),Is.True,error);
            Assert.That((bool)editor.ObserveAuthoredBounds(id)["prepared"],Is.False);
            var first=editor.CaptureMovementCandidates(true,false);Assert.That(first.DormantBoundsReads,Is.EqualTo(1));Assert.That(first.DormantOverlayUpdates,Is.EqualTo(1));Assert.That(first.IndexBuildWork,Is.Zero);
            var distant=CollisionPoint(new(100,100,100));Assert.That(first.Targets(distant,null),Does.Contain(id));Assert.That(editor.ReadCollisionBounds(id).Known,Is.False);
            yield return VisualLookupPrepared();var prepared=editor.CaptureMovementCandidates(true,false);Assert.That(prepared.DormantBoundsReads,Is.EqualTo(1));Assert.That(prepared.IndexBuildWork,Is.Zero);
            Assert.That(editor.ReadCollisionBounds(id).Known,Is.False,"Visual publication must not turn into evidence of native collision readiness");Assert.That(prepared.Targets(distant,null),Does.Contain(id));
            Assert.That(editor.Find(id),Is.Null);MovementOracleMatches(prepared,MovementOracleValues());
        }
        [UnityTest] public IEnumerator IncrementalMovementNativeActivationAndRetirementOnlyChangeTheirOwnPartition(){
            // Fixed bodies avoid the real restoration/physics-input consumer
            // refreshing this shared cache before the measured explicit capture.
            var ids=IncrementalMovementGroup(8,out var area,independent:true,fixedPhysics:true);yield return VisualLookupPrepared();
            Assert.That(ids.All(editor.NativeEntityDormant),Is.True);Assert.That(editor.ReadRegion(area).Loading,Is.EqualTo("independent"));
            var original=editor.CaptureMovementCandidates(true,false);var originalValues=MovementOracleValues();string id=ids[0];var oldPosition=editor.ReadCollisionBounds(id).Bounds.center;
            yield return NativeAreaCompletion(editor.AcquireNativeEntities(new[]{id},()=>true,CancellationToken.None));
            Assert.That(editor.NativeEntityDormant(id),Is.False);Assert.That(editor.Find(id),Is.Not.Null);Assert.That(ids.Skip(1).All(editor.NativeEntityDormant),Is.True);
            var active=editor.CaptureMovementCandidates(true,false);Assert.That(active.DormantBoundsReads,Is.Zero);Assert.That(active.DormantOverlayUpdates,Is.EqualTo(1));Assert.That(active.IndexBuildWork,Is.Zero);
            editor.Find(id).transform.localPosition+=Vector3.up*3;var moved=editor.CaptureMovementCandidates(true,false);MovementNoColdWork(moved);Assert.That(moved.Targets(CollisionPoint(oldPosition),null),Does.Not.Contain(id));
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);var retired=editor.CaptureMovementCandidates(true,false);Assert.That(retired.DormantBoundsReads,Is.EqualTo(1));Assert.That(retired.DormantOverlayUpdates,Is.EqualTo(1));Assert.That(retired.IndexBuildWork,Is.Zero);
            MovementOracleMatches(retired,MovementOracleValues());MovementOracleMatches(original,originalValues);
            yield return null;MovementNoColdWork(editor.CaptureMovementCandidates(true,false));
        }
        [UnityTest] public IEnumerator IncrementalMovementExactAuthoredWindowAvoidsRebuildButLostWindowRefreshesAllDormantObjects(){
            var ids=IncrementalMovementGroup(8,out _);yield return VisualLookupPrepared();editor.CaptureMovementCandidates(true,false);string live=editor.Identity(block);
            for(int round=0;round<2;round++){
                int edits=RoomSpatialChanges.Capacity+round;
                for(int i=0;i<edits;i++){
                    var value=editor.Read(live);value.name="Live edit "+round+" "+i;
                    Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{value},Array.Empty<string>(),out var error),Is.True,error);
                }
                var capture=editor.CaptureMovementCandidates(true,false);Assert.That(capture.DormantBoundsReads,Is.EqualTo(round==0?0:ids.Length));
                if(round==0)Assert.That(capture.IndexBuildWork,Is.Zero);else Assert.That(capture.IndexBuildWork,Is.GreaterThan(0));
                MovementOracleMatches(capture,MovementOracleValues());MovementNoColdWork(editor.CaptureMovementCandidates(true,false));
            }
        }
        IEnumerator IncrementalMovementWindowLoss(string source){
            var ids=IncrementalMovementGroup(4,out _);yield return VisualLookupPrepared();editor.CaptureMovementCandidates(true,false);
            var journal=(RoomJournal)MovementPrivate(editor,"journal");var index=(RoomSpatialIndex)MovementPrivate(editor,"spatialIndex");
            var membership=(RoomSpatialChanges)MovementPrivate(editor,"collisionResidencyChanges");string live=editor.Identity(block);var authored=editor.Snapshot();
            for(int round=0;round<2;round++){
                for(int i=0;i<RoomSpatialChanges.Capacity+round;i++){
                    if(source=="placement"){
                        block.transform.localPosition=Vector3.up*(i%2==0?1:2);editor.RememberPlacement(live);
                    }else if(source=="publication"){
                        // Exercise the owned metadata log without changing the authored log.
                        // The unrelated live projection is always read natively by the consumer.
                        authored.objects.Single(x=>x.id==live).color=i%2==0?Color.blue:Color.red;index.Synchronize(authored);
                    }else{
                        // Membership records are invalidations, not truth: repeat a fixture
                        // identity to lose only this window; current residency stays authoritative.
                        membership.Record(live);
                    }
                }
                var captured=editor.CaptureMovementCandidates(true,false);
                Assert.That(captured.DormantBoundsReads,Is.EqualTo(round==0?0:ids.Length),source);
                if(round==0)Assert.That(captured.IndexBuildWork,Is.Zero,source);else Assert.That(captured.IndexBuildWork,Is.GreaterThan(0),source);
                MovementOracleMatches(captured,MovementOracleValues());MovementNoColdWork(editor.CaptureMovementCandidates(true,false));
            }
        }
        [UnityTest] public IEnumerator IncrementalMovementPlacementWindowLossCannotReuseTheOldDirectory(){yield return IncrementalMovementWindowLoss("placement");}
        [UnityTest] public IEnumerator IncrementalMovementPublicationWindowLossCannotReuseTheOldDirectory(){yield return IncrementalMovementWindowLoss("publication");}
        [UnityTest] public IEnumerator IncrementalMovementMembershipWindowLossCannotReuseTheOldDirectory(){yield return IncrementalMovementWindowLoss("membership");}
        [UnityTest] public IEnumerator IncrementalMovementProjectionFailureDoesNotConsumeDeltasOrMutateCapturedValues(){
            var ids=IncrementalMovementGroup(4,out _);yield return VisualLookupPrepared();var original=editor.CaptureMovementCandidates(true,false);var before=MovementOracleValues();
            string id=ids[0];var value=editor.Read(id);value.position+=Vector3.up*3;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{value},Array.Empty<string>(),out var error),Is.True,error);
            var entries=(IDictionary)MovementPrivate(editor,"dormantNative");var saved=entries[id];
            // Deliberately inject a projection failure only inside this synchronous
            // fixture. No invalid authored model or production test hook is added.
            try{entries[id]=null;Assert.Throws<NullReferenceException>(()=>editor.CaptureMovementCandidates(true,false));}
            finally{entries[id]=saved;}
            var retry=editor.CaptureMovementCandidates(true,false);Assert.That(retry.DormantBoundsReads,Is.EqualTo(1));Assert.That(retry.DormantOverlayUpdates,Is.EqualTo(1));Assert.That(retry.IndexBuildWork,Is.Zero);
            MovementOracleMatches(retry,MovementOracleValues());MovementOracleMatches(original,before);MovementNoColdWork(editor.CaptureMovementCandidates(true,false));
        }
        [UnityTest] public IEnumerator IncrementalMovementEqualRevisionJournalAndIndexReplacementForceOwnedRefresh(){
            var ids=IncrementalMovementGroup(4,out _);yield return VisualLookupPrepared();
            var field=typeof(RoomEditor).GetField("journal",BindingFlags.NonPublic|BindingFlags.Instance);var indexField=typeof(RoomEditor).GetField("spatialIndex",BindingFlags.NonPublic|BindingFlags.Instance);
            var originalJournal=(RoomJournal)field.GetValue(editor);var originalIndex=(RoomSpatialIndex)indexField.GetValue(editor);
            using var firstReplacementIndex=new RoomSpatialIndex((hash,cancel)=>System.Threading.Tasks.Task.FromResult(Maestro.Quest.Imports.ModelAssetBounds.Unknown("Fixture")));
            using var replacementIndex=new RoomSpatialIndex((hash,cancel)=>System.Threading.Tasks.Task.FromResult(Maestro.Quest.Imports.ModelAssetBounds.Unknown("Fixture")));
            try{
                var firstJournal=new RoomJournal(editor.Snapshot());field.SetValue(editor,firstJournal);var old=editor.CaptureMovementCandidates(true,false);var before=MovementOracleValues();
                var nextJournal=new RoomJournal(firstJournal.Snapshot());Assert.That(nextJournal.SpatialCatalogStamp,Is.EqualTo(firstJournal.SpatialCatalogStamp));Assert.That(nextJournal.SpatialAuthoredVersion,Is.EqualTo(firstJournal.SpatialAuthoredVersion));
                field.SetValue(editor,nextJournal);var changed=editor.CaptureMovementCandidates(true,false);Assert.That(changed.DormantBoundsReads,Is.EqualTo(ids.Length));Assert.That(changed.IndexBuildWork,Is.GreaterThan(0));MovementOracleMatches(changed,MovementOracleValues());
                // A new metadata owner is a full-refresh boundary even with no IDs
                // or publication advances in either replacement index.
                indexField.SetValue(editor,firstReplacementIndex);editor.CaptureMovementCandidates(true,false);Assert.That(replacementIndex.Generation,Is.EqualTo(firstReplacementIndex.Generation));
                indexField.SetValue(editor,replacementIndex);var indexed=editor.CaptureMovementCandidates(true,false);Assert.That(indexed.DormantBoundsReads,Is.EqualTo(ids.Length));MovementOracleMatches(indexed,MovementOracleValues());MovementOracleMatches(old,before);
            }finally{field.SetValue(editor,originalJournal);indexField.SetValue(editor,originalIndex);}
        }
        [UnityTest] public IEnumerator IncrementalMovementGroundClassificationNeverClaimsDormantSupportIsPhysicallyReady(){
            var ids=IncrementalMovementGroup(4,out var area);yield return VisualLookupPrepared();editor.CaptureMovementCandidates(true,false);string id=ids[0];
            var data=editor.Read(id);data.physics=ItemPhysics.Fixed;data.heightFields=new[]{new RoomHeightField{cells=4,width=2,depth=2,maxHeight=.5f,heights=Enumerable.Repeat(.1f,25).ToArray()}};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            var pending=editor.CaptureMovementCandidates(true,false);Assert.That(pending.DormantBoundsReads,Is.EqualTo(1));Assert.That(pending.IndexBuildWork,Is.Zero);
            yield return VisualLookupPrepared();var support=CollisionPoint(data.position);Assert.That(editor.GroundComplete(support),Is.False);
            Assert.That(editor.ReadRegion(area).Loading,Is.EqualTo("together"));
            yield return NativeAreaCompletion(editor.AcquireNativeEntities(new[]{id},()=>true,CancellationToken.None));
            Assert.That(ids.All(target=>!editor.NativeEntityDormant(target)&&editor.Find(target)),Is.True,"Together loading restores the complete declared closure");
            support=CollisionPoint(editor.ReadCollisionBounds(id).Bounds.center);var distant=CollisionPoint(new(100,100,100));
            var active=editor.CaptureMovementCandidates(true,false);Assert.That(active.Targets(distant,support),Does.Contain(id));Assert.That(editor.CaptureMovementCandidates(false,false).Targets(distant,support),Does.Not.Contain(id));
            Assert.That(editor.GroundComplete(support),Is.True);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);var dormant=editor.CaptureMovementCandidates(true,false);Assert.That(ids.All(editor.NativeEntityDormant),Is.True);Assert.That(dormant.DormantBoundsReads,Is.EqualTo(ids.Length),"Every restored closure member is retired again");Assert.That(dormant.DormantOverlayUpdates,Is.EqualTo(ids.Length));Assert.That(dormant.IndexBuildWork,Is.Zero);Assert.That(dormant.Targets(distant,support),Does.Contain(id));Assert.That(editor.GroundComplete(support),Is.False,"Cached support extents cannot stand in for native collision readiness");
            data=editor.Read(id);data.heightFields=Array.Empty<RoomHeightField>();data.physics=ItemPhysics.Solid;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
            yield return NativeAreaCompletion(editor.AcquireNativeEntities(new[]{id},()=>true,CancellationToken.None));
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);var noGround=editor.CaptureMovementCandidates(true,false);
            Assert.That(editor.ReadCollisionBounds(id).Known,Is.True);Assert.That(noGround.Targets(distant,support),Does.Not.Contain(id));MovementOracleMatches(noGround,MovementOracleValues());Assert.That(dormant.Targets(distant,support),Does.Contain(id),"Earlier captures keep their ground classification");
        }
        [UnityTest] public IEnumerator IncrementalMovementUnexpectedMissingNativeInstanceRemainsAFreshConservativeCandidate(){
            IncrementalMovementGroup(4,out _);yield return VisualLookupPrepared();var original=editor.CaptureMovementCandidates(true,false);var before=MovementOracleValues();string id=editor.Identity(block);
            UnityEngine.Object.DestroyImmediate(block.gameObject);
            Assert.That(editor.HasSavedObject(id),Is.True);Assert.That(editor.NativeEntityDormant(id),Is.False);Assert.That(editor.Find(id)==null,Is.True);
            var missing=editor.CaptureMovementCandidates(true,false);MovementNoColdWork(missing);
            Assert.That(editor.ReadCollisionBounds(id).Known,Is.False);Assert.That(missing.Targets(CollisionPoint(new(100,100,100)),null),Does.Contain(id));
            MovementOracleMatches(missing,MovementOracleValues());MovementOracleMatches(original,before);
        }
    }
}
