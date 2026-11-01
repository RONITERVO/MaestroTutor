// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        static readonly Bounds IncrementalGroundAll=new(new Vector3(35,0,35),Vector3.one*150);
        string[] incrementalGroundIds;string incrementalGroundArea;
        readonly struct GroundCosts {
            internal readonly long Reads,Dormant,Build,Updates,Compactions;
            internal GroundCosts(RoomEditor value){var w=value.GroundQueryWork;Reads=w.reads;Dormant=w.dormantReads;Build=w.build;Updates=w.overlayUpdates;Compactions=w.compactions;}
        }
        void GroundCostDelta(GroundCosts before,int reads,int updates,int compactions=0,bool rebuilt=false){
            var after=editor.GroundQueryWork;
            Assert.That(after.dormantReads-before.Dormant,Is.EqualTo(reads),"Dormant extent projections");
            Assert.That(after.overlayUpdates-before.Updates,Is.EqualTo(updates),"Published identity replacements/tombstones");
            Assert.That(after.compactions-before.Compactions,Is.EqualTo(compactions));
            if(rebuilt)Assert.That(after.build-before.Build,Is.GreaterThan(0));else Assert.That(after.build-before.Build,Is.Zero);
        }
        void GroundEdit(RoomObjectData value)=>Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{value},Array.Empty<string>(),out var error),Is.True,error);
        static RoomHeightField[] IncrementalGroundField()=>new[]{new RoomHeightField{cells=4,width=2,depth=2,maxHeight=.5f,heights=Enumerable.Repeat(.1f,25).ToArray()}};
        string IncrementalGroundSurface(Vector3 position){
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Incremental ground fixture",position,1,Color.white,out var id,out var error),Is.True,error);
            var value=editor.Read(id);value.physics=ItemPhysics.Fixed;value.heightFields=IncrementalGroundField();GroundEdit(value);return id;
        }
        string[] IncrementalGroundGroup(int count,out string area,bool independent=true){
            // The production room heightfield cap is four; imported models have a separate cap of four.
            Assert.That(count,Is.InRange(1,4));area=NewRegion();GroundRegionPolicy(area,independent);var ids=new string[count];
            for(int i=0;i<count;i++){ids[i]=IncrementalGroundSurface(new(10+i*4,0,10));Assert.That(AssignRegion(ids[i],area,out var error),Is.True,error);}
            Assert.That(editor.RetireNativeArea(area,out var issue),Is.True,issue);return ids;
        }
        void GroundRegionPolicy(string area,bool independent){
            if(!independent)return;var region=editor.ReadRegion(area);region.loading="independent";
            Assert.That(editor.EditRegion(region,area,editor.RegionRevision(area),region.members,out var error),Is.True,error);
        }
        IEnumerator IncrementalImportedGroundGroup(int count){
            Assert.That(count,Is.InRange(1,4),"Keep fixture admission within the production imported-model cap");
            yield return GeometryModel(StaticModelFixture.Create());
            yield return ContainerAction(new RoomAgentExecutor(editor),GeometryCall(BuildingSettings()));
            incrementalGroundArea=NewRegion();GroundRegionPolicy(incrementalGroundArea,true);incrementalGroundIds=new string[count];incrementalGroundIds[0]=geometryTarget;
            Assert.That(editor.MoveObject(geometryTarget,new(-10,0,-10),out var error),Is.True,error);
            for(int i=1;i<count;i++){
                Assert.That(editor.CopyObject(geometryTarget,editor.ObjectRevision(geometryTarget),"Imported ground "+i,new(-10+i%8*3,0,-10+i/8*3),out incrementalGroundIds[i],out error),Is.True,error);
                if(i%8==0)yield return null;
            }
            foreach(string id in incrementalGroundIds)Assert.That(AssignRegion(id,incrementalGroundArea,out error),Is.True,error);
            float deadline=Time.realtimeSinceStartup+20;
            bool Ready(string id){var item=editor.Find(id);return item&&item.GetComponent<CreatedRoomObject>().ModelGeometryReady;}
            while(!incrementalGroundIds.All(Ready)&&Time.realtimeSinceStartup<deadline)yield return null;
            foreach(string id in incrementalGroundIds){
                Assert.That(Ready(id),Is.True,id+": "+editor.Find(id)?.GetComponent<CreatedRoomObject>().ModelGeometryIssue);
                Assert.That(editor.Find(id).SolidColliders.Any(c=>c is MeshCollider),Is.True,"Use actual imported collision, not synthetic bounds");
            }
            Assert.That(editor.RetireNativeArea(incrementalGroundArea,out error),Is.True,error);
            yield return VisualLookupPrepared();
            Assert.That(incrementalGroundIds.All(editor.NativeEntityDormant),Is.True);
        }
        string[] IncrementalGroundOracle(Bounds envelope){
            // Classify detached accepted data independently of GroundCandidates/overlay.
            // Native-ready surfaces are deliberately absent from this missing-support oracle.
            var ids=editor.Snapshot().objects.Where(value=>!value.IsBuiltIn&&value.physics==ItemPhysics.Fixed&&
                ((value.heightFields?.Length??0)>0||value.kind==RoomObjectKind.ImportedModel&&value.modelGeometry?.walkable==true&&value.modelGeometry.meshCollision)&&
                editor.NativeEntityDormant(value.id)).Select(value=>value.id).ToArray();
            return ExhaustiveGroundLookup(ids,envelope);
        }
        Bounds[] IncrementalGroundProbes(){
            var random=new System.Random(9841);var result=new List<Bounds>{IncrementalGroundAll,new(new(100,100,100),Vector3.one)};
            for(int i=0;i<28;i++)result.Add(new Bounds(new((float)random.NextDouble()*44-16,(float)random.NextDouble()*14-4,(float)random.NextDouble()*36-16),new(5,3,5)));
            // Target every accepted dormant surface too; random misses alone
            // would not demonstrate a moved/deleted identity's stale bounds.
            foreach(var value in editor.Snapshot().objects.Where(value=>!value.IsBuiltIn&&value.physics==ItemPhysics.Fixed&&
                ((value.heightFields?.Length??0)>0||value.kind==RoomObjectKind.ImportedModel&&value.modelGeometry?.walkable==true&&value.modelGeometry.meshCollision)&&editor.NativeEntityDormant(value.id))){
                var extent=editor.ReadSpatialBounds(value.id,out _).Visual;
                if(extent.Known&&extent.HasBounds)result.Add(new Bounds(extent.Bounds.center,Vector3.one*.05f));
            }
            return result.ToArray();
        }
        void IncrementalGroundOracleMatches(){
            foreach(var envelope in IncrementalGroundProbes()){
                var expected=IncrementalGroundOracle(envelope);
                CollectionAssert.AreEqual(expected,editor.GroundDependencies(envelope).Select(x=>(string)x["target"]));
                Assert.That(editor.GroundComplete(envelope),Is.EqualTo(expected.Length==0));
            }
        }
        static string[] GroundOverlayTargets(object overlay,Bounds envelope){
            // Reflection is limited to a captured value-only snapshot; it does not bypass native readiness.
            var query=overlay.GetType().GetMethod("Query",BindingFlags.Instance|BindingFlags.NonPublic);
            var result=Activator.CreateInstance(query.GetParameters()[2].ParameterType);
            query.Invoke(overlay,new object[]{envelope,null,result,new List<int>()});
            return ((IEnumerable)result).Cast<object>().Select(entry=>(string)entry.GetType().GetField("Id",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(entry)).ToArray();
        }
        (object snapshot,Bounds[] probes,string[][] expected) GroundFrozenSnapshot(){
            var snapshot=MovementPrivate(editor,"groundDormant");var probes=IncrementalGroundProbes();
            return(snapshot,probes,probes.Select(IncrementalGroundOracle).ToArray());
        }
        static void AssertGroundFrozen((object snapshot,Bounds[] probes,string[][] expected) saved){
            for(int i=0;i<saved.probes.Length;i++)CollectionAssert.AreEqual(saved.expected[i],GroundOverlayTargets(saved.snapshot,saved.probes[i]),"Old ground snapshot "+i);
        }
        [UnityTest] public IEnumerator IncrementalGroundLiveAndMultiObjectEditsReadOnlyChangedDormantSurfaces(){
            yield return IncrementalImportedGroundGroup(4);
            var nonground=IncrementalMovementGroup(60,out _,independent:true,fixedPhysics:true);yield return VisualLookupPrepared();
            Assert.That(nonground.Concat(incrementalGroundIds).Count(editor.NativeEntityDormant),Is.EqualTo(64),"64 accepted dormant identities, of which four are imported ground");
            Assert.That(nonground.Select(editor.Read).All(value=>value.heightFields.Length==0&&value.kind==RoomObjectKind.Block),Is.True);
            editor.GroundComplete(IncrementalGroundAll);
            var original=GroundFrozenSnapshot();var journal=(RoomJournal)MovementPrivate(editor,"journal");var enumeration=MovementPrivate(journal,"groundCandidates");
            var before=new GroundCosts(editor);var live=editor.Read(editor.Identity(block));live.name="Live nonground rename";live.color=Color.blue;GroundEdit(live);
            editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,0,0);
            var value=editor.Read(incrementalGroundIds[0]);value.position+=Vector3.up*6;before=new(editor);GroundEdit(value);editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,1,1);
            before=new(editor);var edits=incrementalGroundIds.Take(3).Select(editor.Read).ToArray();foreach(var edit in edits)edit.position+=Vector3.up*7;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,edits,Array.Empty<string>(),out var error),Is.True,error);
            editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,3,3);IncrementalGroundOracleMatches();AssertGroundFrozen(original);
            Assert.That(MovementPrivate(journal,"groundCandidates"),Is.SameAs(enumeration),"Incremental edits must not enumerate the full ground catalog");
            // Measure a multi-result query with base + override + native-ready candidates.
            IncrementalGroundSurface(new(2,0,2));editor.GroundComplete(IncrementalGroundAll);
            for(int i=0;i<8;i++)editor.GroundComplete(IncrementalGroundAll);
            Assert.That(((Array)MovementPrivate(editor,"groundDynamic")).Length,Is.GreaterThanOrEqualTo(1));
            before=new(editor);long candidates=editor.GroundQueryWork.candidates;long allocated=GC.GetAllocatedBytesForCurrentThread();int complete=0;
            for(int i=0;i<256;i++)if(editor.GroundComplete(IncrementalGroundAll))complete++;
            allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Assert.That(complete,Is.Zero);Assert.That(allocated,Is.Zero,"Both multi-result sorts and the warm cursor check must remain allocation-free");GroundCostDelta(before,0,0);
            Assert.That(editor.GroundQueryWork.candidates-candidates,Is.GreaterThanOrEqualTo(5*256));
            Assert.That(editor.NativeActivationPending,Is.False);
        }
        [UnityTest] public IEnumerator IncrementalGroundRepeatedEditsDeleteUndoAndRedoKeepOldSnapshots(){
            var ids=IncrementalGroundGroup(4,out _);yield return VisualLookupPrepared();editor.GroundComplete(IncrementalGroundAll);
            var original=GroundFrozenSnapshot();string id=ids[0];var origin=editor.Read(id).position;
            for(int i=0;i<40;i++){
                var value=editor.Read(id);value.position=origin+Vector3.up*(i%2==0?3:6);var before=new GroundCosts(editor);GroundEdit(value);
                editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,1,1);Assert.That(editor.GroundQueryWork.overrides,Is.EqualTo(1));
            }
            var previous=GroundFrozenSnapshot();var removed=new GroundCosts(editor);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{id},out var error),Is.True,error);
            editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(removed,0,1);IncrementalGroundOracleMatches();
            var undo=new GroundCosts(editor);editor.Undo();Assert.That(editor.HasSavedObject(id)&&editor.NativeEntityDormant(id),Is.True);
            editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(undo,1,1);IncrementalGroundOracleMatches();
            var redo=new GroundCosts(editor);editor.Redo();Assert.That(editor.HasSavedObject(id),Is.False);
            editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(redo,0,1);Assert.That(editor.GroundQueryWork.overrides,Is.EqualTo(1));IncrementalGroundOracleMatches();
            AssertGroundFrozen(original);AssertGroundFrozen(previous);
        }
        [UnityTest] public IEnumerator IncrementalGroundDistinctIdentityChurnKeepsOverridesBoundedWithoutNeighborReads(){
            // Current validation admits at most four heightfields and four
            // imported models. Ground-only 32/33 compaction is therefore not a
            // reachable accepted room. Pure RoomSpatialOverlayTests cover that
            // threshold; this consumer test keeps more than 33 successive ground
            // identities honest without increasing caps or inventing invalid data.
            var ids=IncrementalMovementGroup(64,out _,independent:true,fixedPhysics:true);
            var current=ids.Take(4).ToArray();
            foreach(string id in current){var value=editor.Read(id);value.heightFields=IncrementalGroundField();GroundEdit(value);}
            yield return VisualLookupPrepared();editor.GroundComplete(IncrementalGroundAll);var original=GroundFrozenSnapshot();
            for(int i=0;i<40;i++){
                int slot=i%4;var old=editor.Read(current[slot]);old.heightFields=Array.Empty<RoomHeightField>();
                var next=editor.Read(ids[i+4]);next.heightFields=IncrementalGroundField();var before=new GroundCosts(editor);
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{old,next},Array.Empty<string>(),out var error),Is.True,error);
                current[slot]=next.id;editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,1,2);
                Assert.That(editor.GroundQueryWork.overrides,Is.EqualTo(Math.Min((i+1)*2,8)),"Four base tombstones plus at most four current replacements; departed nonbase entries do not accumulate");
                CollectionAssert.AreEqual(current.OrderBy(id=>id,StringComparer.Ordinal),editor.GroundDependencies(IncrementalGroundAll).Select(x=>(string)x["target"]));
                Assert.That(editor.Snapshot().Validate(out error),Is.True,error);
            }
            var publication=new GroundCosts(editor);yield return VisualLookupPrepared();editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(publication,4,4);
            IncrementalGroundOracleMatches();AssertGroundFrozen(original);
        }
        [UnityTest] public IEnumerator IncrementalGroundEmptyPartitionAdmitsNewGroundAndMetadataNeverClaimsNativeReadiness(){
            yield return VisualLookupPrepared();Assert.That(editor.GroundComplete(IncrementalGroundAll),Is.True);
            string area=NewRegion();Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Initially nonground",new(7,0,7),1,Color.white,out var id,out var error),Is.True,error);
            Assert.That(AssignRegion(id,area,out error),Is.True,error);Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            var value=editor.Read(id);value.physics=ItemPhysics.Fixed;value.heightFields=IncrementalGroundField();var before=new GroundCosts(editor);GroundEdit(value);
            var far=new Bounds(new(100,100,100),Vector3.one);Assert.That(editor.GroundComplete(far),Is.False);GroundCostDelta(before,1,1);
            Assert.That(editor.GroundDependencies(far).Select(x=>(string)x["target"]),Does.Contain(id));Assert.That((bool)editor.ObserveAuthoredBounds(id)["prepared"],Is.False);
            before=new(editor);yield return VisualLookupPrepared();Assert.That(editor.GroundComplete(far),Is.True);GroundCostDelta(before,1,1);
            Assert.That(editor.GroundComplete(new Bounds(value.position,Vector3.one)),Is.False,"Prepared visual bounds cannot supply native support");
            Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.NativeActivationPending,Is.False);IncrementalGroundOracleMatches();
        }
        [UnityTest] public IEnumerator IncrementalGroundTerrainAndImportedFlagsReclassifyOnlyTheirIdentity(){
            var ids=IncrementalGroundGroup(1,out _);yield return VisualLookupPrepared();editor.GroundComplete(IncrementalGroundAll);
            var data=editor.Read(ids[0]);data.heightFields=Array.Empty<RoomHeightField>();data.physics=ItemPhysics.Solid;
            var before=new GroundCosts(editor);GroundEdit(data);Assert.That(editor.GroundComplete(IncrementalGroundAll),Is.True);GroundCostDelta(before,0,1);
            data=editor.Read(ids[0]);data.physics=ItemPhysics.Fixed;data.heightFields=IncrementalGroundField();before=new(editor);GroundEdit(data);
            Assert.That(editor.GroundComplete(IncrementalGroundAll),Is.False);GroundCostDelta(before,1,1);yield return VisualLookupPrepared();editor.GroundComplete(IncrementalGroundAll);
            yield return IncrementalImportedGroundGroup(1);editor.GroundComplete(IncrementalGroundAll);string imported=incrementalGroundIds[0];
            data=editor.Read(imported);data.modelGeometry.walkable=false;before=new(editor);GroundEdit(data);editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,0,1);
            Assert.That(editor.GroundDependencies(IncrementalGroundAll).Select(x=>(string)x["target"]),Does.Not.Contain(imported));
            data=editor.Read(imported);data.modelGeometry.meshCollision=false;before=new(editor);GroundEdit(data);editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,0,0);
            // Restore both required imported flags in one accepted edit.
            data=editor.Read(imported);data.modelGeometry.meshCollision=true;data.modelGeometry.walkable=true;before=new(editor);GroundEdit(data);
            editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,1,1);IncrementalGroundOracleMatches();
        }
        IEnumerator IncrementalGroundMembership(bool independent){
            var ids=IncrementalGroundGroup(4,out var area,independent);yield return VisualLookupPrepared();editor.GroundComplete(IncrementalGroundAll);var old=GroundFrozenSnapshot();
            var before=new GroundCosts(editor);yield return NativeAreaCompletion(editor.AcquireNativeEntities(new[]{ids[0]},()=>true,CancellationToken.None));
            int loaded=independent?1:4;editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,0,loaded);
            Assert.That(ids.Count(id=>editor.Find(id)),Is.EqualTo(loaded));Assert.That(ids.Count(editor.NativeEntityDormant),Is.EqualTo(4-loaded));IncrementalGroundOracleMatches();
            var support=new Bounds(editor.Read(ids[0]).position,Vector3.one);Assert.That(editor.GroundComplete(support),Is.True,"Published native terrain is ready");
            before=new(editor);Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,loaded,loaded);
            Assert.That(ids.All(editor.NativeEntityDormant),Is.True);Assert.That(editor.GroundComplete(support),Is.False);IncrementalGroundOracleMatches();AssertGroundFrozen(old);
        }
        [UnityTest] public IEnumerator IncrementalGroundIndependentActivationAndRetirementChangeOneMember(){yield return IncrementalGroundMembership(true);}
        [UnityTest] public IEnumerator IncrementalGroundTogetherActivationAndRetirementChangeDeclaredClosure(){yield return IncrementalGroundMembership(false);}
        IEnumerator IncrementalGroundWindowLoss(string source){
            IncrementalGroundGroup(4,out _);yield return VisualLookupPrepared();editor.GroundComplete(IncrementalGroundAll);
            var index=(RoomSpatialIndex)MovementPrivate(editor,"spatialIndex");var membership=(RoomSpatialChanges)MovementPrivate(editor,"collisionResidencyChanges");
            string live=editor.Identity(block);var authored=editor.Snapshot();
            for(int round=0;round<2;round++){
                var before=new GroundCosts(editor);
                for(int i=0;i<RoomSpatialChanges.Capacity+round;i++){
                    if(source=="authored"){var value=editor.Read(live);value.name="Ground window "+round+" "+i;GroundEdit(value);}
                    else if(source=="placement"){block.transform.localPosition=Vector3.up*(i%2==0?1:2);editor.RememberPlacement(live);}
                    else if(source=="publication"){authored.objects.Single(x=>x.id==live).color=i%2==0?Color.blue:Color.red;index.Synchronize(authored);}
                    else membership.Record(live);
                }
                editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,round==0?0:4,0,rebuilt:round==1);IncrementalGroundOracleMatches();
                before=new(editor);editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,0,0);
            }
        }
        [UnityTest] public IEnumerator IncrementalGroundAuthoredWindowLossRequiresFullRefresh(){yield return IncrementalGroundWindowLoss("authored");}
        [UnityTest] public IEnumerator IncrementalGroundPlacementWindowLossRequiresFullRefresh(){yield return IncrementalGroundWindowLoss("placement");}
        [UnityTest] public IEnumerator IncrementalGroundPublicationWindowLossRequiresFullRefresh(){yield return IncrementalGroundWindowLoss("publication");}
        [UnityTest] public IEnumerator IncrementalGroundMembershipWindowLossRequiresFullRefresh(){yield return IncrementalGroundWindowLoss("membership");}
        [UnityTest] public IEnumerator IncrementalGroundProjectionFailurePreservesSnapshotAndRetriesUnconsumedDelta(){
            var ids=IncrementalGroundGroup(4,out _);yield return VisualLookupPrepared();editor.GroundComplete(IncrementalGroundAll);
            var old=GroundFrozenSnapshot();var cursor=MovementPrivate(editor,"groundDirectoryState");var data=editor.Read(ids[0]);data.position+=Vector3.up*5;GroundEdit(data);
            var entries=(IDictionary)MovementPrivate(editor,"dormantNative");var saved=entries[ids[0]];var before=new GroundCosts(editor);
            // Only this bounded synchronous fixture corrupts an internal projection input.
            try{entries[ids[0]]=null;Assert.Throws<NullReferenceException>(()=>editor.GroundComplete(IncrementalGroundAll));}
            finally{entries[ids[0]]=saved;}
            Assert.That(MovementPrivate(editor,"groundDormant"),Is.SameAs(old.snapshot));Assert.That(MovementPrivate(editor,"groundDirectoryState"),Is.SameAs(cursor));
            GroundCostDelta(before,1,0);before=new(editor);editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,1,1);
            IncrementalGroundOracleMatches();AssertGroundFrozen(old);before=new(editor);editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,0,0);
        }
        [UnityTest] public IEnumerator IncrementalGroundEqualCounterOwnersAndExplicitResetRequireFullRefresh(){
            IncrementalGroundGroup(4,out _);yield return VisualLookupPrepared();editor.GroundComplete(IncrementalGroundAll);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;var journalField=typeof(RoomEditor).GetField("journal",flags);
            var indexField=typeof(RoomEditor).GetField("spatialIndex",flags);var membershipField=typeof(RoomEditor).GetField("collisionResidencyChanges",flags);
            var originalJournal=(RoomJournal)journalField.GetValue(editor);var originalIndex=(RoomSpatialIndex)indexField.GetValue(editor);var originalMembership=(RoomSpatialChanges)membershipField.GetValue(editor);
            using var index1=new RoomSpatialIndex((hash,cancel)=>System.Threading.Tasks.Task.FromResult(ModelAssetBounds.Unknown("Ground fixture")));
            using var index2=new RoomSpatialIndex((hash,cancel)=>System.Threading.Tasks.Task.FromResult(ModelAssetBounds.Unknown("Ground fixture")));
            try{
                var first=new RoomJournal(editor.Snapshot());journalField.SetValue(editor,first);editor.GroundComplete(IncrementalGroundAll);var old=GroundFrozenSnapshot();
                var next=new RoomJournal(first.Snapshot());Assert.That(next.SpatialAuthoredVersion,Is.EqualTo(first.SpatialAuthoredVersion));Assert.That(next.SpatialCatalogStamp,Is.EqualTo(first.SpatialCatalogStamp));
                var before=new GroundCosts(editor);journalField.SetValue(editor,next);editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,4,0,rebuilt:true);
                indexField.SetValue(editor,index1);editor.GroundComplete(IncrementalGroundAll);Assert.That(index1.Generation,Is.EqualTo(index2.Generation));
                before=new(editor);indexField.SetValue(editor,index2);editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,4,0,rebuilt:true);
                // Equal stamps from a different log owner cannot license cursor reuse.
                var log1=new RoomSpatialChanges();var log2=new RoomSpatialChanges();membershipField.SetValue(editor,log1);editor.GroundComplete(IncrementalGroundAll);
                Assert.That(log1.Version,Is.EqualTo(log2.Version));before=new(editor);membershipField.SetValue(editor,log2);editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,4,0,rebuilt:true);
                before=new(editor);typeof(RoomEditor).GetMethod("InvalidateMovementResidency",flags).Invoke(editor,new object[]{null});
                editor.GroundComplete(IncrementalGroundAll);GroundCostDelta(before,4,0,rebuilt:true);IncrementalGroundOracleMatches();AssertGroundFrozen(old);
            }finally{journalField.SetValue(editor,originalJournal);indexField.SetValue(editor,originalIndex);membershipField.SetValue(editor,originalMembership);}
        }
        [UnityTest] public IEnumerator IncrementalGroundNativeDisabledMovedAndMissingStayFreshBesideDormantSurfaces(){
            IncrementalGroundGroup(3,out _);string id=AvailabilityTerrain();yield return VisualLookupPrepared();var item=editor.Find(id);
            var near=new Bounds(GroundPoint,Vector3.one*.25f);Assert.That(editor.GroundComplete(near),Is.True);var before=new GroundCosts(editor);int revision=editor.Revision;
            item.gameObject.SetActive(false);Assert.That(editor.GroundComplete(near),Is.False);GroundCostDelta(before,0,0);
            item.transform.localPosition=new(-5,0,-5);Assert.That(editor.GroundComplete(near),Is.True);
            var moved=new Bounds(item.transform.localPosition,Vector3.one*.25f);Assert.That(editor.GroundComplete(moved),Is.False);
            item.gameObject.SetActive(true);Assert.That(editor.GroundComplete(moved),Is.True);GroundCostDelta(before,0,0);
            UnityEngine.Object.DestroyImmediate(item.gameObject);Assert.That(editor.NativeEntityDormant(id),Is.False);
            // Expected-but-missing native objects remain live definitions and reread current authored metadata.
            Assert.That(editor.GroundDependencies(new Bounds(GroundPoint,Vector3.one)).Select(x=>(string)x["target"]),Does.Contain(id));
            GroundCostDelta(before,0,0);Assert.That(editor.GroundQueryWork.reads,Is.GreaterThan(before.Reads));Assert.That(editor.Revision,Is.EqualTo(revision));
        }
        [UnityTest] public IEnumerator IncrementalGroundUnknownEmptyAndMalformedReplacementCannotDisappearAtDistantQueries(){
            var ids=IncrementalGroundGroup(1,out _);yield return VisualLookupPrepared();string id=ids[0];var far=new Bounds(new(100,100,100),Vector3.one);
            Assert.That(editor.GroundComplete(far),Is.True);var entries=(IDictionary)MovementPrivate(editor,"dormantNative");var entry=entries[id];
            var field=entry.GetType().GetField("Spatial",BindingFlags.Instance|BindingFlags.NonPublic);var original=(RoomSpatialBounds)field.GetValue(entry);
            var membership=(RoomSpatialChanges)MovementPrivate(editor,"collisionResidencyChanges");
            try{
                foreach(var extent in new[]{default(RoomSpatialExtent),RoomSpatialExtent.Empty,new RoomSpatialExtent{Known=true,HasBounds=true,Bounds=new Bounds(new(float.NaN,0,0),Vector3.one)}}){
                    field.SetValue(entry,new RoomSpatialBounds{Visual=extent,Collision=RoomSpatialExtent.Empty});membership.Record(id);var before=new GroundCosts(editor);
                    Assert.That(editor.GroundComplete(far),Is.False,"Unknown, empty and invalid retained metadata remain conservative");GroundCostDelta(before,1,1);
                    var fact=editor.GroundDependencies(far).Single(x=>(string)x["target"]==id);Assert.That((bool)fact["boundsKnown"],Is.False);
                }
            }finally{field.SetValue(entry,original);membership.Record(id);}
            Assert.That(editor.GroundComplete(far),Is.True);
            // Retained transforms already sanitize malformed extents. Exercise the
            // candidate/leaf boundary directly too, so index normalization alone
            // cannot hide an invalid extent still retained in candidate.Value.
            var candidateType=typeof(RoomEditor).GetNestedType("GroundCandidate",BindingFlags.NonPublic);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            foreach(var bounds in new[]{new Bounds(new(float.NaN,0,0),Vector3.one),new Bounds(Vector3.zero,new(-1,1,1))}){
                var bad=new RoomSpatialExtent{Known=true,HasBounds=true,Bounds=bounds};
                var candidate=Activator.CreateInstance(candidateType,flags,null,new object[]{(id,true,false),bad,true},null);
                var normalized=(RoomSpatialExtent)candidateType.GetField("Extent",flags).GetValue(candidate);Assert.That(normalized.Known,Is.False);
                var args=new object[]{candidate,far,null,false};
                Assert.That((bool)typeof(RoomEditor).GetMethod("MissingGround",flags).Invoke(editor,args),Is.True);Assert.That((bool)args[3],Is.False);
            }
        }
    }
    public sealed partial class AvatarSpatialTests {
        [UnityTest] public IEnumerator IncrementalGroundValidScanInkRemainsNongroundAcrossAnchorMovementLossAndRecovery(){
            // The accepted schema permits scan anchors on ink canvases only,
            // never heightfields/imported walkable meshes. Do not invent invalid
            // scan-ground authored data to claim unsupported surface behavior.
            var scan=SharedEnvironment(out var platform);var wall=InkWall();var source=new LayoutSource{Entries=new[]{wall}};yield return LoadedLayout(scan,platform,source);
            string id=MakeLayer(out _);yield return InkAdd(id);yield return ScanBoundsPrepared(id);string area=ScanBoundsArea(id);
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            var all=new Bounds(Vector3.zero,Vector3.one*200);Assert.That(editor.GroundDependencies(all),Is.Empty);Assert.That(editor.GroundComplete(all),Is.True);
            int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());var before=editor.GroundQueryWork;
            wall.Position+=Vector3.right*4;yield return null;Assert.That(editor.GroundDependencies(all),Is.Empty);
            source.Entries=Array.Empty<ScannedSurface>();yield return null;Assert.That(editor.ReadCollisionBounds(id).Known,Is.False);Assert.That(editor.GroundComplete(all),Is.True);
            source.Entries=new[]{wall};yield return null;Assert.That(editor.ReadCollisionBounds(id).Known,Is.True);Assert.That(editor.GroundDependencies(all),Is.Empty);
            Assert.That(editor.GroundQueryWork.dormantReads,Is.EqualTo(before.dormantReads));Assert.That(editor.GroundQueryWork.build,Is.EqualTo(before.build));
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));Assert.That(editor.Find(id),Is.Null);
        }
    }
}