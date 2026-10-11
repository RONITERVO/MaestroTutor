// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Maestro/WorldText"
{
    Properties { _MainTex ("Font atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Lighting Off Cull Off ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "UnityCG.cginc"
            #include "../Runtime/Art/MaestroEnvironmentDepth.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float3 world : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.world = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.vertex = UnityObjectToClipPos(input.vertex); output.uv = input.uv; output.color = input.color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                MaestroOccludeEnvironment(input.world);
                return fixed4(input.color.rgb,input.color.a * tex2D(_MainTex,input.uv).a);
            }
            ENDCG
        }
    }
}
