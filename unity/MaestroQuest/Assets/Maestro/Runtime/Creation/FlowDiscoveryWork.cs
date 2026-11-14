// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
namespace Maestro.Quest.Creation {
 // Logical discovery allowance, independently admitted before flow execution.
 // No geometry is needed to price it; preparing inputs cannot need an execution grant.
 internal static class FlowDiscoveryWork {
  internal const int MaximumUnits=524288,MaximumObjects=RoomSpatialLookup.MaximumEntries;
  internal static int Demand(int vessels,int objects){
   if(vessels<0||objects<0||vessels>objects||vessels>RoomContainer.MaximumPerRoom||objects>MaximumObjects)return int.MaxValue;
   if(vessels==0)return 0;
   long n=vessels,m=objects;
   // Query capacity is independent of the saved-room quota. One collision
   // capture and one physical adjacency build, then at most three spatial
   // probes per vessel plus cavity/trajectory/quantity-graph visits. There is
   // at most one physical edge per object, so adjacency construction and each
   // island traversal are linear (including missing referenced identities).
   // Geometry/sort/PhysX internals excluded. Exceeding the work allowance still
   // refuses admission; this bound does not promise capacity for every count.
   return (int)Math.Min(int.MaxValue,9*m+RoomSpatialLookup.MaximumBuildWork(objects)+n*(18*m+4*n+44));
  }
 }
 public sealed partial class LiquidPouring {
  internal struct DiscoveryWorkSample {
   internal int Captures,CollisionReads,CollisionCandidates,CollisionIndexBuild,CollisionNodes,CavityReads,CavityCandidates,Probes,PathSegments,IslandQueries,IslandMembers,IslandBuild,IslandVisits,GraphEdges;
  }
  DiscoveryWorkSample discoveryWork;
  internal DiscoveryWorkSample LastFlowDiscovery=>discoveryWork;
  internal int FlowDiscoveryObjects=>editor?editor.SpatialObjectCount:0;
 }
}
