// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
Shader "Maestro/Watercolor"
{
    Properties
    {
        _Color ("Pigment", Color) = (1,.94,.82,1)
        _PatternMode ("Pattern: solid/checker/stripes", Float) = 0
        _PatternPlane ("Pattern: UV/XY/XZ/YZ", Float) = 0
        _PatternColor ("Alternate pigment", Color) = (1,1,1,1)
        _PatternCounts ("Pattern columns and rows", Vector) = (1,1,0,0)
        _PatternCoordinates ("Normalized part coordinates", Vector) = (1,1,1,0)
        _MainTex ("Page image", 2D) = "white" {}
        _Grain ("Paper grain", Range(0,.2)) = .07
        _PigmentTex ("Dry watercolor", 2D) = "white" {}
        _Shading ("Paper face shade", Range(0,.3)) = .12
        _HasRestCoordinates ("Rest-space coordinates", Float) = 0
        _PencilWidth ("Graphite silhouette in meters", Range(0,.005)) = .0012
        _AlphaCutoff ("Texture cutout", Range(0,1)) = 0
        _SurfaceMode ("Opaque / cutout / blend", Float) = 0
        _SurfaceOpacity ("Surface opacity", Range(0,1)) = 1
        [HideInInspector] _SurfaceCull ("Surface culling", Float) = 0
        [HideInInspector] _SrcBlend ("Source blending", Float) = 1
        [HideInInspector] _DstBlend ("Destination blending", Float) = 0
        [HideInInspector] _ZWrite ("Depth writing", Float) = 1
        [HideInInspector] _VisibilityOpacity ("Layer opacity", Range(0,1)) = 1
        [HideInInspector] _VisibilityRealDepth ("Layer accepts real depth", Float) = 1
        _DecodeBrowserSrgb ("Raw browser sRGB pixels", Float) = 0
    }
    SubShader
    {
        // Dynamic batching replaces object coordinates with world coordinates.
        // Keep local pigment/patterns intact; shared meshes can still use GPU instancing.
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        Pass
        {
            Name "PENCIL"
            Cull Front
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "UnityCG.cginc"
            #include "MaestroEnvironmentDepth.cginc"
            struct Vertex { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varying { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; float alpha : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            float _PencilWidth;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AlphaCutoff, _SurfaceMode, _SurfaceOpacity;
            float _VisibilityOpacity, _VisibilityRealDepth;
            Varying vert(Vertex input)
            {
                Varying output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world = mul(unity_ObjectToWorld, input.vertex).xyz;
                world += UnityObjectToWorldNormal(input.normal) * _PencilWidth;
                output.position = mul(UNITY_MATRIX_VP, float4(world, 1));
                output.world = world;
                output.alpha = input.color.a;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            fixed4 frag(Varying input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_VisibilityOpacity <= 0) clip(-1);
                if (_VisibilityRealDepth > .5) MaestroOccludeEnvironment(input.world);
                float alpha = tex2D(_MainTex, input.uv).a * (_SurfaceMode > .5 ? _SurfaceOpacity * input.alpha : 1);
                clip(alpha - _AlphaCutoff);
                return fixed4(.204,.176,.169,_VisibilityOpacity);
            }
            ENDCG
        }
        Pass
        {
            Cull [_SurfaceCull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #include "UnityCG.cginc"
            #include "MaestroEnvironmentDepth.cginc"
            struct Vertex { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float3 rest : TEXCOORD2; float3 restNormal : TEXCOORD3; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varying { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float3 local : TEXCOORD1; float3 normal : TEXCOORD2; float3 pigmentNormal : TEXCOORD3; float4 color : COLOR; float3 world : TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
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
            Varying vert(Vertex input)
            {
                Varying output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.world = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.local = lerp(input.vertex.xyz, input.rest, _HasRestCoordinates);
                output.normal = input.normal;
                output.pigmentNormal = lerp(input.normal, input.restNormal, _HasRestCoordinates);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            fixed4 frag(Varying input) : SV_Target
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
                float face = 1 - _Shading * (1 - saturate(dot(normalize(input.normal), normalize(float3(-.3,.8,-.5)))));
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
                return fixed4(surface.rgb * color * input.color.rgb * pigment * face, (_SurfaceMode > 1.5 ? alpha : 1) * _VisibilityOpacity);
            }
            ENDCG
        }
    }
}
