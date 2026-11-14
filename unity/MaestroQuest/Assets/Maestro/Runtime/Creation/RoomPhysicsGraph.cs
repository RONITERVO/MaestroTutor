// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
namespace Maestro.Quest.Creation {
 // A coherent synchronous read of accepted, unbroken physical edges. Native
 // break state is captured by the caller; this graph is never cached across ticks.
 internal sealed class RoomPhysicsGraph {
  sealed class Node {
   internal readonly bool Dynamic;internal readonly List<string> Neighbours=new();
   internal Node(bool dynamic){Dynamic=dynamic;}
  }
  readonly Dictionary<string,Node> nodes=new(StringComparer.Ordinal);
  internal int BuildWork {get;private set;}
  internal RoomPhysicsGraph(IEnumerable<(string owner,string other)> edges,Func<string,bool> dynamic,int sourceReads=0){
   BuildWork=sourceReads;
   Node Get(string id){
    if(!nodes.TryGetValue(id,out var node)){BuildWork++;node=new(dynamic(id));nodes.Add(id,node);}return node;
   }
   foreach(var edge in edges){
    BuildWork++;var a=Get(edge.owner);var b=Get(edge.other);
    a.Neighbours.Add(edge.other);b.Neighbours.Add(edge.owner);BuildWork+=2;
   }
  }
  internal string[] Members(string id,out int work){
   work=0;if(id==null)return Array.Empty<string>();
   var found=new HashSet<string>(StringComparer.Ordinal){id};var pending=new Queue<string>();pending.Enqueue(id);
   while(pending.Count>0){
    var next=pending.Dequeue();work++;
    if(!nodes.TryGetValue(next,out var node))continue;
    // Fixed anchors are shared support, not force paths between their children.
    // Starting at the anchor still reaches all its children for interruption.
    if(next!=id&&!node.Dynamic)continue;
    foreach(var peer in node.Neighbours){work++;if(found.Add(peer))pending.Enqueue(peer);}
   }
   return found.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
  }
 }
}
