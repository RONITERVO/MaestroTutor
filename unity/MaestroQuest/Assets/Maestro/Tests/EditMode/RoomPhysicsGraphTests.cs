// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
 public sealed class RoomPhysicsGraphTests {
  static string[] Exhaustive((string owner,string other)[] edges,HashSet<string> fixedIds,string root,out int scans){
   var found=new HashSet<string>(StringComparer.Ordinal){root};var pending=new Queue<string>();pending.Enqueue(root);scans=0;
   while(pending.Count>0){
    string next=pending.Dequeue();if(next!=root&&fixedIds.Contains(next))continue;
    foreach(var edge in edges){scans++;var peer=edge.owner==next?edge.other:edge.other==next?edge.owner:null;if(peer!=null&&found.Add(peer))pending.Enqueue(peer);}
   }
   return found.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
  }
  [Test]public void PhysicsGraphMatchesExhaustiveTraversalAcrossFixedDanglingAndCyclicEdges(){
   var random=new Random(82614);
   for(int sample=0;sample<120;sample++){
    var ids=Enumerable.Range(0,32).Select(i=>"object-"+i).ToArray();var fixedIds=ids.Where(_=>random.Next(5)==0).ToHashSet(StringComparer.Ordinal);
    var edges=ids.Where(id=>!fixedIds.Contains(id)&&random.Next(5)>0).Select(id=>(id,random.Next(8)==0?"missing-"+random.Next(4):ids[random.Next(ids.Length)])).ToArray();
    var graph=new RoomPhysicsGraph(edges,id=>!fixedIds.Contains(id),ids.Length);
    foreach(var id in ids.Append("missing-0").Append("isolated"))Assert.That(graph.Members(id,out _),Is.EqualTo(Exhaustive(edges,fixedIds,id,out _)),"sample="+sample+" root="+id);
   }
  }
  [Test]public void PhysicsGraphFixedAnchorIsSupportAndStillInterruptsAllOfItsChildren(){
   var graph=new RoomPhysicsGraph(new[]{("left","anchor"),("right","anchor"),("tip","left")},id=>id!="anchor");
   Assert.That(graph.Members("left",out _),Is.EqualTo(new[]{"anchor","left","tip"}));
   Assert.That(graph.Members("right",out _),Is.EqualTo(new[]{"anchor","right"}));
   Assert.That(graph.Members("anchor",out _),Is.EqualTo(new[]{"anchor","left","right","tip"}));
   Assert.That(graph.Members(null,out _),Is.Empty);
  }
  [Test]public void PhysicsGraphCapturesTopologyAndDynamicClassificationWithoutRetainingSources(){
   var edges=new List<(string,string)>{("a","b"),("c","b")};bool dynamic=false;
   var snapshot=new RoomPhysicsGraph(edges,_=>dynamic);edges.Clear();dynamic=true;
   Assert.That(snapshot.Members("a",out _),Is.EqualTo(new[]{"a","b"}));
   Assert.That(new RoomPhysicsGraph(edges,_=>dynamic).Members("a",out _),Is.EqualTo(new[]{"a"}));
   var result=snapshot.Members("a",out _);result[0]="corrupted";
   Assert.That(snapshot.Members("a",out _),Is.EqualTo(new[]{"a","b"}));
  }
  [Test]public void PhysicsGraphLargeChainAndUnrelatedComponentsHaveLinearVisitedWork(){
   const int count=1024;var edges=Enumerable.Range(1,count-1).Select(i=>("chain-"+i,"chain-"+(i-1))).ToArray();
   var graph=new RoomPhysicsGraph(edges,_=>true,count);
   var expected=Exhaustive(edges,new(),"chain-0",out int oldScans);var actual=graph.Members("chain-0",out int visits);
   Assert.That(actual,Is.EqualTo(expected));Assert.That(oldScans,Is.EqualTo(count*(count-1)));
   Assert.That(graph.BuildWork,Is.LessThanOrEqualTo(6*count));Assert.That(visits,Is.EqualTo(count+2*(count-1)));
   var unrelated=edges.Concat(new[]{("near-a","near-b")}).ToArray();
   var distant=new RoomPhysicsGraph(unrelated,_=>true,count+2);
   Assert.That(distant.Members("near-a",out int nearVisits),Is.EqualTo(new[]{"near-a","near-b"}));Assert.That(nearVisits,Is.EqualTo(4));
   string folder=Environment.GetEnvironmentVariable("MAESTRO_FLOW_TOPOLOGY_EVIDENCE");
   if(!string.IsNullOrEmpty(folder)){
    System.IO.Directory.CreateDirectory(folder);System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"chain.json"),new Newtonsoft.Json.Linq.JObject{
     ["objects"]=count,["edges"]=edges.Length,["exhaustiveEdgeScans"]=oldScans,["buildWork"]=graph.BuildWork,["queryVisits"]=visits,["separateComponentVisits"]=nearVisits,["sameMembership"]=true}.ToString());
   }
  }
 }
}
