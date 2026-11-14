// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // Heights come from actual supported/buoyant proposals, never from snapping
    // X/Z onto an unrelated floor. Each actual movement step is checked again.
    internal sealed class RoomShoreRoute {
        internal delegate bool Traverse(Vector3 from,Vector3 planar,float distance,bool floating,out RoomShoreMotor.Step next,out string error);
        sealed class Node {internal Vector3 Foot;internal bool Floating;internal Node Parent;internal float Cost;internal int Edge;}
        const int MaximumNodes=512,EdgesPerTick=8;
        internal const float Spacing=.25f,SearchMargin=2;
        static readonly Vector3[] Directions={Vector3.right,Vector3.left,Vector3.forward,Vector3.back,
            new Vector3(1,0,1),new Vector3(-1,0,1),new Vector3(1,0,-1),new Vector3(-1,0,-1)};
        readonly List<Node> open=new();readonly HashSet<(int,int,int,bool)> seen=new();
        readonly Vector3 origin,goal;readonly bool surface;readonly Traverse traverse;readonly Bounds bounds;
        Node current;int nodes=1;bool full;
        internal Vector3[] Path {get;private set;}
        internal bool Done {get;private set;}
        internal string Error {get;private set;}
        internal RoomShoreRoute(Vector3 start,bool floating,Vector3 destination,bool surface,Traverse traverse){
            origin=start;goal=destination;this.surface=surface;this.traverse=traverse;
            var area=new Bounds(start,Vector3.zero);area.Encapsulate(goal);area.Expand(SearchMargin*2);bounds=area;
            open.Add(new Node{Foot=start,Floating=floating});seen.Add(Key(start,floating));
        }
        (int,int,int,bool) Key(Vector3 p,bool wet)=>(Mathf.RoundToInt((p.x-origin.x)/Spacing),Mathf.RoundToInt((p.y-origin.y)/.025f),Mathf.RoundToInt((p.z-origin.z)/Spacing),wet);
        bool Edge(Node from,Vector3 point,out RoomShoreMotor.Step next){
            var delta=point-from.Foot;delta.y=0;next=default;
            if(delta.magnitude>2.56f)return false;
            return traverse(from.Foot,delta,Mathf.Max(.001f,delta.magnitude),from.Floating,out next,out _)&&
                new Vector2(next.Foot.x-point.x,next.Foot.z-point.z).magnitude<.002f;
        }
        internal void Tick(){
            if(Done)return;
            for(int work=0;work<EdgesPerTick;work++){
                if(current==null){
                    if(open.Count==0){Done=true;Error=full?"The bounded shore route search exhausted its capacity":"No supported, clear route through the selected shore was found";return;}
                    int best=0;float score=float.PositiveInfinity;
                    for(int i=0;i<open.Count;i++){float value=open[i].Cost+Vector3.Distance(open[i].Foot,goal);if(value<score){score=value;best=i;}}
                    current=open[best];open.RemoveAt(best);
                }
                if(current.Edge++==0){
                    if(Edge(current,goal,out var end)&&end.Floating==surface&&Vector3.Distance(end.Foot,goal)<.005f){
                        var path=new List<Vector3>{end.Foot};for(var n=current;n!=null;n=n.Parent)path.Add(n.Foot);
                        path.Reverse();Path=path.ToArray();Done=true;return;
                    }
                }else{
                    var point=current.Foot+Directions[current.Edge-2]*Spacing;
                    if(bounds.Contains(point)&&nodes<MaximumNodes&&Edge(current,point,out var next)&&seen.Add(Key(next.Foot,next.Floating))){
                        open.Add(new Node{Foot=next.Foot,Floating=next.Floating,Parent=current,Cost=current.Cost+next.Distance});nodes++;
                    }else if(nodes>=MaximumNodes)full=true;
                    if(current.Edge>Directions.Length)current=null;
                }
            }
        }
    }
}
