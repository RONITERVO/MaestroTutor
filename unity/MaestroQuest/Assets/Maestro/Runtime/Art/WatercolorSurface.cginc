// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Shared pigment, texture, alpha, pattern and liquid-contact calculation for every light pass.
            struct Vertex { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float3 rest : TEXCOORD2; float3 restNormal : TEXCOORD3; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varying { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 local : TEXCOORD1; float3 normal : TEXCOORD2; float3 pigmentNormal : TEXCOORD3; float4 color : COLOR; float3 world : TEXCOORD4; float3 worldNormal : TEXCOORD5;
#ifdef MAESTRO_FORWARD_ADD
                UNITY_LIGHTING_COORDS(6,7)
#endif
                UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            sampler2D _PigmentTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _PatternMode, _PatternPlane;
            float4 _PatternColor, _PatternCounts, _PatternCoordinates;
            float _Grain;
            float _Shading;
            float _HasRestCoordinates;
            float _AlphaCutoff, _SurfaceMode, _SurfaceOpacity;
            float _VisibilityOpacity, _VisibilityRealDepth;
            float _DecodeBrowserSrgb;
            float _LiquidRippleCount;
            float4 _LiquidRipples[8],_LiquidRippleWeights[8];
            Varying vert(Vertex input)
            {
                Varying output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.pos = UnityObjectToClipPos(input.vertex);
                output.world = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.local = lerp(input.vertex.xyz, input.rest, _HasRestCoordinates);
                output.normal = input.normal;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.pigmentNormal = lerp(input.normal, input.restNormal, _HasRestCoordinates);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
#ifdef MAESTRO_FORWARD_ADD
                Vertex v = input;
                UNITY_TRANSFER_LIGHTING(output, input.uv);
#endif
                return output;
            }
            half4 MaestroSurface(Varying input)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_VisibilityOpacity <= 0) clip(-1);
                if (_VisibilityRealDepth > .5) MaestroOccludeEnvironment(input.world);
                fixed4 surface = tex2D(_MainTex, input.uv);
                #ifndef UNITY_COLORSPACE_GAMMA
                if (_DecodeBrowserSrgb > .5) surface.rgb = GammaToLinearSpace(surface.rgb);
                #endif
                float alpha = surface.a * (_SurfaceMode > .5 ? _SurfaceOpacity * input.color.a : 1);
                clip(alpha - _AlphaCutoff);
                // Object-space pigment remains fixed through head motion and between eyes.
                float3 weights = abs(normalize(input.pigmentNormal));
                weights /= max(.001, weights.x + weights.y + weights.z);
                float dry = dot(weights, float3(tex2D(_PigmentTex,input.local.yz*1.8).r, tex2D(_PigmentTex,input.local.xz*1.8).r, tex2D(_PigmentTex,input.local.xy*1.8).r));
                float pigment = lerp(1, dry, saturate(_Grain * 8));
                float3 color = _Color.rgb;
                if (_PatternMode > .5) {
                    float3 local = input.local * _PatternCoordinates.xyz + .5;
                    float2 uv = _PatternPlane < .5 ? input.uv : _PatternPlane < 1.5 ? local.xy : _PatternPlane < 2.5 ? local.xz : local.yz;
                    float2 cells = uv * _PatternCounts.xy;
                    // Integrate alternating cells over the pixel footprint. Distant fine
                    // patterns average instead of producing hard binary shimmer.
                    float2 width = max(fwidth(cells), .0001);
                    float2 wave = 2 * (abs(frac((cells - width * .5) * .5) - .5) - abs(frac((cells + width * .5) * .5) - .5)) / width;
                    float alternate = _PatternMode < 1.5 ? .5 - .5 * wave.x * wave.y : .5 - .5 * wave.x;
                    color = lerp(color, _PatternColor.rgb, saturate(alternate));
                }
                // Only measured liquid surfaces publish this bounded transient array.
                // Drawing on the existing clipped mesh keeps rings inside the cavity.
                float ripple = 0;
                [loop] for (int i = 0; i < min((int)_LiquidRippleCount, 8); i++) {
                    float distance = length(input.world - _LiquidRipples[i].xyz);
                    float width = max(fwidth(distance), .001);
                    ripple += (1 - smoothstep(.008, .008 + width, abs(distance - _LiquidRipples[i].w))) * _LiquidRippleWeights[i].x;
                }
                color = lerp(color, float3(1,1,1), saturate(ripple) * .65);
                return half4(surface.rgb * color * input.color.rgb * pigment, (_SurfaceMode > 1.5 ? alpha : 1) * _VisibilityOpacity);
            }
