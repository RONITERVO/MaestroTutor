// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>Editable 2.5D surface: one height per grid vertex, no fluid/granular solver.</summary>
    [Serializable] public sealed class RoomHeightField {
        public const int MaximumPerRoom=4,MaximumSamples=289;
        public int version=1,cells=16;
        public ConnectionFrame frame=new();
        public float width=1,depth=1,maxHeight=.25f;
        public string material="Snow";
        public Color color=new(.94f,.97f,1,1);
        // Row-major: x increases first, then z. Values are metres along frame +Y.
        public float[] heights=Enumerable.Repeat(.08f,MaximumSamples).ToArray();
        public RoomHeightField Copy()=>new(){version=version,cells=cells,frame=frame?.Copy(),width=width,depth=depth,maxHeight=maxHeight,material=material,color=color,heights=heights?.ToArray()};
        public bool Validate(out string error){
            error="Use a version-1 height field with 4, 8 or 16 cells per side, a normalized frame, width/depth 0.1–4 m, maximum height 0.005–0.5 m and one bounded height per grid vertex";
            if(version!=1||cells!=4&&cells!=8&&cells!=16||frame?.Valid!=true||!Bound(width,.1f,4)||!Bound(depth,.1f,4)||!Bound(maxHeight,.005f,.5f)||!RoomSnapPoint.Identifier(material)||material.Any(char.IsControl)||!Bound(color.r,0,1)||!Bound(color.g,0,1)||!Bound(color.b,0,1)||color.a!=1||heights==null||heights.Length!=(cells+1)*(cells+1)||heights.Any(h=>!Bound(h,0,maxHeight)))return false;
            error=null;return true;
        }
        static bool Bound(float v,float lo,float hi)=>float.IsFinite(v)&&v>=lo&&v<=hi;
        public static bool ValidateCollection(RoomObjectData owner,out string error){
            var fields=owner.heightFields??Array.Empty<RoomHeightField>();error="A created object can have one height field and must use fixed physics while it is configured";
            if(fields.Length>1||fields.Any(f=>f==null)||fields.Length>0&&(owner.IsBuiltIn||owner.physics!=ItemPhysics.Fixed))return false;
            if(fields.Length==1)return fields[0].Validate(out error);error=null;return true;
        }
        internal Vector3 Vertex(int x,int z)=>new((float)x/cells*width-width*.5f,heights[z*(cells+1)+x],(float)z/cells*depth-depth*.5f);
        internal float HeightAt(Vector2 point){
            float x=Mathf.Clamp((point.x/width+.5f)*cells,0,cells),z=Mathf.Clamp((point.y/depth+.5f)*cells,0,cells);
            int ix=Mathf.Min((int)x,cells-1),iz=Mathf.Min((int)z,cells-1),a=iz*(cells+1)+ix;float u=x-ix,v=z-iz;
            return u+v<=1?heights[a]*(1-u-v)+heights[a+1]*u+heights[a+cells+1]*v:heights[a+1]*(1-v)+heights[a+cells+1]*(1-u)+heights[a+cells+2]*(u+v-1);
        }
        // Integrate the same two triangles per cell as the visual/collision mesh.
        // This is authored local volume, not physical mass or conserved simulated snow.
        internal double VolumeLitres {get{double sum=0;for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){int a=z*(cells+1)+x,b=a+1,c=a+cells+1,d=c+1;sum+=heights[a]+2*heights[b]+2*heights[c]+heights[d];}return sum*width*depth/(6.0*cells*cells)*1000;}}
        internal bool Sculpt(string mode,Vector2[] path,float radius,float height,out int changed,out string error){
            changed=0;error="Choose raise, lower or level; one to 32 in-bounds local X/Z points, radius 0.005–2 m and height 0–0.5 m";
            if(!Validate(out _)||mode!="raise"&&mode!="lower"&&mode!="level"||path==null||path.Length<1||path.Length>32||!Bound(radius,.005f,2)||!Bound(height,0,.5f)||path.Any(p=>!float.IsFinite(p.sqrMagnitude)||Mathf.Abs(p.x)>width*.5f||Mathf.Abs(p.y)>depth*.5f))return false;
            for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++){
                var vertex=Vertex(x,z);var p=new Vector2(vertex.x,vertex.z);float distance=(p-path[0]).magnitude;
                for(int i=1;i<path.Length;i++){var d=path[i]-path[i-1];float t=d.sqrMagnitude>1e-12f?Mathf.Clamp01(Vector2.Dot(p-path[i-1],d)/d.sqrMagnitude):0;distance=Mathf.Min(distance,(p-(path[i-1]+t*d)).magnitude);}
                // Union of swept disks: repeated path points never accumulate extra edits.
                float weight=Mathf.Clamp01(1-distance/radius);weight=weight*weight*(3-2*weight);int index=z*(cells+1)+x;float old=heights[index];
                float next=mode=="level"?Mathf.Lerp(old,Mathf.Min(height,maxHeight),weight):Mathf.Clamp(old+(mode=="raise"?height:-height)*weight,0,maxHeight);
                if(next!=old){heights[index]=next;changed++;}
            }
            if(changed==0){error="The brush did not change any grid vertices; enlarge its radius or choose a different height/path";return false;}error=null;return true;
        }
    }
}
