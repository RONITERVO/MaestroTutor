// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Cosmos/FarShellSky"
{
    Properties
    {
        _ScaleU ("Scale Exponent u", Float) = 6.6
        _TimeSec ("Time (Seconds)", Float) = 0.0
        _StarIntensity ("Star Intensity", Range(0, 5)) = 1.6
        _MilkyWayGain ("Milky Way Gain", Range(0, 3)) = 0.85
        _AccentColor ("Accent Color", Color) = (0.28, 0.62, 1.0, 1.0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" "IgnoreProjector"="True" "PreviewType"="Skybox" }
        Cull Front
        ZWrite Off
        ZTest LEqual
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "CosmosCommon.cginc"

            float _ScaleU;
            float _TimeSec;
            float _StarIntensity;
            float _MilkyWayGain;
            float4 _AccentColor;

            struct Vertex
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varying
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varying vert(Vertex input)
            {
                Varying output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.pos = UnityObjectToClipPos(input.vertex);
                output.dir = input.vertex.xyz;
                return output;
            }

            float3 EvalStarLayer(float3 dir, float fwDir, float cellScale, float threshold, float brightness, float timeSec)
            {
                float3 p = dir * cellScale;
                float3 cell = floor(p);
                float3 f = frac(p) - 0.5;
                float3 h = CosmosHash33(cell);
                if (h.x < threshold)
                {
                    return float3(0.0, 0.0, 0.0);
                }

                float3 jitter = (h - 0.5) * 0.56;
                float d = length(f - jitter);

                // Anti-shimmer fwidth footprint integration prevents VR stereo star flicker on head rotation.
                float pxWidth = max(fwDir * cellScale, 1e-4);
                float sigma = max(0.075, pxWidth * 0.68);
                float energyNorm = saturate(0.075 / sigma);
                energyNorm = energyNorm * sqrt(energyNorm);

                float core = exp(-(d * d) / (2.0 * sigma * sigma)) * (1.0 - smoothstep(0.14, 0.46, d));
                if (core < 1e-4)
                {
                    return float3(0.0, 0.0, 0.0);
                }

                float kelvin = lerp(3000.0, 22000.0, CosmosPowSafe(h.y, 1.6));
                float twinkle = 0.88 + 0.12 * sin(timeSec * (1.1 + h.z * 2.4) + h.y * COSMOS_TAU);
                return CosmosBlackbody(kelvin) * core * energyNorm * brightness * twinkle;
            }

            fixed4 frag(Varying input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 dir = normalize(input.dir);
                float fwDir = length(fwidth(dir));
                float t = CosmosResolveTime(_TimeSec);

                // Smooth scale-regime weights derived from logarithmic scale exponent _ScaleU (-35 .. +28.5).
                float mwWeight = smoothstep(-14.0, -8.0, _ScaleU) * (1.0 - smoothstep(21.5, 25.0, _ScaleU));
                float webWeight = smoothstep(20.5, 22.8, _ScaleU) * (1.0 - smoothstep(26.0, 27.0, _ScaleU));
                float cmbWeight = smoothstep(25.4, 26.2, _ScaleU) * (1.0 - smoothstep(26.9, 27.4, _ScaleU));
                float lotusWeight = smoothstep(26.8, 27.4, _ScaleU);
                float quantumWeight = 1.0 - smoothstep(-18.0, -12.0, _ScaleU);

                float3 col = float3(0.0, 0.0, 0.0);

                // 1. Tilted Narrow Milky Way Band + Pinpoint Hash Starfield (>=75% literal #000000 OLED void).
                if (mwWeight > 0.002)
                {
                    float3 gNorth = normalize(float3(0.42, 0.78, -0.46));
                    float lat = abs(dot(dir, gNorth));
                    float bandEnv = exp(-lat * lat * 74.0);

                    if (_MilkyWayGain > 0.002 && bandEnv > 0.002)
                    {
                        float warp = CosmosNoise3(dir * 3.1 + float3(t * 0.001, -0.7, 1.3));
                        float rawNeb = CosmosFbm3(dir * 5.2 + warp * 0.28);
                        float nNeb = CosmosPowSafe(saturate((rawNeb + 0.08) * 1.45), 2.1);
                        float dustLanes = smoothstep(-0.28, 0.42, CosmosNoise3(dir * 7.4 + float3(2.1, -1.4, 0.8)));

                        float3 coolOuter = lerp(float3(0.012, 0.028, 0.078), _AccentColor.rgb * 0.14, 0.35);
                        float3 warmCore = float3(0.16, 0.098, 0.054);
                        float3 bandRgb = lerp(coolOuter, warmCore, nNeb);
                        col += bandRgb * bandEnv * nNeb * (0.28 + 0.72 * dustLanes) * _MilkyWayGain * mwWeight;
                    }

                    if (_StarIntensity > 0.002)
                    {
                        float starGain = _StarIntensity * mwWeight;
                        col += EvalStarLayer(dir, fwDir, 84.0, 0.968, 1.95 * starGain, t);
                        col += EvalStarLayer(dir, fwDir, 168.0, 0.980, 0.95 * starGain, t) * (0.35 + 0.65 * bandEnv);
                    }
                }

                // 2. Deep Cosmic Web Filaments (u ~ 22..26).
                if (webWeight > 0.002)
                {
                    float webN = abs(CosmosFbm3(dir * 3.8));
                    float ridge = CosmosPowSafe(1.0 - smoothstep(0.0, 0.025, webN), 2.4);
                    float node = CosmosPowSafe(smoothstep(0.55, 0.92, CosmosNoise3(dir * 6.5)), 2.2);
                    col += (float3(0.22, 0.38, 0.92) * ridge + _AccentColor.rgb * node) * 0.40 * webWeight;
                }

                // 3. Deep-Planck CMB Horizon (u ~ 26..27) — subdued contrast around sightline.
                if (cmbWeight > 0.002)
                {
                    float cmbSig = CosmosFbm3(dir * 4.5 + float3(1.7, -3.2, 0.8)) - 0.08;
                    float islandMask = smoothstep(0.02, 0.36, cmbSig);
                    float3 planckCol = lerp(float3(0.016, 0.058, 0.21), float3(0.18, 0.10, 0.045), smoothstep(0.14, 0.44, cmbSig));
                    col += planckCol * islandMask * 0.24 * cmbWeight;
                }

                // 4. Outer Horizon Dark-Void Filigree (u > 27).
                if (lotusWeight > 0.002)
                {
                    float az = CosmosAtan2Safe(dir.z, dir.x);
                    float elev = dir.y;
                    float wave = abs(elev) - 0.14 - 0.06 * cos(az * 5.0 + t * 0.015);
                    float ridge = CosmosPowSafe(1.0 - smoothstep(0.0, 0.016, abs(wave)), 2.4);
                    float3 tint = lerp(_AccentColor.rgb, CosmosIridescence(elev, az * COSMOS_INV_PI * 0.5), 0.55);
                    col += tint * ridge * 0.22 * lotusWeight;
                }

                // 5. Sub-Planck Quantum Vacuum Lattice (u < -14).
                if (quantumWeight > 0.002)
                {
                    float gridF = abs(sin(dir.x * 20.0 + t * 0.15) * sin(dir.y * 20.0) * sin(dir.z * 20.0));
                    float edgeE = 1.0 - smoothstep(0.0, 0.045, gridF);
                    float edge = edgeE * edgeE * edgeE;
                    float3 qCol = lerp(_AccentColor.rgb, CosmosIridescence(dir.y, t * 0.04), 0.65);
                    col += qCol * edge * 0.16 * quantumWeight;
                }

                return half4(CosmosAgxOled(col), 1.0);
            }
            ENDCG
        }
    }
}
