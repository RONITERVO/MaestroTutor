// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Cosmos/SpriteField"
{
    Properties
    {
        _Color ("Sprite Color", Color) = (1.0, 0.92, 0.78, 1.0)
        _Intensity ("Intensity", Range(0, 6)) = 1.4
        _Softness ("Airy Disk Softness", Range(0.1, 4.0)) = 1.0
        _SpriteMode ("Mode (0:AiryStar 1:Filament 2:Node 3:HexDyson 4:OrbitalRing)", Float) = 0.0
        _Phase ("Animation Phase", Float) = 0.0
        _Billboard ("Billboard (1:View-Facing 0:Mesh)", Float) = 1.0
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
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Intensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _Softness)
                UNITY_DEFINE_INSTANCED_PROP(float, _SpriteMode)
                UNITY_DEFINE_INSTANCED_PROP(float, _Phase)
                UNITY_DEFINE_INSTANCED_PROP(float, _Billboard)
                UNITY_DEFINE_INSTANCED_PROP(float, _TimeSec)
            UNITY_INSTANCING_BUFFER_END(Props)

            float _VisibilityRealDepth;

            struct Vertex
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varying
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
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

                float billboard = UNITY_ACCESS_INSTANCED_PROP(Props, _Billboard);
                float mode = UNITY_ACCESS_INSTANCED_PROP(Props, _SpriteMode);
                float3 worldPos;

                if (billboard > 0.5)
                {
                    float3 worldOrigin = mul(unity_ObjectToWorld, float4(0.0, 0.0, 0.0, 1.0)).xyz;
                    float3 axisX = mul((float3x3)unity_ObjectToWorld, float3(1.0, 0.0, 0.0));
                    float3 axisY = mul((float3x3)unity_ObjectToWorld, float3(0.0, 1.0, 0.0));
                    float scaleX = length(axisX);
                    float scaleY = length(axisY);

                    float3 camRight = UNITY_MATRIX_V[0].xyz;
                    float3 camUp = UNITY_MATRIX_V[1].xyz;

                    // Cylindrical axis-aligned billboard for elongated filament streamlines (mode 1).
                    if (mode > 0.5 && mode < 1.5 && scaleY > scaleX * 1.15 && scaleY > 1e-5)
                    {
                        float3 toCam = normalize(_WorldSpaceCameraPos - worldOrigin);
                        float3 filUp = axisY / scaleY;
                        float3 filRight = cross(filUp, toCam);
                        float rLen = length(filRight);
                        camUp = filUp;
                        camRight = (rLen > 1e-4) ? (filRight / rLen) : camRight;
                    }

                    worldPos = worldOrigin + camRight * (input.vertex.x * scaleX) + camUp * (input.vertex.y * scaleY);
                    output.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                }
                else
                {
                    worldPos = mul(unity_ObjectToWorld, input.vertex).xyz;
                    output.pos = UnityObjectToClipPos(input.vertex);
                }

                output.uv = input.uv;
                output.worldPos = worldPos;
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

                float2 q = input.uv * 2.0 - 1.0;
                float r = length(q);
                if (r >= 1.0)
                {
                    discard;
                }

                float4 spriteColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float intensity = UNITY_ACCESS_INSTANCED_PROP(Props, _Intensity);
                float softness = max(UNITY_ACCESS_INSTANCED_PROP(Props, _Softness), 0.1);
                float mode = UNITY_ACCESS_INSTANCED_PROP(Props, _SpriteMode);
                float phase = UNITY_ACCESS_INSTANCED_PROP(Props, _Phase);
                float t = CosmosResolveTime(UNITY_ACCESS_INSTANCED_PROP(Props, _TimeSec));

                float3 tint = spriteColor.rgb * input.color.rgb;
                float signal = 0.0;

                if (mode < 0.5)
                {
                    // Mode 0: Crisp Airy-disk pinpoint star with delicate diffraction cross.
                    float core = exp(-r * r * (22.0 / softness));
                    float halo = exp(-r * (7.0 / softness)) * 0.14;
                    float spikeX = exp(-abs(q.y) * 32.0) * exp(-abs(q.x) * 4.5);
                    float spikeY = exp(-abs(q.x) * 32.0) * exp(-abs(q.y) * 4.5);
                    float twinkle = 0.90 + 0.10 * sin(phase * COSMOS_TAU + t * 2.1);
                    signal = (core * 1.35 + halo + (spikeX + spikeY) * 0.20) * (1.0 - smoothstep(0.56, 0.98, r)) * twinkle;
                }
                else if (mode < 1.5)
                {
                    // Mode 1: Volumetric Filament / Flow Streamline.
                    float pulse = 0.78 + 0.22 * sin(phase * COSMOS_TAU + t * 2.4);
                    signal = exp(-q.x * q.x * (10.5 / softness)) * exp(-q.y * q.y * 4.2) * (1.0 - smoothstep(0.52, 0.98, r)) * 0.78 * pulse;
                }
                else if (mode < 2.5)
                {
                    // Mode 2: Spectral Node / Beacon Gem.
                    float core = exp(-r * r * (56.0 / softness));
                    float crossArm = exp(-abs(q.x) * 26.0) * exp(-abs(q.y) * 8.5) + exp(-abs(q.y) * 26.0) * exp(-abs(q.x) * 8.5);
                    float3 spec = CosmosIridescence(r, phase + t * 0.04);
                    tint = lerp(tint, spec * 1.2, 0.35);
                    signal = (core * 1.40 + crossArm * 0.34) * (1.0 - smoothstep(0.55, 1.0, r));
                }
                else if (mode < 3.5)
                {
                    // Mode 3: Faceted Hexagonal Dyson Mirror Glint.
                    float2 pHex = abs(q);
                    float hexD = max(pHex.x * 0.8660254 + pHex.y * 0.5, pHex.y);
                    if (hexD > 0.78)
                    {
                        discard;
                    }
                    float rimD = abs(hexD - 0.70) * 22.0;
                    float rim = exp(-(rimD * rimD));
                    float glintCos = max(0.0, cos(phase * COSMOS_TAU + t * 1.5));
                    float glintSq = glintCos * glintCos;
                    float glint = 0.28 + 0.92 * (glintSq * glintSq);
                    signal = (rim * 1.25 + 0.16) * glint * (1.0 - smoothstep(0.66, 0.78, hexD));
                }
                else
                {
                    // Mode 4: Crisp Hairline Orbital / Radar Ring.
                    float ringD = (r - 0.86) * (32.0 / softness);
                    float ring = exp(-(ringD * ringD));
                    signal = ring * 1.25 * (1.0 - smoothstep(0.92, 1.0, r));
                }

                float total = signal * intensity * spriteColor.a * input.color.a;
                if (total <= 1e-4)
                {
                    discard;
                }

                float3 rgb = CosmosSoftAdditive(tint * total);
                float alphaOut = saturate(max(rgb.r, max(rgb.g, rgb.b)));
                return half4(rgb, alphaOut);
            }
            ENDCG
        }
    }
}
