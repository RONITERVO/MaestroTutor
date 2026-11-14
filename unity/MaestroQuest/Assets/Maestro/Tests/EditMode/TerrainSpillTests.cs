// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class TerrainSpillTests {
  internal static RoomHeightField Bed(){
   var bed=new RoomHeightField{cells=4,width=2,depth=1,maxHeight=.5f,heights=new float[25]};
   for(int z=0;z<=4;z++)for(int x=0;x<=4;x++)bed.heights[z*5+x]=x==0||x==4||z==0||z==4?.4f:.05f;
   bed.heights[14]=.2f;return bed;
  }
  [TestCase(0f)][TestCase(18f)][TestCase(-18f)]
  public void TerrainSpillUsesLowestGravityEdgeForBothRetainedVolumeAndStream(float tilt){
   var bed=Bed();var terrain=new TerrainLiquidGeometry(bed,.5f);var c=new RoomContainer{version=4,height=.5f,rectangle=new(){width=2,depth=1},capacityMl=terrain.Volume*1e6};
   var rotation=Quaternion.Euler(0,0,tilt);var normal=Quaternion.Inverse(rotation)*Vector3.up;
   terrain.SpillLip(normal,out var local,out var outward);
   float lowest=float.PositiveInfinity;
   for(int z=0;z<=4;z++)for(int x=0;x<=4;x++)if(x==0||x==4||z==0||z==4)lowest=Mathf.Min(lowest,Vector3.Dot(normal,bed.Vertex(x,z)));
   Assert.That(Vector3.Dot(local,normal),Is.EqualTo(lowest).Within(1e-6));
   Assert.That(ContainerFlowGeometry.RetainedCapacity(c,rotation,Vector3.up,terrain),Is.EqualTo(terrain.VolumeBelow(normal,lowest)*1e6).Within(.1));
   var matrix=Matrix4x4.TRS(new Vector3(3,4,5),rotation,Vector3.one*2);
   var lip=ContainerFlowGeometry.Lip(c,matrix,rotation,Vector3.up,out var worldOutward,terrain);
   Assert.That(Vector3.Distance(lip,matrix.MultiplyPoint3x4(local)),Is.LessThan(1e-6));
   Assert.That(Vector3.Distance(worldOutward,rotation*outward),Is.LessThan(1e-6));
   Assert.That(outward.y,Is.Zero);Assert.That(outward.magnitude,Is.EqualTo(1));
   if(tilt==0){Assert.That(local,Is.EqualTo(new Vector3(1,.2f,0)));Assert.That(outward,Is.EqualTo(Vector3.right));}
  }
  [Test]public void TerrainSpillLeavesClosedReservoirsUnchangedAndNeedsCanonicalBed(){
   var bed=Bed();var terrain=new TerrainLiquidGeometry(bed,.5f);var c=new RoomContainer{version=3,height=.5f,rectangle=new(){width=2,depth=1},capacityMl=terrain.Volume*1e6};
   c.amountMl=c.capacityMl;
   Assert.That(ContainerFlowGeometry.Excess(c,Quaternion.identity,Vector3.up,terrain),Is.EqualTo(0).Within(.001));
   c.version=4;Assert.That(ContainerFlowGeometry.Excess(c,Quaternion.identity,Vector3.up,terrain),Is.GreaterThan(0));
   Assert.Throws<InvalidOperationException>(()=>ContainerFlowGeometry.Excess(c,Quaternion.identity,Vector3.up));
   Assert.Throws<InvalidOperationException>(()=>ContainerFlowGeometry.Lip(c,Matrix4x4.identity,Quaternion.identity,Vector3.up,out _));
   Assert.That(c.Copy().SpillAtTerrainEdge,Is.True);
  }
  [Test]public void TerrainSpillCacheIsImmutableAndInvalidatesForNewGravity(){
   var bed=Bed();var terrain=new TerrainLiquidGeometry(bed,.5f);
   terrain.SpillLip(Vector3.up,out var original,out _);Array.Fill(bed.heights,0);
   terrain.SpillLip(Vector3.up,out var same,out _);Assert.AreEqual(original,same);
   terrain.SpillLip(new Vector3(1,.01f,0).normalized,out var tilted,out _);Assert.AreEqual(-1,tilted.x);
   terrain.SpillLip(Vector3.up,out var restored,out _);Assert.AreEqual(original,restored);
  }
  [Test]public void TerrainSpillFlagCannotEnableAnOrdinaryOrMismatchedSavedCavity(){
   var ordinary=ContainerCapability.Definition(new RoomContainer());ordinary["spillAtTerrainEdge"]=true;
   Assert.IsFalse(ContainerCapability.ValidDefinition(ordinary,out _));ordinary["terrainFloor"]=true;
   Assert.IsTrue(ContainerCapability.ValidDefinition(ordinary,out var error),error);
   ordinary["version"]=3;Assert.IsFalse(ContainerCapability.ValidDefinition(ordinary,out _));
  }
 }
}
