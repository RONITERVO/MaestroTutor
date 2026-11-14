// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class LiquidAdmissionTests
    {
        [Test]public void LiquidAdmissionIndependentLimitCoversEmptyCandidatesAcrossWorlds(){
            string before=LiquidResources.ObserveBudget().ToString();var held=new List<LiquidResources.Lease>();var a=RoomWorldIdentity.Create();var b=RoomWorldIdentity.Create();
            try{for(int i=0;i<32;i++)held.Add(LiquidResources.Begin(new RoomResourceOwner(i%2==0?a:b,Guid.NewGuid().ToString("N"),"liquid")));Assert.Throws<RoomResourceLimitException>(()=>new PreparedRoomLiquid(a,"full",new[]{new RoomContainer()},null));held[0].Dispose();held[0].Dispose();using var retry=new PreparedRoomLiquid(b,"retry",new[]{new RoomContainer()},null);Assert.That((int)LiquidResources.ObserveBudget()["instances"],Is.EqualTo(32));Assert.That(retry.Lease.Available,Is.True);}finally{foreach(var lease in held)lease.Dispose();}
            Assert.That(LiquidResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test]public void LiquidAdmissionExactSourceAndOwnerGuardPreparedTransfer(){
            var host=new GameObject("Liquid source");var other=new GameObject("Other owner");string before=LiquidResources.ObserveBudget().ToString();
            try{var view=host.AddComponent<ContainerFillView>();var values=new[]{new RoomContainer{amountMl=100}};using var candidate=new PreparedRoomLiquid(null,"liquid",values,view);var changed=new[]{values[0].Copy()};changed[0].amountMl=99;Assert.Throws<InvalidOperationException>(()=>candidate.Validate("liquid",changed));Assert.Throws<InvalidOperationException>(()=>candidate.Validate("other",values));Assert.Throws<InvalidOperationException>(()=>candidate.Take(other.AddComponent<ContainerFillView>(),"liquid",values));view.ApplyPrepared("liquid",values,candidate);Assert.That(view.NativeReady,Is.True);Assert.Throws<InvalidOperationException>(()=>candidate.Validate("liquid",values));}finally{UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(other);LiquidResources.Collect();}
            Assert.That(LiquidResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test]public void LiquidAdmissionNeverActiveOwnerReapsSurfaceStreamMeshAndBothMaterials(){
            string before=LiquidResources.ObserveBudget().ToString();var host=new GameObject("Never active liquid");host.SetActive(false);var view=host.AddComponent<ContainerFillView>();var values=new[]{new RoomContainer{amountMl=100}};using var candidate=new PreparedRoomLiquid(null,"owner",values,null);var lease=candidate.Lease;view.ApplyPrepared("owner",values,candidate);
            UnityEngine.Object.DestroyImmediate(host);LiquidResources.Collect();Assert.That(!lease.Surface&&!lease.Stream&&!lease.Mesh&&!lease.SurfaceMaterial&&!lease.StreamMaterial,Is.True);Assert.That(LiquidResources.ObserveBudget().ToString(),Is.EqualTo(before));
        }
        [Test]public void LiquidAdmissionPreparationIsDetachedInactiveAndCopiesOwnership(){
            var world=RoomWorldIdentity.Create();string id=world.worldId;using var candidate=new PreparedRoomLiquid(world,"source",new[]{new RoomContainer{amountMl=100}},null);var lease=candidate.Lease;world.worldId=Guid.NewGuid().ToString("N");Assert.That(lease.Owner.World,Is.EqualTo(id));Assert.That(lease.Surface.transform.parent,Is.Null);Assert.That(lease.Stream.transform.parent,Is.Null);Assert.That(lease.Surface.activeInHierarchy||lease.Stream.activeInHierarchy||lease.Renderer.enabled||lease.Line.enabled,Is.False);Assert.That(lease.Line.positionCount,Is.EqualTo(33));Assert.That(lease.Mesh,Is.Not.Null);Assert.That(lease.SurfaceMaterial,Is.Not.SameAs(lease.StreamMaterial));
        }
    }
}
