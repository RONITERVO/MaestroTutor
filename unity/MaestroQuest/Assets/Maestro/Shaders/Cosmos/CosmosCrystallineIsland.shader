// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Cosmos/CrystallineIsland"
{
    Properties
    {
        _WorldLighting ("Accept world illumination", Float) = 1
        _Color ("Obsidian Base Color", Color) = (0.016, 0.020, 0.032, 1.0)
        _RimColor ("Edge Rim Color", Color) = (0.24, 0.72, 1.0, 1.0)
        _RimPower ("Rim Fresnel Power", Range(0.5, 8.0)) = 3.2
        _GridScale ("Hex Lattice Scale", Float) = 1.8
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
            Name "CRYSTAL_FORWARD"
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_instancing
            #pragma target 3.5
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "UnityCG.cginc"
            #include "../../Runtime/Art/MaestroEnvironmentDepth.cginc"
            #include "../../Runtime/Art/MaestroWorldLighting.cginc"
            #include "CosmosCommon.cginc"

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RimColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _RimPower)
                UNITY_DEFINE_INSTANCED_PROP(float, _GridScale)
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
                float3 localPos : TEXCOORD2;
                float3 localNormal : TEXCOORD3;
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
                output.localPos = input.vertex.xyz;
                output.localNormal = input.normal;
                output.color = input.color;
                return output;
            }

            float EvalHexLattice(float2 coord)
            {
                const float2 s = float2(1.0, 1.7320508);
                float4 hC = floor(float4(coord, coord - float2(0.5, 1.0)) / s.xyxy) + 0.5;
                float4 h = float4(coord - hC.xy * s, coord - (hC.zw + 0.5) * s);
                float2 hexLocal = (dot(h.xy, h.xy) < dot(h.zw, h.zw)) ? h.xy : h.zw;
                float hexDist = 0.5 - max(abs(hexLocal.x), dot(abs(hexLocal), float2(0.5, 0.8660254)));
                float fw = max(fwidth(hexDist), 0.0015);
                return 1.0 - smoothstep(0.012, 0.012 + fw * 1.6, hexDist);
            }

            fixed4 frag(Varying input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_VisibilityRealDepth > 0.5)
                {
                    MaestroOccludeEnvironment(input.worldPos);
                }

                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float4 rimColor = UNITY_ACCESS_INSTANCED_PROP(Props, _RimColor);
                float rimPower = UNITY_ACCESS_INSTANCED_PROP(Props, _RimPower);
                float gridScale = UNITY_ACCESS_INSTANCED_PROP(Props, _GridScale);

                float3 N = normalize(input.worldNormal);
                float3 V = normalize(UnityWorldSpaceViewDir(input.worldPos));
                float ndotv = saturate(dot(N, V));

                // Top deck hex lattice fading smoothly on cliff facets so steps & edges stay crisp.
                float topDeckMask = smoothstep(0.45, 0.82, abs(N.y));
                float hexLine = EvalHexLattice(input.worldPos.xz * max(gridScale, 0.1)) * topDeckMask;

                // Subtle crystal facet sheen + dark obsidian base keeping Maestro & Book high-contrast.
                float fresnel = CosmosPowSafe(1.0 - ndotv, max(rimPower, 0.5));
                float3 keyDir = normalize(float3(0.35, 0.85, -0.40));
                float3 H = normalize(keyDir + V);
                float facetSpec = CosmosPowSafe(saturate(dot(N, H)), 42.0) * 0.14;
                float legacyShade = 0.72 + 0.28 * saturate(dot(N, keyDir));

                float3 surfaceBase = baseColor.rgb * input.color.rgb * MaestroLight(N, legacyShade) + facetSpec.xxx;
                float3 crystalAccents = rimColor.rgb * (fresnel * 0.68 + hexLine * 0.24);
                float3 lit = MaestroAtmosphere(surfaceBase + crystalAccents, input.worldPos);

                return half4(CosmosAgxOled(lit), 1.0);
            }
            ENDCG
        }

        Pass
        {
            Name "SHADOWCASTER"
            Tags { "LightMode"="ShadowCaster" }
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "UnityCG.cginc"

            struct Vertex
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varying
            {
                V2F_SHADOW_CASTER;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varying vertShadow(Vertex v)
            {
                Varying o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);
                return o;
            }

            float4 fragShadow(Varying i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}
