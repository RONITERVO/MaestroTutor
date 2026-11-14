// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Caller-owned scratch for a synchronous finite-liquid path. Reused without
    // ray buffers/edge allocations; never retain its geometry across a capture.
    internal sealed class LiquidPath {
        readonly LiquidMediumGeometry[] media=new LiquidMediumGeometry[RoomContainer.MaximumPerRoom];
        readonly LiquidIntervals intervals=new();
        readonly LiquidPathPartition partition=new();
        internal int Count {get;private set;}
        internal bool Complete {get;private set;}
        internal float Metres {get;private set;}
        internal double WetMetres {get;private set;}
        internal int WetBodies {get;private set;}
        internal void Clear(){Array.Clear(media,0,media.Length);Count=0;Complete=false;Metres=0;WetMetres=0;WetBodies=0;}
        internal LiquidMediumGeometry Medium(int index)=>Complete&&index>=0&&index<Count?media[index]:throw new ArgumentOutOfRangeException(nameof(index));
        internal double MetresIn(int index)=>Complete?partition.Fraction(index)*Metres:0;
        internal double MetresIn(string id){if(Complete)for(int i=0;i<Count;i++)if(media[i].Id==id)return MetresIn(i);return 0;}
        internal bool Measure(LiquidMediumGeometry[] source,int count,Vector3 from,Vector3 to){
            Clear();
            if(source==null||count<0||count>media.Length||count>source.Length||!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to))return false;
            float length=(to-from).magnitude;if(!float.IsFinite(length))return false;
            for(int i=0;i<count;i++){
                var m=source[i];
                if(string.IsNullOrEmpty(m.Id)||m.Contents==null||!float.IsFinite(m.Volume)||m.Volume<=0){Clear();return false;}
                for(int j=0;j<i;j++)if(media[j].Id==m.Id){Clear();return false;}
                int at=i;while(at>0&&Preferred(m,media[at-1])){media[at]=media[at-1];at--;}media[at]=m;
            }
            partition.Begin(count);
            for(int i=0;i<count;i++){media[i].MeasureSegment(from,to,out _,intervals);partition.Add(i,intervals);}
            if(!partition.Finish()){Clear();return false;}
            Count=count;Metres=length;Complete=true;
            for(int i=0;i<count;i++){WetMetres+=MetresIn(i);if(partition.Fraction(i)>0)WetBodies++;}
            return true;
        }
        internal static bool Preferred(in LiquidMediumGeometry candidate,in LiquidMediumGeometry current)=>
            candidate.Volume<current.Volume||candidate.Volume==current.Volume&&string.CompareOrdinal(candidate.Id,current.Id)<0;
    }
}
