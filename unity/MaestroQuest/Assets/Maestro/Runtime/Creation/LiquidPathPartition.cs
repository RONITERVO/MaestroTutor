// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
namespace Maestro.Quest.Creation {
    // One shared finite ray, with each wet fraction assigned to exactly one
    // medium. Lower index wins overlaps; callers supply stable cavity priority.
    // Disabled presentation settings still occupy their medium's own intervals.
    internal sealed class LiquidPathPartition {
        internal const int MaximumMedia=RoomContainer.MaximumPerRoom;
        readonly struct Edge {
            internal readonly float At;internal readonly int Owner;internal readonly bool Enter;
            internal Edge(float at,int owner,bool enter){At=at;Owner=owner;Enter=enter;}
        }
        sealed class Order:IComparer<Edge> {
            internal static readonly Order Instance=new();
            public int Compare(Edge a,Edge b)=>a.At.CompareTo(b.At);
        }
        readonly Edge[] edges=new Edge[MaximumMedia*LiquidIntervals.Maximum*2];
        readonly double[] fractions=new double[MaximumMedia];
        int count,media;uint supplied;bool collecting,valid;
        internal bool Complete {get;private set;}
        internal double Fraction(int owner)=>Complete&&owner>=0&&owner<media?fractions[owner]:0;
        internal void Begin(int owners){
            count=0;supplied=0;media=owners;Array.Clear(fractions,0,fractions.Length);
            Complete=false;collecting=true;valid=owners>=0&&owners<=MaximumMedia;
        }
        internal void Add(int owner,LiquidIntervals intervals){
            if(!collecting){Complete=false;valid=false;return;}
            if(!valid)return;
            if(owner<0||owner>=media||(supplied&(1u<<owner))!=0||intervals==null||!intervals.Complete||count+intervals.Count*2>edges.Length){valid=false;return;}
            supplied|=1u<<owner;
            for(int i=0;i<intervals.Count;i++){var v=intervals[i];edges[count++]=new Edge(v.x,owner,true);edges[count++]=new Edge(v.y,owner,false);}
        }
        internal bool Finish(){
            if(!collecting)return Complete;
            collecting=false;
            if(!valid||supplied!=(1u<<media)-1){Complete=false;return false;}
            Array.Sort(edges,0,count,Order.Instance);
            uint active=0;float previous=0;int index=0;
            while(index<count){
                float at=edges[index].At;
                if(at>previous&&active!=0){
                    int owner=0;while((active&(1u<<owner))==0)owner++;
                    fractions[owner]+=(double)at-previous;
                }
                do {
                    var edge=edges[index++];uint bit=1u<<edge.Owner;
                    if(edge.Enter){if((active&bit)!=0){Complete=false;return false;}active|=bit;}
                    else {if((active&bit)==0){Complete=false;return false;}active&=~bit;}
                }while(index<count&&edges[index].At==at);
                previous=at;
            }
            Complete=active==0;return Complete;
        }
    }
}
