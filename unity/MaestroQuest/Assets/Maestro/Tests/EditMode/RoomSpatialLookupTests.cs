// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomSpatialLookupTests
    {
        static RoomSpatialExtent Box(Vector3 centre,Vector3 size)=>new(){Known=true,HasBounds=true,Bounds=new Bounds(centre,size)};
        static RoomEditor.MovementCandidates.Candidate Item(int id,RoomSpatialExtent extent,bool ground=false)=>new(id.ToString("D5"),extent,ground);
        static string[] Exhaustive(RoomEditor.MovementCandidates.Candidate[] values,Bounds body,Bounds? support,string actor)=>values.Where(c=>c.Id!=actor&&(!c.Collision.Known||c.Collision.HasBounds&&c.Collision.Bounds.Intersects(body)||support.HasValue&&c.Ground&&(!c.Collision.HasBounds||c.Collision.Bounds.Intersects(support.Value)))).Select(c=>c.Id).ToArray();
        [TestCase(0)][TestCase(1)][TestCase(7)][TestCase(66)][TestCase(1024)]
        public void SpatialLookupMatchesExhaustiveCollisionAndSupportAcrossMixedExtents(int count)
        {
            var random=new System.Random(713+count);
            float Next(float range)=>(float)(random.NextDouble()*range-range*.5);
            var values=Enumerable.Range(0,count).Select(i=>Item(i,i%19==0?default:i%17==0?RoomSpatialExtent.Empty:Box(new(Next(100),Next(20),Next(100)),new((float)random.NextDouble()*8,(float)random.NextDouble()*8,(float)random.NextDouble()*8)),i%3==0)).ToArray();
            var lookup=new RoomEditor.MovementCandidates(values);var direct=new RoomEditor.MovementCandidates(values,repeatedQueries:false);
            for(int probe=0;probe<120;probe++){
                var body=new Bounds(new(Next(100),Next(20),Next(100)),Vector3.one*(float)(random.NextDouble()*20));
                Bounds? support=probe%2==0?new Bounds(body.center-Vector3.up*2,new Vector3(3,.5f,3)):null;
                string actor=count>0?values[probe%count].Id:null;
                var expected=Exhaustive(values,body,support,actor);
                CollectionAssert.AreEqual(expected,lookup.Targets(body,support,actor),"Indexed probe "+probe);
                CollectionAssert.AreEqual(expected,direct.Targets(body,support,actor),"Direct probe "+probe);
            }
            Assert.That(lookup.IndexBuildWork,Is.LessThanOrEqualTo(RoomSpatialLookup.MaximumBuildWork(count)));
            Assert.That(direct.IndexBuildWork,Is.Zero);Assert.That(direct.NodeVisits,Is.Zero);
        }
        [Test] public void SpatialLookupKeepsUnknownGeometryAndEmptyGroundButExcludesTheActor()
        {
            var values=new[]{Item(0,default),Item(1,RoomSpatialExtent.Empty),Item(2,RoomSpatialExtent.Empty,true),Item(3,Box(new(30,0,0),Vector3.one),true),Item(4,Box(Vector3.zero,Vector3.one))};
            var lookup=new RoomEditor.MovementCandidates(values);var body=new Bounds(Vector3.zero,Vector3.one*.1f);
            CollectionAssert.AreEqual(new[]{"00000","00004"},lookup.Targets(body,null));
            CollectionAssert.AreEqual(new[]{"00000","00002","00004"},lookup.Targets(body,body));
            CollectionAssert.AreEqual(new[]{"00002","00004"},lookup.Targets(body,body,"00000"));
        }
        [Test] public void SpatialLookupSnapshotCannotBeMutatedByItsSourceOrReturnedResults()
        {
            var source=new[]{Item(0,Box(Vector3.zero,Vector3.one)),Item(1,Box(new(30,0,0),Vector3.one))};var lookup=new RoomEditor.MovementCandidates(source);
            source[0]=Item(7,Box(new(30,0,0),Vector3.one));var query=new Bounds(Vector3.zero,Vector3.one*.1f);var result=lookup.Targets(query,null);result[0]="changed";
            CollectionAssert.AreEqual(new[]{"00000"},lookup.Targets(query,null));
            Assert.That(new RoomEditor.MovementCandidates(source).Targets(query,null),Is.Empty);
        }
        [Test] public void SpatialLookupSkipsDistantBranchesWithoutLosingLargeEnclosingGeometry()
        {
            var values=Enumerable.Range(0,1024).Select(i=>Item(i,Box(new(i*4,0,0),Vector3.one))).ToList();values.Add(Item(1024,Box(Vector3.zero,Vector3.one*10000)));
            var lookup=new RoomEditor.MovementCandidates(values.ToArray());var body=new Bounds(new(2000,0,0),Vector3.one*.1f);
            CollectionAssert.AreEqual(new[]{"00500","01024"},lookup.Targets(body,null));
            Assert.That(lookup.Visits,Is.EqualTo(2));Assert.That(lookup.NodeVisits,Is.LessThan(80),"A local query must not examine the complete distant inventory");
        }
        [Test] public void SpatialLookupTouchingExtremaAndExtremeCoordinatesRemainConservative()
        {
            var values=new[]{Item(0,Box(new(-1,0,0),Vector3.one*2)),Item(1,Box(new(1,0,0),Vector3.one*2)),Item(2,Box(new(1e30f,0,0),Vector3.one*1e24f)),Item(3,Box(new(-1e30f,0,0),Vector3.one*1e24f))};
            var lookup=new RoomEditor.MovementCandidates(values);
            CollectionAssert.AreEqual(new[]{"00000","00001"},lookup.Targets(new Bounds(Vector3.zero,Vector3.zero),null));
            CollectionAssert.AreEqual(new[]{"00002"},lookup.Targets(new Bounds(new(1e30f,0,0),Vector3.zero),null));
            CollectionAssert.AreEqual(new[]{"00003"},lookup.Targets(new Bounds(new(-1e30f,0,0),Vector3.zero),null));
        }
        [Test] public void SpatialLookupInvalidExtentsOrQueriesCannotProveEmptySpace()
        {
            var invalid=Box(new(float.NaN,0,0),Vector3.one);var values=new[]{Item(0,invalid),Item(1,Box(new(20,0,0),Vector3.one)),Item(2,RoomSpatialExtent.Empty,true)};var lookup=new RoomEditor.MovementCandidates(values);
            CollectionAssert.AreEqual(new[]{"00000"},lookup.Targets(new Bounds(Vector3.zero,Vector3.one),null));
            CollectionAssert.AreEqual(new[]{"00000","00001"},lookup.Targets(invalid.Bounds,null));
            CollectionAssert.AreEqual(new[]{"00000","00001","00002"},lookup.Targets(invalid.Bounds,invalid.Bounds));
        }
        [TestCase(0)][TestCase(1)][TestCase(3)][TestCase(66)][TestCase(1000)][TestCase(4096)]
        public void SpatialLookupConstructionAndWorstCaseQueriesStayInsideExplicitWorkBounds(int count)
        {
            var values=Enumerable.Range(0,count).Select(i=>Item(i,Box(Vector3.zero,Vector3.one))).ToArray();var lookup=new RoomEditor.MovementCandidates(values);
            CollectionAssert.AreEqual(values.Select(v=>v.Id),lookup.Targets(new Bounds(Vector3.zero,Vector3.one),null));
            Assert.That(lookup.IndexBuildWork,Is.LessThanOrEqualTo(RoomSpatialLookup.MaximumBuildWork(count)));
            Assert.That(lookup.NodeVisits,Is.EqualTo(Math.Max(0,2*count-1)));Assert.That(lookup.Visits,Is.EqualTo(count));
        }
        [Test] public void SpatialLookupRefusesOversizedInputInsteadOfDroppingCandidates()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new RoomSpatialLookup(new RoomSpatialExtent[RoomSpatialLookup.MaximumEntries+1]));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new RoomEditor.MovementCandidates(new RoomEditor.MovementCandidates.Candidate[RoomSpatialLookup.MaximumEntries+1]));
        }
    }
}
