// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>A bounded logical liquid store and an explicitly authored cylindrical cavity.</summary>
    [Serializable] public sealed class RoomContainer {
        public const int MaximumPerRoom=16;
        public const double MaximumMillilitres=1000000;
        public int version=1;
        public ConnectionFrame frame=new(); // Centre of the bottom; local +Y is the opening.
        public float radius=.04f,height=.1f;
        // Capacity is a game rule, independent of visual object scaling.
        public double capacityMl=250,amountMl;
        public string liquid="Water";
        public Color color=new(.16f,.55f,.85f,1);
        public RoomContainer Copy()=>new(){version=version,frame=frame?.Copy(),radius=radius,height=height,capacityMl=capacityMl,amountMl=amountMl,liquid=liquid,color=color};
        public bool Validate(out string error){
            error="Use a version-1 cylindrical container with a normalized bottom frame, radius 0.005–1 m, height 0.01–2 m, capacity 1–1000000 ml and contents within capacity";
            if(version!=1||frame?.Valid!=true||!float.IsFinite(radius)||radius<.005f||radius>1||!float.IsFinite(height)||height<.01f||height>2||!double.IsFinite(capacityMl)||capacityMl<1||capacityMl>MaximumMillilitres||!double.IsFinite(amountMl)||amountMl<0||amountMl>capacityMl||!RoomSnapPoint.Identifier(liquid)||liquid.Any(char.IsControl)||!Unit(color.r)||!Unit(color.g)||!Unit(color.b)||color.a!=1)return false;
            error=null;return true;
        }
        static bool Unit(float n)=>float.IsFinite(n)&&n>=0&&n<=1;
        public static bool ValidateCollection(RoomObjectData owner,out string error){
            var items=owner.containers??Array.Empty<RoomContainer>();error="Use at most one liquid container on a created object";
            if(items.Length>1||owner.IsBuiltIn&&items.Length>0||items.Any(c=>c==null))return false;
            if(items.Length==1)return items[0].Validate(out error);error=null;return true;
        }
        // Exact identity and colour prevent silently mixing unlike contents. No rounding to float.
        internal static bool Transfer(RoomContainer source,RoomContainer destination,double requested,out double moved,out string error){
            moved=0;error="Choose two valid containers and a finite positive transfer of at most 1000000 ml";
            if(source==null||destination==null||ReferenceEquals(source,destination)||!source.Validate(out _)||!destination.Validate(out _)||!double.IsFinite(requested)||requested<=0||requested>MaximumMillilitres)return false;
            if(destination.amountMl>0&&(source.liquid!=destination.liquid||!source.color.Equals(destination.color))){error="Empty the destination or choose matching liquid identity and colour; mixing is not supported";return false;}
            moved=Math.Min(requested,Math.Min(source.amountMl,destination.capacityMl-destination.amountMl));
            if(moved<=0){error=source.amountMl<=0?"The source is empty":"The destination is full";return false;}
            // Reject a sub-ULP request rather than claiming an amount neither side can represent.
            double left=source.amountMl-moved,right=destination.amountMl+moved;
            if(left==source.amountMl||right==destination.amountMl){moved=0;error="The requested amount is too small at the current quantities";return false;}
            destination.liquid=source.liquid;destination.color=source.color;source.amountMl=left;destination.amountMl=right;error=null;return true;
        }
    }
}
