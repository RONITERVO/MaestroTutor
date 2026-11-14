// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Cosmos/EarthHero"
{
    Properties
    {
        _SunDir ("Sun Direction", Vector) = (0.72, 0.34, 0.60, 0)
        _CloudTime ("Cloud Animation Time", Float) = 0.0
        _CityGlow ("Night City Lights Glow", Range(0, 4)) = 1.6
        _AtmosColor ("Atmosphere Rayleigh Color", Color) = (0.22, 0.56, 1.0, 1.0)
        _FocusGlow ("Focus Dwell Glow", Range(0, 2)) = 0.0
        [HideInInspector] _VisibilityRealDepth ("Layer accepts real depth", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Back
        ZWrite On
        ZTest LEqual
        Blend Off

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

            float4 _SunDir;
            float _CloudTime;
            float _CityGlow;
            float4 _AtmosColor;
            float _FocusGlow;
            float _VisibilityRealDepth;

            struct Vertex
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varying
            {
                float4 pos : SV_POSITION;
                float3 localDir : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varying vert(Vertex input)
            {
                Varying output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.pos = UnityObjectToClipPos(input.vertex);
                output.localDir = normalize(input.vertex.xyz);
                output.worldPos = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                return output;
            }

            fixed4 frag(Varying input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_VisibilityRealDepth > 0.5)
                {
                    MaestroOccludeEnvironment(input.worldPos);
                }

                float3 p = normalize(input.localDir);
                float3 N = normalize(input.worldNormal);
                float3 V = normalize(UnityWorldSpaceViewDir(input.worldPos));
                float sunLen = length(_SunDir.xyz);
                float3 sunDir = (sunLen > 1e-4) ? (_SunDir.xyz / sunLen) : float3(0.72, 0.34, 0.60);

                float sunDot = dot(N, sunDir);
                float ndotl = saturate(sunDot);
                float viewDot = saturate(dot(N, V));
                float fresnel = CosmosPowSafe(1.0 - viewDot, 3.6);

                // Procedural continents, bathymetry trenches, mountains & polar ice caps.
                float cont = CosmosFbm4(p * 2.35 + float3(0.4, -0.2, 0.9));
                float coast = smoothstep(0.02, 0.08, cont);
                float mtnBase = saturate(CosmosNoise3(p * 10.5));
                float mtn = (mtnBase * mtnBase) * smoothstep(0.04, 0.26, cont);
                float polarIce = smoothstep(0.76, 0.92, abs(p.y) + cont * 0.12);

                float3 oceanCol = lerp(float3(0.010, 0.045, 0.16), float3(0.022, 0.145, 0.34), smoothstep(-0.32, 0.01, cont));
                float3 landBase = lerp(float3(0.042, 0.175, 0.056), float3(0.24, 0.18, 0.10), smoothstep(0.06, 0.36, cont));
                float3 albedo = lerp(oceanCol, landBase + float3(0.16, 0.13, 0.09) * mtn, coast);
                albedo = lerp(albedo, float3(0.84, 0.89, 0.96), polarIce);

                // Animated tropospheric cloud wisps.
                float ct = CosmosResolveTime(_CloudTime);
                float3 cloudCoord = p * 4.2 + float3(ct * 0.020, sin(p.z * 3.0) * 0.05, -ct * 0.014);
                float cloudN = CosmosFbm3(cloudCoord);
                float clouds = smoothstep(0.12, 0.45, cloudN);

                // Ocean solar specular highlight.
                float3 H = normalize(sunDir + V);
                float oceanSpec = CosmosPowSafe(saturate(dot(N, H)), 64.0) * (1.0 - coast) * (1.0 - clouds) * saturate(sunDot * 4.0) * 1.35;

                // Night-side golden city clusters cleanly masked by -dot(N, _SunDir).
                float nightMask = smoothstep(-0.08, 0.22, -sunDot);
                float citySpark = CosmosPowSafe(saturate(CosmosNoise3(p * 20.0) * 0.72 + CosmosNoise3(p * 42.0) * 0.48), 5.0);
                citySpark *= coast * (1.0 - polarIce) * (1.0 - clouds * 0.65);
                float3 cityCol = float3(1.85, 1.08, 0.34) * citySpark * nightMask * _CityGlow;

                // Rayleigh blue atmospheric limb Fresnel.
                float dayTerminator = smoothstep(-0.25, 0.45, sunDot);
                float3 rayleigh = _AtmosColor.rgb * fresnel * (0.28 + 1.38 * dayTerminator + _FocusGlow * 0.65);

                float3 daySurface = lerp(albedo, float3(0.90, 0.93, 0.98), clouds) * (ndotl + 0.012) + oceanSpec.xxx;
                float3 hdr = daySurface + cityCol + rayleigh;
                return half4(CosmosAgxOled(hdr), 1.0);
            }
            ENDCG
        }
    }
}
