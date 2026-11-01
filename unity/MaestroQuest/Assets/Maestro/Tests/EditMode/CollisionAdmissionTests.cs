// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class CollisionAdmissionTests
    {
        [TestCase("entries")][TestCase("colliders")][TestCase("triangles")]
        public void SharedReservationsRefuseEachIndependentLimitWithoutLeaking(string dimension)
        {
            var leases=new List<CollisionResources.Lease>();var before=CollisionResources.ObserveBudget();
            CollisionResources.Cost cost=dimension=="triangles"?new(1,1,50000):new(dimension=="entries"?1:16,0,0);
            int count=dimension=="entries"?CollisionResources.MaximumEntries-(int)before["entries"]:
                dimension=="colliders"?(CollisionResources.MaximumColliders-(int)before["colliders"])/16:
                (int)((CollisionResources.MaximumSourceTriangles-(long)before["sourceTriangles"])/50000);
            var owner=new RoomResourceOwner(null,"pending", "collision");
            try{
                for(int i=0;i<count;i++)leases.Add(CollisionResources.Begin(owner,dimension=="triangles"?"imported":"compound",cost));
                var full=CollisionResources.ObserveBudget().ToString();
                Assert.Throws<RoomResourceLimitException>(()=>CollisionResources.Begin(owner,dimension=="triangles"?"imported":"compound",cost));
                Assert.That(CollisionResources.ObserveBudget().ToString(),Is.EqualTo(full));
                leases[0].Retire();Assert.DoesNotThrow(()=>leases.Add(CollisionResources.Begin(owner,dimension=="triangles"?"imported":"compound",cost)));
            }finally{foreach(var lease in leases)lease.Retire();}
            Assert.That(CollisionResources.ObserveBudget().ToString(),Is.EqualTo(before.ToString()));
        }
        [Test] public void OversizedTriangleCostCannotOverflowIntoAdmission()
        {
            var before=CollisionResources.ObserveBudget().ToString();
            Assert.Throws<RoomResourceLimitException>(()=>CollisionResources.Begin(new RoomResourceOwner(null,"too-large","collision"),"imported",new(1,1,long.MaxValue)));
            Assert.That(CollisionResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
    }
}
