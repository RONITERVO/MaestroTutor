// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
namespace Maestro.Quest.Art
{
    /// <summary>A material adapter may mutate its source in place. Variants are
    /// keyed by its full illustrated value, not just the Material reference.</summary>
    internal static class IllustratedMaterialKey
    {
        internal static StringBuilder Source(Material source) {
            var b=new StringBuilder().Append(source.GetInstanceID()).Append(':').Append(source.renderQueue).Append(':').Append(source.GetShaderPassEnabled("PENCIL"));
            // Source adapters can change colour, maps or render state. Capture all
            // supported shader values; a mutable Material reference alone is not a key.
            var shader=source.shader;
            for(int i=0;i<shader.GetPropertyCount();i++) {
                string name=shader.GetPropertyName(i);b.Append('|');
                switch(shader.GetPropertyType(i)) {
                    case ShaderPropertyType.Texture:
                        b.Append(source.GetTexture(name)?.GetInstanceID()??0);var scale=source.GetTextureScale(name);var offset=source.GetTextureOffset(name);Vector(b,new Vector4(scale.x,scale.y,offset.x,offset.y));break;
                    case ShaderPropertyType.Color: var color=source.GetColor(name);Vector(b,new Vector4(color.r,color.g,color.b,color.a));break;
                    case ShaderPropertyType.Vector:Vector(b,source.GetVector(name));break;
                    case ShaderPropertyType.Int:b.Append(source.GetInteger(name));break;
                    default:b.Append(source.GetFloat(name).ToString("R",CultureInfo.InvariantCulture));break;
                }
            }
            return b.Append('|').Append(source.GetTag("RenderType",false,""));
        }
        internal static void Vector(StringBuilder b,Vector4 v){for(int i=0;i<4;i++)b.Append(',').Append(v[i].ToString("R",CultureInfo.InvariantCulture));}
    }
}
