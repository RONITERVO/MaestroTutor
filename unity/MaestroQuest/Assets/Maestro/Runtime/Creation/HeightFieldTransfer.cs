// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>Bounded geometric transfer. No hidden material ledger or per-frame simulation.</summary>
    internal static class HeightFieldTransfer {
        internal readonly struct Result {
            public readonly double RemovedLitres,AddedLitres;
            public double RoundingLitres=>AddedLitres-RemovedLitres;
            public Result(double removed,double added){RemovedLitres=removed;AddedLitres=added;}
        }
        // Heights are floats. Refuse an unrepresentable transaction rather than
        // slowly losing material through clamping or independently rounded edits.
        internal static double Tolerance(double litres)=>Math.Max(0.000001,litres*0.000001);
        internal static bool Apply(RoomHeightField source,Vector2 from,float fromRadius,
            RoomHeightField destination,Vector2 to,float toRadius,double requested,
            out Result result,out string error){
            result=default;
            error="Choose two valid, distinct height surfaces, matching material and colour, in-bounds local X/Z centres, radii 0.005–2 m and 0.001–8000 local litres";
            if(source==null||destination==null||ReferenceEquals(source,destination)||!source.Validate(out _)||!destination.Validate(out _)||
                source.material!=destination.material||!source.color.Equals(destination.color)||!double.IsFinite(requested)||requested<.001||requested>8000||
                !FootprintValid(source,from,fromRadius)||!FootprintValid(destination,to,toRadius))return false;
            var remove=new Footprint(source,from,fromRadius,false);
            var add=new Footprint(destination,to,toRadius,true);
            double amount=Math.Min(requested,Math.Min(remove.Capacity,add.Capacity));
            error="No transferable volume in these footprints; enlarge the radius or choose a nonempty source and a destination with space";
            if(amount<=0)return false;
            // Source loss is measured from representable heights, then the receiver
            // is fitted to that loss. Neither source changes until both pass.
            var taken=remove.Sample(amount,out double removed);
            if(removed<=0||removed>requested+Tolerance(requested))return false;
            var deposited=add.Fit(removed,out double added);
            error="This amount is below the surfaces' numeric resolution; use a larger transfer or a smaller/lower surface";
            if(added<=0||Math.Abs(added-removed)>Tolerance(removed))return false;
            source.heights=taken;destination.heights=deposited;result=new Result(removed,added);error=null;return true;
        }
        internal static bool Extract(RoomHeightField field,Vector2 centre,float radius,double requested,out double removed,out string error){
            removed=0;error="Choose a valid surface, in-bounds local footprint and 0.001–8000 local litres";
            if(field==null||!field.Validate(out _)||!FootprintValid(field,centre,radius)||!double.IsFinite(requested)||requested<.001||requested>8000)return false;
            var footprint=new Footprint(field,centre,radius,false);
            var next=footprint.FitAtMost(Math.Min(requested,footprint.Capacity),out removed);
            error="The footprint contains less than 0.001 transferable litre; enlarge it or choose another place";
            if(removed<.001){removed=0;return false;}
            field.heights=next;error=null;return true;
        }
        static bool FootprintValid(RoomHeightField f,Vector2 p,float r)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&
            Mathf.Abs(p.x)<=f.width*.5f&&Mathf.Abs(p.y)<=f.depth*.5f&&float.IsFinite(r)&&r>=.005f&&r<=2;
        sealed class Footprint {
            readonly RoomHeightField field;
            readonly bool adding;
            readonly double[] limits,coefficients;
            public double Capacity {get;}
            public Footprint(RoomHeightField f,Vector2 centre,float radius,bool adding){
                field=f;this.adding=adding;limits=new double[f.heights.Length];coefficients=new double[f.heights.Length];
                double coefficient=(double)f.width*f.depth*1000/(6*f.cells*f.cells);
                // The mesh splits each cell at its b/c diagonal: a,d count once;
                // b,c twice. Boundary vertices cannot use an interior approximation.
                for(int z=0;z<f.cells;z++)for(int x=0;x<f.cells;x++){
                    int a=z*(f.cells+1)+x;coefficients[a]+=coefficient;coefficients[a+1]+=2*coefficient;
                    coefficients[a+f.cells+1]+=2*coefficient;coefficients[a+f.cells+2]+=coefficient;
                }
                for(int z=0;z<=f.cells;z++)for(int x=0;x<=f.cells;x++){
                    int i=z*(f.cells+1)+x;var v=f.Vertex(x,z);double w=Math.Max(0,1-Vector2.Distance(new Vector2(v.x,v.z),centre)/radius);w=w*w*(3-2*w);
                    limits[i]=(adding?(double)f.maxHeight-f.heights[i]:f.heights[i])*w;
                    Capacity+=limits[i]*coefficients[i];
                }
            }
            public float[] Sample(double amount,out double moved){
                double fraction=Capacity>0?Math.Min(1,Math.Max(0,amount/Capacity)):0;moved=0;
                var heights=new float[field.heights.Length];
                for(int i=0;i<heights.Length;i++){
                    float old=field.heights[i];double delta=limits[i]*fraction;
                    heights[i]=(float)Math.Max(0,Math.Min(field.maxHeight,adding?old+delta:old-delta));
                    moved+=(adding?(double)heights[i]-old:(double)old-heights[i])*coefficients[i];
                }
                return heights;
            }
            public float[] FitAtMost(double amount,out double moved){
                var candidate=Sample(amount,out double actual);
                if(actual<=amount){moved=actual;return candidate;}
                var best=Sample(0,out moved);double low=0,high=amount;
                for(int step=0;step<40;step++){
                    double mid=(low+high)*.5;candidate=Sample(mid,out actual);
                    if(actual<=amount){if(actual>moved){best=candidate;moved=actual;}low=mid;}else high=mid;
                    if(amount-moved<=Tolerance(amount))break;
                }
                return best;
            }
            public float[] Fit(double amount,out double moved){
                var best=Sample(amount,out moved);double distance=Math.Abs(moved-amount);
                if(distance<=Tolerance(amount))return best;
                double low=0,high=Capacity;
                // Monotonic quantized volume; fixed iterations cap the cost.
                for(int step=0;step<40;step++){
                    double mid=(low+high)*.5;var candidate=Sample(mid,out double actual);
                    double difference=Math.Abs(actual-amount);
                    if(difference<distance){best=candidate;moved=actual;distance=difference;}
                    if(distance<=Tolerance(amount))break;
                    if(actual<amount)low=mid;else high=mid;
                }
                return best;
            }
        }
    }
}
