// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class GroundLookupTests {
        static RoomSpatialExtent Extent(Bounds bounds)=>new(){Known=true,HasBounds=true,Bounds=bounds};
        [Test] public void GroundBoundsDistinguishPreparedMetadataFromRetainedCollision(){
            var visual=Extent(new Bounds(Vector3.zero,Vector3.one));var collision=Extent(new Bounds(Vector3.right*8,Vector3.one));
            var spatial=new RoomSpatialBounds{Visual=visual,Collision=collision};
            var authored=RoomGroundBounds.ForReadiness(spatial,true);Assert.That(authored.Bounds,Is.EqualTo(visual.Bounds));
            var retained=RoomGroundBounds.ForReadiness(spatial,false);Assert.That(retained.Known,Is.True);
            Assert.That(retained.Bounds.Contains(visual.Bounds.min)&&retained.Bounds.Contains(collision.Bounds.max),Is.True);
            spatial.Collision=default;Assert.That(RoomGroundBounds.ForReadiness(spatial,true).Known,Is.True);
            Assert.That(RoomGroundBounds.ForReadiness(spatial,false).Known,Is.False);
        }
        [TestCase(false)] [TestCase(true)] public void GroundBoundsNeverUseUnknownOrEmptySurfacesAsClearance(bool authored){
            Assert.That(RoomGroundBounds.ForReadiness(RoomSpatialBounds.Unknown,authored).Known,Is.False);
            Assert.That(RoomGroundBounds.ForReadiness(new(){Visual=RoomSpatialExtent.Empty,Collision=RoomSpatialExtent.Empty},authored).Known,Is.False);
            Assert.That(RoomGroundBounds.ForReadiness(new(){Visual=default,Collision=Extent(new Bounds(Vector3.zero,Vector3.one))},authored).Known,Is.False);
        }
        [Test] public void GroundLookupMatchesExhaustiveSupportIncludingUnknownEmptyAndEnclosingSurfaces(){
            var random=new System.Random(419);float Next(float width)=>(float)(random.NextDouble()-.5)*width;
            var values=Enumerable.Range(0,1024).Select(i=>i%67==0?default:i%79==0?RoomSpatialExtent.Empty:Extent(new Bounds(new(Next(200),Next(30),Next(200)),new(1+Math.Abs(Next(12)),1+Math.Abs(Next(3)),1+Math.Abs(Next(12)))))).ToArray();
            values[1]=Extent(new Bounds(Vector3.zero,Vector3.one*400));
            // Readiness treats known-empty geometry as uncertain, not absent.
            var lookup=new RoomSpatialLookup(values.Select(v=>v.Known&&v.HasBounds?v:default).ToArray());var selected=new List<int>();
            for(int i=0;i<180;i++){
                var envelope=new Bounds(new(Next(220),Next(35),Next(220)),new(2,3,2));
                bool Included(int n)=>!values[n].Known||!values[n].HasBounds||values[n].Bounds.Intersects(envelope);
                var expected=Enumerable.Range(0,values.Length).Where(Included).ToArray();
                int visited=lookup.Query(envelope,null,selected);
                CollectionAssert.AreEquivalent(expected,selected);Assert.That(visited,Is.LessThan(lookup.NodeCount/3));
            }
        }
    }
}
