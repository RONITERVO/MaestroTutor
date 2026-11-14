// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Cosmos/MsdfLiveInk"
{
    Properties
    {
        _MainTex ("Font / MSDF Atlas", 2D) = "white" {}
        _Color ("Ink Color", Color) = (1.0, 0.94, 0.82, 1.0)
        _HaloColor ("Obsidian Halo Color", Color) = (0.002, 0.003, 0.006, 0.94)
        _RevealRange ("Reveal (startNorm, endNorm, feather, ghostAlpha)", Vector) = (0.0, 1.0, 0.06, 0.16)
        _GlyphIndex ("Glyph Index (-1:Phrase/UV)", Float) = -1.0
        _GlyphCount ("Glyph Count", Float) = 1.0
        _SdfMode ("Atlas Mode (0:Font/Alpha 1:RGB MSDF 2:SingleSDF)", Float) = 0.0
        _PxRange ("SDF Pixel Range", Float) = 6.0
        _HaloWidth ("Halo SDF Width", Range(0, 0.45)) = 0.22
        _NibColor ("Live-Ink Nib Glow Color", Color) = (0.35, 0.85, 1.25, 1.0)
        _PhraseBounds ("Local Phrase X Bounds (minX, spanX, 0, 0)", Vector) = (0.0, 0.0, 0.0, 0.0)
        [HideInInspector] _VisibilityRealDepth ("Layer accepts real depth", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Lighting Off
        Cull Off
        ZWrite Off
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float4, _HaloColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RevealRange)
                UNITY_DEFINE_INSTANCED_PROP(float, _GlyphIndex)
                UNITY_DEFINE_INSTANCED_PROP(float, _GlyphCount)
                UNITY_DEFINE_INSTANCED_PROP(float, _SdfMode)
                UNITY_DEFINE_INSTANCED_PROP(float, _PxRange)
                UNITY_DEFINE_INSTANCED_PROP(float, _HaloWidth)
                UNITY_DEFINE_INSTANCED_PROP(float4, _NibColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _PhraseBounds)
            UNITY_INSTANCING_BUFFER_END(Props)

            float _VisibilityRealDepth;

            struct Vertex
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varying
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float3 localPos : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
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
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.uv1 = input.uv1;
                output.localPos = input.vertex.xyz;
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

                float4 inkColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color) * input.color;
                float4 haloColor = UNITY_ACCESS_INSTANCED_PROP(Props, _HaloColor);
                float4 revealRange = UNITY_ACCESS_INSTANCED_PROP(Props, _RevealRange);
                float glyphIdx = UNITY_ACCESS_INSTANCED_PROP(Props, _GlyphIndex);
                float glyphCount = max(UNITY_ACCESS_INSTANCED_PROP(Props, _GlyphCount), 1.0);
                float sdfMode = UNITY_ACCESS_INSTANCED_PROP(Props, _SdfMode);
                float pxRange = max(UNITY_ACCESS_INSTANCED_PROP(Props, _PxRange), 1.0);
                float haloWidth = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _HaloWidth));
                float4 nibColor = UNITY_ACCESS_INSTANCED_PROP(Props, _NibColor);
                float4 phraseBounds = UNITY_ACCESS_INSTANCED_PROP(Props, _PhraseBounds);

                fixed4 texSample = tex2D(_MainTex, input.uv);
                float fillCov = 0.0;
                float haloCov = 0.0;

                if (sdfMode > 0.5)
                {
                    // Mode 1 = RGB MSDF, Mode 2 = Single-channel SDF (TMP/alpha).
                    float sd = (sdfMode < 1.5)
                        ? CosmosMedian3(texSample.r, texSample.g, texSample.b)
                        : texSample.a;

                    float2 unitRange = pxRange * _MainTex_TexelSize.xy;
                    float2 screenTexSize = 1.0 / max(fwidth(input.uv), 1e-6);
                    float screenPxRange = clamp(0.5 * dot(unitRange, screenTexSize), 1.0, 32.0);
                    float sdPx = (sd - 0.5) * screenPxRange;

                    fillCov = smoothstep(-0.55, 0.55, sdPx);
                    if (haloWidth > 0.001 && haloColor.a > 0.001)
                    {
                        float haloEdge = clamp(0.14 * screenPxRange, 0.18, 0.55);
                        float maxSafeHaloPx = max(0.32 * screenPxRange - haloEdge, 0.0);
                        float haloPx = min(clamp(haloWidth * screenPxRange, 0.0, 3.2), maxSafeHaloPx);
                        float strokeBand = smoothstep(0.12, 0.26, sd);
                        if (maxSafeHaloPx > 0.01)
                        {
                            haloCov = smoothstep(-haloPx - haloEdge, -haloPx + haloEdge, sdPx) * strokeBand * haloColor.a;
                        }
                    }
                }
                else
                {
                    // Mode 0: Graceful fallback for Unity dynamic Font / Alpha sprite atlases.
                    fillCov = texSample.a;
                    if (haloWidth > 0.001 && haloColor.a > 0.001)
                    {
                        float2 stepUv = _MainTex_TexelSize.xy * lerp(0.5, 1.1, saturate(haloWidth * 2.5));
                        float aN = max(
                            max(tex2D(_MainTex, input.uv + float2(stepUv.x, 0.0)).a, tex2D(_MainTex, input.uv - float2(stepUv.x, 0.0)).a),
                            max(tex2D(_MainTex, input.uv + float2(0.0, stepUv.y)).a, tex2D(_MainTex, input.uv - float2(0.0, stepUv.y)).a)
                        );
                        haloCov = saturate(aN - fillCov) * haloColor.a;
                    }
                }

                // Determine normalized glyph progress t in [0..1] along the phrase without mesh rebuilds.
                float t = 0.5;
                if (glyphIdx >= 0.0)
                {
                    t = saturate((glyphIdx + 0.5) / glyphCount);
                }
                else if (phraseBounds.y > 1e-4)
                {
                    t = saturate((input.localPos.x - phraseBounds.x) / phraseBounds.y);
                }
                else if (glyphCount > 1.5)
                {
                    t = saturate(input.uv1.x);
                }
                else
                {
                    t = input.uv.x;
                }

                // Live-ink reveal sweep: _RevealRange = (startNorm, endNorm, feather, ghostAlpha).
                float startNorm = revealRange.x;
                float endNorm = revealRange.y;
                float feather = max(revealRange.z, 0.005);
                float ghostAlpha = saturate(revealRange.w);

                float lead = (endNorm <= 1e-4) ? 0.0 : ((endNorm >= 0.999) ? 1.0 : (1.0 - smoothstep(endNorm - feather * 0.5, endNorm + feather * 0.5, t)));
                float trail = (startNorm <= 1e-4) ? 1.0 : ((startNorm >= 0.999) ? 0.0 : smoothstep(startNorm - feather * 0.5, startNorm + feather * 0.5, t));
                float activeSpan = lead * trail;
                float inkAlpha = inkColor.a * lerp(ghostAlpha, 1.0, activeSpan);

                // Subtle nib glow right at the moving reveal frontier.
                float nibActive = step(0.001, endNorm) * step(endNorm, 0.999);
                float nibDist = (t - endNorm) / max(feather, 0.025);
                float nibWave = exp(-nibDist * nibDist * 2.2) * nibActive;
                float3 fillRgb = lerp(inkColor.rgb, nibColor.rgb, saturate(nibWave * 0.80));

                float effFill = fillCov * inkAlpha;
                float effHalo = haloCov * inkAlpha;
                float outAlpha = effFill + effHalo * (1.0 - effFill);
                if (outAlpha <= 0.002)
                {
                    discard;
                }

                float3 premulRgb = fillRgb * effFill + haloColor.rgb * effHalo * (1.0 - effFill);
                float3 outRgb = CosmosOledVoidClamp(premulRgb / max(outAlpha, 1e-5));
                return half4(outRgb, saturate(outAlpha));
            }
            ENDCG
        }
    }
}
