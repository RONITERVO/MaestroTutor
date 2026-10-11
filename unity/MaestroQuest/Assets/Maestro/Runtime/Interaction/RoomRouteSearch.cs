// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // One transient route request. Native queries and candidate sampling are
    // cooperative; no result outlives its caller's geometry/policy identity.
    internal sealed class RoomRouteSearch {
        internal delegate bool EdgeQuery(Vector3 from,Vector3 to,out Vector3[] path,out string liquid,out Vector3 foot);
        internal delegate void FootprintQuery(string liquid,Vector3 reference,List<Vector3> points);
        internal delegate bool SampleQuery(Vector3 point,out Vector3 floor);
        internal delegate bool SegmentQuery(Vector3 from,Vector3 to);
        internal const int QueriesPerTick=4,MaximumEdges=2048,MaximumNodes=546,MaximumCorners=128;
        sealed class Node {internal Vector3 Point;internal float Cost=float.PositiveInfinity;internal int Parent=-1;internal Vector3[] Incoming;internal bool Expanded;}
        readonly List<Node> nodes=new();readonly Dictionary<long,Vector3[]> edges=new();readonly HashSet<string> liquids=new(StringComparer.Ordinal);
        readonly List<Vector3> candidates=new();readonly List<Vector3> buffer=new();int candidate,active=-1,next=1,validation;
        readonly EdgeQuery edge;readonly FootprintQuery footprint;readonly SampleQuery sample;readonly SegmentQuery segment;
        Vector3[] validating;
        internal readonly Vector3 From,To;
        internal Vector3[] Result {get;private set;}
        internal string Failure {get;private set;}
        internal bool Pending=>Result==null&&Failure==null;
        internal int QueryCount=>edges.Count;
        internal IEnumerable<string> WaterDependencies=>liquids;
        internal RoomRouteSearch(Vector3 from,Vector3 to,string liquid,Vector3 foot,EdgeQuery edge,FootprintQuery footprint,SampleQuery sample,SegmentQuery segment){
            From=from;To=to;this.edge=edge;this.footprint=footprint;this.sample=sample;this.segment=segment;
            nodes.Add(new(){Point=from,Cost=0});nodes.Add(new(){Point=to});edges[Key(0,1)]=null;
            if(!Discover(liquid,foot))Failure="No supported route around this water is available";
        }
        static long Key(int a,int b)=>((long)a<<32)|(uint)b;
        bool Discover(string id,Vector3 reference){
            if(string.IsNullOrEmpty(id)||!liquids.Add(id))return false;
            buffer.Clear();footprint(id,reference,buffer);candidates.AddRange(buffer);return buffer.Count>0;
        }
        void Reopen(){foreach(var n in nodes)if(float.IsFinite(n.Cost))n.Expanded=false;active=-1;next=1;}
        void Add(Vector3 point){
            foreach(var n in nodes)if((n.Point-point).sqrMagnitude<.000001f)return;
            if(nodes.Count==MaximumNodes){Failure="This water route exceeds the supported search size";return;}nodes.Add(new(){Point=point});
        }
        int Best(){int best=-1;float cost=float.PositiveInfinity;for(int i=0;i<nodes.Count;i++){var n=nodes[i];float value=n.Cost+Vector3.Distance(n.Point,To);if(!n.Expanded&&value<cost){best=i;cost=value;}}return best;}
        void Finish(){
            var chain=new List<int>();int cursor=1;
            while(cursor>0&&chain.Count<nodes.Count){chain.Add(cursor);cursor=nodes[cursor].Parent;}
            if(cursor!=0){Failure="The water route could not be reconstructed";return;}
            var points=new List<Vector3>{From};
            for(int i=chain.Count-1;i>=0;i--)foreach(var p in nodes[chain[i]].Incoming){if((p-points[^1]).sqrMagnitude>.000001f)points.Add(p);}
            if(points.Count>MaximumCorners){Failure="The water route has too many turns";return;}
            validating=points.ToArray();validation=1;
        }
        void Relax(int target,Vector3[] path){
            if(path==null)return;float length=0;for(int i=1;i<path.Length;i++)length+=Vector3.Distance(path[i-1],path[i]);
            var source=nodes[active];var destination=nodes[target];float cost=source.Cost+length;
            if(cost>=destination.Cost-.00001f)return;
            destination.Cost=cost;destination.Parent=active;destination.Incoming=path;destination.Expanded=false;
            if(target==1)Finish();
        }
        internal void Tick(){
            if(!Pending)return;long began=Stopwatch.GetTimestamp();int work=0;
            while(Pending&&work<QueriesPerTick&&(Stopwatch.GetTimestamp()-began)/(double)Stopwatch.Frequency<.002){
                if(validation>0){
                    if(validation==validating.Length){Result=validating;return;}
                    work++;if(!segment(validating[validation-1],validating[validation])){Failure="Water or obstacles changed while the route was planned";return;}validation++;continue;
                }
                if(candidate<candidates.Count){
                    work++;if(sample(candidates[candidate++],out var point))Add(point);
                    if(candidate==candidates.Count)Reopen();continue;
                }
                if(active<0){active=Best();next=1;if(active<0){Failure="No supported route around this water is available";return;}}
                if(next==nodes.Count){nodes[active].Expanded=true;active=-1;continue;}
                int target=next++;if(target==active)continue;
                long key=Key(active,target);
                if(edges.TryGetValue(key,out var saved)){Relax(target,saved);continue;}
                if(edges.Count==MaximumEdges){Failure="This water route exceeds the supported search work";return;}
                work++;bool allowed=edge(nodes[active].Point,nodes[target].Point,out var path,out var liquid,out var foot);
                edges[key]=allowed?path:null;
                if(allowed)Relax(target,path);
                else if(Discover(liquid,foot)){active=-1;next=1;}
            }
        }
    }
}
