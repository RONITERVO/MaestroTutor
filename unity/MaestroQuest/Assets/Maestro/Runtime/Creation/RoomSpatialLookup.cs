// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Immutable bounds snapshot. No native object, journal or cached live pose is
    // retained. A caller must capture again when its coherent read ends.
    internal sealed class RoomSpatialLookup
    {
        // Index capacity is independent of saved-room and native-instance limits.
        // This does not increase the room format's currently accepted capacity.
        internal const int MaximumEntries=4096;
        struct Node { internal Vector3 Min,Max;internal int Left,Right,Entry; }
        readonly RoomSpatialExtent[] entries;
        readonly int[] unknown,empty;
        readonly Node[] nodes;
        readonly IComparer<int>[] comparers;
        int nextNode,buildWork;
        internal int BuildWork=>buildWork;
        internal int NodeCount=>nodes.Length;
        internal RoomSpatialLookup(IReadOnlyList<RoomSpatialExtent> source)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            if(source.Count>MaximumEntries)throw new ArgumentOutOfRangeException(nameof(source),"Spatial lookup capacity exceeded");
            entries=new RoomSpatialExtent[source.Count];var uncertain=new List<int>();var absent=new List<int>();var known=new List<int>();
            for(int i=0;i<entries.Length;i++){
                buildWork++;var value=source[i];
                if(value.Known&&value.HasBounds&&!Valid(value.Bounds))value=default;
                entries[i]=value;
                if(!value.Known)uncertain.Add(i);else if(!value.HasBounds)absent.Add(i);else known.Add(i);
            }
            unknown=uncertain.ToArray();empty=absent.ToArray();nodes=new Node[Math.Max(0,2*known.Count-1)];
            comparers=new IComparer<int>[3];
            for(int axis=0;axis<3;axis++){int selected=axis;comparers[axis]=Comparer<int>.Create((a,b)=>{int order=entries[a].Bounds.center[selected].CompareTo(entries[b].Bounds.center[selected]);return order!=0?order:a.CompareTo(b);});}
            if(known.Count>0)Build(known.ToArray(),0,known.Count);
        }
        int Build(int[] order,int first,int count)
        {
            int slot=nextNode++;buildWork++;
            var min=Vector3.positiveInfinity;var max=Vector3.negativeInfinity;
            var centresMin=Vector3.positiveInfinity;var centresMax=Vector3.negativeInfinity;
            for(int i=first;i<first+count;i++){
                buildWork++;var bounds=entries[order[i]].Bounds;
                min=Vector3.Min(min,bounds.min);max=Vector3.Max(max,bounds.max);
                centresMin=Vector3.Min(centresMin,bounds.center);centresMax=Vector3.Max(centresMax,bounds.center);
            }
            if(count==1){nodes[slot]=new(){Min=min,Max=max,Entry=order[first],Left=-1,Right=-1};return slot;}
            int axis=0;double span=(double)centresMax.x-centresMin.x;
            for(int i=1;i<3;i++){double width=(double)centresMax[i]-centresMin[i];if(width>span){axis=i;span=width;}}
            Array.Sort(order,first,count,comparers[axis]);int half=count/2;
            int left=Build(order,first,half),right=Build(order,first+half,count-half);
            // Direct extrema avoid a reconstructed centre/extents rounding a
            // child out of its parent, including very large enclosing bounds.
            nodes[slot]=new(){Min=min,Max=max,Entry=-1,Left=left,Right=right};return slot;
        }
        internal static bool Valid(Bounds bounds)=>RoomRecipe.Finite(bounds.center)&&RoomRecipe.Finite(bounds.extents)&&RoomRecipe.Finite(bounds.min)&&RoomRecipe.Finite(bounds.max)&&bounds.extents.x>=0&&bounds.extents.y>=0&&bounds.extents.z>=0;
        internal static int MaximumBuildWork(int count)
        {
            if(count<0||count>MaximumEntries)throw new ArgumentOutOfRangeException(nameof(count));
            int levels=0;for(int width=1;width<count;width*=2)levels++;
            return count*(levels+4); // classification, bounds reductions and nodes; sorting excluded
        }
        // Always complete for this bounded immutable snapshot. Unknown extents
        // cannot be rejected using artwork or an object's saved centre.
        internal int Query(Bounds body,Bounds? support,List<int> result)
        {
            if(result==null)throw new ArgumentNullException(nameof(result));
            result.Clear();result.AddRange(unknown);if(support.HasValue)result.AddRange(empty);
            bool all=!Valid(body)||support.HasValue&&!Valid(support.Value);int visited=0;
            if(nodes.Length>0)Visit(0,body.min,body.max,support?.min??default,support?.max??default,support.HasValue,all,result,ref visited);
            return visited;
        }
        void Visit(int index,Vector3 bodyMin,Vector3 bodyMax,Vector3 groundMin,Vector3 groundMax,bool ground,bool all,List<int> result,ref int visited)
        {
            visited++;var node=nodes[index];
            if(!all&&!Intersects(node.Min,node.Max,bodyMin,bodyMax)&&!(ground&&Intersects(node.Min,node.Max,groundMin,groundMax)))return;
            if(node.Entry>=0){result.Add(node.Entry);return;}
            Visit(node.Left,bodyMin,bodyMax,groundMin,groundMax,ground,all,result,ref visited);
            Visit(node.Right,bodyMin,bodyMax,groundMin,groundMax,ground,all,result,ref visited);
        }
        static bool Intersects(Vector3 a,Vector3 b,Vector3 c,Vector3 d)=>a.x<=d.x&&b.x>=c.x&&a.y<=d.y&&b.y>=c.y&&a.z<=d.z&&b.z>=c.z;
    }
}
