// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace Maestro.Quest.Art
{
    /// <summary>Explicit render state, independent of physics, acoustics and passthrough.
    /// Immutable values can participate in shared appearance-material cache keys.</summary>
    public readonly struct IllustratedSurface:IEquatable<IllustratedSurface>
    {
        public readonly string Mode;
        public readonly float Opacity,Cutoff;
        public readonly bool DoubleSided;
        IllustratedSurface(string mode,float opacity,float cutoff,bool doubleSided){Mode=mode;Opacity=opacity;Cutoff=cutoff;DoubleSided=doubleSided;}
        public static bool TryCreate(string mode,float opacity,float cutoff,bool doubleSided,out IllustratedSurface value,out string error) {
            value=default;error="Choose opaque, cutout or blend with finite opacity/cutoff between zero and one";
            if(mode is not ("opaque" or "cutout" or "blend")||!float.IsFinite(opacity)||opacity<0||opacity>1||!float.IsFinite(cutoff)||cutoff<0||cutoff>1)return false;
            if(mode=="opaque"&&opacity!=1){error="Use blended rendering for translucent surfaces; opaque opacity must be one";return false;}
            value=new IllustratedSurface(mode,opacity,mode=="cutout"?cutoff:0,doubleSided);error=null;return true;
        }
        public static IllustratedSurface Opaque=>new("opaque",1,0,true);
        public bool Equals(IllustratedSurface other)=>Mode==other.Mode&&Opacity==other.Opacity&&Cutoff==other.Cutoff&&DoubleSided==other.DoubleSided;
        public override bool Equals(object other)=>other is IllustratedSurface value&&Equals(value);
        public override int GetHashCode()=>HashCode.Combine(Mode,Opacity,Cutoff,DoubleSided);
        public void Apply(Material material,bool pencil) {
            if(!material||material.shader.name!="Maestro/Watercolor"||Mode is not ("opaque" or "cutout" or "blend"))throw new ArgumentException("A valid illustrated surface and watercolor material are required");
            bool blend=Mode=="blend";
            material.SetFloat("_SurfaceMode",blend?2:Mode=="cutout"?1:0);
            material.SetFloat("_SurfaceOpacity",Opacity);
            material.SetFloat("_AlphaCutoff",Cutoff);
            material.SetInt("_SurfaceCull",(int)(DoubleSided?CullMode.Off:CullMode.Back));
            material.SetInt("_SrcBlend",(int)(blend?BlendMode.SrcAlpha:BlendMode.One));
            material.SetInt("_DstBlend",(int)(blend?BlendMode.OneMinusSrcAlpha:BlendMode.Zero));
            material.SetInt("_ZWrite",blend?0:1);
            material.renderQueue=(int)(blend?RenderQueue.Transparent:Mode=="cutout"?RenderQueue.AlphaTest:RenderQueue.Geometry);
            material.SetOverrideTag("RenderType",blend?"Transparent":Mode=="cutout"?"TransparentCutout":"Opaque");
            // A solid back-face outline is misleading on translucent surfaces.
            // Cutouts use the same texture/vertex-alpha threshold in both passes.
            material.SetShaderPassEnabled("PENCIL",pencil&&!blend);
        }
        internal static IllustratedSurface Imported(Material source,Color color) {
            string mode="opaque";
            if(source.HasProperty("_SurfaceMode")) {int v=source.GetInt("_SurfaceMode");mode=v==1?"cutout":v==2?"blend":source.GetFloat("_AlphaCutoff")>0?"cutout":"opaque";}
            else if(source.HasProperty("_BlendMode")) {int v=source.GetInt("_BlendMode");mode=v==1?"cutout":v==2?"blend":"opaque";}
            else if(source.HasProperty("_Mode")) {int v=source.GetInt("_Mode");mode=v==1?"cutout":v>=2?"blend":"opaque";}
            else if(source.IsKeywordEnabled("_ALPHATEST_ON")||source.HasProperty("_AlphaClip")&&source.GetFloat("_AlphaClip")>.5f)mode="cutout";
            else if(source.HasProperty("_Surface")&&source.GetFloat("_Surface")>.5f||source.renderQueue>=3000)mode="blend";
            else if(source.renderQueue>=2450)mode="cutout";
            float cutoff=source.HasProperty("_AlphaCutoff")?source.GetFloat("_AlphaCutoff"):source.HasProperty("_Cutoff")?source.GetFloat("_Cutoff"):.5f;
            float opacity=source.HasProperty("_SurfaceOpacity")?source.GetFloat("_SurfaceOpacity"):color.a;
            int cull=source.HasProperty("_SurfaceCull")?source.GetInt("_SurfaceCull"):source.HasProperty("_CullMode")?source.GetInt("_CullMode"):source.HasProperty("_Cull")?source.GetInt("_Cull"):2;
            string sided=source.GetTag("MaestroDoubleSided",false,"");
            bool doubleSided=sided=="true"||sided!="false"&&cull==0;
            if(!TryCreate(mode,mode=="opaque"?1:opacity,cutoff,doubleSided,out var surface,out var error))throw new ArgumentException(error);
            return surface;
        }
    }
}
