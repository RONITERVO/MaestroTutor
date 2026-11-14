// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // Bounded 3D search. Each edge proves an in-place turn followed by translation.
    // Actual movement must recheck both against the current world.
    internal sealed class RoomAquaticRoute {
        internal delegate bool Clear(Pose from,Pose to,out string reason);
        sealed class Node {internal Pose Pose;internal Node Parent;internal float Cost;internal int Edge;internal Vector3Int Cell;}
        const int MaximumNodes=512,EdgesPerTick=8;
        internal const float SearchMargin=2,Spacing=.35f;
        static readonly Vector3Int[] Directions={Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,new(0,0,1),new(0,0,-1)};
        readonly List<Node> open=new();readonly HashSet<(Vector3Int,int)> seen=new();
        readonly Vector3 origin,goal;readonly Bounds area;readonly bool facing;readonly float? surfaceHeight;readonly Clear clear;
        Node current;int nodes=1;bool capacityReached;
        internal Pose[] Path {get;private set;}
        internal bool Done {get;private set;}
        internal string Error {get;private set;}
        internal RoomAquaticRoute(Pose start,Vector3 destination,bool face,Clear check,float? surfaceHeight=null){
            origin=start.position;goal=destination;facing=face;clear=check;this.surfaceHeight=surfaceHeight;
            var corridor=new Bounds(origin,Vector3.zero);corridor.Encapsulate(goal);corridor.Expand(SearchMargin*2);area=corridor;
            open.Add(new Node{Pose=start});seen.Add((Vector3Int.zero,-1));
        }
        static Quaternion Facing(Vector3 direction,Quaternion previous){
            if(direction.sqrMagnitude<1e-10f)return previous;
            var up=Mathf.Abs(Vector3.Dot(direction.normalized,Vector3.up))>.99f?Vector3.forward:Vector3.up;
            return Quaternion.LookRotation(direction,up);
        }
        internal static Quaternion SurfaceFacing(Vector3 direction,Quaternion previous){
            var forward=Vector3.ProjectOnPlane(previous*Vector3.forward,Vector3.up);direction.y=0;
            if(forward.sqrMagnitude<1e-8f||direction.sqrMagnitude<1e-8f)return previous;
            return Quaternion.AngleAxis(Vector3.SignedAngle(forward,direction,Vector3.up),Vector3.up)*previous;
        }
        bool Edge(Node from,Vector3 point,out Pose result){
            result=new(point,facing?(surfaceHeight.HasValue?SurfaceFacing(point-from.Pose.position,from.Pose.rotation):Facing(point-from.Pose.position,from.Pose.rotation)):from.Pose.rotation);
            var turned=new Pose(from.Pose.position,result.rotation);
            return clear(from.Pose,turned,out _)&&clear(turned,result,out _);
        }
        internal void Tick(){
            if(Done)return;
            for(int work=0;work<EdgesPerTick;work++){
                if(current==null){
                    if(open.Count==0){Done=true;Error=capacityReached?"The bounded aquatic route search exhausted its capacity":"No contained, clear aquatic route was found";return;}
                    int best=0;float score=float.PositiveInfinity;
                    for(int i=0;i<open.Count;i++){float s=open[i].Cost+Vector3.Distance(open[i].Pose.position,goal);if(s<score){score=s;best=i;}}
                    current=open[best];open.RemoveAt(best);
                }
                if(current.Edge++==0){
                    if(Edge(current,goal,out var destination)){
                        var result=new List<Pose>{destination};
                        for(var node=current;node!=null;node=node.Parent)result.Add(node.Pose);
                        result.Reverse();Path=result.ToArray();Done=true;return;
                    }
                }else{
                    int direction=current.Edge-2;var cell=current.Cell+Directions[direction];var point=origin+(Vector3)cell*Spacing;
                    if(surfaceHeight.HasValue){
                        point.y=surfaceHeight.Value;
                        if(Directions[direction].y!=0){if(current.Edge>Directions.Length)current=null;continue;}
                    }
                    if(area.Contains(point)&&!seen.Contains((cell,facing?direction:-1))){
                        if(nodes>=MaximumNodes)capacityReached=true;
                        else if(Edge(current,point,out var pose)){
                            seen.Add((cell,facing?direction:-1));
                            open.Add(new Node{Pose=pose,Parent=current,Cell=cell,Cost=current.Cost+Spacing});nodes++;
                        }
                    }
                    if(current.Edge>Directions.Length)current=null;
                }
            }
        }
    }
}
