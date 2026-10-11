// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    [Serializable] public sealed class ContainerRectangle {
        public float width,depth; // Unity materializes missing inline objects; zero means absent for legacy cylinders.
        public ContainerRectangle Copy()=>new(){width=width,depth=depth};
        public bool Valid=>float.IsFinite(width)&&width>=.01f&&width<=2&&float.IsFinite(depth)&&depth>=.01f&&depth<=2;
    }
    /// <summary>A bounded logical liquid store with an explicitly authored cylinder or rectangular cavity.</summary>
    [Serializable] public sealed class RoomContainer {
        public const int MaximumPerRoom=16;
        public const double MaximumMillilitres=1000000;
        public int version=1;
        public ConnectionFrame frame=new(); // Centre of the bottom; local +Y is the opening.
        public float radius=.04f,height=.1f;
        // An optional rectangular footprint replaces the circular radius. Version 1
        // remains cylindrical; version 2 is rectangular. No imported shape inference.
        public ContainerRectangle rectangle;
        public bool IsRectangular=>version==2;
        internal float FootprintArea=>IsRectangular?rectangle.width*rectangle.depth:Mathf.PI*radius*radius;
        internal bool ContainsHorizontal(Vector3 point,float inset=1)=>IsRectangular
            ? Mathf.Abs(point.x)<=rectangle.width*.5f*inset&&Mathf.Abs(point.z)<=rectangle.depth*.5f*inset
            : point.x*point.x+point.z*point.z<=radius*radius*inset*inset;
        internal double HorizontalExtent(Vector3 normal)=>IsRectangular
            ? Math.Abs(normal.x)*rectangle.width*.5+Math.Abs(normal.z)*rectangle.depth*.5
            : radius*Math.Sqrt(normal.x*normal.x+normal.z*normal.z);
        // Capacity is a game rule, independent of visual object scaling.
        public double capacityMl=250,amountMl;
        public string liquid="Water";
        public RoomFluid fluid=new();
        internal bool SameLiquid(RoomContainer other)=>other!=null&&liquid==other.liquid&&color.Equals(other.color)&&RoomFluid.Same(fluid,other.fluid);
        public Color color=new(.16f,.55f,.85f,1);
        public RoomContainer Copy()=>new(){version=version,rectangle=rectangle?.Copy(),frame=frame?.Copy(),radius=radius,height=height,capacityMl=capacityMl,amountMl=amountMl,liquid=liquid,fluid=RoomFluid.Effective(fluid).Copy(),color=color};
        public bool Validate(out string error){
            error="Use a cylindrical v1 or rectangular v2 container, normalized bottom frame, radius 0.005–1 m, rectangular width/depth 0.01–2 m, height 0.01–2 m and contents within capacity (1–1000000 ml)";
            if(!RoomFluid.Effective(fluid).Valid||version is not (1 or 2)||IsRectangular&&(rectangle==null||!rectangle.Valid)||version==1&&rectangle!=null&&(rectangle.width!=0||rectangle.depth!=0)||frame?.Valid!=true||!float.IsFinite(radius)||radius<.005f||radius>1||!float.IsFinite(height)||height<.01f||height>2||!double.IsFinite(capacityMl)||capacityMl<1||capacityMl>MaximumMillilitres||!double.IsFinite(amountMl)||amountMl<0||amountMl>capacityMl||!RoomSnapPoint.Identifier(liquid)||liquid.Any(char.IsControl)||!Unit(color.r)||!Unit(color.g)||!Unit(color.b)||color.a!=1)return false;
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
            if(destination.amountMl>0&&!source.SameLiquid(destination)){error="Empty the destination or choose matching liquid identity, colour and fluid properties; mixing is not supported";return false;}
            moved=Math.Min(requested,Math.Min(source.amountMl,destination.capacityMl-destination.amountMl));
            if(moved<=0){error=source.amountMl<=0?"The source is empty":"The destination is full";return false;}
            // Reject a sub-ULP request rather than claiming an amount neither side can represent.
            double left=source.amountMl-moved,right=destination.amountMl+moved;
            if(left==source.amountMl||right==destination.amountMl){moved=0;error="The requested amount is too small at the current quantities";return false;}
            destination.fluid=RoomFluid.Effective(source.fluid).Copy();destination.liquid=source.liquid;destination.color=source.color;source.amountMl=left;destination.amountMl=right;error=null;return true;
        }
    }
}
