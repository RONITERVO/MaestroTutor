// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Cosmos/WristBead"
{
    Properties
    {
        _Color ("Bead / Ring Color", Color) = (0.12, 0.16, 0.24, 0.94)
        _ActiveGlow ("Active / Hover Glow", Range(0, 4)) = 0.25
        _RimColor ("Pearl Iridescence Rim", Color) = (0.42, 0.86, 1.35, 1.0)
        [HideInInspector] _VisibilityRealDepth ("Layer accepts real depth", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Back
        ZWrite On
        ZTest LEqual
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
            #include "../../Runtime/Art/MaestroEnvironmentDepth.cginc"
            #include "CosmosCommon.cginc"

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _ActiveGlow)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RimColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            float _VisibilityRealDepth;

            struct Vertex
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varying
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varying vert(Vertex input)
            {
                Varying output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.pos = UnityObjectToClipPos(input.vertex);
                output.worldPos = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.color = input.color;
                return output;
            }

            fixed4 frag(Varying input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_VisibilityRealDepth > 0.5)
                {
                    MaestroOccludeEnvironment(input.worldPos);
                }

                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color) * input.color;
                float activeGlow = max(UNITY_ACCESS_INSTANCED_PROP(Props, _ActiveGlow), 0.0);
                float4 rimColor = UNITY_ACCESS_INSTANCED_PROP(Props, _RimColor);

                if (baseColor.a <= 0.002)
                {
                    discard;
                }

                float3 N = normalize(input.worldNormal);
                float3 V = normalize(UnityWorldSpaceViewDir(input.worldPos));
                float3 L = normalize(float3(0.4, 0.75, 0.5));
                float3 H = normalize(L + V);

                float ndotl = saturate(dot(N, L));
                float ndotv = saturate(dot(N, V));
                float fresnel = CosmosPowSafe(1.0 - ndotv, 2.4);
                float spec = CosmosPowSafe(saturate(dot(N, H)), 48.0);
                float3 irid = CosmosIridescence(ndotv, 0.20) * 0.25;

                float3 pearlCore = baseColor.rgb * (0.45 + 0.55 * ndotl + activeGlow * 0.55);
                float3 pearlRim = (baseColor.rgb + rimColor.rgb + irid) * fresnel * (0.65 + activeGlow * 0.90);
                float3 hdr = pearlCore + pearlRim + spec * (1.25 + activeGlow * 0.8);

                return half4(CosmosAgxOled(hdr), saturate(baseColor.a));
            }
            ENDCG
        }
    }
}
