// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Maestro/PassthroughWindow"
{
    Properties { _Reveal ("Reveal physical surroundings", Range(0,1)) = 1 _Ellipse ("Ellipse mask", Float) = 0 }
    SubShader {
        // Share the transparent sort with translucent world surfaces: a nearer
        // transparent prop must composite AFTER the opening, not be erased by it.
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass {
            Cull Off ZWrite Off ZTest LEqual
            BlendOp Add
            Blend Zero SrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "UnityCG.cginc"
            struct Vertex { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            float _Reveal,_Ellipse;
            Varying vert(Vertex input) {
                Varying output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position=UnityObjectToClipPos(input.vertex);output.uv=input.uv;return output;
            }
            fixed4 frag(Varying input):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float mask=1;
                if(_Ellipse>.5){float d=length(input.uv*2-1);mask=1-smoothstep(1-max(fwidth(d),.0001),1,d);}
                // Premultiplied framebuffer RGB and alpha are attenuated together.
                // The underlying Meta passthrough layer supplies the revealed image.
                return float4(0,0,0,1-saturate(_Reveal)*mask);
            }
            ENDCG
        }
    }
}
