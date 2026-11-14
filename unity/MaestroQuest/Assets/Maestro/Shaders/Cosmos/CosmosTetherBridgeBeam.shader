// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Cosmos/TetherBridgeBeam"
{
    Properties
    {
        _Color ("Beam Color", Color) = (0.28, 0.78, 1.35, 1.0)
        _PulseSpeed ("Pulse Speed", Float) = 1.15
        _ActiveFocus ("Active Focus", Range(0, 3)) = 1.0
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
                UNITY_DEFINE_INSTANCED_PROP(float, _PulseSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _ActiveFocus)
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
                output.pos = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.worldPos = mul(unity_ObjectToWorld, input.vertex).xyz;
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

                float4 beamColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float pulseSpeed = UNITY_ACCESS_INSTANCED_PROP(Props, _PulseSpeed);
                float activeFocus = UNITY_ACCESS_INSTANCED_PROP(Props, _ActiveFocus);
                float t = CosmosResolveTime(UNITY_ACCESS_INSTANCED_PROP(Props, _TimeSec));

                float across = abs(input.uv.y * 2.0 - 1.0);
                float core = exp(-across * across * 5.8) * (1.0 - smoothstep(0.82, 1.0, across));
                float endFade = smoothstep(0.0, 0.06, input.uv.x) * (1.0 - smoothstep(0.92, 1.0, input.uv.x));

                // Travelling luminous pulse along UV.x.
                float travel = frac(input.uv.x * 2.2 - t * pulseSpeed);
                float packetD = (travel - 0.5) / 0.16;
                float packet = exp(-(packetD * packetD));
                float ripple = 0.88 + 0.12 * sin(input.uv.x * 24.0 - t * pulseSpeed * 3.0);
                float pulse = (0.42 + 0.95 * packet) * ripple;

                float energy = core * endFade * pulse * (0.55 + 0.65 * activeFocus) * beamColor.a * input.color.a;
                if (energy <= 1e-4)
                {
                    discard;
                }

                float3 rgb = CosmosSoftAdditive(beamColor.rgb * input.color.rgb * energy);
                float alphaOut = saturate(max(rgb.r, max(rgb.g, rgb.b)));
                return half4(rgb, alphaOut);
            }
            ENDCG
        }
    }
}
