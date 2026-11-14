// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Cosmos/AuroralCurtain"
{
    Properties
    {
        _ColorPrimary ("Primary Emerald Color", Color) = (0.08, 1.85, 0.58, 1.0)
        _ColorSecondary ("Secondary Magenta Color", Color) = (0.95, 0.18, 1.85, 1.0)
        _CurtainSpeed ("Curtain Speed", Float) = 0.65
        _Intensity ("Intensity", Range(0, 5)) = 1.4
        _TimeSec ("Time (Seconds)", Float) = 0.0
        [HideInInspector] _VisibilityRealDepth ("Layer accepts real depth", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One One

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
                UNITY_DEFINE_INSTANCED_PROP(float4, _ColorPrimary)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ColorSecondary)
                UNITY_DEFINE_INSTANCED_PROP(float, _CurtainSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _Intensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _TimeSec)
            UNITY_INSTANCING_BUFFER_END(Props)

            float _VisibilityRealDepth;

            struct Vertex
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varying
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
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
                output.uv = input.uv;
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

                float4 colPrimary = UNITY_ACCESS_INSTANCED_PROP(Props, _ColorPrimary);
                float4 colSecondary = UNITY_ACCESS_INSTANCED_PROP(Props, _ColorSecondary);
                float speed = UNITY_ACCESS_INSTANCED_PROP(Props, _CurtainSpeed);
                float intensity = UNITY_ACCESS_INSTANCED_PROP(Props, _Intensity);
                float t = CosmosResolveTime(UNITY_ACCESS_INSTANCED_PROP(Props, _TimeSec)) * speed;

                // Seamless 3-harmonic polar auroral wave folds along uv.x (integer periods for closed rings).
                float phi = input.uv.x * COSMOS_TAU;
                float h1 = sin(phi * 6.0 + t * 1.15);
                float h2 = sin(phi * 13.0 - t * 1.65 + h1 * 1.25);
                float h3 = sin(phi * 29.0 + t * 2.35 + h2 * 0.85);
                float wave = CosmosPowSafe(saturate(0.50 + 0.30 * h1 + 0.14 * h2 + 0.10 * h3), 2.3);

                // Magnetic field-aligned vertical striations & crisp lower ionization arc.
                float strBase = 0.5 + 0.5 * sin(phi * 54.0 + h1 * 2.6 + t * 0.9);
                float striations = 0.52 + 0.48 * (strBase * strBase * strBase);
                float h = saturate(input.uv.y);
                float altEnv = smoothstep(0.0, 0.13, h) * exp(-h * 2.35) * (1.0 - smoothstep(0.72, 1.0, h));

                // Smooth view-angle grazing fold amplification without hard silhouette artifacts.
                float3 N = normalize(input.worldNormal);
                float3 V = normalize(UnityWorldSpaceViewDir(input.worldPos));
                float foldBias = 0.38 + 0.62 * (1.0 - CosmosPowSafe(abs(dot(N, V)), 1.6) * 0.45);

                float3 altTint = lerp(colPrimary.rgb, colSecondary.rgb, smoothstep(0.26, 0.84, h));
                float signal = wave * striations * altEnv * foldBias * intensity * input.color.a;
                if (signal <= 1e-4)
                {
                    discard;
                }

                float3 rgb = CosmosSoftAdditive(altTint * input.color.rgb * signal);
                float alphaOut = saturate(max(rgb.r, max(rgb.g, rgb.b)));
                return half4(rgb, alphaOut);
            }
            ENDCG
        }
    }
}
