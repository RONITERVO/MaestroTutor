// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
namespace Maestro.Quest.Creation {
 // Logical discovery allowance, independently accounted but granted atomically
 // with the host's flow and dependent physics. No geometry is needed to price it.
 internal static class FlowDiscoveryWork {
  internal const int MaximumUnits=524288,MaximumObjects=RoomDocument.MaximumObjects+2;
  internal static int Demand(int vessels,int objects){
   if(vessels<0||objects<0||vessels>objects||objects>MaximumObjects)return int.MaxValue;
   if(vessels==0)return 0;
   long n=vessels,m=objects;
   // Snapshot/ground metadata, at most three spatial probes per vessel, two
   // cavity probes, trajectory/rain points, transitive quantity edges/targets,
   // and worst-case physical-island visits. The accepted format has at most
   // one physical connection per object. Geometry/sort/PhysX internals excluded.
   return (int)Math.Min(int.MaxValue,3*m+n*(m*m+6*m+4*n+40));
  }
 }
 public sealed partial class LiquidPouring {
  internal struct DiscoveryWorkSample {
   internal int Captures,CollisionReads,CollisionCandidates,CavityReads,CavityCandidates,Probes,PathSegments,IslandQueries,IslandMembers,GraphEdges;
  }
  DiscoveryWorkSample discoveryWork;
  internal DiscoveryWorkSample LastFlowDiscovery=>discoveryWork;
  internal int FlowDiscoveryObjects=>editor?editor.SpatialObjectCount:0;
 }
}
