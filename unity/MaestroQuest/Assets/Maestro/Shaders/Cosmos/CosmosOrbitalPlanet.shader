// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Cosmos/OrbitalPlanet"
{
    Properties
    {
        _PlanetMode ("Mode (0:Gas 1:Sun 2:Ring 3:Kerr 4:Lotus 5:Rocky 6:Ice 7:Prism)", Float) = 0
        _BaseColor ("Base Color", Color) = (0.88, 0.76, 0.56, 1.0)
        _BandColor ("Band / Accent Color", Color) = (0.58, 0.44, 0.28, 1.0)
        _RingColor ("Ring / Rim Color", Color) = (0.94, 0.84, 0.66, 1.0)
        _SunDir ("Sun Direction", Vector) = (0.72, 0.34, 0.60, 0)
        _GlowGain ("Glow / Emission Gain", Range(0, 6)) = 1.0
        _TimeSec ("Time (Seconds)", Float) = 0.0
        [HideInInspector] _CullMode ("Cull Mode", Float) = 0
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth Write", Float) = 1
        [HideInInspector] _VisibilityRealDepth ("Layer accepts real depth", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull [_CullMode]
        ZWrite [_ZWrite]
        ZTest LEqual
        Blend [_SrcBlend] [_DstBlend]

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
                UNITY_DEFINE_INSTANCED_PROP(float, _PlanetMode)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BandColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RingColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SunDir)
                UNITY_DEFINE_INSTANCED_PROP(float, _GlowGain)
                UNITY_DEFINE_INSTANCED_PROP(float, _TimeSec)
            UNITY_INSTANCING_BUFFER_END(Props)

            float _ZWrite;
            float _VisibilityRealDepth;

            struct Vertex
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varying
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 localPos : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 worldNormal : TEXCOORD3;
                half4 color : COLOR;
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
                output.localPos = input.vertex.xyz;
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

                float mode = UNITY_ACCESS_INSTANCED_PROP(Props, _PlanetMode);
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float4 bandColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BandColor);
                float4 ringColor = UNITY_ACCESS_INSTANCED_PROP(Props, _RingColor);
                float4 sunDirRaw = UNITY_ACCESS_INSTANCED_PROP(Props, _SunDir);
                float glowGain = UNITY_ACCESS_INSTANCED_PROP(Props, _GlowGain);
                float t = CosmosResolveTime(UNITY_ACCESS_INSTANCED_PROP(Props, _TimeSec));

                float3 p = normalize(input.localPos + float3(1e-5, 0.0, 0.0));
                float3 N = normalize(input.worldNormal);
                float3 V = normalize(UnityWorldSpaceViewDir(input.worldPos));
                // Flip normal on back-side views for double-sided discs/rings/petals/prisms.
                if (dot(N, V) < 0.0 && ((mode > 1.5 && mode < 4.5) || mode > 6.5))
                {
                    N = -N;
                }

                float sunLen = length(sunDirRaw.xyz);
                float3 sunDir = (sunLen > 1e-4) ? (sunDirRaw.xyz / sunLen) : float3(0.72, 0.34, 0.60);
                float sunDot = dot(N, sunDir);
                float ndotl = saturate(sunDot);
                float viewDot = saturate(dot(N, V));
                float fresnel = CosmosPowSafe(1.0 - viewDot, 3.4);

                float3 hdr = float3(0.0, 0.0, 0.0);
                float outAlpha = 1.0;

                if (mode < 0.5)
                {
                    // Mode 0: Banded Gas Giant (Saturn / Jupiter / Uranus / Neptune) + polar hexagon / storm accent.
                    float bandWarp = CosmosFbm3(p * 4.5 + float3(t * 0.02, 0.0, 0.0));
                    float bands = sin(p.y * 15.0 + bandWarp * 3.2);
                    float3 surf = lerp(baseColor.rgb, bandColor.rgb, 0.5 + 0.5 * bands);
                    if (p.y > 0.78)
                    {
                        float hexR = length(p.xz) * 2.8;
                        float hexAng = CosmosAtan2Safe(p.z, p.x);
                        float hex = exp(-abs(hexR - 0.58 - 0.08 * cos(6.0 * hexAng)) * 18.0);
                        surf = lerp(surf, ringColor.rgb, hex * 0.55);
                    }
                    float3 rim = ringColor.rgb * fresnel * (0.22 + 0.78 * ndotl) * glowGain;
                    hdr = surf * (ndotl * 1.12 + 0.015) + rim;
                }
                else if (mode < 1.5)
                {
                    // Mode 1: Solar Photosphere (convective granules + sunspot umbrae + chromosphere limb).
                    float gran = CosmosFbm3(p * 12.0 + float3(t * 0.14, 0.0, t * 0.08));
                    float cellLines = abs(CosmosNoise3(p * 24.0 + float3(0.0, t * 0.22, 0.0)));
                    float granLanes = smoothstep(0.04, 0.26, cellLines);
                    float spot = 1.0 - smoothstep(-0.48, -0.30, CosmosFbm3(p * 3.8 + float3(0.0, t * 0.03, 0.0)));
                    float temp = lerp(5200.0, 2850.0, spot) + gran * 950.0 - (1.0 - granLanes) * 650.0;
                    float limb = CosmosPowSafe(viewDot, 0.62);
                    float cellContrast = (0.36 + 0.64 * granLanes) * lerp(1.0, 0.24, spot);
                    float3 chromoRim = lerp(float3(0.98, 0.22, 0.04), ringColor.rgb, 0.35) * fresnel * 0.85;
                    float3 photosphere = lerp(CosmosBlackbody(temp), baseColor.rgb, 0.25) * (0.40 + 0.58 * limb) * cellContrast;
                    hdr = (photosphere + chromoRim) * max(glowGain, 0.2);
                }
                else if (mode < 2.5)
                {
                    // Mode 2: Planetary Ring Disc (planar UV [0,1]^2 centered at 0.5,0.5) w/ Cassini & Encke gaps.
                    float2 q = input.uv * 2.0 - 1.0;
                    float r = length(q);
                    float ringNoise = CosmosNoise3(float3(r * 38.0, q.x * 3.5, q.y * 3.5));
                    float cassiniGap = smoothstep(0.018, 0.042, abs(r - 0.71));
                    float enckeGap = smoothstep(0.007, 0.016, abs(r - 0.88));
                    float fineBands = 0.52 + 0.48 * sin(r * 155.0 + ringNoise * 3.5);
                    float radialEnv = smoothstep(0.42, 0.46, r) * (1.0 - smoothstep(0.93, 0.97, r));
                    outAlpha = radialEnv * cassiniGap * enckeGap * fineBands;
                    if (_ZWrite > 0.5)
                    {
                        clip(outAlpha - 0.05);
                    }
                    else if (outAlpha < 0.01)
                    {
                        discard;
                    }
                    float3 ringTint = lerp(baseColor.rgb, bandColor.rgb, saturate((r - 0.42) / 0.55));
                    hdr = ringTint * (0.65 + 0.45 * fineBands + 0.25 * ringNoise) * max(glowGain, 0.2) + ringColor.rgb * fresnel * 0.25;
                }
                else if (mode < 3.5)
                {
                    // Mode 3: Kerr Black Hole Event Horizon Shadow + Relativistic Doppler Accretion Ring (planar UV [0,1]^2).
                    float2 q = input.uv * 2.0 - 1.0;
                    float r = length(q);
                    if (r > 0.96)
                    {
                        discard;
                    }
                    if (r < 0.22)
                    {
                        // Literal #000000 event horizon shadow occluding background stars.
                        return half4(0.0, 0.0, 0.0, 1.0);
                    }

                    float ang = CosmosAtan2Safe(q.y, q.x);
                    float spiral = 0.5 + 0.5 * cos(ang * 3.0 + r * 24.0 - t * 1.6);
                    float fil = CosmosPowSafe(saturate(0.42 + spiral * 0.55 + CosmosNoise3(float3(q * 11.0, r * 8.0 - t * 0.45)) * 0.55), 2.1);
                    float2 tangent2D = normalize(float2(-q.y, q.x) + 1e-5);
                    float doppler = dot(tangent2D, normalize(float2(0.85, 0.52)));
                    float beam = CosmosPowSafe(clamp(1.0 + doppler * 0.65, 0.24, 1.85), 2.2);
                    float radialEnv = smoothstep(0.22, 0.29, r) * (1.0 - smoothstep(0.58, 0.95, r));
                    float prDist = (r - 0.255) * 32.0;
                    float photonRing = exp(-(prDist * prDist));

                    float temp = lerp(2200.0, 9200.0, saturate((0.72 - r) * 1.8 + doppler * 0.35));
                    float3 diskCol = lerp(CosmosBlackbody(temp), baseColor.rgb, 0.22) * beam * fil * radialEnv * 1.45;
                    float3 ringGlow = lerp(float3(2.2, 1.35, 0.62), ringColor.rgb, 0.35) * photonRing;
                    hdr = (diskCol + ringGlow) * max(glowGain, 0.3);
                    outAlpha = saturate(radialEnv * fil + photonRing);
                    if (_ZWrite > 0.5)
                    {
                        clip(outAlpha - 0.04);
                    }
                }
                else if (mode < 4.5)
                {
                    // Mode 4: Final Horizon Iridescent Lotus Petal / Manifold.
                    float3 irid = CosmosIridescence(viewDot, input.uv.x * 0.8 + t * 0.06);
                    float veins = exp(-abs(sin(input.uv.x * COSMOS_PI * 5.0)) * 16.0) * smoothstep(0.0, 0.2, input.uv.y);
                    float petalEdge = smoothstep(0.0, 0.12, input.uv.x) * (1.0 - smoothstep(0.88, 1.0, input.uv.x)) * smoothstep(0.0, 0.08, input.uv.y);
                    float3 petalTint = lerp(baseColor.rgb, irid, 0.65);
                    hdr = (petalTint * (0.28 + 0.55 * ndotl) + ringColor.rgb * (veins * 0.85 + fresnel * 1.25)) * max(glowGain, 0.3);
                    outAlpha = saturate((0.35 + 0.65 * fresnel + veins * 0.4) * petalEdge);
                }
                else if (mode < 5.5)
                {
                    // Mode 5: Rocky / Cratered World (Mercury, Mars, Pluto, Asteroid).
                    float crater = abs(CosmosNoise3(p * 9.5 + 1.2));
                    float rimMask = exp(-abs(crater - 0.22) * 26.0);
                    float regolith = CosmosFbm3(p * 5.2);
                    float polarIce = smoothstep(0.84, 0.94, abs(p.y) + regolith * 0.06);
                    float3 rock = lerp(baseColor.rgb, bandColor.rgb, saturate(0.5 + regolith * 0.55 + rimMask * 0.4));
                    rock = lerp(rock, ringColor.rgb, polarIce * 0.65);
                    hdr = rock * (ndotl * 1.15 + 0.018) + ringColor.rgb * fresnel * 0.22 * (0.2 + 0.8 * ndotl);
                }
                else if (mode < 6.5)
                {
                    // Mode 6: Icy Fractured Ocean World (Europa / Enceladus / Kuiper).
                    float crack = abs(CosmosFbm3(p * 7.2 + float3(1.9, -0.7, 2.3)));
                    float fissure = exp(-crack * 20.0);
                    float3 ice = lerp(baseColor.rgb, bandColor.rgb, fissure * 0.45);
                    float3 subsurface = ringColor.rgb * fissure * 1.15 * max(glowGain, 0.3);
                    hdr = ice * (ndotl * 1.08 + 0.022) + subsurface + ringColor.rgb * fresnel * 0.32;
                }
                else
                {
                    // Mode 7: Unlit Vertex-RGB Crystal Prism w/ Fresnel Edge Glow (e.g. 3D HSL Bicone Prism).
                    float3 prismRgb = CosmosGammaToLinear(input.color.rgb);
                    float3 edgeTint = lerp(prismRgb, float3(1.0, 1.0, 1.0), 0.35);
                    float facetGrid = 0.92 + 0.08 * sin(p.y * 28.0);
                    hdr = (prismRgb * facetGrid + edgeTint * fresnel * 0.90) * max(glowGain, 0.25);
                    outAlpha = saturate(input.color.a);
                }

                return half4(CosmosAgxOled(hdr), outAlpha);
            }
            ENDCG
        }
    }
}
