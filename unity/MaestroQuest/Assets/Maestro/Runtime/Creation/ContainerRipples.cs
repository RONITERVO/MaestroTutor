// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class ContainerFillView {
        internal const int MaximumRipples=8;
        struct Ripple {internal Vector3 Local;internal float Began,Strength;internal bool Active;}
        readonly Ripple[] ripples=new Ripple[MaximumRipples];
        readonly Vector4[] rippleData=new Vector4[MaximumRipples],rippleWeights=new Vector4[MaximumRipples];
        MaterialPropertyBlock rippleProperties;int nextRipple;
        internal int RippleCount {get;private set;}
        internal void AddRipple(Vector3 point,float speed){
            if(!surface||!surface.activeSelf||!RoomRecipe.Finite(point)||!float.IsFinite(speed))return;
            ripples[nextRipple]=new(){Local=surface.transform.InverseTransformPoint(point),Began=Time.unscaledTime,Strength=Mathf.Clamp(.25f+speed*.3f,.25f,1),Active=true};
            nextRipple=(nextRipple+1)%MaximumRipples;UpdateRipples();
        }
        internal void ClearRipples(){for(int i=0;i<ripples.Length;i++)ripples[i].Active=false;RippleCount=0;PublishRipples();}
        internal void UpdateRipples(){
            if(!renderer||!surface)return;int count=0;
            for(int i=0;i<ripples.Length;i++){
                var r=ripples[i];if(!r.Active)continue;float age=Time.unscaledTime-r.Began;
                if(age<0||age>=1){ripples[i].Active=false;continue;}
                var point=surface.transform.TransformPoint(r.Local);
                // Keep the wave on the same clipped level mesh as quantities change.
                if(vertices.Count>0&&normals.Count>0){var normal=surface.transform.TransformDirection(normals[0]).normalized;point-=normal*Vector3.Dot(point-surface.transform.TransformPoint(vertices[0]),normal);}
                rippleData[count]=new Vector4(point.x,point.y,point.z,.015f+age*.45f);
                rippleWeights[count]=new Vector4(r.Strength*(1-age),0,0,0);count++;
            }
            if(count==0&&RippleCount==0)return;RippleCount=count;PublishRipples();
        }
        void PublishRipples(){
            if(!renderer)return;rippleProperties??=new MaterialPropertyBlock();renderer.GetPropertyBlock(rippleProperties);
            rippleProperties.SetFloat("_LiquidRippleCount",RippleCount);rippleProperties.SetVectorArray("_LiquidRipples",rippleData);rippleProperties.SetVectorArray("_LiquidRippleWeights",rippleWeights);renderer.SetPropertyBlock(rippleProperties);
        }
        void OnDisable()=>ClearRipples();
    }
}
