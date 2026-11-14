// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using Overlay=Maestro.Quest.Creation.RoomSpatialOverlay<int>;
namespace Maestro.Quest.Tests {
    public sealed class RoomSpatialOverlayTests {
        static string Id(int value)=>value.ToString("D5");
        static RoomSpatialExtent Box(float x)=>new(){Known=true,HasBounds=true,Bounds=new Bounds(new(x,0,0),Vector3.one)};
        static Overlay.Entry Entry(int id,RoomSpatialExtent extent)=>new(Id(id),extent,id);
        static Overlay.Entry Entry(int id,float x)=>Entry(id,Box(x));
        static string[] Query(Overlay overlay,Bounds body,Bounds? support=null){var values=new List<Overlay.Entry>();overlay.Query(body,support,values,new());return values.Select(value=>value.Id).ToArray();}
        static Bounds At(float x)=>new(new(x,0,0),Vector3.one*.1f);
        static string[] Oracle(IEnumerable<Overlay.Entry> entries,Bounds body,Bounds? support)=>entries.Where(value=>!value.Extent.Known||!value.Extent.HasBounds&&support.HasValue||value.Extent.HasBounds&&(!RoomSpatialLookup.Valid(body)||support.HasValue&&!RoomSpatialLookup.Valid(support.Value)||value.Extent.Bounds.Intersects(body)||support.HasValue&&value.Extent.Bounds.Intersects(support.Value))).Select(value=>value.Id).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        [TestCase(0)][TestCase(1)][TestCase(65)][TestCase(1024)]
        public void OverlayMatchesExhaustiveOracleAcrossReplacementsUnknownAndEmptyExtents(int count){
            var random=new System.Random(921+count);var current=new Dictionary<string,Overlay.Entry>(StringComparer.Ordinal);
            for(int i=0;i<count;i++)current.Add(Id(i),Entry(i,i%13==0?default:i%17==0?RoomSpatialExtent.Empty:Box(i*3)));
            var overlay=new Overlay(current.Values.ToArray());
            for(int turn=0;turn<180;turn++){
                int id=random.Next(Math.Max(1,count+20));Overlay.Change change;
                if(turn%5==0){current.Remove(Id(id));change=new(Id(id));}
                else{var entry=Entry(id,turn%7==0?default:turn%11==0?RoomSpatialExtent.Empty:turn%19==0?new RoomSpatialExtent{Known=true,HasBounds=true,Bounds=new(new(float.NaN,0,0),Vector3.one)}:Box((float)random.NextDouble()*count*3));current[entry.Id]=entry;change=new(entry);}
                var previous=overlay;var oldExpected=Query(previous,At(turn*3));
                overlay=overlay.Apply(new[]{change},out _,out _,out _);
                Assert.That(overlay.Count,Is.EqualTo(current.Count));Assert.That(overlay.OverrideCount,Is.LessThanOrEqualTo(Overlay.MaximumOverrides));
                for(int probe=0;probe<5;probe++){
                    var body=At((float)random.NextDouble()*Math.Max(1,count)*3);Bounds? support=probe%2==0?At(body.center.x+10):null;
                    CollectionAssert.AreEqual(Oracle(current.Values,body,support),Query(overlay,body,support));
                }
                CollectionAssert.AreEqual(oldExpected,Query(previous,At(turn*3)),"A later overlay must not mutate its predecessor");
            }
        }
        [Test] public void ReplacementAndUnknownAreConsideredOutsideTheirOldBaseBounds(){
            var original=new Overlay(new[]{Entry(1,0),Entry(2,10)});
            var moved=original.Apply(new[]{new Overlay.Change(Entry(1,100))},out int updated,out int compactions,out int build);
            Assert.That(updated,Is.EqualTo(1));Assert.That(compactions,Is.Zero);Assert.That(build,Is.Zero);
            Assert.That(Query(moved,At(0)),Is.Empty);CollectionAssert.AreEqual(new[]{Id(1)},Query(moved,At(100)));
            var unknown=moved.Apply(new[]{new Overlay.Change(Entry(1,default(RoomSpatialExtent)))},out _,out _,out _);
            CollectionAssert.AreEqual(new[]{Id(1)},Query(unknown,At(-500)));CollectionAssert.AreEqual(new[]{Id(1)},Query(original,At(0)));
        }
        [Test] public void TombstonesAndSameIdentityReaddNeverReturnDuplicates(){
            var first=new Overlay(new[]{Entry(1,0),Entry(2,0)});
            var deleted=first.Apply(new[]{new Overlay.Change(Id(1))},out _,out _,out _);
            Assert.That(deleted.Count,Is.EqualTo(1));Assert.That(deleted.OverrideCount,Is.EqualTo(1));Assert.That(deleted.Contains(Id(1)),Is.False);
            var again=deleted.Apply(new[]{new Overlay.Change(Entry(1,20)),new Overlay.Change(Entry(1,0))},out int updates,out _,out _);
            Assert.That(updates,Is.EqualTo(1));Assert.That(again.Count,Is.EqualTo(2));Assert.That(again.OverrideCount,Is.EqualTo(1));
            CollectionAssert.AreEqual(new[]{Id(1),Id(2)},Query(again,At(0)));CollectionAssert.AreEqual(new[]{Id(2)},Query(deleted,At(0)));
            var added=deleted.Apply(new[]{new Overlay.Change(Entry(3,0))},out _,out _,out _);
            var removed=added.Apply(new[]{new Overlay.Change(Id(3))},out _,out _,out _);
            Assert.That(removed.Count,Is.EqualTo(1));Assert.That(removed.OverrideCount,Is.EqualTo(1),"A removed insertion has no obsolete base slot to suppress");
        }
        [TestCase(false)][TestCase(true)] public void ThirtyThirdDistinctOverrideCompactsWithoutBorrowingEarlierSnapshots(bool tombstones){
            var first=new Overlay(Enumerable.Range(0,64).Select(i=>Entry(i,i*3)).ToArray());var changed=first;
            for(int i=0;i<Overlay.MaximumOverrides;i++){
                changed=changed.Apply(new[]{tombstones?new Overlay.Change(Id(i)):new Overlay.Change(Entry(i,1000+i*3))},out int updates,out int compactions,out int work);
                Assert.That(updates,Is.EqualTo(1));Assert.That(compactions,Is.Zero);Assert.That(work,Is.Zero);Assert.That(changed.OverrideCount,Is.EqualTo(i+1));
            }
            var beforeCompaction=changed;var old=Query(changed,At(99));
            changed=changed.Apply(new[]{tombstones?new Overlay.Change(Id(32)):new Overlay.Change(Entry(32,1096))},out _,out int compacted,out int build);
            Assert.That(compacted,Is.EqualTo(1));Assert.That(build,Is.GreaterThan(0));Assert.That(build,Is.LessThanOrEqualTo(RoomSpatialLookup.MaximumBuildWork(changed.Count)));
            Assert.That(changed.OverrideCount,Is.Zero);Assert.That(changed.Count,Is.EqualTo(tombstones?31:64));
            CollectionAssert.AreEqual(old,Query(beforeCompaction,At(99)));CollectionAssert.AreEqual(new[]{Id(0)},Query(first,At(0)));
        }
        [Test] public void RepeatedChangesToOneIdentityStayBoundedAndNeverRebuild(){
            var overlay=new Overlay(new[]{Entry(1,0)});
            for(int i=0;i<600;i++){
                overlay=overlay.Apply(new[]{new Overlay.Change(Entry(1,i))},out int updates,out int compactions,out int build);
                Assert.That(updates,Is.EqualTo(1));Assert.That(compactions,Is.Zero);Assert.That(build,Is.Zero);Assert.That(overlay.OverrideCount,Is.EqualTo(1));
            }
            CollectionAssert.AreEqual(new[]{Id(1)},Query(overlay,At(599)));Assert.That(Query(overlay,At(0)),Is.Empty);
        }
        [TestCase(false)][TestCase(true)] public void CapacityUsesFinalActiveIdsIncludingRemovalPlusInsertion(bool insertionFirst){
            var full=new Overlay(Enumerable.Range(0,RoomSpatialLookup.MaximumEntries).Select(i=>Entry(i,i*3)).ToArray());
            var changes=new[]{new Overlay.Change(Id(0)),new Overlay.Change(Entry(5000,-10))};if(insertionFirst)Array.Reverse(changes);
            var next=full.Apply(changes,out int updates,out int compactions,out int build);
            Assert.That(next.Count,Is.EqualTo(RoomSpatialLookup.MaximumEntries));Assert.That(updates,Is.EqualTo(2));Assert.That(compactions,Is.Zero);Assert.That(build,Is.Zero);
            Assert.That(Query(next,At(0)),Is.Empty);CollectionAssert.AreEqual(new[]{Id(5000)},Query(next,At(-10)));CollectionAssert.AreEqual(new[]{Id(0)},Query(full,At(0)));
        }
        [Test] public void FailedCapacityOrMalformedBatchCannotPublishPartialReplacements(){
            var full=new Overlay(Enumerable.Range(0,RoomSpatialLookup.MaximumEntries).Select(i=>Entry(i,i*3)).ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(()=>full.Apply(new[]{new Overlay.Change(Entry(0,-30)),new Overlay.Change(Entry(5000,0))},out _,out _,out _));
            Assert.Throws<ArgumentException>(()=>full.Apply(new[]{new Overlay.Change(Entry(0,-30)),default},out _,out _,out _));
            Assert.That(full.Count,Is.EqualTo(RoomSpatialLookup.MaximumEntries));Assert.That(full.OverrideCount,Is.Zero);CollectionAssert.AreEqual(new[]{Id(0)},Query(full,At(0)));Assert.That(Query(full,At(-30)),Is.Empty);
            var retry=full.Apply(new[]{new Overlay.Change(Id(1)),new Overlay.Change(Entry(5000,0))},out _,out _,out _);
            CollectionAssert.AreEqual(new[]{Id(0),Id(5000)},Query(retry,At(0)));
        }
        [Test] public void OverlayOwnsInputAndUpdateArraysAndReturnsCanonicalOrder(){
            var source=new[]{Entry(8,0),Entry(1,0),Entry(5,0)};var initial=new Overlay(source);source[0]=Entry(99,100);
            var changes=new[]{new Overlay.Change(Entry(2,0)),new Overlay.Change(Id(5))};var next=initial.Apply(changes,out _,out _,out _);changes[0]=new(Entry(90,0));
            CollectionAssert.AreEqual(new[]{Id(1),Id(5),Id(8)},Query(initial,At(0)));CollectionAssert.AreEqual(new[]{Id(1),Id(2),Id(8)},Query(next,At(0)));
            var result=Query(next,At(0));result[0]="changed";CollectionAssert.AreEqual(new[]{Id(1),Id(2),Id(8)},Query(next,At(0)));
        }
        [Test] public void InvalidQueriesAndUnknownExtentsRetainConservativeEmptySupportRules(){
            var bad=new Bounds(new(float.NaN,0,0),Vector3.one);var initial=new Overlay(new[]{Entry(1,20),Entry(2,RoomSpatialExtent.Empty)});
            var next=initial.Apply(new[]{new Overlay.Change(Entry(3,new RoomSpatialExtent{Known=true,HasBounds=true,Bounds=bad}))},out _,out _,out _);
            CollectionAssert.AreEqual(new[]{Id(3)},Query(next,At(0)));CollectionAssert.AreEqual(new[]{Id(2),Id(3)},Query(next,At(0),At(0)));
            CollectionAssert.AreEqual(new[]{Id(1),Id(3)},Query(next,bad));CollectionAssert.AreEqual(new[]{Id(1),Id(2),Id(3)},Query(next,bad,bad));
        }
        [Test] public void MovementOverlayAppliesGroundAndActorLeafPolicyAndOwnsQueryAccounting(){
            var values=new[]{new RoomEditor.MovementCandidates.Candidate(Id(1),RoomSpatialExtent.Empty,false),new RoomEditor.MovementCandidates.Candidate(Id(2),RoomSpatialExtent.Empty,true),new RoomEditor.MovementCandidates.Candidate(Id(3),default,false)};
            var dormant=RoomEditor.MovementCandidates.Set.Dormant(values);
            var live=new RoomEditor.MovementCandidates.Set(Array.Empty<RoomEditor.MovementCandidates.Candidate>(),false);
            var first=new RoomEditor.MovementCandidates(live,dormant,0,0,true);var second=new RoomEditor.MovementCandidates(live,dormant,0,0,true);
            CollectionAssert.AreEqual(new[]{Id(2),Id(3)},first.Targets(At(0),At(0)));Assert.That(second.Visits,Is.Zero);Assert.That(second.NodeVisits,Is.Zero);
            CollectionAssert.AreEqual(new[]{Id(2)},second.Targets(At(0),At(0),Id(3)));Assert.That(first.Visits,Is.EqualTo(3));
            CollectionAssert.AreEqual(new[]{Id(3)},first.Targets(At(0),null));
        }
        [Test] public void ConstructionRejectsDuplicateOrMissingIdentitiesAndOverCapacity(){
            Assert.Throws<ArgumentException>(()=>new Overlay(new[]{Entry(1,0),Entry(1,1)}));
            Assert.Throws<ArgumentException>(()=>new Overlay(new Overlay.Entry[1]));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new Overlay(new Overlay.Entry[RoomSpatialLookup.MaximumEntries+1]));
            Assert.Throws<ArgumentNullException>(()=>new Overlay(null));
            Assert.Throws<ArgumentException>(()=>new Overlay.Change((string)null));
        }
        [Test] public void EmptyPartitionCanAcquireAnIdentityAndIrrelevantRemovalDoesNotGrowIt(){
            var empty=new Overlay(Array.Empty<Overlay.Entry>());var absent=empty.Apply(new[]{new Overlay.Change(Id(1))},out _,out _,out int work);
            Assert.That(absent.Count,Is.Zero);Assert.That(absent.OverrideCount,Is.Zero);Assert.That(work,Is.Zero);
            var next=absent.Apply(new[]{new Overlay.Change(Entry(1,default(RoomSpatialExtent)))},out _,out _,out _);
            CollectionAssert.AreEqual(new[]{Id(1)},Query(next,At(500)));Assert.That(Query(empty,At(500)),Is.Empty);
        }
    }
}
